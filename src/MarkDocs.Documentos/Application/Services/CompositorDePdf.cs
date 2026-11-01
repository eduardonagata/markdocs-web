using MarkDocs.Abstractions;
using MarkDocs.Core;
using MarkDocs.Documentos.Infrastructure.Armazenamento;

namespace MarkDocs.Documentos.Application.Services;

/// <summary>
/// A ponte entre um <see cref="IDocumento"/> e o <c>MarkDocs.Core</c>: monta as
/// entradas que o pacote recebe — a família, o <c>template.json</c>, os ativos
/// e o Markdown — e devolve o PDF ou os eixos.
/// </summary>
/// <remarks>
/// Qualquer classe que implemente <see cref="IDocumento"/> é composta assim,
/// venha de qualquer coleção. O que não está no <c>Corpo</c> não vai ao papel;
/// a única exceção é o <c>Tipo</c>, que a moldura pode imprimir.
/// </remarks>
public sealed class CompositorDePdf(RenderizadorPDF renderizador, IArmazenamentoDeObjetos armazenamento)
{
    public async Task<Composicao> ComporAsync(IDocumento documento, CancellationToken ct = default)
    {
        var ativosDoTemplate = await BaixarAsync(documento.Ativos.Where(a => a.Papel == PapelDoAtivo.Template), ct);
        var imagens = await BaixarAsync(documento.Ativos.Where(a => a.Papel == PapelDoAtivo.Imagem), ct);
        return renderizador.Compor(documento.Corpo, Carregar(documento, ativosDoTemplate), imagens);
    }

    /// <summary>
    /// Os controles de formatação do documento, com domínio, limites e o valor
    /// atual. Os ativos não são baixados: a carga só confere que os nomes que o
    /// template cita estão presentes.
    /// </summary>
    public IReadOnlyList<EixoDeFormatacao> Eixos(IDocumento documento) =>
        Carregar(documento, documento.Ativos
            .Where(a => a.Papel == PapelDoAtivo.Template)
            .DistinctBy(a => a.Nome)
            .ToDictionary(a => a.Nome, _ => Array.Empty<byte>())).Eixos;

    private static TemplateCarregado Carregar(IDocumento documento, IReadOnlyDictionary<string, byte[]> ativos) =>
        RegistroDeGeradores.Carregar(
            documento.Familia,
            documento.Formatacao.ParaTemplateJson(documento.Titulo, documento.Tipo),
            ativos);

    /// <summary>Nome → bytes, como o Core recebe.</summary>
    private async Task<IReadOnlyDictionary<string, byte[]>> BaixarAsync(IEnumerable<ReferenciaDeAtivo> ativos, CancellationToken ct)
    {
        var baixados = new Dictionary<string, byte[]>();
        foreach (var ativo in ativos.DistinctBy(a => a.Nome))
            baixados[ativo.Nome] = await armazenamento.LerAsync(ativo.Id, ct);
        return baixados;
    }
}
