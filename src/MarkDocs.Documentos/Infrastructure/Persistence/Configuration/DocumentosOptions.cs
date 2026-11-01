namespace MarkDocs.Documentos.Infrastructure.Persistence.Configuration;

/// <summary>
/// A configuração do slice, lida das variáveis de ambiente em
/// <c>AddDocumentosModule</c> (ver o README, "Variáveis de ambiente").
/// </summary>
public sealed class DocumentosOptions
{
    /// <summary><c>DOCUMENTOS_CONNECTION_STRING</c>.</summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary><c>DOCUMENTOS_DATABASE_NAME</c>. O app é single-tenant: um database só.</summary>
    public string DatabaseName { get; set; } = "markdocs";

    public ArmazenamentoOptions Armazenamento { get; set; } = new();
}

public sealed class ArmazenamentoOptions
{
    /// <summary><c>ARMAZENAMENTO_PROVEDOR</c>: <c>minio</c> (padrão) ou <c>disco</c>.</summary>
    public string Provedor { get; set; } = "minio";

    /// <summary><c>ARMAZENAMENTO_DIRETORIO</c>: a raiz, quando o provedor é <c>disco</c>.</summary>
    public string Diretorio { get; set; } = "dados/objetos";

    public MinioOptions Minio { get; set; } = new();
}

public sealed class MinioOptions
{
    /// <summary><c>MINIO_ENDPOINT</c>: <c>host:porta</c>, sem esquema.</summary>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary><c>MINIO_ACCESS_KEY</c>.</summary>
    public string AccessKey { get; set; } = "";

    /// <summary><c>MINIO_SECRET_KEY</c>.</summary>
    public string SecretKey { get; set; } = "";

    /// <summary><c>MINIO_BUCKET</c>. Criado no primeiro uso, se não existir.</summary>
    public string Bucket { get; set; } = "markdocs";

    /// <summary><c>MINIO_USAR_SSL</c>.</summary>
    public bool UsarSsl { get; set; }

    /// <summary><c>MINIO_REGIAO</c>. Opcional; só S3 e alguns provedores exigem.</summary>
    public string? Regiao { get; set; }
}
