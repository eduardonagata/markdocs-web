using System.Text.Json.Nodes;
using MarkDocs.Abstractions.Formatacoes;
using MarkDocs.Core;

namespace MarkDocs.Abstractions.Tests;

public static class Apoio
{
    static Apoio() => SerializacaoDaFormatacao.RegistrarBson();

    private static string Recursos => Path.Combine(AppContext.BaseDirectory, "Recursos");

    public static IReadOnlyList<string> ArquivosDeFabrica =>
        Directory.GetFiles(Path.Combine(Recursos, "Templates"), "*.json").Select(Path.GetFileName).OfType<string>().Order().ToList();

    public static TheoryData<string> TemplatesDeFabrica => new(ArquivosDeFabrica);

    public static byte[] Json(string arquivo) => File.ReadAllBytes(Path.Combine(Recursos, "Templates", arquivo));

    public static JsonObject No(byte[] json) => JsonNode.Parse(json)!.AsObject();

    public static string Nome(byte[] json) => No(json)["nome"]!.GetValue<string>();

    public static string? TipoDocumental(byte[] json) => No(json)["tipoDocumental"]?.GetValue<string>();

    public static FormatacaoDocumentoDeNegocio Formatacao(string arquivo)
    {
        SerializacaoDaFormatacao.RegistrarBson();
        return FormatacaoDocumentoDeNegocio.DeTemplateJson(Json(arquivo));
    }

    /// <summary>As famílias que o app embarca, com o nome que os templates citam.</summary>
    public static RenderizadorPDF Renderizador { get; } = new(
        Directory.GetFiles(Path.Combine(Recursos, "Fontes"), "*.otf").Select(caminho =>
        {
            var arquivo = Path.GetFileNameWithoutExtension(caminho);
            var familia = arquivo.StartsWith("LibertinusSerif") ? "Libertinus Serif"
                : arquivo.StartsWith("LibertinusSans") ? "Libertinus Sans"
                : "Libertinus Mono";
            return new FonteEmbarcada(familia, File.ReadAllBytes(caminho));
        }));

    /// <summary>Um Markdown que passa por todo elemento que a família estiliza.</summary>
    public const string Markdown = """
        # Contrato de prestação

        Conforme tratado, segue o contrato para **Silva & Cia #2**, com as cláusulas
        que *as partes* acordaram.

        ## Objeto

        O presente instrumento tem por objeto a prestação de serviços.

        ### Detalhe

        | Etapa | Prazo |
        |---|--:|
        | 1 | 10 dias |
        | 2 | 20 dias |

        > Uma citação.
        """;
}
