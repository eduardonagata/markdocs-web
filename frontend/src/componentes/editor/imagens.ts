/**
 * O nome citado por `imagens/<nome>` ou `./imagens/<nome>`, ou null. É a mesma
 * regra do PDF (MarkDocs.Core/Docs/Imagens.md): qualquer outro destino — URL
 * externa, caminho absoluto, subpasta — não é imagem do documento.
 */
export function nomeCitado(destino: string): string | null {
  const caminho = destino.startsWith('./') ? destino.slice(2) : destino;
  if (!caminho.startsWith('imagens/')) return null;
  let nome: string;
  try {
    nome = decodeURIComponent(caminho.slice('imagens/'.length));
  } catch {
    return null;
  }
  return nome.length === 0 || nome.includes('/') ? null : nome;
}

/**
 * O que a tela mostra no lugar de uma imagem que não está nos ativos. No PDF,
 * ela vira o texto alternativo; aqui, um aviso discreto com o destino citado,
 * para que se veja por que ela não vai aparecer no papel.
 */
export function imagemAusente(destino: string): string {
  const texto = `Fora das imagens do documento: ${destino}`
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" width="480" height="56" viewBox="0 0 480 56">` +
    `<rect x="1" y="1" width="478" height="54" rx="6" fill="#fafaf9" stroke="#d6d3d1" stroke-dasharray="4 3"/>` +
    `<text x="240" y="33" text-anchor="middle" font-family="system-ui, sans-serif" font-size="13" fill="#78716c">${texto}</text>` +
    `</svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}
