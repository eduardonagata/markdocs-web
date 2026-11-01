import { useCallback, useEffect, useRef, useState } from 'react';
import { useBlocker, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  Button,
  Caption1,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  DrawerBody,
  DrawerHeader,
  DrawerHeaderTitle,
  Field,
  InlineDrawer,
  Input,
  makeStyles,
  Menu,
  MenuDivider,
  MenuGroupHeader,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
  MessageBar,
  MessageBarBody,
  Spinner,
  Tab,
  TabList,
  Text,
  tokens,
  ToggleButton,
  Tooltip,
} from '@fluentui/react-components';
import {
  ArrowDownloadRegular,
  CheckmarkRegular,
  DeleteRegular,
  DocumentCopyRegular,
  DocumentHeaderFooterRegular,
  ImageMultipleRegular,
  MoreHorizontalRegular,
  PaintBrushRegular,
  TextGrammarWandRegular,
} from '@fluentui/react-icons';
import { api, urls } from '../api/cliente';
import {
  useAtualizarDocumento,
  useCriarTemplate,
  useDocumento,
  useGravarImagem,
  useGravarTimbre,
  useRemoverDocumento,
  useRemoverImagem,
  useRemoverTimbre,
  useTemplates,
} from '../api/consultas';
import type { Ajuste, AtualizarDocumento, Documento } from '../api/tipos';
import { useAssistente } from '../assistente/Assistente';
import { apagarDestaques, destacar, mostrar } from '../assistente/marcas';
import { useAssistenteNoDocumento } from '../assistente/useAssistenteNoDocumento';
import { BarraDeRevisao } from '../componentes/assistente/BarraDeRevisao';
import { PerguntarSobreSelecao } from '../componentes/assistente/PerguntarSobreSelecao';
import { EditorMarkdown, type EditorMarkdownRef } from '../componentes/editor/EditorMarkdown';
import { InspetorDeFormatacao } from '../componentes/InspetorDeFormatacao';
import { PainelDeImagens } from '../componentes/PainelDeImagens';
import { PainelDoTimbre } from '../componentes/PainelDoTimbre';
import { lerModo, SeletorDeModo, Vistas, type Modo } from '../componentes/Vistas';
import { VisualizadorPdf } from '../componentes/VisualizadorPdf';
import { alternativo } from '../util';

const useEstilos = makeStyles({
  raiz: { flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' },
  barra: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalM,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  identidade: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, flex: '1 1 320px', minWidth: 0 },
  titulo: {
    flex: 1,
    minWidth: '120px',
    '& input': { fontSize: tokens.fontSizeBase400, fontWeight: tokens.fontWeightSemibold },
  },
  tipo: { width: '140px', '@media (max-width: 640px)': { display: 'none' } },
  estado: { color: tokens.colorNeutralForeground3, display: 'flex', alignItems: 'center', gap: '4px', minWidth: '84px' },
  acoes: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS },
  corpo: { flex: 1, minHeight: 0, display: 'flex' },
  pdf: { padding: '24px 0' },
  erro: { margin: tokens.spacingVerticalM },
  gaveta: { borderLeft: `1px solid ${tokens.colorNeutralStroke1}`, backgroundColor: tokens.colorNeutralBackground1 },
  cabecalhoDaGaveta: { borderBottom: `1px solid ${tokens.colorNeutralStroke2}`, paddingBottom: tokens.spacingVerticalS },
  carregando: { flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' },
  lenteDoTexto: { position: 'relative', minHeight: '100%', display: 'flex', flexDirection: 'column' },
});

type Painel = 'formatacao' | 'timbre' | 'imagens' | null;
type Estado = 'salvo' | 'salvando' | 'erro';

/** As alterações do assistente na tela, ainda não salvas: o texto de antes e o que ele escreveu. */
interface Revisao {
  original: string;
  trechos: string[];
  edicoes: number;
}

/** Junta duas alterações pendentes: a mais nova vence, e os ajustes se somam por eixo. */
function juntar(a: AtualizarDocumento, b: AtualizarDocumento): AtualizarDocumento {
  const ajustes = new Map<string, Ajuste>();
  for (const ajuste of [...(a.ajustes ?? []), ...(b.ajustes ?? [])]) ajustes.set(ajuste.eixo, ajuste);
  return { ...a, ...b, ajustes: ajustes.size > 0 ? [...ajustes.values()] : undefined };
}

export function DocumentoPagina() {
  const { id = '' } = useParams();
  const estilos = useEstilos();
  const navegar = useNavigate();
  const [parametros, setParametros] = useSearchParams();
  const modo = lerModo(parametros.get('modo'));

  const { data: documento, error, isLoading } = useDocumento(id);
  const { data: templates } = useTemplates();
  const atualizar = useAtualizarDocumento(id);
  const remover = useRemoverDocumento();
  const criarTemplate = useCriarTemplate();
  const gravarImagem = useGravarImagem(id);
  const removerImagem = useRemoverImagem(id);
  const gravarTimbre = useGravarTimbre(id);
  const removerTimbre = useRemoverTimbre(id);

  const [texto, setTexto] = useState<string | null>(null);
  const [titulo, setTitulo] = useState('');
  const [tipo, setTipo] = useState('');
  const [painel, setPainel] = useState<Painel>(null);
  const [estado, setEstado] = useState<Estado>('salvo');
  const [dialogo, setDialogo] = useState<'template' | 'excluir' | null>(null);
  const [nomeDoTemplate, setNomeDoTemplate] = useState('');
  const [revisao, setRevisao] = useState<Revisao | null>(null);
  const editor = useRef<EditorMarkdownRef>(null);
  const assistente = useAssistente();
  const revisaoRef = useRef(revisao);
  revisaoRef.current = revisao;

  // O texto local nasce do servidor uma vez por documento; depois disso, o
  // que se digita é a fonte, e o servidor só confirma.
  const carregadoDe = useRef<string | null>(null);
  useEffect(() => {
    if (documento && carregadoDe.current !== documento.id) {
      carregadoDe.current = documento.id;
      setTexto(documento.corpo);
      setTitulo(documento.titulo);
      setTipo(documento.tipo);
    }
  }, [documento]);

  // ─── Salvar: uma gravação por vez, juntando o que chegar no meio ─────────
  type Espera = { resolver: (d: Documento) => void; rejeitar: (e: unknown) => void };
  const pendente = useRef<AtualizarDocumento | null>(null);
  const esperando = useRef<Espera[]>([]);
  const emVoo = useRef(false);
  const atualizarRef = useRef(atualizar.mutateAsync);
  atualizarRef.current = atualizar.mutateAsync;

  const despachar = useCallback(async () => {
    if (emVoo.current || !pendente.current) return;
    const alteracao = pendente.current;
    const quem = esperando.current;
    pendente.current = null;
    esperando.current = [];
    emVoo.current = true;
    setEstado('salvando');
    try {
      const salvo = await atualizarRef.current(alteracao);
      if (alteracao.titulo !== undefined) setTitulo(salvo.titulo);
      setEstado(pendente.current ? 'salvando' : 'salvo');
      quem.forEach((q) => q.resolver(salvo));
    } catch (e) {
      setEstado('erro');
      quem.forEach((q) => q.rejeitar(e));
    } finally {
      emVoo.current = false;
      if (pendente.current) void despachar();
    }
  }, []);

  const salvar = useCallback(
    (alteracao: AtualizarDocumento) => {
      pendente.current = pendente.current ? juntar(pendente.current, alteracao) : alteracao;
      void despachar();
    },
    [despachar],
  );

  /** Grava e espera a resposta. Gravar o texto encerra a revisão do assistente. */
  const gravar = useCallback(
    (alteracao: AtualizarDocumento) =>
      new Promise<Documento>((resolver, rejeitar) => {
        esperando.current.push({ resolver, rejeitar });
        salvar(alteracao);
      }).then((salvo) => {
        if (alteracao.corpo !== undefined) {
          setRevisao(null);
          apagarDestaques();
        }
        return salvo;
      }),
    [salvar],
  );

  // O Markdown é gravado quando a digitação pausa — a não ser que haja
  // alterações do assistente na tela: essas só são gravadas com a aprovação
  // do usuário (manter e salvar, ou o pedido de salvar do assistente).
  useEffect(() => {
    if (texto === null || !documento || texto === documento.corpo || revisao) return;
    setEstado('salvando');
    const espera = window.setTimeout(() => salvar({ corpo: texto }), 700);
    return () => window.clearTimeout(espera);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [texto, revisao]);

  // ─── O assistente na tela ─────────────────────────────────────────────────
  const marcar = useCallback((trechos: string[], rolar: boolean) => {
    // Depois que o editor redesenha a folha.
    requestAnimationFrame(() =>
      requestAnimationFrame(() => {
        const primeira = destacar(editor.current?.folha() ?? null, trechos);
        if (rolar) mostrar(primeira);
      }),
    );
  }, []);

  // Os destaques acompanham o texto: o editor redesenha, as faixas são refeitas.
  useEffect(() => {
    if (revisao) marcar(revisao.trechos, false);
  }, [texto, revisao, marcar]);

  useEffect(() => () => apagarDestaques(), []);

  const aplicarEdicao = useCallback(
    (novo: string, trechos: string[]) => {
      const antes = editor.current?.obter() ?? '';
      editor.current?.modo('rich-text');
      editor.current?.definir(novo);
      setTexto(novo);
      const anterior = revisaoRef.current;
      const proxima: Revisao = anterior
        ? { ...anterior, trechos: [...anterior.trechos, ...trechos], edicoes: anterior.edicoes + 1 }
        : { original: antes, trechos, edicoes: 1 };
      revisaoRef.current = proxima;
      setRevisao(proxima);
      if (modo === 'pdf') setParametros({ modo: 'lado-a-lado' }, { replace: true });
      marcar(proxima.trechos, true);
    },
    [marcar, modo, setParametros],
  );

  const desfazerRevisao = () => {
    if (!revisao) return;
    editor.current?.modo('rich-text');
    editor.current?.definir(revisao.original);
    setTexto(revisao.original);
    setRevisao(null);
    apagarDestaques();
  };

  const resumoDaRevisao = (r: Revisao | null) =>
    !r ? '' : r.edicoes === 1 ? '1 alteração do assistente' : `${r.edicoes} alterações do assistente`;

  useAssistenteNoDocumento(
    documento && texto !== null
      ? {
          documento,
          templates,
          obterTexto: () => editor.current?.obter() ?? texto,
          obterSelecao: () => editor.current?.selecao() ?? '',
          aplicarEdicao,
          gravar,
          revisao: () => ({ pendente: !!revisaoRef.current, resumo: resumoDaRevisao(revisaoRef.current) }),
          verDiferencas: () => editor.current?.modo('diff'),
          mostrarAlteracoes: () => {
            editor.current?.modo('rich-text');
            marcar(revisaoRef.current?.trechos ?? [], true);
          },
        }
      : null,
  );

  // Sair da tela com alterações do assistente não salvas pede confirmação.
  const bloqueio = useBlocker(
    ({ currentLocation, nextLocation }) => !!revisao && currentLocation.pathname !== nextLocation.pathname,
  );
  useEffect(() => {
    if (!revisao) return;
    const aoSair = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', aoSair);
    return () => window.removeEventListener('beforeunload', aoSair);
  }, [revisao]);

  // ─── O PDF: refeito a cada versão gravada; o anterior fica até o novo chegar ──
  const pdf = useQuery({
    queryKey: ['pdf', id, documento?.alteradoEm],
    queryFn: () => api.pdf(urls.pdfDoDocumento(id, documento!.alteradoEm)),
    enabled: !!documento,
    placeholderData: keepPreviousData,
    retry: false,
  });

  // Uma imagem enviada pelo editor (botão, colar ou arrastar): entra nos
  // ativos do documento, e o editor cita a referência relativa.
  const enviarImagem = async (arquivo: File) => {
    const imagem = await gravarImagem.mutateAsync(arquivo);
    return imagem.referencia;
  };

  if (isLoading) return <div className={estilos.carregando}><Spinner /></div>;
  if (error || !documento)
    return (
      <MessageBar intent="error" className={estilos.erro}>
        <MessageBarBody>{error?.message ?? 'Documento não encontrado.'}</MessageBarBody>
      </MessageBar>
    );

  const mudarModo = (novo: Modo) => setParametros({ modo: novo }, { replace: true });

  return (
    <div className={estilos.raiz}>
      <div className={estilos.barra}>
        <div className={estilos.identidade}>
          <Input
            className={estilos.titulo}
            appearance="filled-lighter"
            value={titulo}
            aria-label="Título"
            onChange={(_, d) => setTitulo(d.value)}
            onBlur={() => titulo !== documento.titulo && salvar({ titulo })}
            onKeyDown={(e) => e.key === 'Enter' && (e.target as HTMLInputElement).blur()}
          />
          <Tooltip content="O tipo documental — a moldura do template pode imprimi-lo" relationship="description">
            <Input
              className={estilos.tipo}
              appearance="filled-lighter"
              size="small"
              value={tipo}
              aria-label="Tipo documental"
              onChange={(_, d) => setTipo(d.value)}
              onBlur={() => tipo.trim() && tipo !== documento.tipo && salvar({ tipo })}
              onKeyDown={(e) => e.key === 'Enter' && (e.target as HTMLInputElement).blur()}
            />
          </Tooltip>
          <Caption1 className={estilos.estado} aria-live="polite">
            {revisao && estado !== 'salvando' && <Text style={{ color: 'var(--assistente)' }}>Não salvo</Text>}
            {estado === 'salvando' && <><Spinner size="extra-tiny" /> Salvando…</>}
            {estado === 'salvo' && !revisao && <><CheckmarkRegular /> Salvo</>}
            {estado === 'erro' && <Text style={{ color: tokens.colorPaletteRedForeground1 }}>Não foi salvo</Text>}
          </Caption1>
        </div>

        <SeletorDeModo modo={modo} onModo={mudarModo} />

        <div className={estilos.acoes}>
          <Tooltip content="Formatação" relationship="label">
            <ToggleButton
              appearance="subtle"
              icon={<PaintBrushRegular />}
              checked={painel === 'formatacao'}
              onClick={() => setPainel(painel === 'formatacao' ? null : 'formatacao')}
            />
          </Tooltip>
          <Tooltip content="Papel timbrado" relationship="label">
            <ToggleButton
              appearance="subtle"
              icon={<DocumentHeaderFooterRegular />}
              checked={painel === 'timbre'}
              onClick={() => setPainel(painel === 'timbre' ? null : 'timbre')}
            />
          </Tooltip>
          <Tooltip content="Imagens do documento" relationship="label">
            <ToggleButton
              appearance="subtle"
              icon={<ImageMultipleRegular />}
              checked={painel === 'imagens'}
              onClick={() => setPainel(painel === 'imagens' ? null : 'imagens')}
            />
          </Tooltip>
          <Menu>
            <MenuTrigger disableButtonEnhancement>
              <Button appearance="subtle" icon={<MoreHorizontalRegular />} aria-label="Mais ações" />
            </MenuTrigger>
            <MenuPopover>
              <MenuList>
                <MenuGroupHeader>Aplicar template</MenuGroupHeader>
                {templates?.map((t) => (
                  <MenuItem key={t.id} icon={<TextGrammarWandRegular />} onClick={() => salvar({ templateId: t.id })}>
                    {t.nome}
                  </MenuItem>
                ))}
                <MenuDivider />
                <MenuItem
                  icon={<DocumentCopyRegular />}
                  onClick={() => {
                    setNomeDoTemplate(`${documento.tipo} — ${documento.titulo}`);
                    setDialogo('template');
                  }}
                >
                  Salvar como template
                </MenuItem>
                <MenuItem
                  icon={<ArrowDownloadRegular />}
                  onClick={() => {
                    const link = document.createElement('a');
                    link.href = urls.pdfDoDocumento(id, documento.alteradoEm);
                    link.download = `${documento.titulo}.pdf`;
                    link.click();
                  }}
                >
                  Baixar PDF
                </MenuItem>
                <MenuDivider />
                <MenuItem icon={<DeleteRegular />} onClick={() => setDialogo('excluir')}>
                  Excluir documento
                </MenuItem>
              </MenuList>
            </MenuPopover>
          </Menu>
        </div>
      </div>

      <div className={estilos.corpo}>
        <Vistas
          modo={modo}
          onModo={mudarModo}
          agindo={assistente.agindoEm === 'documento' ? 'texto' : null}
          texto={
            texto !== null && (
              <>
              {gravarImagem.error && painel !== 'imagens' && (
                <MessageBar intent="error" className={estilos.erro}>
                  <MessageBarBody>A imagem não foi enviada: {gravarImagem.error.message}</MessageBarBody>
                </MessageBar>
              )}
              <div className={estilos.lenteDoTexto}>
                <EditorMarkdown
                  key={documento.id}
                  ref={editor}
                  markdown={texto}
                  onMudar={setTexto}
                  imagens={documento.imagens}
                  versao={documento.alteradoEm}
                  onImagem={enviarImagem}
                  salvo={documento.corpo}
                />
                {revisao && (
                  <BarraDeRevisao
                    resumo={resumoDaRevisao(revisao)}
                    salvando={estado === 'salvando'}
                    onVerDiferencas={() => editor.current?.modo('diff')}
                    onDesfazer={desfazerRevisao}
                    onManter={() => void gravar({ corpo: editor.current?.obter() ?? texto }).catch(() => undefined)}
                  />
                )}
              </div>
              <PerguntarSobreSelecao
                obterSelecao={() => editor.current?.selecao() ?? ''}
                dentroDe={() => editor.current?.folha() ?? null}
                onPerguntar={(trecho) => assistente.anexarSelecao(trecho)}
              />
              </>
            )
          }
          pdf={
            <div className={estilos.pdf}>
              {pdf.error && (
                <MessageBar intent="warning" className={estilos.erro}>
                  <MessageBarBody>{pdf.error.message}</MessageBarBody>
                </MessageBar>
              )}
              <VisualizadorPdf dados={pdf.data} />
            </div>
          }
        />

        <InlineDrawer open={painel !== null} position="end" size="small" className={estilos.gaveta}>
          <DrawerHeader className={estilos.cabecalhoDaGaveta}>
            <DrawerHeaderTitle>
              <TabList size="small" selectedValue={painel ?? 'formatacao'} onTabSelect={(_, d) => setPainel(d.value as Painel)}>
                <Tab value="formatacao">Formatação</Tab>
                <Tab value="timbre">Timbre</Tab>
                <Tab value="imagens">Imagens</Tab>
              </TabList>
            </DrawerHeaderTitle>
          </DrawerHeader>
          <DrawerBody style={{ paddingTop: tokens.spacingVerticalL }}>
            {painel === 'formatacao' && (
              <InspetorDeFormatacao eixos={documento.eixos} onAjuste={(ajuste) => salvar({ ajustes: [ajuste] })} />
            )}
            {painel === 'timbre' && (
              <PainelDoTimbre
                documento={documento}
                enviando={gravarTimbre.isPending || removerTimbre.isPending}
                onEnviar={(paginas, arquivo) => gravarTimbre.mutate({ paginas, arquivo })}
                onRemover={(paginas) => removerTimbre.mutate(paginas)}
              />
            )}
            {painel === 'timbre' && (gravarTimbre.error || removerTimbre.error) && (
              <MessageBar intent="error" style={{ marginTop: tokens.spacingVerticalM }}>
                <MessageBarBody>{(gravarTimbre.error ?? removerTimbre.error)?.message}</MessageBarBody>
              </MessageBar>
            )}
            {painel === 'imagens' && (
              <PainelDeImagens
                imagens={documento.imagens}
                versao={documento.alteradoEm}
                enviando={gravarImagem.isPending}
                onEnviar={(arquivos) => arquivos.forEach((a) => void gravarImagem.mutateAsync(a).catch(() => undefined))}
                onInserir={(imagem) => {
                  if (modo === 'pdf') mudarModo('lado-a-lado');
                  editor.current?.inserir(`![${alternativo(imagem.nome)}](${imagem.referencia})`);
                }}
                onRemover={(imagem) => removerImagem.mutate(imagem.nome)}
              />
            )}
            {painel === 'imagens' && gravarImagem.error && (
              <MessageBar intent="error" style={{ marginTop: tokens.spacingVerticalM }}>
                <MessageBarBody>{gravarImagem.error.message}</MessageBarBody>
              </MessageBar>
            )}
          </DrawerBody>
        </InlineDrawer>
      </div>

      <Dialog open={bloqueio.state === 'blocked'} onOpenChange={(_, d) => !d.open && bloqueio.reset?.()}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Descartar as alterações do assistente?</DialogTitle>
            <DialogContent>
              O assistente alterou este documento, e as alterações ainda não foram salvas. Saindo agora, elas se perdem.
            </DialogContent>
            <DialogActions>
              <Button onClick={() => bloqueio.reset?.()}>Continuar aqui</Button>
              <Button
                onClick={() => {
                  desfazerRevisao();
                  bloqueio.proceed?.();
                }}
              >
                Descartar e sair
              </Button>
              <Button
                appearance="primary"
                onClick={() =>
                  void gravar({ corpo: editor.current?.obter() ?? texto ?? '' })
                    .then(() => bloqueio.proceed?.())
                    .catch(() => bloqueio.reset?.())
                }
              >
                Salvar e sair
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={dialogo === 'template'} onOpenChange={(_, d) => !d.open && setDialogo(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Salvar como template</DialogTitle>
            <DialogContent>
              <Field label="Nome do template" hint="A formatação deste documento vira um preset para os próximos.">
                <Input value={nomeDoTemplate} onChange={(_, d) => setNomeDoTemplate(d.value)} autoFocus />
              </Field>
              {criarTemplate.error && (
                <MessageBar intent="error" style={{ marginTop: tokens.spacingVerticalM }}>
                  <MessageBarBody>{criarTemplate.error.message}</MessageBarBody>
                </MessageBar>
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setDialogo(null)}>Cancelar</Button>
              <Button
                appearance="primary"
                disabled={!nomeDoTemplate.trim() || criarTemplate.isPending}
                onClick={() =>
                  criarTemplate.mutate({ documentoId: id, nome: nomeDoTemplate }, { onSuccess: () => setDialogo(null) })
                }
              >
                Salvar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={dialogo === 'excluir'} onOpenChange={(_, d) => !d.open && setDialogo(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Excluir documento?</DialogTitle>
            <DialogContent>“{documento.titulo}” e as imagens dele serão removidos. Não dá para desfazer.</DialogContent>
            <DialogActions>
              <Button onClick={() => setDialogo(null)}>Cancelar</Button>
              <Button
                appearance="primary"
                disabled={remover.isPending}
                onClick={() => remover.mutate(id, { onSuccess: () => navegar('/documentos') })}
              >
                Excluir
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
