using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Queries;

/// <summary>Os templates: os de fábrica primeiro, depois os criados a partir de documentos.</summary>
public record ListarTemplatesQuery : IRequest<IReadOnlyList<TemplateDeDocumento>>;

public class ListarTemplatesHandler(ITemplateRepository templates)
    : IRequestHandler<ListarTemplatesQuery, IReadOnlyList<TemplateDeDocumento>>
{
    public Task<IReadOnlyList<TemplateDeDocumento>> Handle(ListarTemplatesQuery request, CancellationToken ct) =>
        templates.ListarAsync(ct);
}
