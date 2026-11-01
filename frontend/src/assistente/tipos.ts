import type { Context } from '@ag-ui/client';

/** O pedido de aprovação de uma ação que grava no servidor. */
export interface PedidoDeAprovacao {
  /** “Salvar o documento”. */
  titulo: string;
  /** O que vai acontecer, em uma frase. */
  detalhe?: string;
  /** O verbo do botão: “Salvar”, “Criar”, “Aplicar”. */
  acao: string;
  /** Mostra, na tela, o que vai ser gravado (o modo de diferenças, por exemplo). */
  verAlteracoes?: () => void;
}

/** O que a ferramenta devolve: o texto para o agente e o que a conversa mostra. */
export interface Resultado {
  /** O que o agente lê. Falha de regra volta aqui, como texto, para ele corrigir. */
  paraOAgente: string;
  /** Uma linha para o rastro na conversa: “2 trechos destacados no documento”. */
  resumo?: string;
  /** Falso quando a ação não aconteceu (o rastro fica marcado). */
  ok?: boolean;
  /** Leva o usuário até o que mudou. */
  mostrar?: () => void;
}

/**
 * Uma ferramenta da tela: declarada ao agente a cada execução (AG-UI) e
 * executada aqui, no navegador, sobre o que está aberto. É por elas que o
 * agente edita — e por isso o que ele faz aparece na interface na hora.
 */
export interface FerramentaDaTela {
  nome: string;
  /** Para o agente: o que a ferramenta faz e quando usá-la. */
  descricao: string;
  /** JSON Schema dos argumentos. */
  parametros: Record<string, unknown>;
  /** Presente quando a ação grava no servidor: o usuário aprova antes. */
  aprovacao?: (args: Record<string, unknown>) => PedidoDeAprovacao | Promise<PedidoDeAprovacao>;
  executar: (args: Record<string, unknown>) => Promise<Resultado>;
  /** A região da tela sobre a qual a ferramenta age, para a moldura de presença. */
  area?: 'documento';
}

/** O que uma tela informa ao assistente: o chip do composer e o contexto AG-UI. */
export interface FonteDeContexto {
  /** O chip mostrado no composer (“Contrato de locação”). */
  rotulo?: string;
  contexto: () => Context[];
  /** O trecho selecionado agora, em Markdown, para o atalho e o botão “Perguntar”. */
  selecao?: () => string;
  /** Há alterações do assistente na tela que ainda não foram salvas. */
  pendente?: () => boolean;
}
