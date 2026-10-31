using AppTemplate.TestStubs.Dto;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AppTemplate.TestStubs;

/// <summary>
/// Endpoints stub usados apenas para desenvolvimento e teste do frontend.
///
/// Provê três endpoints:
///   POST /api/dev/login        emite JWT via <see cref="DevTokenIssuer"/>.
///   GET  /api/me               contexto de sessão (hardcoded).
///   GET  /api/tenants?email=…  resolução de tenant no fluxo de login.
///
/// Os dois últimos continuam <c>AllowAnonymous</c> — o primeiro emite o JWT
/// que permite consumir os endpoints reais que têm <c>.RequireAuthorization()</c>.
/// </summary>
public static class TestStubsEndpointExtensions
{
    public static IEndpointRouteBuilder MapTestStubsEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/dev/login — emite JWT. Aceita qualquer email/senha.
        app.MapPost("/api/dev/login", (DevLoginRequest request, DevTokenIssuer issuer) =>
        {
            var (token, tenantId) = issuer.Issue(request.Email);
            return Results.Ok(new DevLoginResponse
            {
                Token = token,
                TenantId = tenantId,
            });
        })
        .AllowAnonymous()
        .Produces<DevLoginResponse>(StatusCodes.Status200OK);

        // GET /api/me — contexto de sessão com escopos totais.
        app.MapGet("/api/me", (DevJwtOptions options) => Results.Ok(new SessionContextResponse
        {
            TenantId = options.TenantId,
            UserId = DevTokenIssuer.DevUserId,
            SubscriptionStatus = "active",
            AdmLocatario = true,
            Scope = new[] { "ex:all" },
        }))
        .AllowAnonymous()
        .Produces<SessionContextResponse>(StatusCodes.Status200OK);

        // GET /api/tenants?email=... — retorna o tenant "dev" único.
        app.MapGet("/api/tenants", (string? email, DevJwtOptions options) => Results.Ok(new[]
        {
            new TenantResponse { TenantId = options.TenantId, Name = "Dev" },
        }))
        .AllowAnonymous()
        .Produces<TenantResponse[]>(StatusCodes.Status200OK);

        return app;
    }
}
