import type { AtualizarDocumento, Documento, DocumentoResumo, Imagem, PaginasDoTimbre, StatusDoAgente, Template } from './tipos';

/** Uma resposta de erro da API, com a mensagem que ela devolveu. */
export class ErroDaApi extends Error {
  constructor(
    readonly status: number,
    mensagem: string,
  ) {
    super(mensagem);
  }
}

async function mensagemDe(resposta: Response): Promise<string> {
  try {
    const corpo = (await resposta.json()) as { mensagem?: string; title?: string };
    return corpo.mensagem ?? corpo.title ?? `Erro ${resposta.status}`;
  } catch {
    return `Erro ${resposta.status}`;
  }
}

async function pedir<T>(caminho: string, init?: RequestInit): Promise<T> {
  const resposta = await fetch(caminho, {
    ...init,
    headers: init?.body instanceof FormData ? init.headers : { 'Content-Type': 'application/json', ...init?.headers },
  });
  if (!resposta.ok) throw new ErroDaApi(resposta.status, await mensagemDe(resposta));
  if (resposta.status === 204) return undefined as T;
  return (await resposta.json()) as T;
}

export const api = {
  documentos: () => pedir<DocumentoResumo[]>('/api/documentos'),

  documento: (id: string) => pedir<Documento>(`/api/documentos/${id}`),

  criarDocumento: (templateId: string, corpo: string, titulo?: string) =>
    pedir<Documento>('/api/documentos', { method: 'POST', body: JSON.stringify({ templateId, corpo, titulo }) }),

  atualizarDocumento: (id: string, alteracao: AtualizarDocumento) =>
    pedir<Documento>(`/api/documentos/${id}`, { method: 'PATCH', body: JSON.stringify(alteracao) }),

  removerDocumento: (id: string) => pedir<void>(`/api/documentos/${id}`, { method: 'DELETE' }),

  /** O PDF, como bytes. Um 422 traz a mensagem de por que o documento não compôs. */
  pdf: async (url: string): Promise<ArrayBuffer> => {
    const resposta = await fetch(url);
    if (!resposta.ok) throw new ErroDaApi(resposta.status, await mensagemDe(resposta));
    return resposta.arrayBuffer();
  },

  gravarImagem: (documentoId: string, arquivo: File) => {
    const formulario = new FormData();
    formulario.append('arquivo', arquivo, arquivo.name);
    return pedir<Imagem>(`/api/documentos/${documentoId}/imagens`, { method: 'POST', body: formulario });
  },

  removerImagem: (documentoId: string, nome: string) =>
    pedir<void>(`/api/documentos/${documentoId}/imagens/${encodeURIComponent(nome)}`, { method: 'DELETE' }),

  /** Põe uma imagem como papel timbrado; devolve o documento, com a formatação nova. */
  gravarTimbre: (documentoId: string, paginas: PaginasDoTimbre, arquivo: File) => {
    const formulario = new FormData();
    formulario.append('arquivo', arquivo, arquivo.name);
    return pedir<Documento>(`/api/documentos/${documentoId}/timbre/${paginas}`, { method: 'PUT', body: formulario });
  },

  removerTimbre: (documentoId: string, paginas: PaginasDoTimbre) =>
    pedir<Documento>(`/api/documentos/${documentoId}/timbre/${paginas}`, { method: 'DELETE' }),

  templates: () => pedir<Template[]>('/api/templates'),

  criarTemplate: (documentoId: string, nome: string) =>
    pedir<Template>('/api/templates', { method: 'POST', body: JSON.stringify({ documentoId, nome }) }),

  statusDoAgente: () => pedir<StatusDoAgente>('/api/agente/status'),
};

export const urls = {
  pdfDoDocumento: (id: string, versao: string) => `/api/documentos/${id}/pdf?v=${encodeURIComponent(versao)}`,
  amostraDoTemplate: (id: string) => `/api/templates/${id}/amostra`,
};
