using AppTemplate.Contatos.Domain.Entities;
using AppTemplate.Contatos.Domain.Exceptions;
using AppTemplate.Contatos.Infrastructure.Persistence.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace AppTemplate.Contatos.Application.EntidadesExternas.Queries;

public record ObterEntidadeExternaPorIdQuery(ObjectId Id) : IRequest<EntidadeExterna>;

public class ObterEntidadeExternaPorIdQueryHandler
    : IRequestHandler<ObterEntidadeExternaPorIdQuery, EntidadeExterna>
{
    private readonly IEntidadeExternaRepository _repository;

    public ObterEntidadeExternaPorIdQueryHandler(IEntidadeExternaRepository repository)
        => _repository = repository;

    public async Task<EntidadeExterna> Handle(
        ObterEntidadeExternaPorIdQuery request,
        CancellationToken ct)
        => await _repository.ObterPorIdAsync(request.Id, ct)
           ?? throw new EntidadeExternaNotFoundException(request.Id.ToString());
}
