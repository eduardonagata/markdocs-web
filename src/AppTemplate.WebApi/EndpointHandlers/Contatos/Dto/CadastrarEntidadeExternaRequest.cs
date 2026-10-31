using AppTemplate.Contatos.Domain.Enums;

namespace AppTemplate.WebApi.EndpointHandlers.Contatos.Dto;

public record CadastrarEntidadeExternaRequest
{
    public required string Nome { get; init; }
    public required IReadOnlyList<PapelEntidadeExterna> Papeis { get; init; }
}
