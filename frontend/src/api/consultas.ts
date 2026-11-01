import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './cliente';
import type { AtualizarDocumento, Documento, PaginasDoTimbre } from './tipos';

export const chaves = {
  documentos: ['documentos'] as const,
  documento: (id: string) => ['documentos', id] as const,
  templates: ['templates'] as const,
};

export const useDocumentos = () => useQuery({ queryKey: chaves.documentos, queryFn: api.documentos });

export const useDocumento = (id: string) => useQuery({ queryKey: chaves.documento(id), queryFn: () => api.documento(id) });

export const useTemplates = () => useQuery({ queryKey: chaves.templates, queryFn: api.templates });

export function useCriarDocumento() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (p: { templateId: string; corpo: string; titulo?: string }) => api.criarDocumento(p.templateId, p.corpo, p.titulo),
    onSuccess: (documento) => {
      cliente.setQueryData(chaves.documento(documento.id), documento);
      void cliente.invalidateQueries({ queryKey: chaves.documentos, exact: true });
    },
  });
}

/** Grava e põe a resposta no cache: a tela passa a mostrar o que o servidor guardou. */
export function useAtualizarDocumento(id: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (alteracao: AtualizarDocumento) => api.atualizarDocumento(id, alteracao),
    onSuccess: (documento: Documento) => {
      cliente.setQueryData(chaves.documento(id), documento);
      void cliente.invalidateQueries({ queryKey: chaves.documentos, exact: true });
    },
  });
}

export function useRemoverDocumento() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.removerDocumento(id),
    onSuccess: (_, id) => {
      cliente.removeQueries({ queryKey: chaves.documento(id) });
      void cliente.invalidateQueries({ queryKey: chaves.documentos, exact: true });
    },
  });
}

export function useCriarTemplate() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (p: { documentoId: string; nome: string }) => api.criarTemplate(p.documentoId, p.nome),
    onSuccess: () => void cliente.invalidateQueries({ queryKey: chaves.templates }),
  });
}

export function useGravarImagem(documentoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (arquivo: File) => api.gravarImagem(documentoId, arquivo),
    onSuccess: () => void cliente.invalidateQueries({ queryKey: chaves.documento(documentoId) }),
  });
}

export function useRemoverImagem(documentoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (nome: string) => api.removerImagem(documentoId, nome),
    onSuccess: () => void cliente.invalidateQueries({ queryKey: chaves.documento(documentoId) }),
  });
}

/** O timbre é formatação: a resposta é o documento inteiro, que vai para o cache (e o PDF é refeito). */
export function useGravarTimbre(documentoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (p: { paginas: PaginasDoTimbre; arquivo: File }) => api.gravarTimbre(documentoId, p.paginas, p.arquivo),
    onSuccess: (documento: Documento) => {
      cliente.setQueryData(chaves.documento(documentoId), documento);
      void cliente.invalidateQueries({ queryKey: chaves.documentos, exact: true });
    },
  });
}

export function useRemoverTimbre(documentoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (paginas: PaginasDoTimbre) => api.removerTimbre(documentoId, paginas),
    onSuccess: (documento: Documento) => {
      cliente.setQueryData(chaves.documento(documentoId), documento);
      void cliente.invalidateQueries({ queryKey: chaves.documentos, exact: true });
    },
  });
}
