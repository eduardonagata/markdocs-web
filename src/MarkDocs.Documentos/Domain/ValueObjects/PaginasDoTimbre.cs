namespace MarkDocs.Documentos.Domain.ValueObjects;

/// <summary>
/// Em que páginas um timbre vai: <see cref="Todas"/>, ou só a
/// <see cref="Primeira"/>, ou as <see cref="Demais"/> (da segunda em diante).
/// </summary>
public enum PaginasDoTimbre { Todas, Primeira, Demais }

public static class PaginasDoTimbreTexto
{
    public static string Texto(this PaginasDoTimbre paginas) => paginas switch
    {
        PaginasDoTimbre.Todas => "todas",
        PaginasDoTimbre.Primeira => "primeira",
        _ => "demais",
    };

    /// <exception cref="ArgumentException">Se não for <c>todas</c>, <c>primeira</c> ou <c>demais</c>.</exception>
    public static PaginasDoTimbre Ler(string texto) => texto.Trim().ToLowerInvariant() switch
    {
        "todas" => PaginasDoTimbre.Todas,
        "primeira" => PaginasDoTimbre.Primeira,
        "demais" => PaginasDoTimbre.Demais,
        _ => throw new ArgumentException($"“{texto}” não é um conjunto de páginas: use todas, primeira ou demais."),
    };
}
