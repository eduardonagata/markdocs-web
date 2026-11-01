using MarkDocs.Documentos.Domain.Entities;
using MongoDB.Bson;

namespace MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;

public interface ITemplateRepository
{
    Task<TemplateDeDocumento?> ObterAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>Os de fábrica primeiro; depois os demais, do mais novo ao mais antigo.</summary>
    Task<IReadOnlyList<TemplateDeDocumento>> ListarAsync(CancellationToken ct = default);

    Task CriarAsync(TemplateDeDocumento template, CancellationToken ct = default);
    Task CriarVariosAsync(IEnumerable<TemplateDeDocumento> templates, CancellationToken ct = default);
    Task<long> ContarDeFabricaAsync(CancellationToken ct = default);
}
