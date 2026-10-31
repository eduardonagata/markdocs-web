using AppTemplate.Contatos.Domain.Enums;

namespace AppTemplate.WebApi.EndpointHandlers.Contatos.Dto;

public record EntidadeExternaResponse
{
    public required string Id { get; init; }
    public required string Nome { get; init; }
    public required IReadOnlyList<PapelEntidadeExterna> Papeis { get; init; }
    public required DateTimeOffset CriadoEm { get; init; }
}
