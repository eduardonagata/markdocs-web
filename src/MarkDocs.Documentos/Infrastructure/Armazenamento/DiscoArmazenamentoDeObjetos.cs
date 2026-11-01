using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Configuration;
using Microsoft.Extensions.Options;

namespace MarkDocs.Documentos.Infrastructure.Armazenamento;

/// <summary>
/// Os objetos numa pasta local. Para desenvolvimento sem MinIO
/// (<c>ARMAZENAMENTO_PROVEDOR=disco</c>) — e a prova de que o slice não
/// depende de nenhum provedor.
/// </summary>
public sealed class DiscoArmazenamentoDeObjetos(IOptions<DocumentosOptions> opcoes) : IArmazenamentoDeObjetos
{
    private readonly string _raiz = Path.GetFullPath(opcoes.Value.Armazenamento.Diretorio);

    public async Task GravarAsync(string chave, byte[] conteudo, string tipoDeConteudo, CancellationToken ct = default)
    {
        var caminho = Caminho(chave);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        await File.WriteAllBytesAsync(caminho, conteudo, ct);
    }

    public async Task<byte[]> LerAsync(string chave, CancellationToken ct = default)
    {
        var caminho = Caminho(chave);
        if (!File.Exists(caminho)) throw new ObjetoNaoEncontradoException(chave);
        return await File.ReadAllBytesAsync(caminho, ct);
    }

    public Task RemoverAsync(string chave, CancellationToken ct = default)
    {
        File.Delete(Caminho(chave));
        return Task.CompletedTask;
    }

    /// <summary>Uma chave nunca sai da raiz.</summary>
    private string Caminho(string chave)
    {
        var caminho = Path.GetFullPath(Path.Combine(_raiz, chave));
        if (!caminho.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"“{chave}” não é uma chave válida", nameof(chave));
        return caminho;
    }
}
