using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>
/// Cria um documento a partir de um template e de um Markdown. É o que a tela
/// faz quando se escolhe um template e se cola o conteúdo.
/// </summary>
public record CriarDocumentoCommand(string TemplateId, string Corpo, string? Titulo = null) : IRequest<Documento>;

public class CriarDocumentoHandler(
    ITemplateRepository templates,
    IDocumentoRepository documentos,
    TimeProvider relogio,
    ILogger<CriarDocumentoHandler> logger) : IRequestHandler<CriarDocumentoCommand, Documento>
{
    public async Task<Documento> Handle(CriarDocumentoCommand request, CancellationToken ct)
    {
        var id = Ids.Ler(request.TemplateId, () => new TemplateNaoEncontradoException(request.TemplateId));
        var template = await templates.ObterAsync(id, ct) ?? throw new TemplateNaoEncontradoException(request.TemplateId);

        var documento = Documento.Criar(request.Corpo, request.Titulo, template.Tipo, template.ComoTemplate(), relogio.GetUtcNow());
        await documentos.CriarAsync(documento, ct);

        logger.LogInformation("Documento {DocumentoId} criado a partir do template {TemplateId}", documento.Id, template.Id);
        return documento;
    }
}
