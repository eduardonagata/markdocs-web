using AppTemplate.Contatos.Domain.Entities;
using MongoDB.Bson;

namespace AppTemplate.Contatos.Infrastructure.Persistence.Interfaces;

public interface IEntidadeExternaRepository
{
    Task<EntidadeExterna?> ObterPorIdAsync(ObjectId id, CancellationToken ct = default);
    Task<List<EntidadeExterna>> ListarAsync(CancellationToken ct = default);
    Task CriarAsync(EntidadeExterna entidade, CancellationToken ct = default);
}
