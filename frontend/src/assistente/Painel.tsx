import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import type { Message } from '@ag-ui/client';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import {
  Button,
  Caption1,
  makeStyles,
  mergeClasses,
  shorthands,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  Textarea,
  tokens,
  Tooltip,
} from '@fluentui/react-components';
import {
  ArrowUpRegular,
  CheckmarkCircleRegular,
  ComposeRegular,
  DismissCircleRegular,
  DismissRegular,
  DocumentTextRegular,
  ErrorCircleRegular,
  SparkleRegular,
  StopRegular,
  TextQuoteRegular,
} from '@fluentui/react-icons';
import { useAssistente, type Aprovacao, type Desfecho } from './Assistente';
import { rotuloDaFerramenta } from './rotulos';
import { atalho } from './atalho';

const useEstilos = makeStyles({
  painel: {
    position: 'fixed',
    top: '64px',
    right: '12px',
    bottom: '12px',
    width: '400px',
    zIndex: 1000,
    display: 'flex',
    flexDirection: 'column',
    backgroundColor: 'rgba(255, 255, 255, 0.97)',
    backdropFilter: 'blur(12px)',
    border: '1px solid var(--assistente-borda)',
    borderRadius: '14px',
    boxShadow: '0 24px 64px rgba(28, 25, 23, 0.18), 0 2px 8px rgba(28, 25, 23, 0.08)',
    overflow: 'hidden',
    transitionProperty: 'transform, opacity',
    transitionDuration: '260ms',
    transitionTimingFunction: 'cubic-bezier(0.22, 1, 0.36, 1)',
    '@media (max-width: 640px)': { top: 0, right: 0, bottom: 0, width: '100%', borderRadius: 0 },
  },
  fechado: { transform: 'translateX(24px)', opacity: 0, pointerEvents: 'none', visibility: 'hidden' },
  cabecalho: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalS} ${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  marca: { color: 'var(--assistente)', fontSize: '20px', display: 'flex' },
  titulo: { display: 'flex', flexDirection: 'column', flex: 1, lineHeight: 1.2 },
  secundario: { color: tokens.colorNeutralForeground3 },
  conversa: {
    flex: 1,
    overflowY: 'auto',
    padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalL}`,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  vazio: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, color: tokens.colorNeutralForeground2, marginTop: 'auto' },
  sugestoes: { display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: tokens.spacingVerticalXS },
  usuario: {
    alignSelf: 'flex-end',
    maxWidth: '85%',
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: '12px 12px 4px 12px',
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    whiteSpace: 'pre-wrap',
    overflowWrap: 'anywhere',
  },
  assistente: {
    fontSize: tokens.fontSizeBase300,
    lineHeight: 1.55,
    overflowWrap: 'anywhere',
    '& p': { margin: '0 0 0.5em' },
    '& p:last-child': { marginBottom: 0 },
    '& ul, & ol': { margin: '0 0 0.5em', paddingLeft: '1.3em' },
    '& code': { fontFamily: tokens.fontFamilyMonospace, fontSize: '0.9em', backgroundColor: tokens.colorNeutralBackground3, padding: '0 3px', borderRadius: '3px' },
    '& pre': { backgroundColor: tokens.colorNeutralBackground3, padding: '8px', borderRadius: '6px', overflowX: 'auto' },
  },
  rastro: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    borderLeft: '2px solid var(--assistente-borda)',
    paddingLeft: tokens.spacingHorizontalS,
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
    minHeight: '22px',
  },
  rastroAcao: { color: 'var(--assistente)' },
  link: {
    border: 'none',
    background: 'none',
    padding: 0,
    color: 'var(--assistente)',
    cursor: 'pointer',
    fontSize: tokens.fontSizeBase200,
    textDecoration: 'underline',
    textUnderlineOffset: '2px',
  },
  aprovacao: {
    border: '1px solid var(--assistente-borda)',
    backgroundColor: 'var(--assistente-fundo)',
    borderRadius: '10px',
    padding: tokens.spacingHorizontalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  botoes: { display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'center', flexWrap: 'wrap' },
  composer: {
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    padding: tokens.spacingHorizontalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  chips: { display: 'flex', gap: tokens.spacingHorizontalXS, flexWrap: 'wrap' },
  chip: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
    maxWidth: '100%',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: '999px',
    padding: '1px 8px',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  chipTexto: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '260px' },
  chipSelecao: { ...shorthands.borderColor('var(--assistente-borda)'), backgroundColor: 'var(--assistente-fundo)' },
  removerChip: { border: 'none', background: 'none', padding: 0, cursor: 'pointer', display: 'flex', color: 'inherit' },
  entrada: { display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-end' },
});

const sugestoesNoDocumento = [
  'Revise a ortografia e a concordância do documento',
  'Acrescente uma cláusula de confidencialidade',
  'Resuma o documento em três tópicos',
];

const sugestoesFora = ['Quais documentos eu tenho?', 'Crie um recibo de R$ 500,00 para Maria Souza', 'Que templates existem?'];

function texto(mensagem: Message): string {
  const conteudo = (mensagem as { content?: unknown }).content;
  if (typeof conteudo === 'string') return conteudo;
  if (Array.isArray(conteudo))
    return conteudo.map((p) => (typeof p === 'object' && p && 'text' in p ? String((p as { text: unknown }).text) : '')).join('');
  return '';
}

function Rastro({ nome, desfecho, concluida }: { nome: string; desfecho: Desfecho | undefined; concluida: boolean }) {
  const estilos = useEstilos();
  const estado = desfecho?.estado ?? (concluida ? 'ok' : 'executando');
  const icone =
    estado === 'executando' || estado === 'aguardando' ? (
      <Spinner size="extra-tiny" />
    ) : estado === 'ok' ? (
      <CheckmarkCircleRegular />
    ) : estado === 'recusada' ? (
      <DismissCircleRegular />
    ) : (
      <ErrorCircleRegular />
    );
  return (
    <div className={mergeClasses(estilos.rastro, desfecho && estilos.rastroAcao)}>
      {icone}
      <span>
        {rotuloDaFerramenta(nome)}
        {estado === 'aguardando' && ' — aguardando você'}
        {desfecho?.resumo && ` — ${desfecho.resumo}`}
      </span>
      {desfecho?.mostrar && estado === 'ok' && (
        <button type="button" className={estilos.link} onClick={desfecho.mostrar}>
          Mostrar
        </button>
      )}
    </div>
  );
}

function CartaoDeAprovacao({ aprovacao }: { aprovacao: Aprovacao }) {
  const estilos = useEstilos();
  const { pedido, responder } = aprovacao;
  return (
    <div className={estilos.aprovacao} role="group" aria-label={pedido.titulo}>
      <Text weight="semibold">{pedido.titulo}?</Text>
      {pedido.detalhe && <Caption1 className={estilos.secundario}>{pedido.detalhe}</Caption1>}
      <div className={estilos.botoes}>
        <Button appearance="primary" size="small" onClick={() => responder(true)}>
          {pedido.acao}
        </Button>
        <Button size="small" onClick={() => responder(false)}>
          Recusar
        </Button>
        {pedido.verAlteracoes && (
          <button type="button" className={estilos.link} onClick={pedido.verAlteracoes}>
            Ver as alterações
          </button>
        )}
      </div>
    </div>
  );
}

/**
 * O assistente: um painel que flutua à direita, por cima de qualquer tela —
 * a camada de quem age em nome do usuário. O que ele faz aparece na própria
 * tela (destacado), e aqui fica o rastro: cada ação, o que mudou, e o pedido
 * de aprovação de tudo o que grava no servidor.
 */
export function PainelDoAssistente() {
  const estilos = useEstilos();
  const a = useAssistente();
  const [escrito, setEscrito] = useState('');
  const conversa = useRef<HTMLDivElement>(null);
  const entrada = useRef<HTMLTextAreaElement>(null);

  // A conversa rola junto com o que chega.
  useEffect(() => {
    const elemento = conversa.current;
    if (elemento) elemento.scrollTop = elemento.scrollHeight;
  }, [a.mensagens, a.aprovacoes, a.desfechos]);

  useEffect(() => {
    if (a.focoNoComposer > 0) window.setTimeout(() => entrada.current?.focus(), 60);
  }, [a.focoNoComposer]);

  const enviar = (texto = escrito) => {
    if (!texto.trim() || a.rodando || !a.disponivel) return;
    a.enviar(texto);
    setEscrito('');
  };

  const aoTeclar = (evento: KeyboardEvent<HTMLTextAreaElement>) => {
    if (evento.key === 'Enter' && !evento.shiftKey) {
      evento.preventDefault();
      enviar();
    }
    if (evento.key === 'Escape') a.fechar();
  };

  const resultados = new Set(a.mensagens.filter((m) => m.role === 'tool').map((m) => (m as { toolCallId: string }).toolCallId));
  const noDocumento = a.rotulos.length > 0;

  return (
    <aside
      className={mergeClasses(estilos.painel, !a.aberto && estilos.fechado)}
      aria-label="Assistente"
      aria-hidden={!a.aberto}
    >
      <header className={estilos.cabecalho}>
        <span className={estilos.marca} aria-hidden>
          <SparkleRegular />
        </span>
        <div className={estilos.titulo}>
          <Text weight="semibold">Assistente</Text>
          <Caption1 className={estilos.secundario}>Age na tela, em seu nome · {atalho}</Caption1>
        </div>
        <Tooltip content="Nova conversa" relationship="label">
          <Button appearance="subtle" icon={<ComposeRegular />} onClick={a.novaConversa} disabled={a.mensagens.length === 0} />
        </Tooltip>
        <Tooltip content="Fechar (Esc)" relationship="label">
          <Button appearance="subtle" icon={<DismissRegular />} onClick={a.fechar} />
        </Tooltip>
      </header>

      <div ref={conversa} className={estilos.conversa} aria-live="polite">
        {a.disponivel === false && (
          <MessageBar intent="warning" layout="multiline">
            <MessageBarBody>O assistente não está configurado neste ambiente. {a.oQueFalta}</MessageBarBody>
          </MessageBar>
        )}

        {a.mensagens.length === 0 && a.disponivel !== false && (
          <div className={estilos.vazio}>
            <Text>
              Peça em linguagem natural. Eu edito {noDocumento ? 'o documento aberto' : 'os seus documentos'} direto na tela —
              você vê o que mudou, destacado — e nada é gravado sem a sua aprovação.
            </Text>
            <Caption1 className={estilos.secundario}>
              Selecione um trecho no editor para perguntar sobre ele.
            </Caption1>
            <div className={estilos.sugestoes}>
              {(noDocumento ? sugestoesNoDocumento : sugestoesFora).map((s) => (
                <Button key={s} size="small" appearance="outline" disabled={!a.disponivel} onClick={() => enviar(s)}>
                  {s}
                </Button>
              ))}
            </div>
          </div>
        )}

        {a.mensagens.map((mensagem) => {
          if (mensagem.role === 'user')
            return (
              <div key={mensagem.id} className={estilos.usuario}>
                {texto(mensagem)}
              </div>
            );
          if (mensagem.role !== 'assistant') return null;
          const conteudo = texto(mensagem);
          return (
            <div key={mensagem.id} style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS }}>
              {conteudo && (
                <div className={estilos.assistente}>
                  <Markdown remarkPlugins={[remarkGfm]}>{conteudo}</Markdown>
                </div>
              )}
              {mensagem.toolCalls?.map((chamada) => (
                <div key={chamada.id} style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS }}>
                  <Rastro nome={chamada.function.name} desfecho={a.desfechos.get(chamada.id)} concluida={resultados.has(chamada.id)} />
                  {a.aprovacoes.get(chamada.id) && <CartaoDeAprovacao aprovacao={a.aprovacoes.get(chamada.id)!} />}
                </div>
              ))}
            </div>
          );
        })}

        {a.rodando && a.aprovacoes.size === 0 && (
          <div className={estilos.rastro}>
            <Spinner size="extra-tiny" /> Trabalhando…
          </div>
        )}

        {a.erro && (
          <MessageBar intent="error" layout="multiline">
            <MessageBarBody>{a.erro}</MessageBarBody>
          </MessageBar>
        )}
      </div>

      <div className={estilos.composer}>
        {(a.rotulos.length > 0 || a.selecao) && (
          <div className={estilos.chips}>
            {a.rotulos.map((r) => (
              <Tooltip key={r} content="O documento aberto é o alvo das edições, a não ser que você diga outro" relationship="description">
                <span className={estilos.chip}>
                  <DocumentTextRegular />
                  <span className={estilos.chipTexto}>{r}</span>
                </span>
              </Tooltip>
            ))}
            {a.selecao && (
              <span className={mergeClasses(estilos.chip, estilos.chipSelecao)}>
                <TextQuoteRegular />
                <span className={estilos.chipTexto}>{a.selecao.replace(/\s+/g, ' ')}</span>
                <button type="button" className={estilos.removerChip} aria-label="Tirar o trecho" onClick={a.removerSelecao}>
                  <DismissRegular fontSize={12} />
                </button>
              </span>
            )}
          </div>
        )}
        <div className={estilos.entrada}>
          <Textarea
            ref={entrada}
            style={{ flex: 1 }}
            resize="none"
            rows={2}
            placeholder={a.selecao ? 'O que fazer com o trecho selecionado?' : 'Peça ao assistente…'}
            value={escrito}
            disabled={a.disponivel === false}
            onChange={(_, d) => setEscrito(d.value)}
            onKeyDown={aoTeclar}
          />
          {a.rodando ? (
            <Tooltip content="Parar" relationship="label">
              <Button icon={<StopRegular />} onClick={a.parar} />
            </Tooltip>
          ) : (
            <Tooltip content="Enviar (Enter)" relationship="label">
              <Button appearance="primary" icon={<ArrowUpRegular />} disabled={!escrito.trim() || !a.disponivel} onClick={() => enviar()} />
            </Tooltip>
          )}
        </div>
      </div>
    </aside>
  );
}
