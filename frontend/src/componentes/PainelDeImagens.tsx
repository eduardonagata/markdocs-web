import { useRef } from 'react';
import { Button, Caption1, makeStyles, Text, tokens, Tooltip } from '@fluentui/react-components';
import { AddRegular, CopyRegular, DeleteRegular, TextBulletListAddRegular } from '@fluentui/react-icons';
import type { Imagem } from '../api/tipos';

const useEstilos = makeStyles({
  raiz: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  explicacao: { color: tokens.colorNeutralForeground3 },
  lista: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  item: {
    display: 'grid',
    gridTemplateColumns: '48px 1fr auto',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    padding: tokens.spacingVerticalXS,
    borderRadius: tokens.borderRadiusMedium,
    ':hover': { backgroundColor: tokens.colorNeutralBackground2 },
  },
  miniatura: {
    width: '48px',
    height: '48px',
    objectFit: 'cover',
    borderRadius: tokens.borderRadiusSmall,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground3,
  },
  nome: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  referencia: {
    fontFamily: tokens.fontFamilyMonospace,
    color: tokens.colorNeutralForeground3,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    display: 'block',
  },
  acoes: { display: 'flex' },
});

interface Props {
  imagens: Imagem[];
  versao: string;
  enviando: boolean;
  onEnviar: (arquivos: File[]) => void;
  onInserir: (imagem: Imagem) => void;
  onRemover: (imagem: Imagem) => void;
}

/**
 * Os ativos de imagem do documento. O Markdown cita cada um pelo caminho
 * relativo `imagens/<nome>` — o mesmo texto vale na tela, no PDF e numa
 * exportação — e só as imagens desta lista aparecem.
 */
export function PainelDeImagens({ imagens, versao, enviando, onEnviar, onInserir, onRemover }: Props) {
  const estilos = useEstilos();
  const entrada = useRef<HTMLInputElement>(null);

  return (
    <div className={estilos.raiz}>
      <Caption1 className={estilos.explicacao}>
        Cite uma imagem no Markdown por <code>imagens/&lt;nome&gt;</code>. Você também pode colar ou arrastar imagens direto no editor.
      </Caption1>
      <div>
        <Button icon={<AddRegular />} onClick={() => entrada.current?.click()} disabled={enviando}>
          {enviando ? 'Enviando…' : 'Adicionar imagem'}
        </Button>
        <input
          ref={entrada}
          type="file"
          accept="image/png,image/jpeg,image/gif,image/svg+xml,image/webp"
          multiple
          hidden
          onChange={(e) => {
            onEnviar(Array.from(e.target.files ?? []));
            e.target.value = '';
          }}
        />
      </div>
      <div className={estilos.lista}>
        {imagens.map((imagem) => (
          <div key={imagem.nome} className={estilos.item}>
            <img className={estilos.miniatura} src={`${imagem.url}?v=${encodeURIComponent(versao)}`} alt="" />
            <div style={{ minWidth: 0 }}>
              <Text className={estilos.nome} block>
                {imagem.nome}
              </Text>
              <span className={estilos.referencia}>{imagem.referencia}</span>
            </div>
            <div className={estilos.acoes}>
              <Tooltip content="Inserir no cursor" relationship="label">
                <Button appearance="subtle" size="small" icon={<TextBulletListAddRegular />} onClick={() => onInserir(imagem)} />
              </Tooltip>
              <Tooltip content="Copiar a referência" relationship="label">
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<CopyRegular />}
                  onClick={() => void navigator.clipboard?.writeText(`![${imagem.nome}](${imagem.referencia})`)}
                />
              </Tooltip>
              <Tooltip content="Remover dos ativos" relationship="label">
                <Button appearance="subtle" size="small" icon={<DeleteRegular />} onClick={() => onRemover(imagem)} />
              </Tooltip>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
