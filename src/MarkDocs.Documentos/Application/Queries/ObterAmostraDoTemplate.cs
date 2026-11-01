using MarkDocs.Abstractions;
using MarkDocs.Core;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace MarkDocs.Documentos.Application.Queries;

/// <summary>
/// Um PDF de amostra com a formatação do template: a tela de templates mostra
/// a papelaria sem precisar saber o que cada eixo significa.
/// </summary>
public record ObterAmostraDoTemplateQuery(string Id) : IRequest<Composicao>;

public class ObterAmostraDoTemplateHandler(ITemplateRepository templates, CompositorDePdf compositor)
    : IRequestHandler<ObterAmostraDoTemplateQuery, Composicao>
{
    public const string Amostra = """
        # Título do documento

        Este é um parágrafo de exemplo, com **negrito**, *itálico* e o texto corrido que um
        documento de negócio costuma ter. A formatação vem inteira do template: papel, margens,
        tipografia, títulos e tabelas.

        ## Uma seção

        O segundo parágrafo mostra o espaço entre parágrafos e a entrelinha escolhidos.

        | Item | Quantidade | Valor |
        |---|:-:|--:|
        | Serviço de exemplo | 1 | R$ 1.000,00 |
        | Outro serviço | 2 | R$ 500,00 |

        ### Um detalhe

        - Um item de lista.
        - Outro item de lista.
        """;

    public async Task<Composicao> Handle(ObterAmostraDoTemplateQuery request, CancellationToken ct)
    {
        var id = Ids.Ler(request.Id, () => new TemplateNaoEncontradoException(request.Id));
        var template = await templates.ObterAsync(id, ct) ?? throw new TemplateNaoEncontradoException(request.Id);
        return await compositor.ComporAsync(new AmostraDeTemplate(template.Id, template.Nome, template.Tipo, template.Formatacao, template.Ativos), ct);
    }

    /// <summary>O template visto como um documento de amostra. Não é gravado.</summary>
    private sealed record AmostraDeTemplate(
        ObjectId Id, string Titulo, string Tipo, Formatacao Formatacao, IReadOnlyList<ReferenciaDeAtivo> Ativos) : IDocumento
    {
        public string Corpo => Amostra;
    }
}
