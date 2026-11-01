using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MarkDocs.Documentos.Infrastructure.TemplatesDeFabrica;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Infrastructure.Workers;

/// <summary>
/// Semeia os templates de fábrica na primeira execução: sem nenhum template, a
/// criação de documento não teria por onde começar. A checagem é pela
/// contagem, não por nome — um template renomeado não reaparece duplicado.
/// </summary>
public sealed class SemeadorDeTemplates(
    ITemplateRepository templates,
    TimeProvider relogio,
    ILogger<SemeadorDeTemplates> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            if (await templates.ContarDeFabricaAsync(ct) > 0) return;

            var deFabrica = CatalogoDeFabrica.Carregar(relogio.GetUtcNow());
            await templates.CriarVariosAsync(deFabrica, ct);
            logger.LogInformation("{Quantidade} templates de fábrica semeados", deFabrica.Count);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // O app sobe mesmo sem o MongoDB: a semeadura é tentada de novo na
            // próxima partida, e o erro de conexão aparece na primeira chamada.
            logger.LogError(erro, "Não foi possível semear os templates de fábrica");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
