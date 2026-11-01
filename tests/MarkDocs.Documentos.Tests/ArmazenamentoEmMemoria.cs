using System.Collections.Concurrent;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Armazenamento;

namespace MarkDocs.Documentos.Tests;

/// <summary>O bucket, num dicionário. É o único dublê dos testes: o banco é real.</summary>
public sealed class ArmazenamentoEmMemoria : IArmazenamentoDeObjetos
{
    public ConcurrentDictionary<string, byte[]> Objetos { get; } = new();

    public Task GravarAsync(string chave, byte[] conteudo, string tipoDeConteudo, CancellationToken ct = default)
    {
        Objetos[chave] = conteudo;
        return Task.CompletedTask;
    }

    public Task<byte[]> LerAsync(string chave, CancellationToken ct = default) =>
        Objetos.TryGetValue(chave, out var conteudo)
            ? Task.FromResult(conteudo)
            : throw new ObjetoNaoEncontradoException(chave);

    public Task RemoverAsync(string chave, CancellationToken ct = default)
    {
        Objetos.TryRemove(chave, out _);
        return Task.CompletedTask;
    }
}
