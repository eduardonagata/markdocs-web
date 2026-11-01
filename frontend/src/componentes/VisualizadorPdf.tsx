import { useEffect, useRef, useState } from 'react';
import { makeStyles, mergeClasses, Spinner, tokens } from '@fluentui/react-components';
import * as pdfjs from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';

pdfjs.GlobalWorkerOptions.workerSrc = workerUrl;

const useEstilos = makeStyles({
  raiz: {
    position: 'relative',
    width: '100%',
  },
  paginas: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: '20px',
    transition: 'opacity 200ms ease',
  },
  pagina: {
    display: 'block',
    backgroundColor: '#fff',
    boxShadow: 'var(--folha-sombra)',
  },
  carregando: {
    position: 'absolute',
    top: tokens.spacingVerticalL,
    right: tokens.spacingHorizontalL,
  },
});

interface Props {
  /** Os bytes do PDF. Enquanto um PDF novo é preparado, o anterior continua na tela. */
  dados: ArrayBuffer | undefined;
  /** Só a primeira página (miniatura). */
  somentePrimeira?: boolean;
  /** Margem lateral, em pixels, entre a folha e a borda do contêiner. */
  respiro?: number;
  className?: string;
}

/**
 * As páginas do PDF desenhadas com o pdf.js, cada uma do tamanho da largura
 * disponível. As páginas novas só substituem as antigas depois de prontas: a
 * folha não pisca quando a formatação muda.
 */
export function VisualizadorPdf({ dados, somentePrimeira = false, respiro = 24, className }: Props) {
  const estilos = useEstilos();
  const raiz = useRef<HTMLDivElement>(null);
  const paginas = useRef<HTMLDivElement>(null);
  const [largura, setLargura] = useState(0);
  const [desenhando, setDesenhando] = useState(false);

  useEffect(() => {
    const elemento = raiz.current;
    if (!elemento) return;
    let quadro = 0;
    const observador = new ResizeObserver(([entrada]) => {
      cancelAnimationFrame(quadro);
      quadro = requestAnimationFrame(() => setLargura(Math.floor(entrada?.contentRect.width ?? 0)));
    });
    observador.observe(elemento);
    return () => {
      cancelAnimationFrame(quadro);
      observador.disconnect();
    };
  }, []);

  useEffect(() => {
    if (!dados || largura < 40) return;
    let cancelado = false;
    setDesenhando(true);

    (async () => {
      // O pdf.js transfere o buffer para o worker: entrega-se uma cópia.
      const documento = await pdfjs.getDocument({ data: dados.slice(0) }).promise;
      try {
        const total = somentePrimeira ? 1 : documento.numPages;
        const telas: HTMLCanvasElement[] = [];
        const densidade = Math.min(window.devicePixelRatio || 1, 2);

        for (let numero = 1; numero <= total; numero++) {
          const pagina = await documento.getPage(numero);
          const natural = pagina.getViewport({ scale: 1 });
          // Cabe na largura, sem ampliar além de 150%: uma folha A5 não vira um cartaz.
          const escala = Math.max(0.1, Math.min(1.5, (largura - respiro * 2) / natural.width));
          const vista = pagina.getViewport({ scale: escala * densidade });

          const tela = document.createElement('canvas');
          tela.width = Math.floor(vista.width);
          tela.height = Math.floor(vista.height);
          tela.style.width = `${Math.floor(vista.width / densidade)}px`;
          tela.style.height = `${Math.floor(vista.height / densidade)}px`;
          tela.className = estilos.pagina;

          await pagina.render({ canvasContext: tela.getContext('2d')!, viewport: vista }).promise;
          if (cancelado) return;
          telas.push(tela);
        }

        if (!cancelado && paginas.current) paginas.current.replaceChildren(...telas);
      } finally {
        void documento.destroy();
        if (!cancelado) setDesenhando(false);
      }
    })().catch(() => {
      if (!cancelado) setDesenhando(false);
    });

    return () => {
      cancelado = true;
    };
  }, [dados, largura, somentePrimeira, respiro, estilos.pagina]);

  return (
    <div ref={raiz} className={mergeClasses(estilos.raiz, className)}>
      <div ref={paginas} className={estilos.paginas} />
      {desenhando && !somentePrimeira && <Spinner size="tiny" className={estilos.carregando} />}
    </div>
  );
}
