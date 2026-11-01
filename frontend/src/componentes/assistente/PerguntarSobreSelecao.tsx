import { useEffect, useState } from 'react';
import { Button, makeStyles } from '@fluentui/react-components';
import { SparkleRegular } from '@fluentui/react-icons';
import { atalho } from '../../assistente/atalho';

const useEstilos = makeStyles({
  botao: {
    position: 'fixed',
    zIndex: 900,
    boxShadow: '0 6px 18px rgba(28, 25, 23, 0.16)',
    borderRadius: '999px',
    animationName: { from: { opacity: 0, transform: 'translateY(4px)' }, to: { opacity: 1, transform: 'none' } },
    animationDuration: '160ms',
  },
});

interface Props {
  obterSelecao: () => string;
  /** A folha do editor: só seleções dentro dela contam. */
  dentroDe: () => HTMLElement | null;
  onPerguntar: (trecho: string) => void;
}

/**
 * Um botão que aparece junto do texto selecionado na folha, como o “Ask AI”
 * dos editores: leva o trecho ao assistente, que passa a falar sobre ele.
 */
export function PerguntarSobreSelecao({ obterSelecao, dentroDe, onPerguntar }: Props) {
  const estilos = useEstilos();
  const [posicao, setPosicao] = useState<{ top: number; left: number } | null>(null);

  useEffect(() => {
    let quadro = 0;
    const atualizar = () => {
      cancelAnimationFrame(quadro);
      quadro = requestAnimationFrame(() => {
        const selecao = window.getSelection();
        const folha = dentroDe();
        if (!selecao || selecao.isCollapsed || selecao.rangeCount === 0 || !folha || !folha.contains(selecao.anchorNode)) {
          setPosicao(null);
          return;
        }
        const caixa = selecao.getRangeAt(0).getBoundingClientRect();
        if (caixa.width === 0 && caixa.height === 0) return setPosicao(null);
        setPosicao({
          top: Math.min(window.innerHeight - 48, caixa.bottom + 8),
          left: Math.max(8, Math.min(window.innerWidth - 220, caixa.right - 110)),
        });
      });
    };
    const esconder = () => setPosicao(null);
    document.addEventListener('selectionchange', atualizar);
    window.addEventListener('scroll', esconder, true);
    return () => {
      cancelAnimationFrame(quadro);
      document.removeEventListener('selectionchange', atualizar);
      window.removeEventListener('scroll', esconder, true);
    };
  }, [dentroDe]);

  if (!posicao) return null;
  return (
    <Button
      className={estilos.botao}
      style={posicao}
      size="small"
      icon={<SparkleRegular />}
      title={`Levar o trecho ao assistente (${atalho})`}
      // Sem roubar a seleção antes do clique.
      onMouseDown={(e) => e.preventDefault()}
      onClick={() => {
        const trecho = obterSelecao();
        if (trecho.trim()) onPerguntar(trecho);
        setPosicao(null);
      }}
    >
      Perguntar ao assistente
    </Button>
  );
}
