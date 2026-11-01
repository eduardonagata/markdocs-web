using MarkDocs.Documentos.Application.Agente;
using MarkDocs.Documentos.Infrastructure.Agente;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarkDocs.Documentos.Hosting;

/// <summary>
/// Esconde o <c>MapAGUIServer</c> do <c>MarkDocs.WebApi</c>: a camada HTTP
/// chama <c>app.MapAgente("/api/agente")</c> sem referenciar o Agent
/// Framework, que fica confinado a este slice (o mesmo arranjo do
/// <c>MapNotiqAgentChat</c> no servico-dfe).
/// </summary>
public static class AgenteEndpointRouteBuilderExtensions
{
    /// <summary>
    /// O endpoint AG-UI do assistente. Sem configuração (<c>AGENTE_*</c>), o
    /// mesmo caminho responde <c>503</c> dizendo o que falta, em vez de o app
    /// não subir.
    /// </summary>
    public static IEndpointConventionBuilder MapAgente(this IEndpointRouteBuilder endpoints, string pattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var agente = endpoints.ServiceProvider.GetKeyedService<AIAgent>(Assistente.Nome);
        if (agente is null)
        {
            var oQueFalta = endpoints.ServiceProvider.GetRequiredService<IOptions<AgenteOptions>>().Value.OQueFalta;
            return endpoints.MapPost(pattern, () => Results.Problem(
                title: "O assistente não está configurado",
                detail: oQueFalta,
                statusCode: StatusCodes.Status503ServiceUnavailable));
        }

        return endpoints.MapAGUIServer(pattern, agente);
    }
}
