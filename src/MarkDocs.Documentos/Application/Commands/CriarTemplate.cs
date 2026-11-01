using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>
/// Cria um template a partir de um documento formatado: a formatação dele vira
/// um preset que outros documentos podem aplicar.
/// </summary>
public record CriarTemplateCommand(string DocumentoId, string Nome) : IRequest<TemplateDeDocumento>;

public class CriarTemplateHandler(
    IDocumentoRepository documentos,
    ITemplateRepository templates,
    TimeProvider relogio,
    ILogger<CriarTemplateHandler> logger) : IRequestHandler<CriarTemplateCommand, TemplateDeDocumento>
{
    public async Task<TemplateDeDocumento> Handle(CriarTemplateCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new ArgumentException("O template precisa de um nome.");

        var id = Ids.Ler(request.DocumentoId, () => new DocumentoNaoEncontradoException(request.DocumentoId));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.DocumentoId);

        var template = TemplateDeDocumento.DoDocumento(documento, request.Nome, relogio.GetUtcNow());
        await templates.CriarAsync(template, ct);

        logger.LogInformation("Template {TemplateId} criado a partir do documento {DocumentoId}", template.Id, documento.Id);
        return template;
    }
}
