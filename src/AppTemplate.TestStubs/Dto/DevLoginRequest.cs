namespace AppTemplate.TestStubs.Dto;

/// <summary>
/// Payload do endpoint <c>POST /api/dev/login</c>. Qualquer combinacao
/// email/senha e aceita — o email e gravado no claim do JWT apenas para
/// identificacao em logs.
/// </summary>
public record DevLoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
