namespace AppTemplate.TestStubs.Dto;

/// <summary>
/// Resposta do endpoint <c>POST /api/dev/login</c>: o JWT emitido e o
/// tenantId que ficou gravado como claim <c>tid</c>.
/// </summary>
public record DevLoginResponse
{
    public required string Token { get; init; }
    public required string TenantId { get; init; }
}
