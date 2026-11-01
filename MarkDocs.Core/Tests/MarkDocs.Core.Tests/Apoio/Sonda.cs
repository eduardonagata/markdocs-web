using MarkDocs.Core.Conteudo;
using MarkDocs.Core.Nucleo;

namespace MarkDocs.Core.Tests;

/// <summary>
/// O Acervo de sonda da §1.3 de <c>Docs/Testes.md</c>: pergunta ao próprio
/// Typst que elemento um literal produziu, sem olhar pixel.
/// </summary>
/// <remarks>
/// <b>Este é o único arquivo dos testes que escreve Typst</b>, e o que ele
/// escreve é só o andaime da pergunta: as duas importações, o
/// <c>#let c = renderizar(documento)</c> e as asserções. O literal é sempre
/// gerado pelo mesmo código da produção.
/// </remarks>
public static class Sonda
{
    /// <summary>
    /// Uma pergunta sobre o conteúdo composto. <c>c</c> é o que
    /// <c>renderizar</c> devolveu; com um bloco só, é o próprio elemento.
    /// </summary>
    public abstract record Afirmacao
    {
        private Afirmacao() { }

        /// <summary>
        /// <c>Alvo</c> é do tipo <c>Funcao</c>, e cada campo tem o valor dado —
        /// em sintaxe de valor Typst: <c>"2"</c>, <c>"true"</c>, <c>"none"</c>,
        /// <c>"\"swift\""</c>.
        /// </summary>
        public sealed record Elemento(string Funcao, IReadOnlyDictionary<string, string> Campos, string Alvo = "c") : Afirmacao;

        /// <summary>Algum descendente de <c>c</c> é do tipo <c>Funcao</c>.</summary>
        public sealed record Descendente(string Funcao) : Afirmacao;

        /// <summary><c>c</c> é uma string, e sem as bordas em branco vale <c>Texto</c>.</summary>
        public sealed record TextoCru(string Texto) : Afirmacao;

        /// <summary>Duas expressões Typst são iguais.</summary>
        public sealed record Igual(string Esquerda, string Direita) : Afirmacao;

        /// <summary>Põe <c>c</c> no papel. Sem isto o PDF da sonda sai em branco.</summary>
        public sealed record Mostrar : Afirmacao;
    }

    // Os construtores com a forma dos casos Swift: `.elemento("heading", campos: ["level": "2"])`.

    public static Afirmacao Elemento(string funcao, Dictionary<string, string>? campos = null, string em = "c") =>
        new Afirmacao.Elemento(funcao, campos ?? new Dictionary<string, string>(), em);

    /// <summary>O mesmo que <see cref="Elemento"/>, sobre o corpo do parágrafo — onde moram os inlines.</summary>
    public static Afirmacao NoCorpo(string funcao, Dictionary<string, string>? campos = null) =>
        Elemento(funcao, campos, "c.body");

    public static Afirmacao Descendente(string funcao) => new Afirmacao.Descendente(funcao);
    public static Afirmacao TextoCru(string texto) => new Afirmacao.TextoCru(texto);
    public static Afirmacao Igual(string esquerda, string direita) => new Afirmacao.Igual(esquerda, direita);
    public static Afirmacao Mostrar() => new Afirmacao.Mostrar();

    /// <summary>A falha da sonda, com a mensagem do Typst — é o diagnóstico inteiro.</summary>
    public sealed class Falha(string descricao) : Exception(descricao);

    /// <summary>Um valor de string em sintaxe Typst, para usar num campo.</summary>
    public static string Texto(string valor) => LiteralTypst.Texto(valor);

    /// <summary>
    /// Os pontos de código de uma string como arranjo Typst, <c>(97, 98,)</c>.
    /// Não passa pelo escape: é com isto que se confere o escape.
    /// </summary>
    public static string PontosDeCodigo(string valor)
    {
        var numeros = valor.EnumerateRunes().Select(r => r.Value.ToString()).ToList();
        return numeros.Count == 0 ? "()" : "(" + string.Join(", ", numeros) + ",)";
    }

    // ─── Verificar ──────────────────────────────────────────────────────

    public static void Verificar(string markdown, params Afirmacao[] afirmacoes) =>
        Verificar(ArvoreDeMarkdown.Converter(markdown), afirmacoes);

    internal static void Verificar(IReadOnlyList<BlocoDoc> blocos, params Afirmacao[] afirmacoes) =>
        VerificarLiteral(ValorTypst.Literal(blocos), afirmacoes);

    /// <summary>
    /// Todas as asserções valem: o PDF é descartado e nada é lançado. Uma
    /// falha: o motor recusa o documento, e a mensagem dele vira a da falha.
    /// </summary>
    public static void VerificarLiteral(string literal, params Afirmacao[] afirmacoes)
    {
        try
        {
            Compor(literal, afirmacoes);
        }
        catch (ErroTypst.Compilacao erro)
        {
            throw new Falha(string.Join("\n", erro.Diagnosticos.Select(d => d.DescricaoLegivel)));
        }
    }

    /// <summary>
    /// Compõe a sonda e devolve o PDF, lançando o <see cref="ErroTypst"/> sem
    /// tradução — para os testes que esperam uma recusa do motor.
    /// </summary>
    public static byte[] Compor(string literal, params Afirmacao[] afirmacoes)
    {
        var acervo = new Acervo(
            documento: DocumentoTyp(literal, afirmacoes),
            template: "#let estilo(body) = body",
            @base: Acervo.Base(),
            ativos: new Dictionary<string, byte[]>());
        return MotorTypst.Compor(acervo, Apoio.Fontes, DateTimeOffset.UnixEpoch).Pdf;
    }

    // ─── O andaime ──────────────────────────────────────────────────────

    private static string DocumentoTyp(string literal, IEnumerable<Afirmacao> afirmacoes) =>
        $$"""
        #import "{{Acervo.CaminhoDaBase}}": renderizar
        #import "{{Acervo.CaminhoDoTemplate}}": estilo

        #let documento = {{literal}}

        #let c = renderizar(documento)

        #let descendentes(c) = {
          if type(c) != content { return () }
          let filhos = ()
          if c.has("children") { filhos = c.children }
          if c.has("body") { filhos = (c.body,) }
          if c.has("child") { filhos = (c.child,) }
          filhos + filhos.map(descendentes).flatten()
        }

        {{string.Join("\n", afirmacoes.SelectMany(Linhas))}}
        """;

    private static IEnumerable<string> Linhas(Afirmacao afirmacao) => afirmacao switch
    {
        Afirmacao.Elemento e =>
            new[] { $"#assert.eq({e.Alvo}.func(), {e.Funcao})" }
                .Concat(e.Campos.Keys.Order(StringComparer.Ordinal).Select(campo =>
                    $"#assert.eq({e.Alvo}.at({Texto(campo)}, default: none), {e.Campos[campo]})")),
        Afirmacao.Descendente d =>
            [$"#assert(descendentes(c).any(d => d.func() == {d.Funcao}), "
             + $"message: {Texto($"nenhum descendente é {d.Funcao}")} + \": \" + repr(c))"],
        Afirmacao.TextoCru t => ["#assert.eq(type(c), str)", $"#assert.eq(c.trim(), {Texto(t.Texto)})"],
        Afirmacao.Igual i => [$"#assert.eq({i.Esquerda}, {i.Direita})"],
        Afirmacao.Mostrar => ["#c"],
        _ => throw new InvalidOperationException(afirmacao.ToString()),
    };
}
