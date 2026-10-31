using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AppTemplate.Contatos.Infrastructure.Tenancy;

/// <summary>
/// Implementacao padrao de <see cref="ITenantAccessor"/> que le a claim
/// <c>tid</c> do <see cref="ClaimsPrincipal"/> da requisicao corrente.
/// </summary>
public class HttpContextTenantAccessor : ITenantAccessor
{
    public const string TenantClaimType = "tid";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "Tentativa de resolver TenantId fora de uma requisicao HTTP.");

            var tenantId = httpContext.User.FindFirstValue(TenantClaimType);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                var presentClaims = string.Join(
                    ", ",
                    httpContext.User.Claims.Select(c => $"\"{c.Type}\"").DefaultIfEmpty("(nenhuma)"));

                throw new InvalidOperationException(
                    $"JWT autenticado nao carrega a claim \"{TenantClaimType}\". " +
                    $"Claims presentes: {presentClaims}. " +
                    "Se a claim existe com nome mapeado (ex: schemas.microsoft.com/...), " +
                    "confirme que o JwtBearer esta com MapInboundClaims = false.");
            }

            return tenantId;
        }
    }
}
