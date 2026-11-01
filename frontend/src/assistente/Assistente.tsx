import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { HttpAgent, type Context, type Message, type Tool, type ToolCall } from '@ag-ui/client';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/cliente';
import type { FerramentaDaTela, FonteDeContexto, PedidoDeAprovacao, Resultado } from './tipos';

/** Quantas vezes uma mensagem pode voltar ao agente com resultados de ferramentas da tela. */
const RODADAS_MAXIMAS = 12;

export type EstadoDaFerramenta = 'executando' | 'aguardando' | 'ok' | 'erro' | 'recusada';

export interface Desfecho extends Partial<Resultado> {
  estado: EstadoDaFerramenta;
}

export interface Aprovacao {
  pedido: PedidoDeAprovacao;
  responder: (aprovado: boolean) => void;
}

interface Assistente {
  aberto: boolean;
  abrir: () => void;
  fechar: () => void;
  alternar: () => void;
  disponivel: boolean | undefined;
  oQueFalta: string | null;
  mensagens: Message[];
  rodando: boolean;
  erro: string | null;
  enviar: (texto: string) => void;
  parar: () => void;
  novaConversa: () => void;
  /** O desfecho de cada chamada de ferramenta da tela, pelo id da chamada. */
  desfechos: ReadonlyMap<string, Desfecho>;
  aprovacoes: ReadonlyMap<string, Aprovacao>;
  /** A região da tela sobre a qual o assistente está agindo agora. */
  agindoEm: string | null;
  /** Os chips do composer: o que a tela informa. */
  rotulos: string[];
  selecao: string | null;
  anexarSelecao: (markdown: string) => void;
  removerSelecao: () => void;
  /** Pede o foco no composer (depois de abrir o painel, por exemplo). */
  focoNoComposer: number;
  registrarFerramentas: (ferramentas: FerramentaDaTela[]) => () => void;
  registrarFonte: (fonte: FonteDeContexto) => () => void;
  /** Há alterações do assistente não salvas em alguma tela aberta. */
  pendente: () => boolean;
}

const ContextoDoAssistente = createContext<Assistente | null>(null);

export function useAssistente(): Assistente {
  const valor = useContext(ContextoDoAssistente);
  if (!valor) throw new Error('useAssistente fora do AssistenteProvider');
  return valor;
}

const novoId = () => crypto.randomUUID();

const novoAgente = () => new HttpAgent({ url: '/api/agente', threadId: novoId() });

/** A chamada de ferramenta, pelo id, nas mensagens do agente. */
function chamadaPorId(mensagens: Message[], id: string): ToolCall | undefined {
  for (let i = mensagens.length - 1; i >= 0; i--) {
    const m = mensagens[i]!;
    if (m.role === 'assistant') {
      const achada = m.toolCalls?.find((c) => c.id === id);
      if (achada) return achada;
    }
  }
  return undefined;
}

function mensagemDeErro(erro: unknown): string {
  const texto = erro instanceof Error ? erro.message : String(erro);
  if (/503/.test(texto)) return 'O assistente não está configurado neste ambiente.';
  if (/HTTP|fetch|network/i.test(texto)) return 'Não foi possível falar com o assistente. Tente de novo.';
  return texto;
}

/**
 * O assistente, por cima de todas as telas. Guarda a conversa (no cliente,
 * pelo `HttpAgent` do AG-UI), declara ao agente as ferramentas da tela aberta
 * e executa as que ele chamar — pedindo aprovação ao usuário quando a ação
 * grava no servidor. Docs/decisoes-arquitetura/EI-001.
 */
export function AssistenteProvider({ children }: { children: ReactNode }) {
  const [aberto, setAberto] = useState(false);
  const [mensagens, setMensagens] = useState<Message[]>([]);
  const [rodando, setRodando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [desfechos, setDesfechos] = useState<Map<string, Desfecho>>(new Map());
  const [aprovacoes, setAprovacoes] = useState<Map<string, Aprovacao>>(new Map());
  const [agindoEm, setAgindoEm] = useState<string | null>(null);
  const [selecao, setSelecao] = useState<string | null>(null);
  const [focoNoComposer, setFocoNoComposer] = useState(0);
  const [versaoDasFontes, setVersaoDasFontes] = useState(0);
  const [geracao, setGeracao] = useState(0);

  const agente = useRef(novoAgente());
  const interrompida = useRef(false);
  const ferramentas = useRef(new Map<string, FerramentaDaTela>());
  const fontes = useRef(new Set<FonteDeContexto>());
  const selecaoRef = useRef<string | null>(null);
  selecaoRef.current = selecao;

  const status = useQuery({ queryKey: ['agente', 'status'], queryFn: api.statusDoAgente, staleTime: 60_000 });

  // A tela é uma função das mensagens do agente: elas mudam, a tela acompanha.
  useEffect(() => {
    const assinatura = agente.current.subscribe({
      onMessagesChanged: ({ messages }) => setMensagens([...messages]),
    });
    return () => assinatura.unsubscribe();
  }, [geracao]);

  const marcar = (id: string, desfecho: Desfecho) => setDesfechos((m) => new Map(m).set(id, desfecho));

  const contexto = (): Context[] => {
    const lista: Context[] = [
      {
        description: 'A tela aberta',
        value: `${window.location.pathname} — ${document.title}`,
      },
    ];
    for (const fonte of fontes.current) lista.push(...fonte.contexto());
    if (selecaoRef.current)
      lista.push({
        description: 'Trecho selecionado pelo usuário (Markdown) — o pedido é sobre ele',
        value: selecaoRef.current,
      });
    return lista;
  };

  const declaracoes = (): Tool[] =>
    [...ferramentas.current.values()].map((f) => ({ name: f.nome, description: f.descricao, parameters: f.parametros }));

  const executarFerramenta = async (id: string, chamada: ToolCall | undefined): Promise<string> => {
    const ferramenta = chamada ? ferramentas.current.get(chamada.function.name) : undefined;
    if (!chamada || !ferramenta) {
      marcar(id, { estado: 'erro', resumo: 'Não está disponível nesta tela' });
      return `A ferramenta “${chamada?.function.name ?? id}” não está disponível na tela aberta agora.`;
    }

    let args: Record<string, unknown>;
    try {
      args = JSON.parse(chamada.function.arguments || '{}') as Record<string, unknown>;
    } catch {
      marcar(id, { estado: 'erro', resumo: 'Argumentos inválidos' });
      return 'Os argumentos não são um JSON válido. Chame de novo com um objeto JSON.';
    }

    if (ferramenta.aprovacao) {
      const pedido = await ferramenta.aprovacao(args);
      marcar(id, { estado: 'aguardando' });
      setAberto(true);
      const aprovado = await new Promise<boolean>((responder) =>
        setAprovacoes((m) => new Map(m).set(id, { pedido, responder })),
      );
      setAprovacoes((m) => {
        const copia = new Map(m);
        copia.delete(id);
        return copia;
      });
      if (!aprovado) {
        marcar(id, { estado: 'recusada', resumo: 'Recusado por você' });
        return 'O usuário recusou esta ação: nada foi gravado. O que está na tela continua como está. Não peça de novo, a não ser que ele peça.';
      }
    }

    marcar(id, { estado: 'executando' });
    if (ferramenta.area) setAgindoEm(ferramenta.area);
    try {
      const resultado = await ferramenta.executar(args);
      marcar(id, { ...resultado, estado: resultado.ok === false ? 'erro' : 'ok' });
      return resultado.paraOAgente;
    } catch (e) {
      const texto = e instanceof Error ? e.message : String(e);
      marcar(id, { estado: 'erro', resumo: texto });
      return `Erro: ${texto}`;
    } finally {
      // A moldura fica um instante: é o que faz a ação ser vista.
      if (ferramenta.area) window.setTimeout(() => setAgindoEm(null), 900);
    }
  };

  const rodar = async () => {
    // A conversa desta execução: “nova conversa” ou “parar” no meio não a
    // fazem continuar em outra.
    const conversa = agente.current;
    const ativa = () => agente.current === conversa && !interrompida.current;
    interrompida.current = false;
    setRodando(true);
    setErro(null);
    try {
      for (let rodada = 0; rodada < RODADAS_MAXIMAS && ativa(); rodada++) {
        let pendentes: string[] = [];
        await conversa.runAgent(
          { tools: declaracoes(), context: contexto() },
          {
            onRunFinishedEvent: (p) => {
              if (p.outcome === 'success') pendentes = p.pendingToolCallIds;
            },
          },
        );
        if (pendentes.length === 0) break;
        for (const id of pendentes) {
          const conteudo = ativa()
            ? await executarFerramenta(id, chamadaPorId(conversa.messages, id))
            : 'O usuário interrompeu a execução.';
          conversa.addMessage({ id: novoId(), role: 'tool', toolCallId: id, content: conteudo });
        }
      }
    } catch (e) {
      if (!(e instanceof DOMException && e.name === 'AbortError')) setErro(mensagemDeErro(e));
    } finally {
      setRodando(false);
    }
  };

  const enviar = (texto: string) => {
    if (!texto.trim() || rodando) return;
    agente.current.addMessage({ id: novoId(), role: 'user', content: texto.trim() });
    void rodar().then(() => setSelecao(null));
  };

  const recusarPendentes = () =>
    setAprovacoes((m) => {
      for (const a of m.values()) a.responder(false);
      return new Map();
    });

  const parar = () => {
    interrompida.current = true;
    recusarPendentes();
    agente.current.abortRun();
  };

  const novaConversa = () => {
    parar();
    agente.current = novoAgente();
    setGeracao((g) => g + 1);
    setMensagens([]);
    setDesfechos(new Map());
    setErro(null);
    setSelecao(null);
  };

  const registrarFerramentas = useCallback((lista: FerramentaDaTela[]) => {
    for (const f of lista) ferramentas.current.set(f.nome, f);
    return () => {
      for (const f of lista) if (ferramentas.current.get(f.nome) === f) ferramentas.current.delete(f.nome);
    };
  }, []);

  const registrarFonte = useCallback((fonte: FonteDeContexto) => {
    fontes.current.add(fonte);
    setVersaoDasFontes((v) => v + 1);
    return () => {
      fontes.current.delete(fonte);
      setVersaoDasFontes((v) => v + 1);
    };
  }, []);

  const pendente = useCallback(() => [...fontes.current].some((f) => f.pendente?.()), []);

  const abrir = useCallback(() => {
    setAberto(true);
    setFocoNoComposer((n) => n + 1);
  }, []);

  // ⌘J / Ctrl+J: abre (levando a seleção, se houver) ou fecha o assistente.
  useEffect(() => {
    const aoTeclar = (evento: KeyboardEvent) => {
      if (!(evento.metaKey || evento.ctrlKey) || evento.key.toLowerCase() !== 'j') return;
      evento.preventDefault();
      setAberto((estava) => {
        if (estava) return false;
        for (const fonte of fontes.current) {
          const trecho = fonte.selecao?.();
          if (trecho?.trim()) setSelecao(trecho.trim());
        }
        setFocoNoComposer((n) => n + 1);
        return true;
      });
    };
    window.addEventListener('keydown', aoTeclar);
    return () => window.removeEventListener('keydown', aoTeclar);
  }, []);

  const rotulos = useMemo(
    () => [...fontes.current].map((f) => f.rotulo).filter((r): r is string => !!r),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [versaoDasFontes],
  );

  const valor: Assistente = {
    aberto,
    abrir,
    fechar: () => setAberto(false),
    alternar: () => (aberto ? setAberto(false) : abrir()),
    disponivel: status.data?.disponivel,
    oQueFalta: status.data?.oQueFalta ?? (status.error ? 'Não foi possível consultar o assistente.' : null),
    mensagens,
    rodando,
    erro,
    enviar,
    parar,
    novaConversa,
    desfechos,
    aprovacoes,
    agindoEm,
    rotulos,
    selecao,
    anexarSelecao: (markdown) => {
      setSelecao(markdown.trim() || null);
      abrir();
    },
    removerSelecao: () => setSelecao(null),
    focoNoComposer,
    registrarFerramentas,
    registrarFonte,
    pendente,
  };

  return <ContextoDoAssistente.Provider value={valor}>{children}</ContextoDoAssistente.Provider>;
}
