using MarkDocs.Core.Nucleo;

namespace MarkDocs.Core.Conteudo;

/// <summary>Camada B — árvore → literal de dados.</summary>
/// <remarks>
/// <b>O conversor nunca escreve marcação Typst. Escreve dados.</b> Num literal
/// de dados, o texto do usuário aparece somente dentro de string entre aspas —
/// e ali o escape tem exatamente dois caracteres. <c>Silva &amp; Cia #2</c>
/// atravessa como conteúdo de string: nenhum caractere do nome pode virar
/// sintaxe, porque nenhum está em posição sintática.
/// </remarks>
internal static class ValorTypst
{
    /// <summary>
    /// O contrato entre o C# e o <c>base.typ</c>. Caso novo em
    /// <see cref="BlocoDoc"/>/<see cref="InlineDoc"/> → este número sobe → o
    /// que estiver desatualizado para de compilar <b>de propósito</b>.
    /// </summary>
    /// <remarks>
    /// 1 é o vocabulário do NotiqTypst; 2 acrescenta <c>"imagem"</c>
    /// (extensão do MarkDocs.Core, ver <c>Docs/Imagens.md</c>).
    /// </remarks>
    public const int VersaoDoVocabulario = 2;

    public static string Literal(IReadOnlyList<BlocoDoc> blocos) =>
        "(\n"
        + $"  versao: {VersaoDoVocabulario},\n"
        + $"  blocos: {Arranjo(blocos.Select(LiteralDe))},\n"
        + ")";

    // ─── Blocos ─────────────────────────────────────────────────────────

    private static string LiteralDe(BlocoDoc bloco) => bloco switch
    {
        BlocoDoc.Titulo t => Dicionario(
            ("tipo", LiteralTypst.Texto("titulo")),
            ("nivel", t.Nivel.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("filhos", Arranjo(t.Filhos.Select(LiteralDe)))),

        BlocoDoc.Paragrafo p => Dicionario(
            ("tipo", LiteralTypst.Texto("paragrafo")),
            ("filhos", Arranjo(p.Filhos.Select(LiteralDe)))),

        BlocoDoc.Lista l => Dicionario(
            ("tipo", LiteralTypst.Texto("lista")),
            ("ordenada", l.Ordenada ? "true" : "false"),
            ("itens", Arranjo(l.Itens.Select(item => Arranjo(item.Select(LiteralDe)))))),

        BlocoDoc.Citacao c => Dicionario(
            ("tipo", LiteralTypst.Texto("citacao")),
            ("filhos", Arranjo(c.Filhos.Select(LiteralDe)))),

        BlocoDoc.BlocoDeCodigo c => Dicionario(
            ("tipo", LiteralTypst.Texto("bloco-de-codigo")),
            ("linguagem", c.Linguagem is null ? "none" : LiteralTypst.Texto(c.Linguagem)),
            ("texto", LiteralTypst.Texto(c.Texto))),

        BlocoDoc.Tabela t => Dicionario(
            ("tipo", LiteralTypst.Texto("tabela")),
            ("cabecalho", Arranjo(t.Cabecalho.Select(celula => Arranjo(celula.Select(LiteralDe))))),
            ("linhas", Arranjo(t.Linhas.Select(linha =>
                Arranjo(linha.Select(celula => Arranjo(celula.Select(LiteralDe))))))),
            ("alinhamentos", Arranjo(t.Alinhamentos.Select(a => LiteralTypst.Texto(TextoDo(a)))))),

        BlocoDoc.Regra => Dicionario(("tipo", LiteralTypst.Texto("regra"))),

        BlocoDoc.TextoLiteral t => Dicionario(
            ("tipo", LiteralTypst.Texto("texto-literal")),
            ("valor", LiteralTypst.Texto(t.Valor))),

        _ => throw new InvalidOperationException($"bloco sem literal: {bloco}"),
    };

    // ─── Inlines ────────────────────────────────────────────────────────

    private static string LiteralDe(InlineDoc inline) => inline switch
    {
        InlineDoc.Texto t => Dicionario(
            ("tipo", LiteralTypst.Texto("texto")),
            ("valor", LiteralTypst.Texto(t.Valor))),

        InlineDoc.Enfase e => Encadeado("enfase", e.Filhos),
        InlineDoc.Forte f => Encadeado("forte", f.Filhos),
        InlineDoc.Riscado r => Encadeado("riscado", r.Filhos),

        InlineDoc.Codigo c => Dicionario(
            ("tipo", LiteralTypst.Texto("codigo")),
            ("valor", LiteralTypst.Texto(c.Valor))),

        InlineDoc.Link l => Dicionario(
            ("tipo", LiteralTypst.Texto("link")),
            ("destino", LiteralTypst.Texto(l.Destino)),
            ("filhos", Arranjo(l.Filhos.Select(LiteralDe)))),

        InlineDoc.QuebraDeLinha => Dicionario(("tipo", LiteralTypst.Texto("quebra-de-linha"))),

        InlineDoc.Imagem i => Dicionario(
            ("tipo", LiteralTypst.Texto("imagem")),
            ("caminho", LiteralTypst.Texto(Acervo.CaminhoDaImagem(i.Nome))),
            ("alternativo", LiteralTypst.Texto(i.Alternativo))),

        _ => throw new InvalidOperationException($"inline sem literal: {inline}"),
    };

    private static string Encadeado(string tipo, IEnumerable<InlineDoc> filhos) =>
        Dicionario(("tipo", LiteralTypst.Texto(tipo)), ("filhos", Arranjo(filhos.Select(LiteralDe))));

    private static string TextoDo(Alinhamento a) => a switch
    {
        Alinhamento.Centro => "centro",
        Alinhamento.Direita => "direita",
        _ => "esquerda",
    };

    // ─── A sintaxe de literal, e só ela ─────────────────────────────────

    /// <summary>
    /// <c>()</c> vazio, <c>(x,)</c> com um só — a vírgula final é o que distingue
    /// um arranjo de um parêntese de agrupamento no Typst.
    /// </summary>
    private static string Arranjo(IEnumerable<string> itens)
    {
        var lista = itens.ToList();
        return lista.Count == 0 ? "()" : "(" + string.Join(", ", lista) + ",)";
    }

    private static string Dicionario(params (string Chave, string Valor)[] pares) =>
        "(" + string.Join(", ", pares.Select(p => $"{p.Chave}: {p.Valor}")) + ")";
}
