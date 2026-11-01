import { forwardRef, useEffect, useImperativeHandle, useMemo, useRef, useState, type MutableRefObject } from 'react';
import {
  BlockTypeSelect,
  BoldItalicUnderlineToggles,
  codeBlockPlugin,
  codeMirrorPlugin,
  CreateLink,
  diffSourcePlugin,
  DiffSourceToggleWrapper,
  headingsPlugin,
  imagePlugin,
  InsertImage,
  InsertTable,
  InsertThematicBreak,
  linkDialogPlugin,
  linkPlugin,
  listsPlugin,
  ListsToggle,
  markdownShortcutPlugin,
  MDXEditor,
  quotePlugin,
  Separator,
  StrikeThroughSupSubToggles,
  tablePlugin,
  thematicBreakPlugin,
  toolbarPlugin,
  UndoRedo,
  usePublisher,
  viewMode$,
  diffMarkdown$,
  type MDXEditorMethods,
  type ViewMode,
} from '@mdxeditor/editor';
import '@mdxeditor/editor/style.css';
import { MessageBar, MessageBarBody, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import type { Imagem } from '../../api/tipos';
import { imagemAusente, nomeCitado } from './imagens';
import { traducao } from './traducao';

const useEstilos = makeStyles({
  raiz: {
    minHeight: '100%',
    display: 'flex',
    flexDirection: 'column',
  },
  aviso: { margin: `${tokens.spacingVerticalM} auto 0`, maxWidth: '760px', width: 'calc(100% - 48px)' },
});

export interface EditorMarkdownRef {
  /** O Markdown atual do editor. */
  obter: () => string;
  /** Substitui o Markdown inteiro (não grava nada: quem grava é a página). */
  definir: (markdown: string) => void;
  /** Insere um trecho de Markdown no cursor. */
  inserir: (markdown: string) => void;
  focar: () => void;
  /** O trecho selecionado, em Markdown; vazio sem seleção. */
  selecao: () => string;
  /** O documento formatado, as diferenças em relação ao salvo, ou o Markdown como texto. */
  modo: (modo: ViewMode) => void;
  /** A folha renderizada (onde o assistente destaca o que alterou). */
  folha: () => HTMLElement | null;
}

interface Props {
  /** O Markdown inicial. Depois de montado, o editor é a fonte do texto; mudanças vêm por `onMudar`. */
  markdown: string;
  onMudar: (markdown: string) => void;
  /** As imagens dos ativos do documento: só elas aparecem, e são as sugeridas no diálogo de imagem. */
  imagens: Imagem[];
  /** Muda quando o documento muda, para o navegador revalidar as imagens. */
  versao: string;
  /** Uma imagem enviada, colada ou arrastada: grava e devolve a referência `imagens/<nome>`. */
  onImagem: (arquivo: File) => Promise<string>;
  /** A versão gravada no servidor: é contra ela que o modo de diferenças compara. */
  salvo: string;
}

/** O que só existe dentro do contexto do editor (o "realm" do MDXEditor), exposto a quem está fora. */
interface Ponte {
  mudarModo: (modo: ViewMode) => void;
  mudarBase: (markdown: string) => void;
}

function PonteDoEditor({ ponte }: { ponte: MutableRefObject<Ponte | null> }) {
  const mudarModo = usePublisher(viewMode$);
  const mudarBase = usePublisher(diffMarkdown$);
  ponte.current = { mudarModo, mudarBase };
  return null;
}

function BarraDeFerramentas({ ponte }: { ponte: MutableRefObject<Ponte | null> }) {
  return (
    <DiffSourceToggleWrapper options={['rich-text', 'diff', 'source']}>
      <PonteDoEditor ponte={ponte} />
      <UndoRedo />
      <Separator />
      <BlockTypeSelect />
      {/* Sublinhado, sobrescrito e subscrito virariam HTML, que o PDF não lê. */}
      <BoldItalicUnderlineToggles options={['Bold', 'Italic']} />
      <StrikeThroughSupSubToggles options={['Strikethrough']} />
      <Separator />
      {/* Sem a lista de tarefas: `- [ ]` não tem forma no papel. */}
      <ListsToggle options={['bullet', 'number']} />
      <Separator />
      <CreateLink />
      <InsertImage />
      <InsertTable />
      <InsertThematicBreak />
    </DiffSourceToggleWrapper>
  );
}

/**
 * O Markdown do documento no MDXEditor: edita-se o documento já formatado, e
 * o texto guardado continua sendo Markdown. As imagens seguem a regra do PDF —
 * o Markdown cita `imagens/<nome>`, a tela resolve contra o endereço do
 * documento, e só as imagens dos ativos aparecem.
 *
 * O que o MDXEditor faria virar HTML (sublinhado, sobrescrito, imagem
 * redimensionada, lista de tarefas) fica fora da barra: o PDF só entende
 * Markdown.
 */
export const EditorMarkdown = forwardRef<EditorMarkdownRef, Props>(function EditorMarkdown(
  { markdown, onMudar, imagens, versao, onImagem, salvo },
  ref,
) {
  const estilos = useEstilos();
  const editor = useRef<MDXEditorMethods>(null);
  const raiz = useRef<HTMLDivElement>(null);
  const ponte = useRef<Ponte | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  // O que o editor visual não lê, edita-se como texto: o documento continua valendo.
  useEffect(() => {
    if (erro) ponte.current?.mudarModo('source');
  }, [erro]);

  // O modo de diferenças compara com o que está gravado.
  useEffect(() => {
    ponte.current?.mudarBase(salvo);
  }, [salvo]);

  // Os plugins são lidos uma vez, na montagem; o que muda (as imagens, a
  // versão, o envio) é lido por referência.
  const atual = useRef({ imagens, versao, onImagem, onMudar });
  atual.current = { imagens, versao, onImagem, onMudar };

  useImperativeHandle(ref, () => ({
    obter: () => editor.current?.getMarkdown() ?? '',
    definir: (texto) => editor.current?.setMarkdown(texto),
    inserir: (texto) => {
      editor.current?.focus(() => editor.current?.insertMarkdown(texto));
    },
    focar: () => editor.current?.focus(),
    selecao: () => editor.current?.getSelectionMarkdown() ?? '',
    modo: (modo) => ponte.current?.mudarModo(modo),
    folha: () => raiz.current?.querySelector<HTMLElement>('.folha') ?? null,
  }));

  const plugins = useMemo(
    () => [
      headingsPlugin({ allowedHeadingLevels: [1, 2, 3, 4, 5, 6] }),
      listsPlugin(),
      quotePlugin(),
      thematicBreakPlugin(),
      linkPlugin(),
      linkDialogPlugin(),
      tablePlugin(),
      imagePlugin({
        imageUploadHandler: (arquivo) => atual.current.onImagem(arquivo),
        imageAutocompleteSuggestions: imagens.map((i) => i.referencia),
        // A imagem redimensionada vira <img width>, que o PDF não lê.
        disableImageResize: true,
        allowSetImageDimensions: false,
        imagePreviewHandler: async (origem) => {
          const nome = nomeCitado(origem);
          const imagem = nome ? atual.current.imagens.find((i) => i.nome === nome) : undefined;
          // Fora dos ativos, nada aparece — como no PDF.
          return imagem ? `${imagem.url}?v=${encodeURIComponent(atual.current.versao)}` : imagemAusente(origem);
        },
      }),
      codeBlockPlugin({ defaultCodeBlockLanguage: '' }),
      codeMirrorPlugin({ codeBlockLanguages: { '': 'Texto', json: 'JSON', xml: 'XML', sql: 'SQL', js: 'JavaScript' } }),
      markdownShortcutPlugin(),
      diffSourcePlugin({ viewMode: 'rich-text', diffMarkdown: salvo, readOnlyDiff: true }),
      toolbarPlugin({ toolbarContents: () => <BarraDeFerramentas ponte={ponte} /> }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );

  return (
    <div ref={raiz} className={estilos.raiz}>
      {erro && (
        <MessageBar intent="warning" layout="multiline" className={estilos.aviso}>
          <MessageBarBody>
            O editor visual não reconhece parte deste Markdown, e ele está aberto como texto. O documento e o PDF
            continuam valendo.
          </MessageBarBody>
        </MessageBar>
      )}
      <MDXEditor
        ref={editor}
        className={mergeClasses('editor-markdocs')}
        contentEditableClassName="markdown folha"
        markdown={markdown}
        plugins={plugins}
        translation={traducao}
        // O hífen é o marcador de lista mais comum, e o que o agente escreve.
        toMarkdownOptions={{ bullet: '-' }}
        spellCheck
        onChange={(texto, normalizacaoInicial) => {
          // A normalização do Markdown na carga (marcadores de lista,
          // espaços) não é edição de ninguém, e não é gravada.
          if (!normalizacaoInicial) atual.current.onMudar(texto);
        }}
        onError={({ error }) => setErro(error)}
      />
    </div>
  );
});
