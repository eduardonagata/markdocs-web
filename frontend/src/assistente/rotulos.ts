/**
 * Como cada ferramenta aparece no rastro da conversa. Ferramenta nova ganha
 * uma linha aqui; sem ela, aparece pelo nome técnico — feio, mas visível.
 */
const rotulos: Record<string, string> = {
  // Do backend: só leitura.
  listarDocumentos: 'Consultou os documentos',
  consultarDocumento: 'Leu um documento',
  listarTemplates: 'Consultou os templates',
  // Da tela: editam o que está aberto.
  substituirTrecho: 'Alterou um trecho do documento',
  inserirTexto: 'Acrescentou texto ao documento',
  reescreverDocumento: 'Reescreveu o documento',
  salvarDocumento: 'Salvar o documento',
  aplicarTemplate: 'Aplicar um template',
  criarDocumento: 'Criar um documento',
  abrirDocumento: 'Abriu um documento',
  irPara: 'Mudou de tela',
};

export const rotuloDaFerramenta = (nome: string) => rotulos[nome] ?? nome;
