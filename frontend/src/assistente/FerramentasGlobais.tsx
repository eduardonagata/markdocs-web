import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../api/cliente';
import { chaves } from '../api/consultas';
import { useAssistente } from './Assistente';
import type { FerramentaDaTela } from './tipos';

const telas = { documentos: '/documentos', templates: '/templates', 'novo-documento': '/documentos/novo' } as const;

const pendenteNaTela =
  'Há alterações do assistente no documento aberto que ainda não foram salvas. Peça ao usuário para salvá-las '
  + '(ou salve, com a aprovação dele) ou desfazê-las antes de sair desta tela.';

/** Espera a tela nova montar — o que ela registra no assistente vem com ela. */
const aguardarTela = () => new Promise((pronto) => window.setTimeout(pronto, 400));

/**
 * As ferramentas que valem em qualquer tela: mudar de tela, abrir e criar
 * documentos. Criar grava no servidor, e por isso pede aprovação.
 */
export function FerramentasGlobais() {
  const { registrarFerramentas, pendente } = useAssistente();
  const navegar = useNavigate();
  const consultas = useQueryClient();

  useEffect(() => {
    const ferramentas: FerramentaDaTela[] = [
      {
        nome: 'abrirDocumento',
        descricao: 'Abre um documento na tela, pelo id, para lê-lo ou editá-lo. Depois de aberto, ele passa a ser o documento do contexto.',
        parametros: {
          type: 'object',
          properties: { documentoId: { type: 'string', description: 'O id do documento, como veio da lista.' } },
          required: ['documentoId'],
          additionalProperties: false,
        },
        executar: async ({ documentoId }) => {
          if (pendente()) return { paraOAgente: pendenteNaTela, ok: false, resumo: 'há alterações não salvas' };
          let titulo: string;
          try {
            titulo = (await consultas.fetchQuery({ queryKey: chaves.documento(String(documentoId)), queryFn: () => api.documento(String(documentoId)) })).titulo;
          } catch {
            return { paraOAgente: `Não existe documento com o id “${String(documentoId)}”. Liste os documentos.`, ok: false };
          }
          navegar(`/documentos/${String(documentoId)}`);
          await aguardarTela();
          return { paraOAgente: `Documento “${titulo}” aberto na tela.`, resumo: titulo };
        },
      },
      {
        nome: 'irPara',
        descricao: 'Leva o usuário a uma tela do app: a lista de documentos, os templates ou a criação de um documento novo.',
        parametros: {
          type: 'object',
          properties: { tela: { type: 'string', enum: Object.keys(telas), description: 'A tela.' } },
          required: ['tela'],
          additionalProperties: false,
        },
        executar: async ({ tela }) => {
          const destino = telas[tela as keyof typeof telas];
          if (!destino) return { paraOAgente: `“${String(tela)}” não é uma tela conhecida.`, ok: false };
          if (pendente()) return { paraOAgente: pendenteNaTela, ok: false, resumo: 'há alterações não salvas' };
          navegar(destino);
          await aguardarTela();
          return { paraOAgente: `A tela “${String(tela)}” está aberta.` };
        },
      },
      {
        nome: 'criarDocumento',
        descricao:
          'Cria um documento novo a partir de um template, com o Markdown informado, e o abre na tela. Grava no servidor: '
          + 'o usuário aprova antes. O título, se vazio, é o primeiro título do Markdown.',
        parametros: {
          type: 'object',
          properties: {
            templateId: { type: 'string', description: 'O id do template, como veio da lista de templates.' },
            markdown: { type: 'string', description: 'O conteúdo do documento, em Markdown.' },
            titulo: { type: 'string', description: 'O título, ou vazio para usar o primeiro título do Markdown.' },
          },
          required: ['templateId', 'markdown', 'titulo'],
          additionalProperties: false,
        },
        aprovacao: async ({ templateId, markdown, titulo }) => {
          const templates = await consultas.fetchQuery({ queryKey: chaves.templates, queryFn: api.templates });
          const template = templates.find((t) => t.id === templateId);
          const nome = String(titulo ?? '').trim() || /^\s{0,3}#{1,6}\s+(.+)$/m.exec(String(markdown ?? ''))?.[1]?.trim() || 'sem título';
          return {
            titulo: 'Criar um documento',
            detalhe: `“${nome}”, a partir do template “${template?.nome ?? String(templateId)}”${pendente() ? '' : ', e abri-lo na tela'}.`,
            acao: 'Criar',
          };
        },
        executar: async ({ templateId, markdown, titulo }) => {
          const tituloInformado = String(titulo ?? '').trim();
          const criado = await api.criarDocumento(String(templateId), String(markdown ?? ''), tituloInformado || undefined);
          consultas.setQueryData(chaves.documento(criado.id), criado);
          void consultas.invalidateQueries({ queryKey: chaves.documentos, exact: true });
          if (pendente())
            return {
              paraOAgente: `Documento “${criado.titulo}” criado (id ${criado.id}). Não foi aberto: há alterações não salvas na tela atual.`,
              resumo: criado.titulo,
            };
          navegar(`/documentos/${criado.id}`);
          await aguardarTela();
          return { paraOAgente: `Documento “${criado.titulo}” criado (id ${criado.id}) e aberto na tela.`, resumo: criado.titulo };
        },
      },
    ];
    return registrarFerramentas(ferramentas);
  }, [registrarFerramentas, pendente, navegar, consultas]);

  return null;
}
