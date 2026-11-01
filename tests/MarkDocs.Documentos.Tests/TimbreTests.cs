using MarkDocs.Abstractions;
using MarkDocs.Abstractions.Formatacoes;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Queries;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;

namespace MarkDocs.Documentos.Tests;

/// <summary>
/// O papel timbrado: a imagem de fundo das páginas, em todas, na primeira ou
/// nas demais — conferida no PDF, página a página.
/// </summary>
[Collection("Mongo")]
public class TimbreTests(MongoFixture mongo)
{
    /// <summary>Texto bastante para três páginas.</summary>
    private static readonly string Longo =
        "# Contrato de locação\n\n" + string.Join("\n\n", Enumerable.Range(1, 60).Select(i =>
            $"Parágrafo {i}: as partes acordam a locação do imóvel, nos termos e condições deste instrumento, que se rege pelas cláusulas seguintes."));

    private static Task<Documento> GravarAsync(Ambiente ambiente, Documento documento, string paginas, string arquivo = "Timbre da firma.PNG") =>
        new GravarTimbreHandler(ambiente.Documentos, ambiente.Armazenamento, ambiente.Relogio, NullLogger<GravarTimbreHandler>.Instance)
            .Handle(new GravarTimbreCommand(documento.Id.ToString(), paginas, arquivo, Ambiente.Png), CancellationToken.None);

    private static Task<Documento> RemoverAsync(Ambiente ambiente, Documento documento, string paginas) =>
        new RemoverTimbreHandler(ambiente.Documentos, ambiente.Relogio)
            .Handle(new RemoverTimbreCommand(documento.Id.ToString(), paginas), CancellationToken.None);

    private static Task<ImagemDoDocumento> ObterAsync(Ambiente ambiente, Documento documento, string paginas) =>
        new ObterTimbreDoDocumentoHandler(ambiente.Documentos, ambiente.Armazenamento)
            .Handle(new ObterTimbreDoDocumentoQuery(documento.Id.ToString(), paginas), CancellationToken.None);

    /// <summary>Quantas imagens cada página do PDF tem — o timbre é uma.</summary>
    private static async Task<int[]> ImagensPorPaginaAsync(Ambiente ambiente, Documento documento)
    {
        using var pdf = PdfDocument.Open((await ambiente.PdfAsync(documento.Id.ToString())).Pdf);
        return pdf.GetPages().Select(p => p.GetImages().Count()).ToArray();
    }

    private static async Task<(Ambiente, Documento)> CriarAsync(MongoFixture mongo, string template, string corpo)
    {
        var ambiente = new Ambiente(mongo);
        var modelo = await ambiente.TemplateAsync(template);
        return (ambiente, await ambiente.CriarAsync(new CriarDocumentoCommand(modelo.Id.ToString(), corpo)));
    }

    private static FormatacaoDocumentoDeNegocio.MolduraFormatada Moldura(Documento d) =>
        ((FormatacaoDocumentoDeNegocio)d.Formatacao).Moldura!;

    [Fact]
    public async Task UmTemplateSemMolduraGanhaOFundoEmTodasAsPaginas()
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Recibo padrão", Longo);
        Assert.Null(((FormatacaoDocumentoDeNegocio)documento.Formatacao).Moldura);

        var comTimbre = await GravarAsync(ambiente, documento, "todas");

        var nome = comTimbre.Timbre(Domain.ValueObjects.PaginasDoTimbre.Primeira)!;
        Assert.Matches("^timbre-todas-[0-9a-f]{8}\\.png$", nome);
        Assert.Equal(nome, comTimbre.Timbre(Domain.ValueObjects.PaginasDoTimbre.Demais));
        Assert.Equal(nome, Moldura(comTimbre).Todas!.Fundo!.Arquivo);
        var ativo = Assert.Single(comTimbre.Ativos, a => a.Nome == nome);
        Assert.Equal(PapelDoAtivo.Template, ativo.Papel);
        Assert.True(ambiente.Armazenamento.Objetos.ContainsKey(ativo.Id));

        var paginas = await ImagensPorPaginaAsync(ambiente, comTimbre);
        Assert.True(paginas.Length > 1);
        Assert.All(paginas, quantas => Assert.Equal(1, quantas));
    }

    [Fact]
    public async Task TodasAsPaginasValeTambemParaAsVariantesDeclaradas()
    {
        // O Contrato padrão declara `demais` (com cabeçalho): sem a regra, o
        // timbre de `todas` sumiria da segunda página em diante.
        var (ambiente, documento) = await CriarAsync(mongo, "Contrato padrão", Longo);
        Assert.NotNull(Moldura(documento).Demais);

        var comTimbre = await GravarAsync(ambiente, documento, "todas");

        Assert.NotNull(Moldura(comTimbre).Demais!.Fundo);
        Assert.NotNull(Moldura(comTimbre).Demais!.Cabecalho);
        Assert.All(await ImagensPorPaginaAsync(ambiente, comTimbre), quantas => Assert.Equal(1, quantas));
    }

    [Fact]
    public async Task SoNaPrimeiraPaginaSemPerderORodape()
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Contrato padrão", Longo);

        var comTimbre = await GravarAsync(ambiente, documento, "primeira");

        var primeira = Moldura(comTimbre).Primeira!;
        Assert.NotNull(primeira.Fundo);
        // A variante nova copia o rodapé de `todas`: a primeira página continua numerada.
        Assert.NotNull(primeira.Rodape);
        Assert.Null(comTimbre.Timbre(Domain.ValueObjects.PaginasDoTimbre.Demais));

        var paginas = await ImagensPorPaginaAsync(ambiente, comTimbre);
        Assert.Equal(1, paginas[0]);
        Assert.All(paginas.Skip(1), quantas => Assert.Equal(0, quantas));
        using var pdf = PdfDocument.Open((await ambiente.PdfAsync(comTimbre.Id.ToString())).Pdf);
        Assert.Contains("Página1de", string.Concat(pdf.GetPage(1).Letters.Select(l => l.Value)).Replace(" ", ""));
    }

    [Fact]
    public async Task PrimeiraEDemaisComTimbresDiferentes()
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Orçamento padrão", Longo);

        await GravarAsync(ambiente, documento, "primeira", "capa.png");
        var comDois = await GravarAsync(ambiente, documento, "demais", "continuacao.png");

        var primeira = comDois.Timbre(Domain.ValueObjects.PaginasDoTimbre.Primeira);
        var demais = comDois.Timbre(Domain.ValueObjects.PaginasDoTimbre.Demais);
        Assert.StartsWith("timbre-primeira-", primeira);
        Assert.StartsWith("timbre-demais-", demais);
        Assert.Equal(2, comDois.Ativos.Count(a => a.Nome.StartsWith(Documento.PrefixoDoTimbre)));
        Assert.All(await ImagensPorPaginaAsync(ambiente, comDois), quantas => Assert.Equal(1, quantas));
    }

    [Fact]
    public async Task TirarDaPrimeiraPaginaUmTimbreDeTodas()
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Recibo padrão", Longo);
        await GravarAsync(ambiente, documento, "todas");

        var semNaPrimeira = await RemoverAsync(ambiente, documento, "primeira");

        Assert.Null(semNaPrimeira.Timbre(Domain.ValueObjects.PaginasDoTimbre.Primeira));
        Assert.NotNull(semNaPrimeira.Timbre(Domain.ValueObjects.PaginasDoTimbre.Demais));
        var paginas = await ImagensPorPaginaAsync(ambiente, semNaPrimeira);
        Assert.Equal(0, paginas[0]);
        Assert.All(paginas.Skip(1), quantas => Assert.Equal(1, quantas));

        var semNenhum = await RemoverAsync(ambiente, documento, "todas");
        Assert.Null(semNenhum.Timbre(Domain.ValueObjects.PaginasDoTimbre.Demais));
        Assert.DoesNotContain(semNenhum.Ativos, a => a.Nome.StartsWith(Documento.PrefixoDoTimbre));
        Assert.All(await ImagensPorPaginaAsync(ambiente, semNenhum), quantas => Assert.Equal(0, quantas));
    }

    [Fact]
    public async Task TrocarOTimbreSoltaAReferenciaAntigaMasNaoApagaOObjeto()
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Recibo padrão", "# Recibo\n\nRecebi.");
        var primeiro = await GravarAsync(ambiente, documento, "todas");
        var antigo = primeiro.Ativos.Single(a => a.Nome.StartsWith(Documento.PrefixoDoTimbre));

        var segundo = await GravarAsync(ambiente, documento, "todas");

        var novo = Assert.Single(segundo.Ativos, a => a.Nome.StartsWith(Documento.PrefixoDoTimbre));
        Assert.NotEqual(antigo.Nome, novo.Nome);
        // O objeto antigo pode estar num template criado a partir do documento.
        Assert.True(ambiente.Armazenamento.Objetos.ContainsKey(antigo.Id));
    }

    [Fact]
    public async Task OTimbreVaiComOTemplateESaiQuandoOutroEAplicado()
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Recibo padrão", "# Recibo\n\nRecebi.");
        var comTimbre = await GravarAsync(ambiente, documento, "todas");

        var template = await ambiente.CriarTemplateAsync(documento.Id.ToString(), "Recibo timbrado");
        Assert.Contains(template.Ativos, a => a.Nome.StartsWith(Documento.PrefixoDoTimbre));

        var outro = await ambiente.TemplateAsync("Contrato padrão");
        var reaplicado = await ambiente.AtualizarAsync(new AtualizarDocumentoCommand(documento.Id.ToString(), TemplateId: outro.Id.ToString()));
        Assert.Null(reaplicado.Timbre(Domain.ValueObjects.PaginasDoTimbre.Primeira));

        // E o documento criado do template timbrado nasce com ele.
        var doTemplate = await ambiente.CriarAsync(new CriarDocumentoCommand(template.Id.ToString(), "# Outro recibo\n\nRecebi."));
        Assert.Equal(comTimbre.Timbre(Domain.ValueObjects.PaginasDoTimbre.Primeira), doTemplate.Timbre(Domain.ValueObjects.PaginasDoTimbre.Primeira));
        var paginas = await ImagensPorPaginaAsync(ambiente, doTemplate);
        Assert.Equal(new[] { 1 }, paginas);
    }

    [Fact]
    public async Task AImagemDoTimbreEServidaPelasPaginas()
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Recibo padrão", "# Recibo\n\nRecebi.");
        await Assert.ThrowsAsync<TimbreNaoEncontradoException>(() => ObterAsync(ambiente, documento, "primeira"));

        await GravarAsync(ambiente, documento, "demais");

        var demais = await ObterAsync(ambiente, documento, "demais");
        Assert.Equal(Ambiente.Png, demais.Conteudo);
        Assert.Equal("image/png", demais.TipoDeConteudo);
        await Assert.ThrowsAsync<TimbreNaoEncontradoException>(() => ObterAsync(ambiente, documento, "primeira"));
    }

    [Theory]
    [InlineData("todas", "timbre.txt")]
    [InlineData("todas", "timbre.pdf")]
    [InlineData("metade", "timbre.png")]
    public async Task EntradaInvalidaERecusada(string paginas, string arquivo)
    {
        var (ambiente, documento) = await CriarAsync(mongo, "Recibo padrão", "x");
        await Assert.ThrowsAsync<ArgumentException>(() => GravarAsync(ambiente, documento, paginas, arquivo));
        await Assert.ThrowsAnyAsync<NaoEncontradoException>(() => GravarAsync(ambiente, new Documento
        {
            Titulo = "",
            Tipo = "",
            Corpo = "",
            Formatacao = documento.Formatacao,
            CriadoEm = DateTimeOffset.UnixEpoch,
            AlteradoEm = DateTimeOffset.UnixEpoch,
        }, "todas"));
    }
}
