using System.IO.Compression;
using System.Text;
using MarkDocs.Core.Conteudo;
using MarkDocs.Core.Nucleo;
using static MarkDocs.Core.Tests.Sonda;

namespace MarkDocs.Core.Tests.Extensoes;

/// <summary>
/// <b>Extensão do MarkDocs.Core</b> (vocabulário 2), sem par no NotiqTypst:
/// as imagens do documento (<c>Docs/Imagens.md</c>). Os testes transcritos
/// continuam valendo sem alteração — sem imagens entregues, a imagem é o texto
/// alternativo, como lá.
/// </summary>
[Trait("Tag", "extensao")]
public class ImagensTests
{
    private static readonly IReadOnlySet<string> Planta = new HashSet<string> { "planta.png" };

    // ─── A convenção ────────────────────────────────────────────────────

    [Theory]
    [InlineData("planta.png", true)]
    [InlineData("foto-2026_01.jpeg", true)]
    [InlineData("situação.png", true)]
    [InlineData("", false)]
    [InlineData(".oculto.png", false)]
    [InlineData("a/b.png", false)]
    [InlineData("..", false)]
    [InlineData("com espaço.png", false)]
    [InlineData("a%20b.png", false)]
    public void NomeValido(string nome, bool valido) =>
        Assert.Equal(valido, ImagensDoDocumento.NomeValido(nome));

    [Theory]
    [InlineData("imagens/planta.png", "planta.png")]
    [InlineData("./imagens/planta.png", "planta.png")]
    [InlineData("planta.png", null)]
    [InlineData("https://exemplo.com/imagens/planta.png", null)]
    [InlineData("/imagens/planta.png", null)]
    [InlineData("imagens/../segredo.png", null)]
    [InlineData("imagens/sub/planta.png", null)]
    public void NomeCitado(string destino, string? nome) =>
        Assert.Equal(nome, ImagensDoDocumento.NomeCitado(destino));

    // ─── A árvore e o literal ───────────────────────────────────────────

    [Theory]
    [InlineData("![Planta](imagens/planta.png)")]
    [InlineData("![Planta](./imagens/planta.png)")]
    public void ImagemEntregueViraImagem(string markdown) =>
        Assert.Equal(
            new BlocoDoc[] { new BlocoDoc.Paragrafo([new InlineDoc.Imagem("planta.png", "Planta")]) },
            ArvoreDeMarkdown.Converter(markdown, Planta));

    [Theory]
    [InlineData("![Planta](imagens/outra.png)")]
    [InlineData("![Planta](planta.png)")]
    [InlineData("![Planta](https://exemplo.com/planta.png)")]
    public void ImagemNaoEntregueContinuaTexto(string markdown) =>
        Assert.Equal(
            new BlocoDoc[] { new BlocoDoc.Paragrafo([new InlineDoc.Texto("Planta")]) },
            ArvoreDeMarkdown.Converter(markdown, Planta));

    [Fact]
    public void LiteralCitaOCaminhoNoAcervo()
    {
        var literal = ValorTypst.Literal(ArvoreDeMarkdown.Converter("![Planta](imagens/planta.png)", Planta));
        Assert.Contains("tipo: \"imagem\"", literal);
        Assert.Contains("caminho: \"/imagens/planta.png\"", literal);
        Assert.Contains("alternativo: \"Planta\"", literal);
    }

    // ─── O Acervo e o motor ─────────────────────────────────────────────

    [Fact]
    public void AcervoGuardaAImagemAoLadoDoDocumento()
    {
        var acervo = Apoio.Renderizador().Acervo(
            "![Planta](imagens/planta.png)",
            Apoio.TemplateMinimo(),
            new Dictionary<string, byte[]> { ["planta.png"] = Apoio.Png });
        Assert.Equal(
            new HashSet<string> { "/documento.typ", "/template.typ", "/notiq/base.typ", "/imagens/planta.png" },
            acervo.Arquivos.Keys.ToHashSet());
    }

    [Fact]
    public void NomeInvalidoERecusado()
    {
        var erro = Apoio.Erro(() => Apoio.Renderizador().Pdf(
            "x", Apoio.TemplateMinimo(), new Dictionary<string, byte[]> { ["../segredo.png"] = Apoio.Png }));
        Assert.IsType<ErroTypst.EntradaInvalida>(erro);
    }

    [Fact, Trait("Tag", Tags.Motor)]
    public void SondaVeUmaImagem()
    {
        var blocos = ArvoreDeMarkdown.Converter("![Planta](imagens/planta.png)", Planta);
        var acervo = new Acervo(
            documento: $$"""
                #import "{{Acervo.CaminhoDaBase}}": renderizar
                #let c = renderizar({{ValorTypst.Literal(blocos)}})
                #assert.eq(c.func(), par)
                #assert.eq(c.body.func(), box)
                #c
                """,
            template: "#let estilo(body) = body",
            @base: Acervo.Base(),
            ativos: new Dictionary<string, byte[]>(),
            imagens: new Dictionary<string, byte[]> { ["planta.png"] = Png(40, 20) });
        var pdf = MotorTypst.Compor(acervo, Apoio.Fontes, DateTimeOffset.UnixEpoch).Pdf;
        Assert.Equal("%PDF-"u8.ToArray(), pdf.Take(5));
    }

    [Fact, Trait("Tag", Tags.Motor)]
    public void ImagemChegaAoPapel()
    {
        var pdf = Apoio.Renderizador().Pdf(
            "Antes.\n\n![Planta](imagens/planta.png)\n\nDepois.",
            Apoio.TemplateMinimo(),
            new Dictionary<string, byte[]> { ["planta.png"] = Png(40, 20) });
        using var documento = UglyToad.PdfPig.PdfDocument.Open(pdf);
        Assert.Single(documento.GetPage(1).GetImages());
    }

    /// <summary>Uma imagem larga não passa da coluna de texto; uma pequena fica no tamanho natural.</summary>
    [Fact, Trait("Tag", Tags.Motor)]
    public void ImagemNuncaPassaDaColuna()
    {
        double Largura(byte[] png)
        {
            var pdf = Apoio.Renderizador().Pdf(
                "![x](imagens/x.png)", Apoio.TemplateMinimo(), new Dictionary<string, byte[]> { ["x.png"] = png });
            using var documento = UglyToad.PdfPig.PdfDocument.Open(pdf);
            return documento.GetPage(1).GetImages().Single().BoundingBox.Width;
        }

        // A4 com margens de 22 mm: a coluna tem 166 mm.
        var coluna = (210 - 22 - 22) / 25.4 * 72;
        Assert.True(Math.Abs(Largura(Png(4000, 100)) - coluna) < 1, "a imagem larga não ocupa a coluna");
        Assert.True(Largura(Png(40, 20)) < coluna / 2, "a imagem pequena foi esticada");
    }

    [Fact, Trait("Tag", Tags.Motor)]
    public void SemImagensOPdfEOMesmo()
    {
        var renderizador = Apoio.Renderizador();
        var template = Apoio.TemplateMinimo();
        Assert.Equal(
            renderizador.Pdf(Apoio.DocumentoDeTresPaginas, template),
            renderizador.Pdf(Apoio.DocumentoDeTresPaginas, template, new Dictionary<string, byte[]>()));
    }

    /// <summary>Um PNG em tons de cinza do tamanho pedido, sem dependência externa.</summary>
    internal static byte[] Png(int largura, int altura)
    {
        using var saida = new MemoryStream();
        saida.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        void Bloco(string tipo, byte[] dados)
        {
            var tamanho = BitConverter.GetBytes(dados.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(tamanho);
            saida.Write(tamanho);
            var corpo = Encoding.ASCII.GetBytes(tipo).Concat(dados).ToArray();
            saida.Write(corpo);
            var crc = BitConverter.GetBytes(Crc32(corpo));
            if (BitConverter.IsLittleEndian) Array.Reverse(crc);
            saida.Write(crc);
        }

        byte[] BigEndian(int valor)
        {
            var b = BitConverter.GetBytes(valor);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return b;
        }

        Bloco("IHDR", [.. BigEndian(largura), .. BigEndian(altura), 8, 0, 0, 0, 0]);

        using var cru = new MemoryStream();
        for (var y = 0; y < altura; y++)
        {
            cru.WriteByte(0);
            for (var x = 0; x < largura; x++) cru.WriteByte((byte)(x * 255 / Math.Max(1, largura - 1)));
        }
        using var comprimido = new MemoryStream();
        using (var z = new ZLibStream(comprimido, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(cru.ToArray());
        Bloco("IDAT", comprimido.ToArray());
        Bloco("IEND", []);
        return saida.ToArray();
    }

    private static uint Crc32(byte[] dados)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in dados)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
