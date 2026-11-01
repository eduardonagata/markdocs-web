import type { ReactNode } from 'react';
import { makeStyles, mergeClasses, Tab, TabList, tokens, Tooltip } from '@fluentui/react-components';
import {
  DocumentPdfRegular,
  DocumentSplitHintRegular,
  EyeRegular,
  TextDescriptionRegular,
} from '@fluentui/react-icons';
import { useEstreito } from '../util';

/**
 * Os três modos de ver o mesmo documento — o Markdown como texto, renderizado,
 * e o PDF — e o lado a lado, que é texto e PDF juntos.
 */
export type Modo = 'texto' | 'lado-a-lado' | 'visualizacao' | 'pdf';

type Lente = 'texto' | 'visualizacao' | 'pdf';

const abertas: Record<Modo, Lente[]> = {
  texto: ['texto'],
  'lado-a-lado': ['texto', 'pdf'],
  visualizacao: ['visualizacao'],
  pdf: ['pdf'],
};

const rotulos: Record<Lente, string> = { texto: 'Markdown', visualizacao: 'Visualização', pdf: 'PDF' };

const icones: Record<Lente, ReactNode> = {
  texto: <TextDescriptionRegular />,
  visualizacao: <EyeRegular />,
  pdf: <DocumentPdfRegular />,
};

const useEstilos = makeStyles({
  raiz: {
    display: 'flex',
    flex: 1,
    minHeight: 0,
    gap: '1px',
    backgroundColor: tokens.colorNeutralStroke1,
  },
  lente: {
    position: 'relative',
    display: 'flex',
    minWidth: '44px',
    flexBasis: '44px',
    flexGrow: 0,
    flexShrink: 0,
    overflow: 'hidden',
    backgroundColor: 'var(--mesa)',
    // A transição é o que liga os três modos: a lente que abre cresce
    // enquanto as outras recolhem até virar um trilho.
    transitionProperty: 'flex-grow, flex-basis',
    transitionDuration: '420ms',
    transitionTimingFunction: 'cubic-bezier(0.22, 1, 0.36, 1)',
  },
  aberta: {
    flexGrow: 1,
    flexBasis: '0px',
    flexShrink: 1,
  },
  conteudo: {
    flex: 1,
    minWidth: 0,
    overflow: 'auto',
    opacity: 1,
    transition: 'opacity 260ms ease 120ms',
  },
  escondido: {
    opacity: 0,
    pointerEvents: 'none',
    transition: 'opacity 120ms ease',
  },
  trilho: {
    position: 'absolute',
    inset: 0,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalS,
    paddingTop: tokens.spacingVerticalM,
    border: 'none',
    backgroundColor: tokens.colorNeutralBackground2,
    color: tokens.colorNeutralForeground3,
    cursor: 'pointer',
    fontSize: tokens.fontSizeBase200,
    fontFamily: tokens.fontFamilyBase,
    ':hover': { color: tokens.colorNeutralForeground1, backgroundColor: tokens.colorNeutralBackground3 },
  },
  rotuloVertical: {
    writingMode: 'vertical-rl',
    letterSpacing: '0.04em',
  },
});

interface PropsSeletor {
  modo: Modo;
  onModo: (modo: Modo) => void;
}

/** O seletor dos modos, para a barra de ferramentas. */
export function SeletorDeModo({ modo, onModo }: PropsSeletor) {
  // Estreito, só os ícones — e sem o lado a lado, que não cabe.
  const estreito = useEstreito();
  const rotulo = (texto: string) => (estreito ? undefined : texto);
  return (
    <TabList size="small" selectedValue={modo} onTabSelect={(_, d) => onModo(d.value as Modo)}>
      <Tooltip content="Markdown como texto" relationship="description">
        <Tab value="texto" icon={<TextDescriptionRegular />} aria-label="Markdown">{rotulo('Markdown')}</Tab>
      </Tooltip>
      {!estreito && (
        <Tooltip content="Markdown e PDF, lado a lado" relationship="description">
          <Tab value="lado-a-lado" icon={<DocumentSplitHintRegular />}>Lado a lado</Tab>
        </Tooltip>
      )}
      <Tooltip content="Markdown renderizado" relationship="description">
        <Tab value="visualizacao" icon={<EyeRegular />} aria-label="Visualização">{rotulo('Visualização')}</Tab>
      </Tooltip>
      <Tooltip content="O PDF composto" relationship="description">
        <Tab value="pdf" icon={<DocumentPdfRegular />} aria-label="PDF">{rotulo('PDF')}</Tab>
      </Tooltip>
    </TabList>
  );
}

interface Props extends PropsSeletor {
  texto: ReactNode;
  visualizacao: ReactNode;
  pdf: ReactNode;
}

/**
 * As três lentes lado a lado, numa ordem fixa — texto, visualização, PDF. O
 * modo decide quais estão abertas; as fechadas viram um trilho estreito, que
 * abre a lente com um clique. As três ficam montadas: trocar de modo não
 * perde o cursor do editor nem redesenha o PDF.
 */
export function Vistas({ modo, onModo, texto, visualizacao, pdf }: Props) {
  const estilos = useEstilos();
  const estreito = useEstreito();
  if (estreito && modo === 'lado-a-lado') modo = 'texto';
  const conteudos: Record<Lente, ReactNode> = { texto, visualizacao, pdf };

  return (
    <div className={estilos.raiz}>
      {(['texto', 'visualizacao', 'pdf'] as Lente[]).map((lente) => {
        const aberta = abertas[modo].includes(lente);
        return (
          <section key={lente} className={mergeClasses(estilos.lente, aberta && estilos.aberta)} aria-label={rotulos[lente]}>
            <div className={mergeClasses(estilos.conteudo, !aberta && estilos.escondido)} aria-hidden={!aberta}>
              {conteudos[lente]}
            </div>
            {!aberta && (
              <button type="button" className={estilos.trilho} onClick={() => onModo(lente)} title={`Abrir ${rotulos[lente]}`}>
                {icones[lente]}
                <span className={estilos.rotuloVertical}>{rotulos[lente]}</span>
              </button>
            )}
          </section>
        );
      })}
    </div>
  );
}
