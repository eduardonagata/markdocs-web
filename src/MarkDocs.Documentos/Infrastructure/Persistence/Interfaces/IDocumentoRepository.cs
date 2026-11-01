using MarkDocs.Documentos.Domain.Entities;
using MongoDB.Bson;

namespace MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;

/// <summary>O que a listagem mostra: sem o corpo e sem a formatação.</summary>
public sealed record ResumoDeDocumento(
    ObjectId Id,
    string Titulo,
    string Tipo,
    string? TemplateDeOrigem,
    DateTimeOffset CriadoEm,
    DateTimeOffset AlteradoEm);

public interface IDocumentoRepository
{
    Task<Documento?> ObterAsync(ObjectId id, CancellationToken ct = default);
    Task<IReadOnlyList<ResumoDeDocumento>> ListarAsync(CancellationToken ct = default);
    Task CriarAsync(Documento documento, CancellationToken ct = default);
    Task SubstituirAsync(Documento documento, CancellationToken ct = default);
    Task<bool> RemoverAsync(ObjectId id, CancellationToken ct = default);
}
