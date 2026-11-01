using MarkDocs.Core.Conteudo;
using static MarkDocs.Core.Tests.Sonda;

namespace MarkDocs.Core.Tests;

/// <summary>
/// A tabela de casos da §3 de <c>Docs/Testes.md</c>. Cada linha é um
/// Markdown, a árvore esperada, o <c>tipo</c> no literal e o elemento Typst —
/// e as três camadas da suíte 1 são dirigidas por ela.
/// </summary>
/// <remarks>
/// <b>Acrescentar um elemento ao vocabulário é acrescentar uma linha aqui.</b>
/// Quem acrescenta escreve Markdown e expectativas em C#, nunca Typst.
/// </remarks>
public static class CasosDeConteudo
{
    internal sealed record Bloco(
        string Nome,
        string Markdown,
        Sequencia<BlocoDoc> Arvore,
        string Tipo,
        Afirmacao[] Elemento,
        // Trechos que o literal contém, além do `tipo`.
        string[]? NoLiteral = null,
        // Um ponto em aberto da §6: a expectativa é a correta, e a falha atual
        // fica registrada como problema conhecido.
        string? ProblemaConhecido = null);

    internal sealed record Inline(
        string Nome,
        string Markdown,
        // Os filhos do parágrafo.
        Sequencia<InlineDoc> Arvore,
        string Tipo,
        Afirmacao[] Elemento);

    private static Dictionary<string, string> Campos(params (string Chave, string Valor)[] pares) =>
        pares.ToDictionary(p => p.Chave, p => p.Valor);

    private static BlocoDoc.Paragrafo Paragrafo(params InlineDoc[] filhos) => new([.. filhos]);
    private static InlineDoc.Texto T(string valor) => new(valor);

    // ─── §3.1 Blocos ────────────────────────────────────────────────────

    /// <summary>Um caso por nível: <c>#</c> a <c>######</c> viram <c>heading(level: n)</c>.</summary>
    private static readonly Bloco[] Titulos = Enumerable.Range(1, 6).Select(nivel => new Bloco(
        Nome: $"título nível {nivel}",
        Markdown: new string('#', nivel) + " Objeto",
        Arvore: [new BlocoDoc.Titulo(nivel, [T("Objeto")])],
        Tipo: "titulo",
        NoLiteral: [$"nivel: {nivel}"],
        Elemento: [Elemento("heading", Campos(("level", $"{nivel}")))])).ToArray();

    internal static readonly Bloco[] Blocos =
    [
        .. Titulos,
        new(
            Nome: "parágrafo",
            Markdown: "Texto.",
            Arvore: [Paragrafo(T("Texto."))],
            Tipo: "paragrafo",
            Elemento: [Elemento("par")]),
        new(
            Nome: "lista",
            Markdown: "- a",
            Arvore: [new BlocoDoc.Lista(false, [[Paragrafo(T("a"))]])],
            Tipo: "lista",
            NoLiteral: ["ordenada: false"],
            Elemento: [Elemento("list"), Igual("c.children.len()", "1")]),
        new(
            Nome: "lista numerada",
            Markdown: "1. a",
            Arvore: [new BlocoDoc.Lista(true, [[Paragrafo(T("a"))]])],
            Tipo: "lista",
            NoLiteral: ["ordenada: true"],
            Elemento: [Elemento("enum")]),
        new(
            Nome: "lista numerada a partir de 3",
            Markdown: "3. a",
            Arvore: [new BlocoDoc.Lista(true, [[Paragrafo(T("a"))]])],
            Tipo: "lista",
            NoLiteral: ["ordenada: true"],
            Elemento: [Elemento("enum", Campos(("start", "3")))],
            ProblemaConhecido: "§6.2: a árvore não guarda o início, e o base.typ chama enum sem start:"),
        new(
            Nome: "lista aninhada",
            Markdown: "- a\n  - b",
            Arvore: [new BlocoDoc.Lista(false, [[
                Paragrafo(T("a")),
                new BlocoDoc.Lista(false, [[Paragrafo(T("b"))]]),
            ]])],
            Tipo: "lista",
            Elemento: [Elemento("list"), Descendente("list")]),
        new(
            Nome: "citação",
            Markdown: "> q",
            Arvore: [new BlocoDoc.Citacao([Paragrafo(T("q"))])],
            Tipo: "citacao",
            Elemento: [Elemento("quote", Campos(("block", "true"))), Descendente("par")]),
        new(
            Nome: "bloco de código",
            Markdown: "```swift\nlet x = 1\n```",
            Arvore: [new BlocoDoc.BlocoDeCodigo("swift", "let x = 1\n")],
            Tipo: "bloco-de-codigo",
            NoLiteral: ["linguagem: \"swift\""],
            Elemento: [Elemento("raw", Campos(("block", "true"), ("lang", Texto("swift"))))]),
        new(
            Nome: "código sem linguagem",
            Markdown: "```\nx\n```",
            Arvore: [new BlocoDoc.BlocoDeCodigo(null, "x\n")],
            Tipo: "bloco-de-codigo",
            NoLiteral: ["linguagem: none"],
            Elemento: [Elemento("raw", Campos(("block", "true"), ("lang", "none")))]),
        new(
            Nome: "tabela",
            Markdown: "| A | B |\n|---|---|\n| 1 | 2 |",
            Arvore: [new BlocoDoc.Tabela(
                Cabecalho: [[T("A")], [T("B")]],
                Linhas: [[[T("1")], [T("2")]]],
                Alinhamentos: [Alinhamento.Esquerda, Alinhamento.Esquerda])],
            Tipo: "tabela",
            NoLiteral: ["""alinhamentos: ("esquerda", "esquerda",)"""],
            Elemento:
            [
                Elemento("table"),
                Igual("c.columns.len()", "2"),
                Descendente("table.header"),
            ]),
        new(
            Nome: "regra",
            Markdown: "---",
            Arvore: [new BlocoDoc.Regra()],
            Tipo: "regra",
            Elemento: [Elemento("line", Campos(("length", "100%")))]),
        new(
            Nome: "HTML",
            Markdown: "<div>x</div>",
            Arvore: [new BlocoDoc.TextoLiteral("<div>x</div>\n")],
            Tipo: "texto-literal",
            Elemento: [TextoCru("<div>x</div>")]),
    ];

    internal static Bloco BlocoChamado(string nome) => Blocos.Single(b => b.Nome == nome);

    public static TheoryData<string> NomesDosBlocos => new(Blocos.Select(b => b.Nome));

    // ─── §3.2 Inlines ───────────────────────────────────────────────────

    internal static readonly Inline[] Inlines =
    [
        new(
            Nome: "texto",
            Markdown: "abc",
            Arvore: [T("abc")],
            Tipo: "texto",
            Elemento: [NoCorpo("text", Campos(("text", Texto("abc"))))]),
        new(
            Nome: "ênfase com asterisco",
            Markdown: "*a*",
            Arvore: [new InlineDoc.Enfase([T("a")])],
            Tipo: "enfase",
            Elemento: [NoCorpo("emph")]),
        new(
            Nome: "ênfase com sublinhado",
            Markdown: "_a_",
            Arvore: [new InlineDoc.Enfase([T("a")])],
            Tipo: "enfase",
            Elemento: [NoCorpo("emph")]),
        new(
            Nome: "forte",
            Markdown: "**a**",
            Arvore: [new InlineDoc.Forte([T("a")])],
            Tipo: "forte",
            Elemento: [NoCorpo("strong")]),
        new(
            Nome: "riscado",
            Markdown: "~~a~~",
            Arvore: [new InlineDoc.Riscado([T("a")])],
            Tipo: "riscado",
            Elemento: [NoCorpo("strike")]),
        new(
            Nome: "código",
            Markdown: "`a`",
            Arvore: [new InlineDoc.Codigo("a")],
            Tipo: "codigo",
            Elemento:
            [
                NoCorpo("raw", Campos(("text", Texto("a")))),
                Igual("c.body.at(\"block\", default: false)", "false"),
            ]),
        new(
            Nome: "link",
            Markdown: "[a](https://x)",
            Arvore: [new InlineDoc.Link("https://x", [T("a")])],
            Tipo: "link",
            Elemento: [NoCorpo("link", Campos(("dest", Texto("https://x"))))]),
        new(
            Nome: "quebra forçada com dois espaços",
            Markdown: "a  \nb",
            Arvore: [T("a"), new InlineDoc.QuebraDeLinha(), T("b")],
            Tipo: "quebra-de-linha",
            Elemento: [Descendente("linebreak")]),
        new(
            Nome: "quebra forçada com contrabarra",
            Markdown: "a\\\nb",
            Arvore: [T("a"), new InlineDoc.QuebraDeLinha(), T("b")],
            Tipo: "quebra-de-linha",
            Elemento: [Descendente("linebreak")]),
        new(
            Nome: "quebra simples",
            Markdown: "a\nb",
            Arvore: [T("a"), T(" "), T("b")],
            Tipo: "texto",
            Elemento: [NoCorpo("text", Campos(("text", Texto("a b"))))]),
        new(
            Nome: "imagem",
            Markdown: "![alt](f.png)",
            Arvore: [T("alt")],
            Tipo: "texto",
            Elemento: [NoCorpo("text", Campos(("text", Texto("alt"))))]),
        new(
            Nome: "HTML inline",
            Markdown: "a <b> c",
            Arvore: [T("a "), T("<b>"), T(" c")],
            Tipo: "texto",
            Elemento: [NoCorpo("text", Campos(("text", Texto("a <b> c"))))]),
        new(
            Nome: "link sem destino",
            Markdown: "[a]()",
            Arvore: [new InlineDoc.Enfase([T("a")])],
            Tipo: "enfase",
            Elemento: [NoCorpo("emph")]),
    ];

    internal static Inline InlineChamado(string nome) => Inlines.Single(i => i.Nome == nome);

    public static TheoryData<string> NomesDosInlines => new(Inlines.Select(i => i.Nome));

    // ─── §3.3 Texto perigoso ────────────────────────────────────────────

    /// <summary>Caracteres que seriam sintaxe em marcação Typst, mais texto fora do ASCII.</summary>
    public static readonly string[] Perigosos =
    [
        "#", "$x$", "\"", "\\", "<tag>", "@ref", "~", "#let x = 1",
        "[", "]", "*", "_", "`", "🙂", "مرحبا",
    ];

    public static TheoryData<string> PerigososComoDados => new(Perigosos);
}
