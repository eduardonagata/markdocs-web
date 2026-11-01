using EphemeralMongo;
using MarkDocs.Abstractions;
using MongoDB.Driver;

namespace MarkDocs.Documentos.Tests;

/// <summary>
/// Um MongoDB de verdade para os testes: <c>MONGODB_TEST_URI</c> quando
/// definida; senão um <c>mongod</c> efêmero, que vem dentro do pacote
/// EphemeralMongo e morre com a execução. Cada teste pede um database próprio.
/// </summary>
public sealed class MongoFixture : IDisposable
{
    private readonly IMongoRunner? _runner;

    public MongoFixture()
    {
        SerializacaoDaFormatacao.RegistrarBson();

        var uri = Environment.GetEnvironmentVariable("MONGODB_TEST_URI");
        if (string.IsNullOrWhiteSpace(uri))
        {
            _runner = MongoRunner.Run(new MongoRunnerOptions { AdditionalArguments = "--quiet" });
            uri = _runner.ConnectionString;
        }
        Client = new MongoClient(uri);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase NovoDatabase() => Client.GetDatabase($"teste_documentos_{Guid.NewGuid():N}");

    public void Dispose() => _runner?.Dispose();
}

[CollectionDefinition("Mongo")]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>;
