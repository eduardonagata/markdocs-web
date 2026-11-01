namespace MarkDocs.Documentos.Infrastructure.Armazenamento;

/// <summary>
/// O adaptador do armazenamento de objetos — os timbres, logotipos e imagens
/// dos documentos. A implementação pode ser MinIO, S3, Azure Blob Storage ou o
/// disco local; o resto do slice só conhece esta interface.
/// </summary>
/// <remarks>
/// <b>Objeto no bucket é imutável.</b> Uma imagem nova é um objeto novo, com
/// chave nova (<see cref="NovaChave"/>). É o que faz copiar uma referência
/// equivaler a copiar o arquivo, e o que impede que mudar um template altere
/// documentos já criados.
/// </remarks>
public interface IArmazenamentoDeObjetos
{
    Task GravarAsync(string chave, byte[] conteudo, string tipoDeConteudo, CancellationToken ct = default);

    /// <exception cref="Domain.Exceptions.ObjetoNaoEncontradoException">Se a chave não existe.</exception>
    Task<byte[]> LerAsync(string chave, CancellationToken ct = default);

    /// <summary>Remove o objeto. Uma chave que não existe não é erro.</summary>
    Task RemoverAsync(string chave, CancellationToken ct = default);

    /// <summary>Uma chave nova para um objeto novo: <c>ativos/&lt;guid&gt;.png</c>.</summary>
    static string NovaChave(string nome) =>
        $"ativos/{Guid.NewGuid():N}{Path.GetExtension(nome).ToLowerInvariant()}";
}
