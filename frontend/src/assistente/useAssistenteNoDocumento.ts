import { useEffect, useRef } from 'react';
import type { AtualizarDocumento, Documento, Template } from '../api/tipos';
import { useAssistente } from './Assistente';
import { linhasNovas, textosVisiveis } from './marcas';
import type { FerramentaDaTela, Resultado } from './tipos';

export interface DocumentoNaTela {
  documento: Documento;
  templates: Template[] | undefined;
  /** O Markdown que está no editor agora (inclusive o que não foi salvo). */
  obterTexto: () => string;
  /** O trecho selecionado no editor, em Markdown. */
  obterSelecao: () => string;
  /** Troca o texto na tela — sem gravar — e destaca os trechos novos. */
  aplicarEdicao: (novo: string, trechos: string[]) => void;
  /** Grava no servidor e espera a resposta. */
  gravar: (alteracao: AtualizarDocumento) => Promise<Documento>;
  /** Há alterações do assistente na tela, ainda não salvas; e o resumo delas. */
  revisao: () => { pendente: boolean; resumo: string };
  verDiferencas: () => void;
  mostrarAlteracoes: () => void;
}

const objeto = (propriedades: Record<string, unknown>, obrigatorias: string[]) => ({
  type: 'object',
  properties: propriedades,
  required: obrigatorias,
  additionalProperties: false,
});

const texto = (descricao: string) => ({ type: 'string', description: descricao });

function escaparRegex(t: string) {
  return t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Onde um trecho está no Markdown: exato, ou com os espaços e quebras de linha
 * afrouxados. Mais de uma ocorrência é ambíguo — e o agente é avisado.
 */
function localizar(markdown: string, trecho: string): { inicio: number; fim: number } | string {
  if (!trecho) return 'O trecho está vazio.';
  const exatas = markdown.split(trecho).length - 1;
  if (exatas === 1) {
    const inicio = markdown.indexOf(trecho);
    return { inicio, fim: inicio + trecho.length };
  }
  if (exatas > 1) return `O trecho aparece ${exatas} vezes no Markdown. Inclua mais texto ao redor para dizer qual.`;

  const solto = new RegExp(escaparRegex(trecho.trim()).replace(/\s+/g, '\\s+'), 'g');
  const achados = [...markdown.matchAll(solto)];
  if (achados.length === 1) return { inicio: achados[0]!.index, fim: achados[0]!.index + achados[0]![0].length };
  if (achados.length > 1) return `O trecho aparece ${achados.length} vezes no Markdown. Inclua mais texto ao redor para dizer qual.`;
  return 'Não achei esse trecho no Markdown atual do documento. Copie-o exatamente do Markdown que está no contexto (ele pode ter mudado).';
}

/** Junta blocos de Markdown com uma linha em branco entre eles, sem acumular linhas vazias. */
function juntar(...partes: string[]) {
  return partes
    .map((p) => p.replace(/^\n+|\n+$/g, ''))
    .filter((p) => p.length > 0)
    .join('\n\n')
    .concat('\n');
}

const naoSalvo = 'A alteração está na tela, destacada para o usuário, e ainda não foi salva.';

/**
 * Liga o documento aberto ao assistente: o contexto (o documento, o Markdown
 * atual, a seleção) e as ferramentas que editam a tela. Editar não grava;
 * gravar (salvar, aplicar um template) passa pela aprovação do usuário.
 */
export function useAssistenteNoDocumento(tela: DocumentoNaTela | null) {
  const { registrarFerramentas, registrarFonte } = useAssistente();
  const atual = useRef(tela);
  atual.current = tela;
  const id = tela?.documento.id;
  const titulo = tela?.documento.titulo;

  useEffect(() => {
    if (!id) return;
    const t = () => atual.current!;

    const editar = (novo: string, trechos: string[], resumo: string): Resultado => {
      t().aplicarEdicao(novo, trechos);
      return { paraOAgente: `Feito. ${naoSalvo}`, resumo, mostrar: () => t().mostrarAlteracoes() };
    };

    const ferramentas: FerramentaDaTela[] = [
      {
        nome: 'substituirTrecho',
        descricao:
          'Substitui, no documento aberto na tela, um trecho exato do Markdown atual por um texto novo (em Markdown). '
          + 'É a forma preferida de editar: mude só o que precisa mudar. Para apagar um trecho, use um texto novo vazio. '
          + 'Altera a tela, não grava.',
        parametros: objeto(
          {
            trechoAtual: texto('O trecho a trocar, copiado exatamente do Markdown atual do documento. Inclua o bastante para ser único.'),
            novoTexto: texto('O texto que entra no lugar, em Markdown.'),
          },
          ['trechoAtual', 'novoTexto'],
        ),
        area: 'documento',
        executar: async ({ trechoAtual, novoTexto }) => {
          const markdown = t().obterTexto();
          const onde = localizar(markdown, String(trechoAtual ?? ''));
          if (typeof onde === 'string') return { paraOAgente: onde, ok: false, resumo: 'trecho não encontrado' };
          const novo = String(novoTexto ?? '');
          return editar(
            markdown.slice(0, onde.inicio) + novo + markdown.slice(onde.fim),
            textosVisiveis(novo),
            novo.trim() ? 'destacado no documento' : 'trecho removido',
          );
        },
      },
      {
        nome: 'inserirTexto',
        descricao:
          'Acrescenta um bloco de Markdown ao documento aberto: no início, no fim, ou antes/depois do parágrafo (ou linha) '
          + 'que contém um trecho de referência. Altera a tela, não grava.',
        parametros: objeto(
          {
            texto: texto('O Markdown a acrescentar.'),
            onde: { type: 'string', enum: ['inicio', 'fim', 'antes', 'depois'], description: 'Onde acrescentar.' },
            referencia: texto('Para "antes" e "depois": um trecho exato do Markdown atual que identifica o bloco. Vazio nos demais.'),
          },
          ['texto', 'onde', 'referencia'],
        ),
        area: 'documento',
        executar: async ({ texto: bloco, onde, referencia }) => {
          const markdown = t().obterTexto();
          const novoBloco = String(bloco ?? '');
          let novo: string;
          if (onde === 'inicio') novo = juntar(novoBloco, markdown);
          else if (onde === 'fim') novo = juntar(markdown, novoBloco);
          else {
            const achado = localizar(markdown, String(referencia ?? ''));
            if (typeof achado === 'string') return { paraOAgente: achado, ok: false, resumo: 'referência não encontrada' };
            if (onde === 'antes') {
              const inicioDaLinha = markdown.lastIndexOf('\n', achado.inicio - 1) + 1;
              novo = juntar(markdown.slice(0, inicioDaLinha), novoBloco, markdown.slice(inicioDaLinha));
            } else {
              const quebra = markdown.indexOf('\n', achado.fim);
              const fimDaLinha = quebra < 0 ? markdown.length : quebra;
              novo = juntar(markdown.slice(0, fimDaLinha), novoBloco, markdown.slice(fimDaLinha));
            }
          }
          return editar(novo, textosVisiveis(novoBloco), 'destacado no documento');
        },
      },
      {
        nome: 'reescreverDocumento',
        descricao:
          'Substitui o Markdown inteiro do documento aberto. Use só quando o pedido for refazer o documento; '
          + 'para mudanças pontuais, substitua trechos. Altera a tela, não grava.',
        parametros: objeto({ markdown: texto('O documento inteiro, em Markdown.') }, ['markdown']),
        area: 'documento',
        executar: async ({ markdown }) => {
          const antigo = t().obterTexto();
          const novo = String(markdown ?? '');
          if (!novo.trim()) return { paraOAgente: 'O Markdown novo está vazio: nada foi alterado.', ok: false };
          const linhas = linhasNovas(antigo, novo);
          return editar(novo, textosVisiveis(linhas.join('\n')), `${linhas.length} linha(s) nova(s) destacada(s)`);
        },
      },
      {
        nome: 'salvarDocumento',
        descricao:
          'Grava no servidor o texto que está na tela do documento aberto (e, se informado, um título novo). '
          + 'O usuário aprova antes; se recusar, nada é gravado.',
        parametros: objeto({ titulo: texto('Um título novo para o documento, ou vazio para manter o atual.') }, ['titulo']),
        aprovacao: ({ titulo: novoTitulo }) => {
          const { documento, revisao, verDiferencas } = t();
          const r = revisao();
          const tituloNovo = String(novoTitulo ?? '').trim();
          return {
            titulo: 'Salvar o documento',
            detalhe:
              `“${documento.titulo}”${r.pendente ? ` — ${r.resumo}` : ''}. Grava o texto que está na tela`
              + (tituloNovo ? ` e muda o título para “${tituloNovo}”.` : '.'),
            acao: 'Salvar',
            verAlteracoes: r.pendente ? verDiferencas : undefined,
          };
        },
        executar: async ({ titulo: novoTitulo }) => {
          const tituloNovo = String(novoTitulo ?? '').trim();
          const salvo = await t().gravar({ corpo: t().obterTexto(), ...(tituloNovo ? { titulo: tituloNovo } : {}) });
          return { paraOAgente: `Salvo: “${salvo.titulo}”.`, resumo: 'salvo' };
        },
      },
      {
        nome: 'aplicarTemplate',
        descricao:
          'Aplica um template ao documento aberto: troca a formatação inteira (papel, margens, tipografia, moldura) pela do '
          + 'template; o texto não muda. Grava no servidor; o usuário aprova antes.',
        parametros: objeto({ templateId: texto('O id do template, como veio da lista de templates.') }, ['templateId']),
        aprovacao: ({ templateId }) => {
          const { documento, templates } = t();
          const template = templates?.find((x) => x.id === templateId);
          return {
            titulo: 'Aplicar um template',
            detalhe: `Troca a formatação de “${documento.titulo}” pela do template “${template?.nome ?? String(templateId)}”. O texto não muda.`,
            acao: 'Aplicar',
          };
        },
        executar: async ({ templateId }) => {
          const template = t().templates?.find((x) => x.id === templateId);
          if (!template)
            return { paraOAgente: `Não existe template com o id “${String(templateId)}”. Liste os templates.`, ok: false };
          await t().gravar({ templateId: template.id });
          return { paraOAgente: `Template “${template.nome}” aplicado e gravado.`, resumo: template.nome };
        },
      },
    ];

    const tirarFerramentas = registrarFerramentas(ferramentas);
    const tirarFonte = registrarFonte({
      rotulo: titulo,
      contexto: () => {
        const { documento, obterTexto, revisao } = t();
        const r = revisao();
        const imagens = documento.imagens.map((i) => i.referencia);
        return [
          {
            description: 'O documento aberto na tela — o alvo das edições, a não ser que o usuário diga outro',
            value: [
              `id: ${documento.id}`,
              `título: ${documento.titulo}`,
              `tipo: ${documento.tipo}`,
              `template de origem: ${documento.templateDeOrigem ?? '—'}`,
              `imagens que o Markdown pode citar: ${imagens.length > 0 ? imagens.join(', ') : 'nenhuma'}`,
              `alterações do assistente ainda não salvas: ${r.pendente ? `sim (${r.resumo})` : 'não'}`,
            ].join('\n'),
          },
          { description: 'O Markdown atual do documento aberto (o texto a editar)', value: obterTexto() },
        ];
      },
      selecao: () => t().obterSelecao(),
      pendente: () => t().revisao().pendente,
    });

    return () => {
      tirarFerramentas();
      tirarFonte();
    };
  }, [id, titulo, registrarFerramentas, registrarFonte]);
}
