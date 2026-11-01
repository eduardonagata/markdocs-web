import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Button, makeStyles, Tab, TabList, Text, tokens } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
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
  conteudo: {
    flex: 1,
    minHeight: 0,
    display: 'flex',
    flexDirection: 'column',
    animationName: { from: { opacity: 0, transform: 'translateY(4px)' }, to: { opacity: 1, transform: 'none' } },
    animationDuration: '260ms',
    animationTimingFunction: 'ease-out',
  },
});

/** O topo do app — a marca, as duas páginas e o "novo documento" — e a página aberta. */
export function Casca() {
  const estilos = useEstilos();
  const navegar = useNavigate();
  const { pathname } = useLocation();
  const secao = pathname.startsWith('/templates') ? 'templates' : 'documentos';
  const estreito = useEstreito(640);

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
        <Button appearance="primary" icon={<AddRegular />} aria-label="Novo documento" onClick={() => navegar('/documentos/novo')}>
          {estreito ? undefined : 'Novo documento'}
        </Button>
      </header>
      <main key={pathname.split('/').slice(0, 3).join('/')} className={estilos.conteudo}>
        <Outlet />
      </main>
    </div>
  );
}
