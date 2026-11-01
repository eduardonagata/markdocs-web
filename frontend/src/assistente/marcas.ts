/**
 * As marcas do assistente: o que ele alterou, destacado na folha do editor
 * até o usuário decidir — manter e salvar, ou desfazer.
 *
 * O destaque usa a CSS Custom Highlight API (`CSS.highlights`): faixas sobre o
 * texto renderizado, sem tocar no DOM nem no Markdown — o editor não sabe que
 * elas existem, e o que se grava continua limpo. Onde a API não existe, nada
 * é destacado; a barra de revisão e o modo de diferenças continuam valendo.
 */

const NOME = 'assistente';

/** O texto que um trecho de Markdown mostra na folha, linha a linha, sem a marcação. */
export function textosVisiveis(markdown: string): string[] {
  return markdown
    .split('\n')
    .flatMap((linha) => {
      const semBloco = linha
        .replace(/^\s{0,3}#{1,6}\s+/, '')
        .replace(/^\s*>\s?/, '')
        .replace(/^\s*(?:[-*+]|\d+[.)])\s+/, '')
        .trim();
      if (/^\|?\s*:?-{3,}/.test(semBloco) || /^(?:-{3,}|\*{3,}|_{3,})$/.test(semBloco)) return [];
      // Uma linha de tabela vira uma célula por trecho.
      const partes = semBloco.startsWith('|') ? semBloco.split('|') : [semBloco];
      return partes.map((p) =>
        p
          .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
          .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
          .replace(/(\*\*|__|~~|`)/g, '')
          .replace(/(^|[^\w])[*_]([^*_]+)[*_](?=[^\w]|$)/g, '$1$2')
          .replace(/\\([\\`*_{}[\]()#+\-.!|])/g, '$1')
          .trim(),
      );
    })
    .filter((t) => t.length >= 2);
}

/** As linhas de `novo` que não estão em `antigo`, pela maior subsequência comum. */
export function linhasNovas(antigo: string, novo: string): string[] {
  const a = antigo.split('\n');
  const b = novo.split('\n');
  if (a.length * b.length > 4_000_000) return b.filter((l) => !a.includes(l));
  const m = a.length;
  const n = b.length;
  const tabela: Uint32Array[] = Array.from({ length: m + 1 }, () => new Uint32Array(n + 1));
  for (let i = m - 1; i >= 0; i--)
    for (let j = n - 1; j >= 0; j--)
      tabela[i]![j] = a[i] === b[j] ? tabela[i + 1]![j + 1]! + 1 : Math.max(tabela[i + 1]![j]!, tabela[i]![j + 1]!);
  const novas: string[] = [];
  let i = 0;
  let j = 0;
  while (j < n) {
    if (i < m && a[i] === b[j]) {
      i++;
      j++;
    } else if (i < m && tabela[i + 1]![j]! >= tabela[i]![j + 1]!) i++;
    else novas.push(b[j++]!);
  }
  return novas.filter((l) => l.trim().length > 0);
}

/** Os nós de texto da folha, concatenados, com o caminho de volta de cada caractere. */
function indexar(raiz: HTMLElement) {
  const nos: Text[] = [];
  const inicios: number[] = [];
  let texto = '';
  const caminhante = document.createTreeWalker(raiz, NodeFilter.SHOW_TEXT);
  for (let no = caminhante.nextNode(); no; no = caminhante.nextNode()) {
    nos.push(no as Text);
    inicios.push(texto.length);
    texto += (no as Text).data;
  }
  return { nos, inicios, texto };
}

function posicao(indice: ReturnType<typeof indexar>, deslocamento: number): [Text, number] {
  let k = 0;
  while (k + 1 < indice.inicios.length && indice.inicios[k + 1]! <= deslocamento) k++;
  return [indice.nos[k]!, deslocamento - indice.inicios[k]!];
}

/** As faixas da folha onde cada texto aparece. */
export function faixas(raiz: HTMLElement, textos: string[]): Range[] {
  const indice = indexar(raiz);
  if (indice.nos.length === 0) return [];
  const resultado: Range[] = [];
  for (const alvo of new Set(textos)) {
    let de = 0;
    for (;;) {
      const achado = indice.texto.indexOf(alvo, de);
      if (achado < 0) break;
      const [noInicio, inicio] = posicao(indice, achado);
      const [noFim, fim] = posicao(indice, achado + alvo.length - 1);
      const faixa = document.createRange();
      faixa.setStart(noInicio, inicio);
      faixa.setEnd(noFim, fim + 1);
      resultado.push(faixa);
      de = achado + alvo.length;
    }
  }
  return resultado;
}

interface RegistroDeDestaques {
  set(nome: string, destaque: unknown): void;
  delete(nome: string): void;
}

function registro(): RegistroDeDestaques | null {
  const css = (globalThis as { CSS?: { highlights?: RegistroDeDestaques } }).CSS;
  return css?.highlights && typeof (globalThis as { Highlight?: unknown }).Highlight === 'function' ? css.highlights : null;
}

/** Pinta as faixas; devolve a primeira, para a tela rolar até ela. */
export function destacar(raiz: HTMLElement | null, textos: string[]): Range | null {
  const destaques = registro();
  if (!raiz || !destaques) return null;
  const lista = textos.length > 0 ? faixas(raiz, textos) : [];
  if (lista.length === 0) {
    destaques.delete(NOME);
    return null;
  }
  const Construtor = (globalThis as unknown as { Highlight: new (...faixas: Range[]) => unknown }).Highlight;
  destaques.set(NOME, new Construtor(...lista));
  return lista.reduce((primeira, f) =>
    f.compareBoundaryPoints(Range.START_TO_START, primeira) < 0 ? f : primeira,
  );
}

export function apagarDestaques() {
  registro()?.delete(NOME);
}

/** Rola a lente até a faixa, se ela estiver fora da vista. */
export function mostrar(faixa: Range | null) {
  const elemento = faixa?.startContainer.parentElement;
  elemento?.scrollIntoView({ block: 'center', behavior: 'smooth' });
}
