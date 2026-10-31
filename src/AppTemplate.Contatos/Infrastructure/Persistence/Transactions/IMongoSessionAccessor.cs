using MongoDB.Driver;

namespace AppTemplate.Contatos.Infrastructure.Persistence.Transactions;

/// <summary>
/// Fornece acesso a uma sessao MongoDB ambient no escopo da requisicao.
/// Usada por repositories para propagar transacoes sem passar a sessao como parametro.
/// </summary>
public interface IMongoSessionAccessor
{
    IClientSessionHandle? Session { get; }
    void Set(IClientSessionHandle session);
    void Clear();
}
