using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MarkDocs.Documentos.Infrastructure.Persistence.Repositories;

public sealed class DocumentoRepository(IMongoDatabase database) : IDocumentoRepository
{
    public const string Colecao = "documentos.documentos";

    private readonly IMongoCollection<Documento> _colecao = database.GetCollection<Documento>(Colecao);

    public async Task<Documento?> ObterAsync(ObjectId id, CancellationToken ct = default) =>
        await _colecao.Find(d => d.Id == id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ResumoDeDocumento>> ListarAsync(CancellationToken ct = default) =>
        await _colecao.Find(FilterDefinition<Documento>.Empty)
            .SortByDescending(d => d.AlteradoEm)
            .Project(d => new ResumoDeDocumento(d.Id, d.Titulo, d.Tipo, d.TemplateDeOrigem, d.CriadoEm, d.AlteradoEm))
            .ToListAsync(ct);

    public Task CriarAsync(Documento documento, CancellationToken ct = default) =>
        _colecao.InsertOneAsync(documento, cancellationToken: ct);

    public Task SubstituirAsync(Documento documento, CancellationToken ct = default) =>
        _colecao.ReplaceOneAsync(d => d.Id == documento.Id, documento, cancellationToken: ct);

    public async Task<bool> RemoverAsync(ObjectId id, CancellationToken ct = default) =>
        (await _colecao.DeleteOneAsync(d => d.Id == id, ct)).DeletedCount > 0;
}
