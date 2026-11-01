using MarkDocs.Core.Conteudo;
using MarkDocs.Core.Nucleo;

namespace MarkDocs.Core;

/// <summary>
/// Uma família tipográfica embarcada pelo app.
/// </summary>
/// <remarks>
/// A família vem declarada junto porque sem ela a regra <i>"a fonte que falta
/// é detectada antes de compor"</i> não é verificável — e o Typst substituiria
/// a família em silêncio, produzindo um documento com a tipografia errada
/// <b>parecendo certo</b>.
/// </remarks>
public sealed record FonteEmbarcada(string Familia, byte[] Dados);

/// <summary>O PDF composto e os avisos do motor.</summary>
public sealed record Composicao(byte[] Pdf, IReadOnlyList<Diagnostico> Avisos);

/// <summary>
/// A interface do bloco, inteira: <b>duas entradas, um PDF</b>.
/// </summary>
/// <remarks>
/// O retorno vem paginado, com o texto selecionável. Não há canal paralelo de
/// metadados: o que tiver de aparecer no papel está escrito no Markdown, e o
/// que não está no Markdown não é renderizado.
///
/// Não tem estado mutável, e pode ser compartilhado entre threads (é
/// registrado como singleton pelo app).
/// </remarks>
public sealed class RenderizadorPDF
{
    private static readonly IReadOnlyDictionary<string, byte[]> SemImagens = new Dictionary<string, byte[]>();

    private readonly IReadOnlyList<FonteEmbarcada> _fontes;
    private readonly DateTimeOffset _instante;

    /// <param name="fontes">
    /// Os bytes das famílias que os documentos usam. O motor não enxerga as
    /// fontes do sistema; ele só usa o que entregamos.
    /// </param>
    /// <param name="instante">
    /// A resposta fixa de <c>today()</c>. O motor <b>exige</b> uma, e um valor
    /// fixo é o que faz o mesmo Markdown produzir sempre o mesmo PDF. Nenhum
    /// template deve usá-la. O padrão é 1970-01-01.
    /// </param>
    public RenderizadorPDF(IEnumerable<FonteEmbarcada> fontes, DateTimeOffset? instante = null)
    {
        _fontes = fontes.ToList();
        _instante = instante ?? DateTimeOffset.UnixEpoch;
    }

    /// <summary>As famílias que este renderizador tem para oferecer.</summary>
    public IReadOnlyList<string> FamiliasEmbarcadas =>
        _fontes.Select(f => f.Familia).Distinct().Order(StringComparer.Ordinal).ToList();

    public byte[] Pdf(string markdown, TemplateCarregado template) =>
        Compor(markdown, template).Pdf;

    /// <summary>
    /// O mesmo que <see cref="Pdf(string, TemplateCarregado)"/>, com os avisos do
    /// motor junto — é o que a tela usa para mostrar o estado degradado sem
    /// bloquear nada.
    /// </summary>
    public Composicao Compor(string markdown, TemplateCarregado template) =>
        Compor(markdown, template, SemImagens);

    /// <summary>
    /// Compõe com as imagens do documento. Ver <see cref="ImagensDoDocumento"/>:
    /// o Markdown cita <c>imagens/&lt;nome&gt;</c>, e só os nomes entregues aqui
    /// viram imagem no papel — os demais continuam sendo o texto alternativo.
    /// </summary>
    public byte[] Pdf(string markdown, TemplateCarregado template, IReadOnlyDictionary<string, byte[]> imagens) =>
        Compor(markdown, template, imagens).Pdf;

    /// <inheritdoc cref="Pdf(string, TemplateCarregado, IReadOnlyDictionary{string, byte[]})"/>
    public Composicao Compor(string markdown, TemplateCarregado template, IReadOnlyDictionary<string, byte[]> imagens)
    {
        // Antes de compor, não depois: uma família que falta é erro de
        // template, e o erro tem de chegar com o nome dela.
        var embarcadas = _fontes.Select(f => f.Familia).ToHashSet();
        foreach (var exigida in template.FontesExigidas)
        {
            if (!embarcadas.Contains(exigida))
                throw new ErroTypst.FonteNaoEmbarcada(exigida);
        }

        return MotorTypst.Compor(Acervo(markdown, template, imagens), _fontes, _instante);
    }

    /// <summary>
    /// O que o motor recebe, montado num lugar só. É <c>internal</c> para que os
    /// testes de integração confiram exatamente estes arquivos, e não uma
    /// remontagem paralela.
    /// </summary>
    internal Acervo Acervo(string markdown, TemplateCarregado template, IReadOnlyDictionary<string, byte[]>? imagens = null)
    {
        imagens ??= SemImagens;
        foreach (var nome in imagens.Keys)
        {
            if (!ImagensDoDocumento.NomeValido(nome))
                throw new ErroTypst.EntradaInvalida($"“{nome}” não é um nome de imagem válido");
        }

        return new Acervo(
            documento: Nucleo.Acervo.DocumentoTyp(ArvoreDeMarkdown.Converter(markdown, imagens.Keys.ToHashSet())),
            template: template.EmitirTyp(),
            @base: Nucleo.Acervo.Base(),
            ativos: template.Ativos,
            imagens: imagens);
    }
}
