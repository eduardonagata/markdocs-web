using EphemeralMongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MarkDocs.WebApi.IntegrationTests;

/// <summary>
/// O app inteiro, pelo Host de verdade: MongoDB efêmero (ou <c>MONGODB_TEST_URI</c>)
/// e o armazenamento de objetos em disco, numa pasta temporária.
/// </summary>
public class AppFactory : WebApplicationFactory<Program>
{
    private readonly IMongoRunner? _runner;
    private readonly string _uri;
    private readonly string _objetos = Path.Combine(Path.GetTempPath(), $"markdocs-objetos-{Guid.NewGuid():N}");

    public AppFactory()
    {
        var uri = Environment.GetEnvironmentVariable("MONGODB_TEST_URI");
        if (string.IsNullOrWhiteSpace(uri))
        {
            _runner = MongoRunner.Run(new MongoRunnerOptions { AdditionalArguments = "--quiet" });
            uri = _runner.ConnectionString;
        }
        _uri = uri;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DOCUMENTOS_CONNECTION_STRING", _uri);
        builder.UseSetting("DOCUMENTOS_DATABASE_NAME", $"teste_api_{Guid.NewGuid():N}");
        builder.UseSetting("ARMAZENAMENTO_PROVEDOR", "disco");
        builder.UseSetting("ARMAZENAMENTO_DIRETORIO", _objetos);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _runner?.Dispose();
        if (Directory.Exists(_objetos)) Directory.Delete(_objetos, recursive: true);
    }
}
