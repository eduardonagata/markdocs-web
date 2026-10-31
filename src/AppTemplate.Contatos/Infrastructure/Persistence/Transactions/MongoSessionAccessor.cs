using MongoDB.Driver;

namespace AppTemplate.Contatos.Infrastructure.Persistence.Transactions;

public class MongoSessionAccessor : IMongoSessionAccessor
{
    public IClientSessionHandle? Session { get; private set; }

    public void Set(IClientSessionHandle session) => Session = session;

    public void Clear() => Session = null;
}
