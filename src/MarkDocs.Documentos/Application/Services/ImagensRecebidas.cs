using System.Globalization;
using System.Text;
using MarkDocs.Core;

namespace MarkDocs.Documentos.Application.Services;

/// <summary>
/// As regras de uma imagem enviada: que formatos o motor aceita, o tamanho
/// máximo, e o nome que o Markdown vai citar.
/// </summary>
public static class ImagensRecebidas
{
    public const int TamanhoMaximo = 10 * 1024 * 1024;

    /// <summary>Os formatos que o Typst lê, pela extensão.</summary>
    public static readonly IReadOnlyDictionary<string, string> TiposPorExtensao = new Dictionary<string, string>
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
    };

    public static string TipoDeConteudo(string nome) =>
        TiposPorExtensao.GetValueOrDefault(Path.GetExtension(nome).ToLowerInvariant(), "application/octet-stream");

    /// <summary>
    /// O nome do arquivo enviado, reduzido ao que <see cref="ImagensDoDocumento.NomeValido"/>
    /// aceita: sem acento, sem espaço, sem pasta. <c>Planta Baixa (1).PNG</c> →
    /// <c>planta-baixa-1.png</c>.
    /// </summary>
    public static string NomeSeguro(string original)
    {
        var nome = Path.GetFileName(original.Replace('\\', '/'));
        var extensao = Path.GetExtension(nome).ToLowerInvariant();
        var base_ = Path.GetFileNameWithoutExtension(nome).Normalize(NormalizationForm.FormD);

        var limpo = new StringBuilder();
        foreach (var c in base_)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c)) limpo.Append(char.ToLowerInvariant(c));
            else if (c is '-' or '_') limpo.Append(c);
            else if (limpo.Length > 0 && limpo[^1] != '-') limpo.Append('-');
        }
        var semBordas = limpo.ToString().Trim('-', '_');
        return (semBordas.Length == 0 ? "imagem" : semBordas) + extensao;
    }

    /// <exception cref="ArgumentException">Se o formato não é aceito, ou o arquivo é vazio ou grande demais.</exception>
    public static void Validar(string nome, byte[] conteudo)
    {
        if (!TiposPorExtensao.ContainsKey(Path.GetExtension(nome).ToLowerInvariant()))
            throw new ArgumentException(
                $"“{nome}” não é uma imagem aceita. Formatos: {string.Join(", ", TiposPorExtensao.Keys)}.");
        if (conteudo.Length == 0)
            throw new ArgumentException("A imagem está vazia.");
        if (conteudo.Length > TamanhoMaximo)
            throw new ArgumentException($"A imagem passa de {TamanhoMaximo / (1024 * 1024)} MB.");
        if (!ImagensDoDocumento.NomeValido(nome))
            throw new ArgumentException($"“{nome}” não é um nome de imagem válido.");
    }
}
