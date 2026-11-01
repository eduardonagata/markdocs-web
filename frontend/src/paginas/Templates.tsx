import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Caption1,
  Card,
  CardFooter,
  CardHeader,
  CardPreview,
  makeStyles,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  Title3,
  tokens,
} from '@fluentui/react-components';
import { useTemplates } from '../api/consultas';
import { MiniaturaDeTemplate } from '../componentes/MiniaturaDeTemplate';
import { quando } from '../util';

const useEstilos = makeStyles({
  pagina: { flex: 1, overflow: 'auto', padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalXXL}` },
  miolo: { maxWidth: '1040px', margin: '0 auto' },
  cabecalho: { marginBottom: tokens.spacingVerticalL, display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  secundario: { color: tokens.colorNeutralForeground3 },
  grade: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(230px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  cartao: { padding: 0, overflow: 'hidden' },
  rodape: { padding: `0 ${tokens.spacingHorizontalM} ${tokens.spacingVerticalM}` },
  cabecalhoDoCartao: { padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM} 0` },
});

export function Templates() {
  const estilos = useEstilos();
  const navegar = useNavigate();
  const { data, isLoading, error } = useTemplates();

  return (
    <div className={estilos.pagina}>
      <div className={estilos.miolo}>
        <div className={estilos.cabecalho}>
          <Title3>Templates</Title3>
          <Text className={estilos.secundario}>
            A papelaria dos documentos. Os de fábrica vêm com o app; os demais nascem de um documento formatado (“Salvar como template”).
          </Text>
        </div>

        {isLoading && <Spinner />}
        {error && (
          <MessageBar intent="error">
            <MessageBarBody>Não foi possível listar os templates: {error.message}</MessageBarBody>
          </MessageBar>
        )}

        <div className={estilos.grade}>
          {data?.map((t) => (
            <Card key={t.id} className={estilos.cartao} appearance="outline">
              <CardPreview>
                <MiniaturaDeTemplate templateId={t.id} />
              </CardPreview>
              <CardHeader
                className={estilos.cabecalhoDoCartao}
                header={<Text weight="semibold">{t.nome}</Text>}
                description={
                  <Caption1 className={estilos.secundario}>
                    {t.tipo} · {t.deFabrica ? 'de fábrica' : `criado ${quando(t.criadoEm)}`}
                  </Caption1>
                }
                action={t.deFabrica ? <Badge appearance="outline" color="informative">Fábrica</Badge> : undefined}
              />
              <CardFooter className={estilos.rodape}>
                <Button size="small" onClick={() => navegar(`/documentos/novo?template=${t.id}`)}>
                  Novo documento
                </Button>
              </CardFooter>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}
