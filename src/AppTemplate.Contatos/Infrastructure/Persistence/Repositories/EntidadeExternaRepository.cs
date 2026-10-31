using AppTemplate.Contatos.Domain.Entities;
using AppTemplate.Contatos.Infrastructure.Persistence.Interfaces;
using AppTemplate.Contatos.Infrastructure.Persistence.Transactions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AppTemplate.Contatos.Infrastructure.Persistence.Repositories;

public class EntidadeExternaRepository : IEntidadeExternaRepository
{
    private const string CollectionName = "contatos.entidades_externas";

    private readonly IMongoCollection<EntidadeExterna> _collection;
    private readonly IMongoSessionAccessor _sessionAccessor;

    public EntidadeExternaRepository(IMongoDatabase database, IMongoSessionAccessor sessionAccessor)
    {
        _collection = database.GetCollection<EntidadeExterna>(CollectionName);
        _sessionAccessor = sessionAccessor;
    }

    public async Task<EntidadeExterna?> ObterPorIdAsync(ObjectId id, CancellationToken ct = default)
    {
        var filter = Builders<EntidadeExterna>.Filter.Eq(x => x.Id, id);
        return _sessionAccessor.Session is { } session
            ? await _collection.Find(session, filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<List<EntidadeExterna>> ListarAsync(CancellationToken ct = default)
    {
        var filter = Builders<EntidadeExterna>.Filter.Empty;
        var find = _sessionAccessor.Session is { } session
            ? _collection.Find(session, filter)
            : _collection.Find(filter);
        return await find.SortBy(x => x.Nome).ToListAsync(ct);
    }

    public async Task CriarAsync(EntidadeExterna entidade, CancellationToken ct = default)
    {
        if (_sessionAccessor.Session is { } session)
            await _collection.InsertOneAsync(session, entidade, cancellationToken: ct);
        else
            await _collection.InsertOneAsync(entidade, cancellationToken: ct);
    }
}
