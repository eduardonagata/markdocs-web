using System.ComponentModel;
using System.Globalization;
using System.Text;
using MarkDocs.Abstractions;
using MarkDocs.Core;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using Microsoft.Extensions.AI;
using MongoDB.Bson;

namespace MarkDocs.Documentos.Application.Agente;

/// <summary>
/// As ferramentas que o agente executa <b>no backend</b>: só leitura — listar
/// e consultar documentos, listar templates. Tudo o que altera um documento é
/// ferramenta da <b>tela</b> (AG-UI, declarada pelo frontend a cada execução),
/// para que o que o agente faz apareça na interface e as gravações passem pela
/// aprovação do usuário (EI-001).
/// </summary>
/// <remarks>
/// Uma falha de regra — documento que não existe — volta como <b>texto</b>, e
/// não como exceção: o modelo lê e corrige na mesma rodada. O que é lançado é
/// falha de infraestrutura, que o modelo não tem como corrigir.
/// </remarks>
public sealed class FerramentasDoAgente(IDocumentoRepository documentos, ITemplateRepository templates)
{
    public const string ListarDocumentos = "listarDocumentos";
    public const string ConsultarDocumento = "consultarDocumento";
    public const string ListarTemplates = "listarTemplates";

    public IList<AITool> Todas() =>
    [
        AIFunctionFactory.Create(ListarDocumentosAsync, ListarDocumentos,
            "Lista os documentos do usuário (id, título, tipo, template de origem, data da última alteração), "
            + "do alterado mais recentemente ao mais antigo. Filtra por um trecho do título ou do tipo, se informado."),
        AIFunctionFactory.Create(ConsultarDocumentoAsync, ConsultarDocumento,
            "Lê um documento gravado, pelo id: título, tipo, as imagens que ele pode citar e o Markdown inteiro, "
            + "como está salvo. Para o documento aberto na tela, o Markdown atual (com o que ainda não foi salvo) "
            + "já está no contexto."),
        AIFunctionFactory.Create(ListarTemplatesAsync, ListarTemplates,
            "Lista os templates (id, nome, tipo documental, se é de fábrica) — a papelaria a partir da qual "
            + "documentos são criados."),
    ];

    public sealed record ResumoParaOAgente(string Id, string Titulo, string Tipo, string? Template, DateTimeOffset AlteradoEm);

    public sealed record DocumentoParaOAgente(
        string Id, string Titulo, string Tipo, string? Template, IReadOnlyList<string> Imagens, string Markdown);

    public sealed record TemplateParaOAgente(string Id, string Nome, string Tipo, bool DeFabrica);

    internal async Task<IReadOnlyList<ResumoParaOAgente>> ListarDocumentosAsync(
        [Description("Um trecho do título ou do tipo. Vazio lista todos.")] string filtro = "",
        CancellationToken ct = default)
    {
        var lista = await documentos.ListarAsync(ct);
        var trecho = SemAcento(filtro.Trim());
        return lista
            .Where(d => trecho.Length == 0 || SemAcento(d.Titulo).Contains(trecho) || SemAcento(d.Tipo).Contains(trecho))
            .Select(d => new ResumoParaOAgente(d.Id.ToString(), d.Titulo, d.Tipo, d.TemplateDeOrigem, d.AlteradoEm))
            .ToList();
    }

    internal async Task<object> ConsultarDocumentoAsync(
        [Description("O id do documento, como veio da lista.")] string documentoId,
        CancellationToken ct = default)
    {
        var documento = ObjectId.TryParse(documentoId, out var id) ? await documentos.ObterAsync(id, ct) : null;
        if (documento is null)
            return $"Não existe documento com o id “{documentoId}”. Liste os documentos para achar o id certo.";

        return new DocumentoParaOAgente(
            documento.Id.ToString(),
            documento.Titulo,
            documento.Tipo,
            documento.TemplateDeOrigem,
            documento.Imagens.Select(i => ImagensDoDocumento.Referencia(i.Nome)).ToList(),
            documento.Corpo);
    }

    internal async Task<IReadOnlyList<TemplateParaOAgente>> ListarTemplatesAsync(CancellationToken ct = default) =>
        (await templates.ListarAsync(ct))
            .Select(t => new TemplateParaOAgente(t.Id.ToString(), t.Nome, t.Tipo, t.DeFabrica))
            .ToList();

    private static string SemAcento(string texto)
    {
        var decomposto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
