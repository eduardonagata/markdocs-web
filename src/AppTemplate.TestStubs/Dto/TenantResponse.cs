namespace AppTemplate.TestStubs.Dto;

/// <summary>
/// Resposta do endpoint stub <c>GET /api/tenants?email=...</c>.
/// </summary>
public record TenantResponse
{
    public required string TenantId { get; init; }
    public string? Name { get; init; }
}
