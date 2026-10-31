namespace AppTemplate.TestStubs.Dto;

/// <summary>
/// Resposta do endpoint stub <c>GET /api/me</c>.
/// Reflete o shape que o frontend (src/shell/session/types.ts) espera.
/// </summary>
public record SessionContextResponse
{
    public required string TenantId { get; init; }
    public required string UserId { get; init; }
    public required string SubscriptionStatus { get; init; }
    public required bool AdmLocatario { get; init; }
    public required string[] Scope { get; init; }
}
