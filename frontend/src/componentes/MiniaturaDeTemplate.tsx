import { useQuery } from '@tanstack/react-query';
import { makeStyles, Spinner, tokens } from '@fluentui/react-components';
import { api, urls } from '../api/cliente';
import { VisualizadorPdf } from './VisualizadorPdf';

const useEstilos = makeStyles({
  raiz: {
    height: '220px',
    overflow: 'hidden',
    display: 'flex',
    alignItems: 'flex-start',
    justifyContent: 'center',
    paddingTop: tokens.spacingVerticalL,
    backgroundColor: 'var(--mesa)',
  },
});

/** A primeira página da amostra do template, composta pelo próprio backend. */
export function MiniaturaDeTemplate({ templateId }: { templateId: string }) {
  const estilos = useEstilos();
  const { data } = useQuery({
    queryKey: ['amostra', templateId],
    queryFn: () => api.pdf(urls.amostraDoTemplate(templateId)),
    staleTime: Infinity,
  });
  return <div className={estilos.raiz}>{data ? <VisualizadorPdf dados={data} somentePrimeira respiro={28} /> : <Spinner size="tiny" />}</div>;
}
