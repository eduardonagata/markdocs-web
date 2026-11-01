import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  makeStyles,
  MessageBar,
  MessageBarBody,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Title3,
  tokens,
} from '@fluentui/react-components';
import { DeleteRegular, DocumentTextRegular } from '@fluentui/react-icons';
import { useDocumentos, useRemoverDocumento } from '../api/consultas';
import type { DocumentoResumo } from '../api/tipos';
import { quando } from '../util';

const useEstilos = makeStyles({
  pagina: {
    flex: 1,
    overflow: 'auto',
    padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalXXL}`,
  },
  miolo: { maxWidth: '1040px', margin: '0 auto' },
  cabecalho: { display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: tokens.spacingVerticalL },
  tabela: {
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusLarge,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    overflow: 'hidden',
  },
  linha: { cursor: 'pointer' },
  secundario: { color: tokens.colorNeutralForeground3 },
  vazio: {
    textAlign: 'center',
    padding: '96px 24px',
    color: tokens.colorNeutralForeground3,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalM,
  },
});

export function Documentos() {
  const estilos = useEstilos();
  const navegar = useNavigate();
  const { data, isLoading, error } = useDocumentos();
  const remover = useRemoverDocumento();
  const [aRemover, setARemover] = useState<DocumentoResumo | null>(null);

  return (
    <div className={estilos.pagina}>
      <div className={estilos.miolo}>
        <div className={estilos.cabecalho}>
          <Title3>Documentos</Title3>
          {data && data.length > 0 && <Text className={estilos.secundario}>{data.length} documento(s)</Text>}
        </div>

        {isLoading && <Spinner />}
        {error && (
          <MessageBar intent="error">
            <MessageBarBody>Não foi possível listar os documentos: {error.message}</MessageBarBody>
          </MessageBar>
        )}

        {data && data.length === 0 && (
          <div className={estilos.vazio}>
            <DocumentTextRegular fontSize={40} />
            <Text size={400}>Nenhum documento ainda.</Text>
            <Text>Escolha um template e cole um Markdown — o PDF aparece na hora.</Text>
            <Button appearance="primary" onClick={() => navegar('/documentos/novo')}>
              Criar o primeiro documento
            </Button>
          </div>
        )}

        {data && data.length > 0 && (
          <div className={estilos.tabela}>
            <Table aria-label="Documentos">
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Título</TableHeaderCell>
                  <TableHeaderCell style={{ width: 160 }}>Tipo</TableHeaderCell>
                  <TableHeaderCell style={{ width: 200 }}>Template</TableHeaderCell>
                  <TableHeaderCell style={{ width: 200 }}>Alterado</TableHeaderCell>
                  <TableHeaderCell style={{ width: 56 }} />
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.map((d) => (
                  <TableRow key={d.id} className={estilos.linha} onClick={() => navegar(`/documentos/${d.id}`)}>
                    <TableCell>
                      <TableCellLayout media={<DocumentTextRegular />}>{d.titulo}</TableCellLayout>
                    </TableCell>
                    <TableCell>{d.tipo}</TableCell>
                    <TableCell className={estilos.secundario}>{d.templateDeOrigem ?? '—'}</TableCell>
                    <TableCell className={estilos.secundario}>{quando(d.alteradoEm)}</TableCell>
                    <TableCell>
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<DeleteRegular />}
                        aria-label={`Excluir ${d.titulo}`}
                        onClick={(e) => {
                          e.stopPropagation();
                          setARemover(d);
                        }}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>

      <Dialog open={aRemover !== null} onOpenChange={(_, d) => !d.open && setARemover(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Excluir documento?</DialogTitle>
            <DialogContent>“{aRemover?.titulo}” e as imagens dele serão removidos. Não dá para desfazer.</DialogContent>
            <DialogActions>
              <Button onClick={() => setARemover(null)}>Cancelar</Button>
              <Button
                appearance="primary"
                disabled={remover.isPending}
                onClick={() => aRemover && remover.mutate(aRemover.id, { onSuccess: () => setARemover(null) })}
              >
                Excluir
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
