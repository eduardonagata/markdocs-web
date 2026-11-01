using Microsoft.Extensions.FileProviders;

namespace MarkDocs.WebUi.Hosting;

public static class WebUiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Configura a hospedagem do SPA:
    /// <list type="bullet">
    ///   <item><c>UseDefaultFiles</c> — reescreve <c>/</c> para <c>/index.html</c>.</item>
    ///   <item><c>UseStaticFiles</c> — serve o <c>wwwroot/</c> deste slice, com cache
    ///         longo nos arquivos com hash e <c>no-cache</c> no <c>index.html</c>.</item>
    ///   <item><c>/api/*</c> sem handler devolve 404 — não cai no SPA.</item>
    ///   <item><c>MapFallbackToFile("index.html")</c> — as demais rotas são do SPA
    ///         (roteamento no cliente).</item>
    /// </list>
    /// O <c>wwwroot/</c> fica ao lado do assembly (<c>{bin}/wwwroot/</c>), copiado
    /// pelo target <c>BuildFrontend</c> do Host em Release/Publish. Em Debug ele
    /// pode não existir: o SpaProxy manda o navegador para o Vite.
    ///
    /// Chame depois de <c>app.MapEndpoints(typeof(WebApiMarker).Assembly)</c>.
    /// </summary>
    public static WebApplication MapWebUi(this WebApplication app)
    {
        var wwwroot = Path.Combine(Path.GetDirectoryName(typeof(WebUiMarker).Assembly.Location)!, "wwwroot");
        Directory.CreateDirectory(wwwroot);

        var arquivos = new PhysicalFileProvider(wwwroot);
        var opcoes = new StaticFileOptions
        {
            FileProvider = arquivos,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = ctx.File.Name == "index.html"
                    ? "no-cache"
                    : "public, max-age=31536000, immutable";
            },
        };

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = arquivos });
        app.UseStaticFiles(opcoes);

        app.MapFallback("/api/{**catchall}", () => Results.NotFound());
        app.MapFallbackToFile("index.html", opcoes);

        return app;
    }
}
