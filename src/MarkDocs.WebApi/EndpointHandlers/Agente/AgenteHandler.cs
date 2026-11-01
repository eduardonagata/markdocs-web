using MarkDocs.Documentos.Application.Queries;
using MarkDocs.Documentos.Hosting;
using MediatR;

namespace MarkDocs.WebApi.EndpointHandlers.Agente;

/// <summary>
/// <c>/api/agente</c>: o assistente, em AG-UI — e o <c>status</c>, que a tela
/// consulta antes de abrir a conversa.
/// </summary>
/// <remarks>
/// O endpoint AG-UI é montado pelo slice (<c>MapAgente</c>), que esconde o
/// Agent Framework desta camada. Sem autenticação na borda, como o resto da
/// API, por ora; quando ela entrar, entra aqui também (Docs/decisoes-arquitetura/EI-001).
/// </remarks>
public class AgenteHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapAgente("/api/agente").WithTags("Assistente");

        app.MapGet("/api/agente/status", StatusAsync)
            .WithTags("Assistente")
            .Produces<StatusDoAgenteResponse>();
    }

    internal static async Task<IResult> StatusAsync(IMediator mediator, CancellationToken ct)
    {
        var status = await mediator.Send(new ObterStatusDoAgenteQuery(), ct);
        return Results.Ok(new StatusDoAgenteResponse(status.Disponivel, status.Provedor, status.Modelo, status.OQueFalta));
    }
}

public sealed record StatusDoAgenteResponse(bool Disponivel, string Provedor, string? Modelo, string? OQueFalta);
