using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkDocs.Core.Conteudo;

/// <summary>
/// O vocabulário fechado do conteúdo. É <b>um só</b>, para toda família: um
/// <c>## Seção</c> tem de ser um <c>heading</c> de nível 2 em qualquer família;
/// o que a família decide é <b>como</b> ele aparece, nunca <b>o que</b> ele é.
/// </summary>
internal abstract record BlocoDoc
{
    private BlocoDoc() { }

    public sealed record Titulo(int Nivel, Sequencia<InlineDoc> Filhos) : BlocoDoc;
    public sealed record Paragrafo(Sequencia<InlineDoc> Filhos) : BlocoDoc;
    public sealed record Lista(bool Ordenada, Sequencia<Sequencia<BlocoDoc>> Itens) : BlocoDoc;
    public sealed record Citacao(Sequencia<BlocoDoc> Filhos) : BlocoDoc;
    public sealed record BlocoDeCodigo(string? Linguagem, string Texto) : BlocoDoc;
    public sealed record Tabela(
        Sequencia<Sequencia<InlineDoc>> Cabecalho,
        Sequencia<Sequencia<Sequencia<InlineDoc>>> Linhas,
        Sequencia<Alinhamento> Alinhamentos) : BlocoDoc;
    public sealed record Regra : BlocoDoc;

    /// <summary>
    /// A rede de segurança: nó de Markdown fora do conjunto cai aqui com o
    /// texto cru — degrada, não desaparece.
    /// </summary>
    public sealed record TextoLiteral(string Valor) : BlocoDoc;
}

internal abstract record InlineDoc
{
    private InlineDoc() { }

    public sealed record Texto(string Valor) : InlineDoc;
    public sealed record Enfase(Sequencia<InlineDoc> Filhos) : InlineDoc;
    public sealed record Forte(Sequencia<InlineDoc> Filhos) : InlineDoc;
    public sealed record Riscado(Sequencia<InlineDoc> Filhos) : InlineDoc;
    public sealed record Codigo(string Valor) : InlineDoc;
    public sealed record Link(string Destino, Sequencia<InlineDoc> Filhos) : InlineDoc;
    public sealed record QuebraDeLinha : InlineDoc;

    /// <summary>
    /// <b>Extensão do MarkDocs.Core</b> (vocabulário 2): uma imagem do
    /// documento, citada por <c>imagens/&lt;nome&gt;</c> e entregue ao
    /// renderizador. Ver <see cref="ImagensDoDocumento"/>.
    /// </summary>
    public sealed record Imagem(string Nome, string Alternativo) : InlineDoc;
}

internal enum Alinhamento { Esquerda, Centro, Direita }

/// <summary>
/// Camada A — Markdown → árvore fechada. <c>string → record</c>, pura, sem I/O.
/// É onde os testes moram.
/// </summary>
/// <remarks>
/// O NotiqTypst usa o swift-markdown (cmark-gfm); aqui é o Markdig, com as
/// mesmas extensões do GFM que importam ao vocabulário — tabelas, riscado e
/// links automáticos. As diferenças de forma entre os dois parsers são
/// absorvidas neste arquivo, e os testes de <c>CasosDeConteudo</c> fixam que a
/// árvore sai igual à do Swift.
/// </remarks>
internal static class ArvoreDeMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseAutoLinks()
        .Build();

    private static readonly IReadOnlySet<string> SemImagens = new HashSet<string>();

    public static Sequencia<BlocoDoc> Converter(string markdown) => Converter(markdown, SemImagens);

    /// <param name="imagens">
    /// Os nomes das imagens que o documento entrega. Uma imagem citada fora
    /// deste conjunto continua sendo o texto alternativo.
    /// </param>
    public static Sequencia<BlocoDoc> Converter(string markdown, IReadOnlySet<string> imagens)
    {
        var documento = Markdown.Parse(markdown, Pipeline);
        return new Conversor(imagens).Blocos(documento);
    }

    private sealed class Conversor(IReadOnlySet<string> imagens)
    {
        // ─── Blocos ──────────────────────────────────────────────────

        public Sequencia<BlocoDoc> Blocos(IEnumerable<Block> filhos) =>
            filhos.Select(Bloco).OfType<BlocoDoc>().ParaSequencia();

        private BlocoDoc? Bloco(Block no)
        {
            switch (no)
            {
                case HeadingBlock titulo:
                    // O Markdown permite nível 1 a 6; o Typst exige ≥ 1.
                    return new BlocoDoc.Titulo(Math.Max(1, titulo.Level), Inlines(titulo.Inline));

                case ParagraphBlock paragrafo:
                    return new BlocoDoc.Paragrafo(Inlines(paragrafo.Inline));

                case ListBlock lista:
                    return new BlocoDoc.Lista(
                        lista.IsOrdered,
                        lista.OfType<ListItemBlock>().Select(item => Blocos(item)).ParaSequencia());

                case QuoteBlock citacao:
                    return new BlocoDoc.Citacao(Blocos(citacao));

                case FencedCodeBlock cercado:
                    return new BlocoDoc.BlocoDeCodigo(
                        string.IsNullOrEmpty(cercado.Info) ? null : cercado.Info,
                        TextoDasLinhas(cercado));

                case CodeBlock indentado:
                    return new BlocoDoc.BlocoDeCodigo(null, TextoDasLinhas(indentado));

                case Table tabela:
                    return Tabela(tabela);

                case ThematicBreakBlock:
                    return new BlocoDoc.Regra();

                case HtmlBlock html:
                    // HTML não tem equivalente no vocabulário. Vira texto, e aparece.
                    return new BlocoDoc.TextoLiteral(TextoDasLinhas(html));

                case LinkReferenceDefinitionGroup:
                case LinkReferenceDefinition:
                    // Definições de link não são conteúdo: o cmark não as põe na árvore.
                    return null;

                case ContainerBlock container:
                    // Um contêiner desconhecido ainda tem filhos que conhecemos:
                    // um nível de estrutura se perde, o conteúdo não.
                    return new BlocoDoc.Citacao(Blocos(container));

                case LeafBlock folha:
                    // Nada de silêncio: o texto bruto do nó entra na página.
                    var texto = TextoDasLinhas(folha);
                    return texto.Length == 0 ? null : new BlocoDoc.TextoLiteral(texto);

                default:
                    return null;
            }
        }

        /// <summary>
        /// O texto de um bloco folha, linha a linha, cada uma terminada por
        /// <c>\n</c> — como o cmark entrega o código e o HTML.
        /// </summary>
        private static string TextoDasLinhas(LeafBlock folha)
        {
            var linhas = folha.Lines;
            var texto = new System.Text.StringBuilder();
            for (var i = 0; i < linhas.Count; i++)
                texto.Append(linhas.Lines[i].Slice.ToString()).Append('\n');
            return texto.ToString();
        }

        /// <summary>
        /// Como no GFM: a largura é a do cabeçalho; uma linha curta é
        /// completada com células vazias, e uma longa é cortada.
        /// </summary>
        private BlocoDoc Tabela(Table tabela)
        {
            var linhas = tabela.OfType<TableRow>().ToList();
            var cabecalho = linhas.FirstOrDefault(l => l.IsHeader);
            var largura = tabela.ColumnDefinitions.Count > 0
                ? tabela.ColumnDefinitions.Count
                : cabecalho?.Count ?? linhas.Select(l => l.Count).DefaultIfEmpty(0).Max();

            Sequencia<Sequencia<InlineDoc>> Celulas(TableRow linha) =>
                Enumerable.Range(0, largura)
                    .Select(i => i < linha.Count && linha[i] is TableCell celula
                        ? Inlines(celula.OfType<LeafBlock>().Select(b => b.Inline))
                        : Sequencia<InlineDoc>.Vazia)
                    .ParaSequencia();

            var alinhamentos = Enumerable.Range(0, largura)
                .Select(coluna => (coluna < tabela.ColumnDefinitions.Count ? tabela.ColumnDefinitions[coluna].Alignment : null) switch
                {
                    TableColumnAlign.Center => Alinhamento.Centro,
                    TableColumnAlign.Right => Alinhamento.Direita,
                    _ => Alinhamento.Esquerda,
                })
                .ParaSequencia();

            return new BlocoDoc.Tabela(
                cabecalho is null ? Sequencia<Sequencia<InlineDoc>>.Vazia : Celulas(cabecalho),
                linhas.Where(l => l != cabecalho).Select(Celulas).ParaSequencia(),
                alinhamentos);
        }

        // ─── Inlines ─────────────────────────────────────────────────

        private Sequencia<InlineDoc> Inlines(ContainerInline? container) =>
            Inlines(container is null ? [] : [container]);

        private Sequencia<InlineDoc> Inlines(IEnumerable<ContainerInline?> containers) =>
            containers.OfType<ContainerInline>().SelectMany(c => c).SelectMany(Inline).ParaSequencia();

        private IEnumerable<InlineDoc> Inline(Inline no)
        {
            switch (no)
            {
                case LiteralInline texto:
                    var valor = texto.Content.ToString();
                    if (valor.Length > 0) yield return new InlineDoc.Texto(valor);
                    break;

                case EmphasisInline enfase:
                    var filhos = Inlines(enfase);
                    yield return enfase.DelimiterChar == '~' ? new InlineDoc.Riscado(filhos)
                        : enfase.DelimiterCount >= 2 ? new InlineDoc.Forte(filhos)
                        : new InlineDoc.Enfase(filhos);
                    break;

                case CodeInline codigo:
                    yield return new InlineDoc.Codigo(codigo.Content);
                    break;

                case LinkInline { IsImage: true } imagem:
                    // O que o motor pode mostrar vem do Acervo, e o Acervo só
                    // tem as imagens que o documento entregou. Fora delas sobra
                    // o texto alternativo, que é informação de verdade.
                    var alternativo = TextoPlano(imagem);
                    if (ImagensDoDocumento.NomeCitado(imagem.Url) is { } nome && imagens.Contains(nome))
                        yield return new InlineDoc.Imagem(nome, alternativo);
                    else if (alternativo.Length > 0)
                        yield return new InlineDoc.Texto(alternativo);
                    break;

                case LinkInline link:
                    // Link sem destino é só o texto dele.
                    yield return string.IsNullOrEmpty(link.Url)
                        ? new InlineDoc.Enfase(Inlines(link))
                        : new InlineDoc.Link(link.Url, Inlines(link));
                    break;

                case AutolinkInline automatico:
                    yield return new InlineDoc.Link(
                        automatico.IsEmail ? "mailto:" + automatico.Url : automatico.Url,
                        [new InlineDoc.Texto(automatico.Url)]);
                    break;

                case LineBreakInline { IsHard: true }:
                    yield return new InlineDoc.QuebraDeLinha();
                    break;

                case LineBreakInline:
                    // Quebra simples é espaço: quem decide onde a linha quebra é
                    // o motor, e é a propriedade inteira da paginação.
                    yield return new InlineDoc.Texto(" ");
                    break;

                case HtmlInline html:
                    yield return new InlineDoc.Texto(html.Tag);
                    break;

                case HtmlEntityInline entidade:
                    yield return new InlineDoc.Texto(entidade.Transcoded.ToString());
                    break;

                case DelimiterInline delimitador:
                    // Um delimitador que não fechou (`[` sozinho) é texto.
                    yield return new InlineDoc.Texto(delimitador.ToLiteral());
                    foreach (var filho in delimitador.SelectMany(Inline))
                        yield return filho;
                    break;

                case ContainerInline container:
                    yield return new InlineDoc.Enfase(Inlines(container));
                    break;

                default:
                    var plano = TextoPlano(no);
                    if (plano.Length > 0) yield return new InlineDoc.Texto(plano);
                    break;
            }
        }

        private static string TextoPlano(Inline no) => no switch
        {
            LiteralInline l => l.Content.ToString(),
            CodeInline c => c.Content,
            HtmlEntityInline e => e.Transcoded.ToString(),
            LineBreakInline => " ",
            ContainerInline c => string.Concat(c.Select(TextoPlano)),
            _ => "",
        };
    }
}
