using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MarkDocs.Documentos.Infrastructure.Persistence.Repositories;

public sealed class TemplateRepository(IMongoDatabase database) : ITemplateRepository
{
    public const string Colecao = "documentos.templates";

    private readonly IMongoCollection<TemplateDeDocumento> _colecao = database.GetCollection<TemplateDeDocumento>(Colecao);

    public async Task<TemplateDeDocumento?> ObterAsync(ObjectId id, CancellationToken ct = default) =>
        await _colecao.Find(t => t.Id == id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TemplateDeDocumento>> ListarAsync(CancellationToken ct = default) =>
        await _colecao.Find(FilterDefinition<TemplateDeDocumento>.Empty)
            .SortByDescending(t => t.DeFabrica)
            .ThenByDescending(t => t.CriadoEm)
            .ThenBy(t => t.Nome)
            .ToListAsync(ct);

    public Task CriarAsync(TemplateDeDocumento template, CancellationToken ct = default) =>
        _colecao.InsertOneAsync(template, cancellationToken: ct);

    public Task CriarVariosAsync(IEnumerable<TemplateDeDocumento> templates, CancellationToken ct = default) =>
        _colecao.InsertManyAsync(templates, cancellationToken: ct);

    public Task<long> ContarDeFabricaAsync(CancellationToken ct = default) =>
        _colecao.CountDocumentsAsync(t => t.DeFabrica, cancellationToken: ct);
}
