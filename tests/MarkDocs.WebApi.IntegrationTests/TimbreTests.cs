using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MarkDocs.WebApi.IntegrationTests;

/// <summary>O papel timbrado pela borda: enviar, ver, tirar.</summary>
public class TimbreTests(AppFactory app) : IClassFixture<AppFactory>
{
    private readonly HttpClient _http = app.CreateClient();

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent Arquivo(string nome)
    {
        var conteudo = new ByteArrayContent(Png);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { conteudo, "arquivo", nome } };
    }

    [Fact]
    public async Task EnviarVerETirarOTimbre()
    {
        var templates = (await _http.GetFromJsonAsync<JsonArray>("/api/templates"))!;
        var contrato = templates.Single(t => t!["nome"]!.GetValue<string>() == "Contrato padrão")!["id"]!.GetValue<string>();
        var criado = (await (await _http.PostAsJsonAsync("/api/documentos", new { templateId = contrato, corpo = "# Contrato\n\nTexto." }))
            .Content.ReadFromJsonAsync<JsonObject>())!;
        var id = criado["id"]!.GetValue<string>();
        Assert.Null(criado["timbres"]!["primeira"]);
        Assert.Null(criado["timbres"]!["demais"]);

        using var envio = Arquivo("timbre.png");
        var resposta = await _http.PutAsync($"/api/documentos/{id}/timbre/todas", envio);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var documento = (await resposta.Content.ReadFromJsonAsync<JsonObject>())!;
        var primeira = documento["timbres"]!["primeira"]!;
        Assert.StartsWith("timbre-todas-", primeira["nome"]!.GetValue<string>());
        Assert.Equal($"/api/documentos/{id}/timbre/primeira", primeira["url"]!.GetValue<string>());
        Assert.Equal(primeira["nome"]!.GetValue<string>(), documento["timbres"]!["demais"]!["nome"]!.GetValue<string>());

        var imagem = await _http.GetAsync($"/api/documentos/{id}/timbre/demais");
        Assert.Equal(HttpStatusCode.OK, imagem.StatusCode);
        Assert.Equal("image/png", imagem.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await imagem.Content.ReadAsByteArrayAsync());

        var pdf = await _http.GetAsync($"/api/documentos/{id}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);

        var tirado = (await (await _http.DeleteAsync($"/api/documentos/{id}/timbre/primeira")).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Null(tirado["timbres"]!["primeira"]);
        Assert.NotNull(tirado["timbres"]!["demais"]);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/documentos/{id}/timbre/primeira")).StatusCode);
    }

    [Fact]
    public async Task Erros()
    {
        var templates = (await _http.GetFromJsonAsync<JsonArray>("/api/templates"))!;
        var criado = (await (await _http.PostAsJsonAsync("/api/documentos", new { templateId = templates[0]!["id"]!.GetValue<string>(), corpo = "x" }))
            .Content.ReadFromJsonAsync<JsonObject>())!;
        var id = criado["id"]!.GetValue<string>();

        using var paginasErradas = Arquivo("timbre.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.PutAsync($"/api/documentos/{id}/timbre/metade", paginasErradas)).StatusCode);
        using var formatoErrado = Arquivo("timbre.txt");
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.PutAsync($"/api/documentos/{id}/timbre/todas", formatoErrado)).StatusCode);
        using var semDocumento = Arquivo("timbre.png");
        Assert.Equal(HttpStatusCode.NotFound, (await _http.PutAsync("/api/documentos/65f0c0ffee0000000000beef/timbre/todas", semDocumento)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/documentos/{id}/timbre/todas")).StatusCode);
    }
}
