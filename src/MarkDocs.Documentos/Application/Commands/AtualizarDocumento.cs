using MarkDocs.Core;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>Uma edição de formatação: o id opaco de um eixo e o valor novo.</summary>
public record AjusteDeFormatacao(string Eixo, ValorDeEixo Valor);

/// <summary>
/// Altera um documento: título, tipo, Markdown, a formatação eixo a eixo, ou
/// um template inteiro aplicado de novo. Só o que vier é alterado.
/// </summary>
/// <remarks>
/// A ordem é a de quem edita: primeiro o template (que substitui a formatação
/// inteira), depois os ajustes por cima dele.
/// </remarks>
public record AtualizarDocumentoCommand(
    string Id,
    string? Titulo = null,
    string? Tipo = null,
    string? Corpo = null,
    IReadOnlyList<AjusteDeFormatacao>? Ajustes = null,
    string? TemplateId = null) : IRequest<Documento>;

public class AtualizarDocumentoHandler(
    IDocumentoRepository documentos,
    ITemplateRepository templates,
    CompositorDePdf compositor,
    TimeProvider relogio,
    ILogger<AtualizarDocumentoHandler> logger) : IRequestHandler<AtualizarDocumentoCommand, Documento>
{
    public async Task<Documento> Handle(AtualizarDocumentoCommand request, CancellationToken ct)
    {
        var id = Ids.Ler(request.Id, () => new DocumentoNaoEncontradoException(request.Id));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.Id);

        if (request.TemplateId is { } templateId)
        {
            var template = await templates.ObterAsync(Ids.Ler(templateId, () => new TemplateNaoEncontradoException(templateId)), ct)
                           ?? throw new TemplateNaoEncontradoException(templateId);
            documento.AplicarTemplate(template.ComoTemplate());
        }

        if (request.Titulo is { } titulo)
            documento.Titulo = string.IsNullOrWhiteSpace(titulo) ? Documento.TituloDoMarkdown(request.Corpo ?? documento.Corpo) : titulo.Trim();
        if (!string.IsNullOrWhiteSpace(request.Tipo))
            documento.Tipo = request.Tipo.Trim();
        if (request.Corpo is { } corpo)
            documento.Corpo = corpo;

        if (request.Ajustes is { Count: > 0 } ajustes)
        {
            // Os limites vêm do MarkDocs.Core: cada valor é aparado ao domínio
            // do eixo que o pacote declara. Um eixo desconhecido é ignorado,
            // como no Core — degrada em vez de quebrar.
            var eixos = compositor.Eixos(documento).ToDictionary(e => e.Id);
            foreach (var ajuste in ajustes)
            {
                if (eixos.TryGetValue(ajuste.Eixo, out var eixo))
                    documento.Formatacao.Aplicar(eixo, ajuste.Valor);
            }
        }

        documento.AlteradoEm = relogio.GetUtcNow();
        await documentos.SubstituirAsync(documento, ct);

        logger.LogInformation("Documento {DocumentoId} alterado", documento.Id);
        return documento;
    }
}
