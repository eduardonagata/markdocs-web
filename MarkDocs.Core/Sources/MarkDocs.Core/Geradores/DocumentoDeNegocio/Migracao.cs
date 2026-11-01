using System.Text.Json;

namespace MarkDocs.Core.Geradores.DocumentoDeNegocio;

/// <summary>
/// A migração do eixo <b>vertical</b>: campos dentro do mesmo esquema,
/// migrados na leitura.
/// </summary>
/// <remarks>
/// A confusão a evitar: <i>"preciso guardar o gerador versão 1 para abrir um
/// arquivo de esquema 1"</i>. <b>Não.</b> O esquema 1 é migrado para o atual na
/// leitura, e o gerador atual emite. O que se retém é código de migração de
/// JSON, não geradores antigos.
///
/// A política é a de sempre: <b>aditivo é o normal.</b> Campo novo com valor
/// padrão não sobe <c>esquema</c>; renomear ou remover um campo sobe.
/// </remarks>
internal static class MigracaoDeNegocio
{
    /// <summary>
    /// Lê o <c>esquema</c> declarado sem decodificar o resto — é o primeiro
    /// campo lido, e a decisão de compatibilidade acontece antes de qualquer
    /// leitura de conteúdo.
    /// </summary>
    public static int EsquemaDeclarado(byte[] json)
    {
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind == JsonValueKind.Object
                && documento.RootElement.TryGetProperty("esquema", out var esquema)
                && esquema.ValueKind == JsonValueKind.Number
                && esquema.TryGetInt32(out var numero))
                return numero;
        }
        catch (JsonException)
        {
            // Cai no erro abaixo: sem esquema legível, não há o que decidir.
        }
        throw new ErroTypst.TemplateInvalido("esquema", "o template não declara a versão do esquema");
    }

    /// <summary>
    /// Migra do <c>esquema</c> declarado até <c>esquemaAtual</c>. Hoje há uma
    /// versão só, e a função é a identidade. A estrutura entra agora porque
    /// acrescentá-la depois obriga a adivinhar o que os arquivos já gravados
    /// queriam dizer.
    /// </summary>
    public static byte[] Migrar(byte[] json, int origem, int destino)
    {
        if (origem > destino)
            throw new ErroTypst.EsquemaFuturo(DocumentoDeNegocio.Familia, origem, destino);

        var atual = json;
        for (var versao = origem; versao < destino; versao++)
            atual = Passo(atual, versao);
        return atual;
    }

    private static byte[] Passo(byte[] json, int versao) => versao switch
    {
        // 1 => De1Para2(json),
        _ => throw new ErroTypst.TemplateInvalido(
            "esquema", $"não há migração do esquema {versao} para o {versao + 1}"),
    };
}
