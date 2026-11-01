// O contrato HTTP de /api/*, espelhado à mão a partir dos DTOs de
// src/MarkDocs.WebApi/EndpointHandlers/Documentos/Dto. O frontend não conhece
// slice nenhum: só isto.

export interface DocumentoResumo {
  id: string;
  titulo: string;
  tipo: string;
  templateDeOrigem: string | null;
  criadoEm: string;
  alteradoEm: string;
}

export interface Documento {
  id: string;
  titulo: string;
  tipo: string;
  corpo: string;
  familia: string;
  templateDeOrigem: string | null;
  criadoEm: string;
  alteradoEm: string;
  eixos: Eixo[];
  imagens: Imagem[];
}

/** Uma imagem do documento. `referencia` é o que o Markdown escreve: `imagens/planta.png`. */
export interface Imagem {
  nome: string;
  referencia: string;
  url: string;
}

export interface Template {
  id: string;
  nome: string;
  tipo: string;
  familia: string;
  deFabrica: boolean;
  documentoDeOrigem: string | null;
  criadoEm: string;
}

/**
 * Um controle de formatação. O `id` é opaco: a tela o devolve num ajuste sem
 * nunca decompô-lo. A tela não sabe de família nenhuma — desenha um controle
 * por tipo de domínio.
 */
export interface Eixo {
  id: string;
  grupo: string;
  rotulo: string;
  dominio: Dominio;
  valor: ValorDeEixo;
}

export type Dominio =
  | { tipo: 'medida'; min: number; max: number; passo: number; unidade: string }
  | { tipo: 'escolha'; opcoes: { id: string; rotulo: string }[] }
  | { tipo: 'alternancia' }
  | { tipo: 'cor' }
  | { tipo: 'familiaTipografica'; disponiveis: string[] };

export type ValorDeEixo =
  | { tipo: 'medida'; valor: number; unidade: string }
  | { tipo: 'escolha'; id: string }
  | { tipo: 'alternancia'; ligado: boolean }
  | { tipo: 'cor'; hexadecimal: string }
  | { tipo: 'familiaTipografica'; familia: string };

export interface Ajuste {
  eixo: string;
  valor: ValorDeEixo;
}

export interface AtualizarDocumento {
  titulo?: string;
  tipo?: string;
  corpo?: string;
  templateId?: string;
  ajustes?: Ajuste[];
}

/** Se o assistente está configurado no servidor — ou o que falta para estar. */
export interface StatusDoAgente {
  disponivel: boolean;
  provedor: string;
  modelo: string | null;
  oQueFalta: string | null;
}
