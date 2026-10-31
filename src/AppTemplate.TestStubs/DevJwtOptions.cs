using Microsoft.Extensions.Configuration;

namespace AppTemplate.TestStubs;

/// <summary>
/// Opções do JWT emitido pelo endpoint <c>POST /api/dev/login</c>
/// e validado pelo pipeline de autenticação em desenvolvimento.
///
/// Em produção, o JWT é emitido pelo IdP real (Firebase/Entra ID) e
/// validado via <c>JWT_AUTHORITY</c> — este tipo não é usado.
/// </summary>
public class DevJwtOptions
{
    public const string MinimumSigningKeyBytes = "32"; // HS256 exige >= 32 bytes.

    /// <summary>
    /// Chave simétrica HS256 (mínimo 32 bytes). Se ausente, o template
    /// gera uma chave volátil por processo e emite aviso — basta reiniciar
    /// para invalidar todos os JWTs.
    /// </summary>
    public required string SigningKey { get; init; }

    /// <summary>TenantId gravado como claim <c>tid</c> em todo JWT emitido.</summary>
    public string TenantId { get; init; } = "dev";

    public string Issuer { get; init; } = "apptemplate-dev";

    public string Audience { get; init; } = "apptemplate-api";

    public int ExpirationHours { get; init; } = 24;

    /// <summary>
    /// Le as opcoes do <see cref="IConfiguration"/> (env vars + appsettings).
    /// Variaveis reconhecidas:
    ///   DEV_JWT_SIGNING_KEY     — chave HS256 (>= 32 bytes).
    ///   DEV_JWT_TENANT_ID       — default "dev".
    ///   DEV_JWT_ISSUER          — default "apptemplate-dev".
    ///   DEV_JWT_AUDIENCE        — default "apptemplate-api".
    ///   DEV_JWT_EXPIRATION_HOURS — default 24.
    /// </summary>
    public static DevJwtOptions FromConfiguration(IConfiguration config)
    {
        var signingKey = config["DEV_JWT_SIGNING_KEY"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            // Chave efêmera para dev quando a env var não foi definida.
            // Reiniciar o processo invalida todos os JWTs emitidos antes.
            signingKey = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // 64 chars
            Console.WriteLine(
                "[TestStubs] DEV_JWT_SIGNING_KEY ausente ou com menos de 32 bytes. " +
                "Gerando chave volátil para esta execução — todos os JWTs emitidos " +
                "serão invalidados ao reiniciar o processo.");
        }

        return new DevJwtOptions
        {
            SigningKey = signingKey,
            TenantId = config["DEV_JWT_TENANT_ID"] ?? "dev",
            Issuer = config["DEV_JWT_ISSUER"] ?? "apptemplate-dev",
            Audience = config["DEV_JWT_AUDIENCE"] ?? "apptemplate-api",
            ExpirationHours = int.TryParse(config["DEV_JWT_EXPIRATION_HOURS"], out var h) ? h : 24,
        };
    }
}
