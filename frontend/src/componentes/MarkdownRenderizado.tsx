import { makeStyles } from '@fluentui/react-components';
import Markdown, { defaultUrlTransform, type Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import type { Imagem } from '../api/tipos';

const useEstilos = makeStyles({
  folha: {
    backgroundColor: '#fff',
    boxShadow: 'var(--folha-sombra)',
    maxWidth: '760px',
    margin: '24px auto',
    padding: '56px 64px',
    boxSizing: 'border-box',
    '@media (max-width: 700px)': { padding: '28px 24px', margin: '12px' },
  },
});

interface Props {
  markdown: string;
  /** A base do documento, contra a qual `imagens/<nome>` é resolvido. */
  base: string;
  /** As imagens dos ativos do documento. Só elas são mostradas. */
  imagens: Imagem[];
  /** Muda quando o documento muda, para o navegador revalidar as imagens. */
  versao: string;
}

/** O nome citado por `imagens/<nome>` ou `./imagens/<nome>`, ou null. */
function nomeCitado(destino: string): string | null {
  const caminho = destino.startsWith('./') ? destino.slice(2) : destino;
  if (!caminho.startsWith('imagens/')) return null;
  const nome = decodeURIComponent(caminho.slice('imagens/'.length));
  return nome.includes('/') ? null : nome;
}

/**
 * O Markdown renderizado sobre uma folha. As imagens seguem a mesma regra do
 * PDF: o Markdown cita `imagens/<nome>`, o caminho é resolvido contra o
 * endereço do documento, e só as imagens dos ativos aparecem — as demais
 * ficam no texto alternativo.
 */
export function MarkdownRenderizado({ markdown, base, imagens, versao }: Props) {
  const estilos = useEstilos();
  const nomes = new Set(imagens.map((i) => i.nome));

  const componentes: Components = {
    img: ({ src, alt }) => {
      const nome = src ? nomeCitado(src) : null;
      if (nome && nomes.has(nome)) {
        return <img src={`${base}imagens/${encodeURIComponent(nome)}?v=${encodeURIComponent(versao)}`} alt={alt ?? ''} />;
      }
      return (
        <span className="imagem-ausente" title={src ?? ''}>
          {alt || src}
        </span>
      );
    },
    a: ({ href, children }) => (
      <a href={href} target="_blank" rel="noreferrer">
        {children}
      </a>
    ),
  };

  return (
    <article className={`${estilos.folha} markdown`}>
      <Markdown remarkPlugins={[remarkGfm]} components={componentes} urlTransform={(url) => (nomeCitado(url) ? url : defaultUrlTransform(url))}>
        {markdown}
      </Markdown>
    </article>
  );
}
