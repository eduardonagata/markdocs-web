namespace MarkDocs.Abstractions;

/// <summary>
/// Um preset: valores de formatação e os ativos que eles citam. Não é
/// referenciado pelo documento — aplicar é copiar.
/// </summary>
public sealed class Template
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public Formatacao Formatacao { get; set; } = default!;
    public List<ReferenciaDeAtivo> Ativos { get; set; } = [];
}
