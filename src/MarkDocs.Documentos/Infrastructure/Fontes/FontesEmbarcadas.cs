using MarkDocs.Core;

namespace MarkDocs.Documentos.Infrastructure.Fontes;

/// <summary>
/// As famílias tipográficas que o app embarca. O motor Typst <b>não enxerga as
/// fontes do sistema</b>: ele só usa o que entregamos — e é por isso que o PDF
/// sai idêntico em qualquer máquina.
/// </summary>
/// <remarks>
/// <b>A lista é append-only.</b> Uma família embarcada nunca sai, porque
/// documentos antigos a nomeiam. A licença é a SIL OFL (<c>Libertinus-OFL.txt</c>),
/// que cobre embarcar no app e redistribuir dentro de um documento.
/// </remarks>
public static class FontesEmbarcadas
{
    /// <summary>A família e os arquivos de cada face. A chave é o nome que um template escreve em <c>fontes</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Familias = new Dictionary<string, string[]>
    {
        ["Libertinus Serif"] = ["LibertinusSerif-Regular", "LibertinusSerif-Bold", "LibertinusSerif-Italic", "LibertinusSerif-BoldItalic"],
        ["Libertinus Sans"] = ["LibertinusSans-Regular", "LibertinusSans-Bold", "LibertinusSans-Italic"],
        ["Libertinus Mono"] = ["LibertinusMono-Regular"],
    };

    /// <summary>Lidas uma vez, na partida: o renderizador as guarda e o motor as recebe a cada composição.</summary>
    public static IReadOnlyList<FonteEmbarcada> Carregar()
    {
        var assembly = typeof(FontesEmbarcadas).Assembly;
        var fontes = new List<FonteEmbarcada>();
        foreach (var (familia, arquivos) in Familias.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            foreach (var arquivo in arquivos)
            {
                using var fluxo = assembly.GetManifestResourceStream($"MarkDocs.Documentos.Fontes.{arquivo}.otf")
                    ?? throw new InvalidOperationException($"A face {arquivo}.otf não está no assembly.");
                using var memoria = new MemoryStream();
                fluxo.CopyTo(memoria);
                fontes.Add(new FonteEmbarcada(familia, memoria.ToArray()));
            }
        }
        return fontes;
    }
}
