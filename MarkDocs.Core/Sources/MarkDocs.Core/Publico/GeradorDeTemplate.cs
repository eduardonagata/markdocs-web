namespace MarkDocs.Core;

/// <summary>
/// Uma família de template: um esquema próprio, um gerador, migrações próprias.
/// </summary>
/// <remarks>
/// O contrato de saída é um só: emitir uma <c>string</c> de Typst que define
/// <c>estilo(body)</c>. Tudo o mais é da família. Os membros são estáticos,
/// como no protocolo Swift: uma família é um tipo, não uma instância.
/// </remarks>
public interface IGeradorDeTemplate
{
    /// <summary>Identidade da família. É o que vai no <c>template.json</c>.</summary>
    static abstract string Familia { get; }

    /// <summary>A versão de esquema que este gerador escreve hoje.</summary>
    static abstract int EsquemaAtual { get; }

    /// <summary>
    /// Lê, migra do <c>esquema</c> declarado até <see cref="EsquemaAtual"/>, e
    /// devolve a forma apagada. Lança se o esquema for maior que o atual.
    /// </summary>
    static abstract TemplateCarregado Carregar(byte[] json, IReadOnlyDictionary<string, byte[]> ativos);
}
