using Xunit.Sdk;

namespace MarkDocs.Core.Tests;

/// <summary>
/// O <c>withKnownIssue</c> do Swift Testing: registra um ponto em aberto da §6
/// sem cimentá-lo como especificação.
/// </summary>
/// <remarks>
/// A expectativa dentro do bloco é a <b>correta</b>. Enquanto ela falhar, o
/// teste passa — o comportamento atual fica registrado. No dia em que for
/// corrigido, o bloco deixa de falhar e o teste <b>acusa a correção</b>, para
/// que o registro saia.
/// </remarks>
public static class ProblemaConhecido
{
    public static void Registrar(string motivo, Action confere)
    {
        try
        {
            confere();
        }
        catch (Exception)
        {
            return;
        }
        throw new XunitException(
            $"O problema conhecido não ocorreu — foi corrigido? Remova o registro. ({motivo})");
    }
}

/// <summary>As tags do Swift Testing, como <c>Trait</c> do xUnit (<c>dotnet test --filter Tag=motor</c>).</summary>
public static class Tags
{
    /// <summary>Suíte 1: Markdown → Typst.</summary>
    public const string Conteudo = "conteudo";
    /// <summary>Suíte 2: um gerador de template.</summary>
    public const string Gerador = "gerador";
    /// <summary>Suíte 3: pelo <c>RenderizadorPDF</c>.</summary>
    public const string Integracao = "integracao";
    /// <summary>Atravessa o Rust. Mais lento que o C# puro.</summary>
    public const string Motor = "motor";
}
