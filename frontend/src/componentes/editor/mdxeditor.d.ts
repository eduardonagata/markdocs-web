import type { NodeRef } from '@mdxeditor/editor';

declare module '@mdxeditor/editor' {
  /**
   * O Markdown contra o qual o modo de diferenças compara. Exportado pelo
   * pacote em tempo de execução, mas fora dos tipos publicados.
   */
  export const diffMarkdown$: NodeRef<string>;
}
