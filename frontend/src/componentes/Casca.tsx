import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Button, makeStyles, mergeClasses, Tab, TabList, Text, tokens, ToggleButton, Tooltip } from '@fluentui/react-components';
import { AddRegular, SparkleRegular } from '@fluentui/react-icons';
import { AssistenteProvider, useAssistente } from '../assistente/Assistente';
import { atalho } from '../assistente/atalho';
import { FerramentasGlobais } from '../assistente/FerramentasGlobais';
import { PainelDoAssistente } from '../assistente/Painel';
import { useEstreito } from '../util';

const useEstilos = makeStyles({
  raiz: {
    height: '100%',
    display: 'flex',
    flexDirection: 'column',
  },
  topo: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXL,
    height: '52px',
    padding: `0 ${tokens.spacingHorizontalXL}`,
    '@media (max-width: 640px)': { gap: tokens.spacingHorizontalM, padding: `0 ${tokens.spacingHorizontalM}` },
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    flexShrink: 0,
  },
  marca: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    fontWeight: tokens.fontWeightSemibold,
    letterSpacing: '-0.01em',
    cursor: 'pointer',
    userSelect: 'none',
  },
  folhinha: {
    width: '14px',
    height: '18px',
    border: `1.5px solid ${tokens.colorNeutralForeground1}`,
    borderRadius: '2px',
    backgroundImage: `linear-gradient(${tokens.colorNeutralForeground1}, ${tokens.colorNeutralForeground1})`,
    backgroundSize: '8px 1.5px',
    backgroundRepeat: 'no-repeat',
    backgroundPosition: '2px 5px',
    boxSizing: 'border-box',
  },
  espaco: { flex: 1 },
  botaoDoAssistente: { position: 'relative' },
  // O assistente trabalhando, ou esperando uma aprovação com o painel fechado.
  sinal: {
    position: 'absolute',
    top: '5px',
    right: '5px',
    width: '7px',
    height: '7px',
    borderRadius: '50%',
    backgroundColor: 'var(--assistente)',
  },
  // Em tela larga, o conteúdo abre espaço para o painel: o assistente age
  // sobre a tela, e o que ele faz precisa continuar à vista.
  comAssistente: { '@media (min-width: 1100px)': { paddingRight: '424px' } },
  pulsando: {
    animationName: { '0%': { opacity: 1 }, '50%': { opacity: 0.25 }, '100%': { opacity: 1 } },
    animationDuration: '1.2s',
    animationIterationCount: 'infinite',
  },
  conteudo: {
    flex: 1,
    minHeight: 0,
    display: 'flex',
    flexDirection: 'column',
    transition: 'padding-right 260ms cubic-bezier(0.22, 1, 0.36, 1)',
    animationName: { from: { opacity: 0, transform: 'translateY(4px)' }, to: { opacity: 1, transform: 'none' } },
    animationDuration: '260ms',
    animationTimingFunction: 'ease-out',
  },
});

/** O botão do assistente no topo: abre e fecha o painel, e sinaliza quando ele trabalha ou espera você. */
function BotaoDoAssistente({ compacto }: { compacto: boolean }) {
  const estilos = useEstilos();
  const a = useAssistente();
  const esperando = a.aprovacoes.size > 0 && !a.aberto;
  return (
    <Tooltip content={`Assistente (${atalho})`} relationship="label">
      <ToggleButton
        className={estilos.botaoDoAssistente}
        appearance="subtle"
        icon={<SparkleRegular />}
        checked={a.aberto}
        onClick={a.alternar}
        aria-label="Assistente"
      >
        {compacto ? undefined : 'Assistente'}
        {(a.rodando || esperando) && <span className={mergeClasses(estilos.sinal, a.rodando && !esperando && estilos.pulsando)} />}
      </ToggleButton>
    </Tooltip>
  );
}

/** O topo do app — a marca, as duas páginas e o "novo documento" — e a página aberta, sob o assistente. */
export function Casca() {
  return (
    <AssistenteProvider>
      <FerramentasGlobais />
      <Moldura />
      <PainelDoAssistente />
    </AssistenteProvider>
  );
}

function Moldura() {
  const estilos = useEstilos();
  const navegar = useNavigate();
  const { pathname } = useLocation();
  const secao = pathname.startsWith('/templates') ? 'templates' : 'documentos';
  const estreito = useEstreito(640);
  const { aberto } = useAssistente();

  return (
    <div className={estilos.raiz}>
      <header className={estilos.topo}>
        <div className={estilos.marca} onClick={() => navegar('/documentos')}>
          <span className={estilos.folhinha} aria-hidden />
          {!estreito && (
            <Text weight="semibold" size={400}>
              MarkDocs
            </Text>
          )}
        </div>
        <TabList selectedValue={secao} onTabSelect={(_, d) => navegar(`/${String(d.value)}`)} size="small">
          <Tab value="documentos">Documentos</Tab>
          <Tab value="templates">Templates</Tab>
        </TabList>
        <div className={estilos.espaco} />
        <BotaoDoAssistente compacto={estreito} />
        <Button appearance="primary" icon={<AddRegular />} aria-label="Novo documento" onClick={() => navegar('/documentos/novo')}>
          {estreito ? undefined : 'Novo documento'}
        </Button>
      </header>
      <main key={pathname.split('/').slice(0, 3).join('/')} className={mergeClasses(estilos.conteudo, aberto && estilos.comAssistente)}>
        <Outlet />
      </main>
    </div>
  );
}
