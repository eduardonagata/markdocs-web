namespace AppTemplate.Contatos.Infrastructure.Tenancy;

/// <summary>
/// Fornece o <c>tenantId</c> do ator responsavel pela requisicao atual.
/// Scoped por request — resolve lendo a claim <c>tid</c> do JWT autenticado.
///
/// Usado pela DI do slice para escolher qual database MongoDB abrir no
/// padrao <c>tid-{tenantId}</c>.
/// </summary>
public interface ITenantAccessor
{
    /// <summary>
    /// TenantId da requisicao corrente. Lanca <see cref="InvalidOperationException"/>
    /// se nao ha <c>HttpContext</c> ou se o JWT nao traz a claim <c>tid</c>.
    /// </summary>
    string TenantId { get; }
}
