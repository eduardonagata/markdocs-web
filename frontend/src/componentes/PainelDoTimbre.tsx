import { useRef, useState } from 'react';
import { Button, Caption1, makeStyles, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { ArrowUploadRegular, DeleteRegular, WarningRegular } from '@fluentui/react-icons';
import type { Documento, PaginasDoTimbre, Timbre } from '../api/tipos';

/** Os papéis do Core, em milímetros (largura × altura). */
const papeis: Record<string, { nome: string; largura: number; altura: number }> = {
  a4: { nome: 'A4', largura: 210, altura: 297 },
  a5: { nome: 'A5', largura: 148, altura: 210 },
  'us-letter': { nome: 'Carta', largura: 215.9, altura: 279.4 },
  'us-legal': { nome: 'Ofício', largura: 215.9, altura: 355.6 },
};

const aceitos = 'image/png,image/jpeg,image/gif,image/svg+xml,image/webp';

const useEstilos = makeStyles({
  raiz: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  explicacao: { color: tokens.colorNeutralForeground3 },
  papel: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
    padding: tokens.spacingHorizontalM,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  numeros: { fontVariantNumeric: 'tabular-nums', color: tokens.colorNeutralForeground2 },
  variante: { display: 'grid', gridTemplateColumns: '84px 1fr', gap: tokens.spacingHorizontalM, alignItems: 'start' },
  folha: {
    width: '84px',
    backgroundColor: '#fff',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    boxShadow: 'var(--folha-sombra)',
    objectFit: 'fill',
    display: 'block',
  },
  vazia: {
    width: '84px',
    border: `1px dashed ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground1,
    boxSizing: 'border-box',
  },
  detalhes: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS, minWidth: 0 },
  nome: { fontFamily: tokens.fontFamilyMonospace, fontSize: tokens.fontSizeBase100, color: tokens.colorNeutralForeground3, overflowWrap: 'anywhere' },
  acoes: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS },
  aviso: { display: 'flex', gap: '4px', color: tokens.colorPaletteMarigoldForeground2 },
});

interface Props {
  documento: Documento;
  enviando: boolean;
  onEnviar: (paginas: PaginasDoTimbre, arquivo: File) => void;
  onRemover: (paginas: PaginasDoTimbre) => void;
}

function Variante({
  titulo,
  paginas,
  timbre,
  versao,
  proporcao,
  nomeDoPapel,
  enviando,
  onEscolher,
  onRemover,
}: {
  titulo: string;
  paginas: PaginasDoTimbre;
  timbre: Timbre | null;
  versao: string;
  proporcao: number | null;
  nomeDoPapel: string;
  enviando: boolean;
  onEscolher: (paginas: PaginasDoTimbre) => void;
  onRemover: (paginas: PaginasDoTimbre) => void;
}) {
  const estilos = useEstilos();
  const [tamanho, setTamanho] = useState<{ largura: number; altura: number } | null>(null);
  const altura = `${Math.round(84 / (proporcao ?? 210 / 297))}px`;
  const destoa =
    proporcao !== null && tamanho !== null && tamanho.largura > 0 && tamanho.altura > 0
      ? Math.abs(tamanho.largura / tamanho.altura - proporcao) / proporcao > 0.02
      : false;

  return (
    <div className={estilos.variante}>
      {timbre ? (
        <img
          className={estilos.folha}
          style={{ height: altura }}
          src={`${timbre.url}?v=${encodeURIComponent(versao)}`}
          alt={`Timbre: ${titulo.toLowerCase()}`}
          onLoad={(e) => setTamanho({ largura: e.currentTarget.naturalWidth, altura: e.currentTarget.naturalHeight })}
        />
      ) : (
        <div className={mergeClasses(estilos.vazia)} style={{ height: altura }} aria-hidden />
      )}
      <div className={estilos.detalhes}>
        <Text weight="semibold">{titulo}</Text>
        {timbre ? <span className={estilos.nome}>{timbre.nome}</span> : <Caption1 className={estilos.explicacao}>Sem timbre</Caption1>}
        {destoa && tamanho && (
          <Caption1 className={estilos.aviso}>
            <WarningRegular />
            {tamanho.largura} × {tamanho.altura} px não é a proporção do papel {nomeDoPapel}: no PDF, a imagem é esticada à página.
          </Caption1>
        )}
        <div className={estilos.acoes}>
          <Button size="small" icon={<ArrowUploadRegular />} disabled={enviando} onClick={() => onEscolher(paginas)}>
            {timbre ? 'Trocar' : 'Enviar'}
          </Button>
          {timbre && (
            <Button size="small" appearance="subtle" icon={<DeleteRegular />} disabled={enviando} onClick={() => onRemover(paginas)}>
              Tirar
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

/**
 * O papel timbrado do documento: a imagem de fundo das páginas — logotipo,
 * faixas, cores da firma já desenhados. O app não ajusta a imagem: ela ocupa a
 * página inteira, como está. Por isso a tela diz o tamanho do papel, e avisa
 * quando a proporção da imagem é outra.
 */
export function PainelDoTimbre({ documento, enviando, onEnviar, onRemover }: Props) {
  const estilos = useEstilos();
  const entrada = useRef<HTMLInputElement>(null);
  const destino = useRef<PaginasDoTimbre>('todas');

  const eixoDoPapel = documento.eixos.find((e) => e.id === 'pagina.papel');
  const papel = eixoDoPapel?.valor.tipo === 'escolha' ? papeis[eixoDoPapel.valor.id] : undefined;
  const proporcao = papel ? papel.largura / papel.altura : null;
  const pixels = (mm: number) => Math.round((mm / 25.4) * 300);

  const escolher = (paginas: PaginasDoTimbre) => {
    destino.current = paginas;
    entrada.current?.click();
  };

  const { primeira, demais } = documento.timbres;
  const iguais = primeira?.nome === demais?.nome;

  return (
    <div className={estilos.raiz}>
      <Caption1 className={estilos.explicacao}>
        A imagem de fundo do papel: o logotipo, as faixas e as cores da firma, já desenhados. Ela ocupa a página inteira,
        como está — sem cortes nem ajustes —, por baixo do texto.
      </Caption1>

      {papel && (
        <div className={estilos.papel}>
          <Text size={200} weight="semibold">
            Papel {papel.nome}
          </Text>
          <Caption1 className={estilos.numeros}>
            {String(papel.largura).replace('.', ',')} × {String(papel.altura).replace('.', ',')} mm · a 300 dpi,{' '}
            {pixels(papel.largura)} × {pixels(papel.altura)} px
          </Caption1>
          <Caption1 className={estilos.explicacao}>Prepare a imagem nessa proporção, com as margens do texto livres.</Caption1>
        </div>
      )}

      <div>
        <Button appearance="primary" icon={<ArrowUploadRegular />} disabled={enviando} onClick={() => escolher('todas')}>
          {enviando ? 'Enviando…' : primeira || demais ? 'Trocar em todas as páginas' : 'Enviar para todas as páginas'}
        </Button>
        <input
          ref={entrada}
          type="file"
          accept={aceitos}
          hidden
          onChange={(e) => {
            const arquivo = e.target.files?.[0];
            if (arquivo) onEnviar(destino.current, arquivo);
            e.target.value = '';
          }}
        />
      </div>

      <Variante
        titulo="Primeira página"
        paginas="primeira"
        timbre={primeira}
        versao={documento.alteradoEm}
        proporcao={proporcao}
        nomeDoPapel={papel?.nome ?? ''}
        enviando={enviando}
        onEscolher={escolher}
        onRemover={onRemover}
      />
      <Variante
        titulo="Demais páginas"
        paginas="demais"
        timbre={demais}
        versao={documento.alteradoEm}
        proporcao={proporcao}
        nomeDoPapel={papel?.nome ?? ''}
        enviando={enviando}
        onEscolher={escolher}
        onRemover={onRemover}
      />
      {primeira && demais && iguais && (
        <Caption1 className={estilos.explicacao}>O mesmo timbre vale em todas as páginas. Troque um deles para diferenciar a primeira.</Caption1>
      )}
    </div>
  );
}
