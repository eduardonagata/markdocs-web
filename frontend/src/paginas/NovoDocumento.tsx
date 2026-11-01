import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Button,
  Caption1,
  makeStyles,
  mergeClasses,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  shorthands,
  Textarea,
  Title3,
  tokens,
} from '@fluentui/react-components';
import { ClipboardPasteRegular } from '@fluentui/react-icons';
import { useCriarDocumento, useTemplates } from '../api/consultas';
import { MiniaturaDeTemplate } from '../componentes/MiniaturaDeTemplate';

const useEstilos = makeStyles({
  pagina: { flex: 1, overflow: 'auto', padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalXXL}` },
  miolo: { maxWidth: '1040px', margin: '0 auto', display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXL },
  passo: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  rotuloDoPasso: { color: tokens.colorNeutralForeground3, textTransform: 'uppercase', letterSpacing: '0.06em', fontSize: tokens.fontSizeBase100 },
  templates: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(170px, 1fr))', gap: tokens.spacingHorizontalM },
  template: {
    textAlign: 'left',
    padding: 0,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusLarge,
    overflow: 'hidden',
    backgroundColor: tokens.colorNeutralBackground1,
    cursor: 'pointer',
    fontFamily: tokens.fontFamilyBase,
    transition: 'border-color 160ms ease, box-shadow 160ms ease',
    ':hover': { ...shorthands.borderColor(tokens.colorNeutralStroke1Hover) },
  },
  selecionado: {
    ...shorthands.borderColor(tokens.colorNeutralForeground1),
    boxShadow: `0 0 0 1px ${tokens.colorNeutralForeground1}`,
    ':hover': { ...shorthands.borderColor(tokens.colorNeutralForeground1) },
  },
  nomeDoTemplate: { padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`, display: 'flex', flexDirection: 'column' },
  secundario: { color: tokens.colorNeutralForeground3 },
  zona: {
    border: `1.5px dashed ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusLarge,
    backgroundColor: tokens.colorNeutralBackground1,
    padding: '48px 24px',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalM,
    textAlign: 'center',
    outline: 'none',
    transition: 'border-color 160ms ease, background-color 160ms ease',
    ':focus-visible': { ...shorthands.borderColor(tokens.colorNeutralForeground1) },
  },
  tecla: {
    fontFamily: tokens.fontFamilyMonospace,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderBottomWidth: '2px',
    borderRadius: tokens.borderRadiusSmall,
    padding: '1px 6px',
    fontSize: tokens.fontSizeBase200,
  },
  escrever: { width: '100%', maxWidth: '720px', display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS, alignItems: 'flex-end' },
});

const ehMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform);

/**
 * Criar um documento é escolher um template e colar o Markdown. Assim que o
 * texto é colado, o documento é criado e o editor abre com o PDF ao lado.
 */
export function NovoDocumento() {
  const estilos = useEstilos();
  const navegar = useNavigate();
  const [parametros] = useSearchParams();
  const { data: templates, isLoading } = useTemplates();
  const criar = useCriarDocumento();
  const [escolhido, setEscolhido] = useState<string | null>(parametros.get('template'));
  const [escrito, setEscrito] = useState('');
  const zona = useRef<HTMLDivElement>(null);

  const templateId = escolhido ?? templates?.[0]?.id ?? null;

  const criarCom = (corpo: string) => {
    if (!templateId || !corpo.trim() || criar.isPending) return;
    criar.mutate(
      { templateId, corpo },
      { onSuccess: (documento) => navegar(`/documentos/${documento.id}?modo=lado-a-lado`) },
    );
  };

  // Colar em qualquer lugar da página cria o documento — menos dentro do
  // campo de escrever, onde colar é só colar.
  useEffect(() => {
    const aoColar = (evento: ClipboardEvent) => {
      const alvo = evento.target;
      if (alvo instanceof Element && alvo.closest('textarea, input')) return;
      const texto = evento.clipboardData?.getData('text/plain') ?? '';
      if (!texto.trim()) return;
      evento.preventDefault();
      criarCom(texto);
    };
    document.addEventListener('paste', aoColar);
    return () => document.removeEventListener('paste', aoColar);
  });

  useEffect(() => zona.current?.focus(), []);

  return (
    <div className={estilos.pagina}>
      <div className={estilos.miolo}>
        <Title3>Novo documento</Title3>

        <section className={estilos.passo}>
          <Text className={estilos.rotuloDoPasso}>1 · Escolha o template</Text>
          {isLoading && <Spinner size="small" />}
          <div className={estilos.templates} role="radiogroup" aria-label="Template">
            {templates?.map((t) => (
              <button
                key={t.id}
                type="button"
                role="radio"
                aria-checked={t.id === templateId}
                className={mergeClasses(estilos.template, t.id === templateId && estilos.selecionado)}
                onClick={() => setEscolhido(t.id)}
              >
                <MiniaturaDeTemplate templateId={t.id} />
                <span className={estilos.nomeDoTemplate}>
                  <Text weight="semibold">{t.nome}</Text>
                  <Caption1 className={estilos.secundario}>{t.tipo}</Caption1>
                </span>
              </button>
            ))}
          </div>
        </section>

        <section className={estilos.passo}>
          <Text className={estilos.rotuloDoPasso}>2 · Cole o Markdown</Text>
          <div ref={zona} className={estilos.zona} tabIndex={0} aria-label="Cole o Markdown aqui">
            {criar.isPending ? (
              <Spinner label="Criando o documento…" />
            ) : (
              <>
                <ClipboardPasteRegular fontSize={32} />
                <Text size={400}>
                  Cole o conteúdo com <span className={estilos.tecla}>{ehMac ? '⌘' : 'Ctrl'}</span> +{' '}
                  <span className={estilos.tecla}>V</span>
                </Text>
                <Caption1 className={estilos.secundario}>O documento é criado na hora, com o PDF ao lado.</Caption1>
              </>
            )}
            <div className={estilos.escrever}>
              <Textarea
                style={{ width: '100%' }}
                resize="vertical"
                placeholder="…ou escreva aqui"
                value={escrito}
                onChange={(_, d) => setEscrito(d.value)}
                textarea={{ style: { minHeight: 80, fontFamily: tokens.fontFamilyMonospace } }}
              />
              <Button appearance="primary" disabled={!escrito.trim() || criar.isPending || !templateId} onClick={() => criarCom(escrito)}>
                Criar documento
              </Button>
            </div>
          </div>
          {criar.error && (
            <MessageBar intent="error">
              <MessageBarBody>{criar.error.message}</MessageBarBody>
            </MessageBar>
          )}
        </section>
      </div>
    </div>
  );
}
