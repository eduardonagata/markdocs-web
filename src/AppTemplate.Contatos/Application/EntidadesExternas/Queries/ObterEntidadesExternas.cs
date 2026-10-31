using AppTemplate.Contatos.Domain.Entities;
using AppTemplate.Contatos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace AppTemplate.Contatos.Application.EntidadesExternas.Queries;

public record ObterEntidadesExternasQuery : IRequest<List<EntidadeExterna>>;

public class ObterEntidadesExternasQueryHandler
    : IRequestHandler<ObterEntidadesExternasQuery, List<EntidadeExterna>>
{
    private readonly IEntidadeExternaRepository _repository;

    public ObterEntidadesExternasQueryHandler(IEntidadeExternaRepository repository)
        => _repository = repository;

    public Task<List<EntidadeExterna>> Handle(
        ObterEntidadesExternasQuery request,
        CancellationToken ct)
        => _repository.ListarAsync(ct);
}
