using AppTemplate.Contatos.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AppTemplate.Contatos.Application.EventHandlers;

public class EntidadeExternaCadastradaLogHandler
    : INotificationHandler<EntidadeExternaCadastradaEvent>
{
    private readonly ILogger<EntidadeExternaCadastradaLogHandler> _logger;

    public EntidadeExternaCadastradaLogHandler(
        ILogger<EntidadeExternaCadastradaLogHandler> logger)
        => _logger = logger;

    public Task Handle(EntidadeExternaCadastradaEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Evento] EntidadeExterna cadastrada: {Id} ({Nome})",
            notification.EntidadeExternaId, notification.Nome);
        return Task.CompletedTask;
    }
}
