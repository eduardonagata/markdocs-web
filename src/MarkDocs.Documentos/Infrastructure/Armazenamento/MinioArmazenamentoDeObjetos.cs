using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace MarkDocs.Documentos.Infrastructure.Armazenamento;

/// <summary>
/// O armazenamento no MinIO (ou em qualquer serviço compatível com a API do
/// S3). O bucket é criado no primeiro uso, se não existir.
/// </summary>
public sealed class MinioArmazenamentoDeObjetos : IArmazenamentoDeObjetos, IDisposable
{
    private readonly IMinioClient _cliente;
    private readonly string _bucket;
    private readonly ILogger<MinioArmazenamentoDeObjetos> _logger;
    private readonly SemaphoreSlim _trava = new(1, 1);
    private bool _bucketConferido;

    public MinioArmazenamentoDeObjetos(IOptions<DocumentosOptions> opcoes, ILogger<MinioArmazenamentoDeObjetos> logger)
    {
        var minio = opcoes.Value.Armazenamento.Minio;
        var cliente = new MinioClient()
            .WithEndpoint(minio.Endpoint)
            .WithCredentials(minio.AccessKey, minio.SecretKey)
            .WithSSL(minio.UsarSsl);
        if (!string.IsNullOrWhiteSpace(minio.Regiao)) cliente = cliente.WithRegion(minio.Regiao);

        _cliente = cliente.Build();
        _bucket = minio.Bucket;
        _logger = logger;
    }

    public async Task GravarAsync(string chave, byte[] conteudo, string tipoDeConteudo, CancellationToken ct = default)
    {
        await GarantirBucketAsync(ct);
        using var fluxo = new MemoryStream(conteudo, writable: false);
        await _cliente.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(chave)
            .WithStreamData(fluxo)
            .WithObjectSize(conteudo.LongLength)
            .WithContentType(tipoDeConteudo), ct);
    }

    public async Task<byte[]> LerAsync(string chave, CancellationToken ct = default)
    {
        await GarantirBucketAsync(ct);
        using var destino = new MemoryStream();
        try
        {
            await _cliente.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(chave)
                .WithCallbackStream((fluxo, token) => fluxo.CopyToAsync(destino, token)), ct);
        }
        catch (ObjectNotFoundException)
        {
            throw new ObjetoNaoEncontradoException(chave);
        }
        return destino.ToArray();
    }

    public async Task RemoverAsync(string chave, CancellationToken ct = default)
    {
        await GarantirBucketAsync(ct);
        await _cliente.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(chave), ct);
    }

    private async Task GarantirBucketAsync(CancellationToken ct)
    {
        if (_bucketConferido) return;
        await _trava.WaitAsync(ct);
        try
        {
            if (_bucketConferido) return;
            if (!await _cliente.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), ct))
            {
                await _cliente.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
                _logger.LogInformation("Bucket {Bucket} criado no armazenamento de objetos", _bucket);
            }
            _bucketConferido = true;
        }
        finally
        {
            _trava.Release();
        }
    }

    public void Dispose()
    {
        _trava.Dispose();
        (_cliente as IDisposable)?.Dispose();
    }
}
