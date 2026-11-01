using MarkDocs.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MarkDocs.Documentos.Domain.Entities;

/// <summary>
/// Um template salvo: o <see cref="Template"/> da especificação (formatação e
/// ativos) com o que o app precisa para listá-lo e criar documentos a partir
/// dele — o tipo documental, a origem e a data.
/// </summary>
/// <remarks>
/// Os templates de fábrica nascem dos <c>template.json</c> do app; os demais,
/// de um documento já formatado. Os dois passam pelo mesmo caminho.
/// </remarks>
public sealed class TemplateDeDocumento
{
    public ObjectId Id { get; init; }
    public required string Nome { get; set; }

    /// <summary>O tipo documental dos documentos criados a partir dele: <c>"Contrato"</c>.</summary>
    public required string Tipo { get; set; }

    public required Formatacao Formatacao { get; set; }
    public List<ReferenciaDeAtivo> Ativos { get; set; } = [];

    /// <summary>Semeado pelo app, a partir de um <c>template.json</c> de fábrica.</summary>
    public bool DeFabrica { get; init; }

    /// <summary>O documento de onde a formatação foi copiada, quando houver.</summary>
    public ObjectId? DocumentoDeOrigem { get; init; }

    [BsonRepresentation(BsonType.DateTime)]
    public required DateTimeOffset CriadoEm { get; init; }

    /// <summary>A forma da especificação, para aplicar a um documento.</summary>
    public Template ComoTemplate() => new()
    {
        Id = Id.ToString(),
        Nome = Nome,
        Formatacao = Formatacao,
        Ativos = [.. Ativos],
    };

    /// <summary>
    /// Um template novo a partir de um documento formatado: a formatação
    /// copiada e os ativos de template. As imagens do Markdown são do
    /// documento, não do template.
    /// </summary>
    public static TemplateDeDocumento DoDocumento(Documento documento, string nome, DateTimeOffset agora) => new()
    {
        Id = ObjectId.GenerateNewId(),
        Nome = nome.Trim(),
        Tipo = documento.Tipo,
        Formatacao = documento.Formatacao.Copiar(),
        Ativos = [.. documento.Ativos.Where(a => a.Papel == PapelDoAtivo.Template)],
        DocumentoDeOrigem = documento.Id,
        CriadoEm = agora,
    };
}
