import { Button, Caption1, makeStyles, Spinner, tokens } from '@fluentui/react-components';
import { ArrowUndoRegular, CheckmarkRegular, DocumentSplitHintRegular, SparkleRegular } from '@fluentui/react-icons';

const useEstilos = makeStyles({
  barra: {
    position: 'sticky',
    bottom: '16px',
    zIndex: 3,
    alignSelf: 'center',
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'nowrap',
    maxWidth: 'calc(100% - 32px)',
    gap: tokens.spacingHorizontalS,
    margin: `0 ${tokens.spacingHorizontalL} ${tokens.spacingVerticalL}`,
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS} ${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
    backgroundColor: tokens.colorNeutralBackground1,
    border: '1px solid var(--assistente-borda)',
    borderRadius: '999px',
    boxShadow: '0 8px 24px rgba(28, 25, 23, 0.14)',
    animationName: { from: { opacity: 0, transform: 'translateY(8px)' }, to: { opacity: 1, transform: 'none' } },
    animationDuration: '240ms',
    animationTimingFunction: 'ease-out',
  },
  botao: { whiteSpace: 'nowrap', flexShrink: 0 },
  resumo: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    color: 'var(--assistente)',
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    minWidth: 0,
  },
});

interface Props {
  resumo: string;
  salvando: boolean;
  onVerDiferencas: () => void;
  onDesfazer: () => void;
  onManter: () => void;
}

/**
 * O que o assistente alterou e ainda não foi salvo: a decisão é do usuário —
 * ver as diferenças, desfazer tudo, ou manter e salvar. Enquanto ela não vem,
 * o salvamento automático fica suspenso.
 */
export function BarraDeRevisao({ resumo, salvando, onVerDiferencas, onDesfazer, onManter }: Props) {
  const estilos = useEstilos();
  return (
    <div className={estilos.barra} role="region" aria-label="Alterações do assistente">
      <Caption1 className={estilos.resumo}>
        <SparkleRegular /> {resumo}
      </Caption1>
      <Button className={estilos.botao} size="small" appearance="subtle" icon={<DocumentSplitHintRegular />} onClick={onVerDiferencas}>
        Diferenças
      </Button>
      <Button className={estilos.botao} size="small" appearance="subtle" icon={<ArrowUndoRegular />} onClick={onDesfazer} disabled={salvando}>
        Desfazer
      </Button>
      <Button
        className={estilos.botao}
        size="small"
        appearance="primary"
        icon={salvando ? <Spinner size="extra-tiny" /> : <CheckmarkRegular />}
        onClick={onManter}
        disabled={salvando}
      >
        Manter e salvar
      </Button>
    </div>
  );
}
