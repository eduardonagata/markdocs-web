using MongoDB.Driver;

namespace AppTemplate.Contatos.Tests;

/// <summary>
/// Fornece um IMongoDatabase isolado por execucao de teste.
/// Usa MONGODB_TEST_URI se definida, senao cai em localhost replica set.
/// Cada fixture cria um database efemero que e dropado no dispose.
/// </summary>
public class MongoFixture : IAsyncLifetime
{
    private MongoClient? _client;

    public IMongoClient Client => _client
        ?? throw new InvalidOperationException("Fixture nao inicializada.");
    public IMongoDatabase Database { get; private set; } = default!;
    public bool IsAvailable { get; private set; }

    public Task InitializeAsync()
    {
        var uri = Environment.GetEnvironmentVariable("MONGODB_TEST_URI")
                  ?? "mongodb://localhost:27017/?replicaSet=rs0";
        try
        {
            _client = new MongoClient(uri);
            Database = _client.GetDatabase($"test_contatos_{Guid.NewGuid():N}");

            // Ping para detectar se ha Mongo disponivel; nao falha construcao do fixture
            _client.ListDatabaseNames(new ListDatabaseNamesOptions());
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_client is not null && IsAvailable)
        {
            try
            {
                await _client.DropDatabaseAsync(Database.DatabaseNamespace.DatabaseName);
            }
            catch { }
        }
    }
}

[CollectionDefinition("Mongo")]
public class MongoCollection : ICollectionFixture<MongoFixture> { }
