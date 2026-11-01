using System.Text.Json;
using MarkDocs.Abstractions.Formatacoes;
using MarkDocs.Documentos.Domain.Entities;
using MongoDB.Bson;

namespace MarkDocs.Documentos.Infrastructure.TemplatesDeFabrica;

/// <summary>
/// Contrato, Orçamento e Recibo: os <c>template.json</c> de fábrica, os mesmos
/// do app Swift, lidos com <see cref="FormatacaoDocumentoDeNegocio.DeTemplateJson"/>.
/// </summary>
public static class CatalogoDeFabrica
{
    public static readonly IReadOnlyList<string> Arquivos = ["contrato-padrao", "orcamento-padrao", "recibo-padrao"];

    public static IReadOnlyList<TemplateDeDocumento> Carregar(DateTimeOffset agora) =>
        Arquivos.Select(arquivo => Carregar(arquivo, agora)).ToList();

    private static TemplateDeDocumento Carregar(string arquivo, DateTimeOffset agora)
    {
        using var fluxo = typeof(CatalogoDeFabrica).Assembly
            .GetManifestResourceStream($"MarkDocs.Documentos.TemplatesDeFabrica.{arquivo}.json")
            ?? throw new InvalidOperationException($"O template {arquivo}.json não está no assembly.");
        using var memoria = new MemoryStream();
        fluxo.CopyTo(memoria);
        var json = memoria.ToArray();

        // Nome e tipo documental não são formatação: ficam no template salvo.
        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;
        var nome = raiz.GetProperty("nome").GetString()!;
        var tipo = raiz.TryGetProperty("tipoDocumental", out var t) ? t.GetString() ?? nome : nome;

        return new TemplateDeDocumento
        {
            Id = ObjectId.GenerateNewId(),
            Nome = nome,
            Tipo = tipo,
            Formatacao = FormatacaoDocumentoDeNegocio.DeTemplateJson(json),
            DeFabrica = true,
            CriadoEm = agora,
        };
    }
}
