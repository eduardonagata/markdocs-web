using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MarkDocs.Core.Tests;

/// <summary>
/// O que os testes Swift leem com o PDFKit — o texto da página, onde está um
/// trecho e com que corpo foi composto —, lido aqui com o PdfPig.
/// </summary>
/// <remarks>
/// O PDF do Typst posiciona as palavras sem desenhar o espaço entre elas, e o
/// texto da página é reconstruído como o PDFKit o faz: um espaço onde há um
/// vão entre letras, uma quebra de linha onde a linha de base muda.
/// </remarks>
public sealed class DocumentoPdf : IDisposable
{
    private readonly PdfDocument _documento;

    public DocumentoPdf(byte[] pdf) => _documento = PdfDocument.Open(pdf);

    public int Paginas => _documento.NumberOfPages;

    /// <summary>A página, pelo índice a partir de zero (como <c>PDFDocument.page(at:)</c>).</summary>
    public PaginaPdf Pagina(int indice) => new(_documento.GetPage(indice + 1));

    /// <summary>O texto de todas as páginas (<c>PDFDocument.string</c>).</summary>
    public string Texto =>
        string.Join("\n", Enumerable.Range(0, Paginas).Select(i => Pagina(i).Texto));

    public void Dispose() => _documento.Dispose();
}

public sealed class PaginaPdf
{
    private readonly Page _pagina;
    private readonly string _texto;
    // Para cada caractere do texto reconstruído, a letra de onde ele veio
    // (ou null, se foi um espaço ou uma quebra inserida).
    private readonly List<Letter?> _origem = [];

    public PaginaPdf(Page pagina)
    {
        _pagina = pagina;
        var texto = new StringBuilder();
        Letter? anterior = null;

        foreach (var letra in pagina.Letters)
        {
            if (anterior is not null)
            {
                var mudouDeLinha = Math.Abs(letra.StartBaseLine.Y - anterior.StartBaseLine.Y) > 1
                                   || letra.StartBaseLine.X < anterior.StartBaseLine.X - 1;
                var vao = letra.StartBaseLine.X - anterior.EndBaseLine.X;
                var temEspaco = string.IsNullOrWhiteSpace(letra.Value) || string.IsNullOrWhiteSpace(anterior.Value);

                if (mudouDeLinha)
                    Acrescentar(texto, "\n", null);
                else if (!temEspaco && vao > 0.15 * Math.Max(letra.PointSize, 1))
                    Acrescentar(texto, " ", null);
            }
            Acrescentar(texto, letra.Value, letra);
            anterior = letra;
        }
        _texto = texto.ToString();
    }

    private void Acrescentar(StringBuilder texto, string valor, Letter? letra)
    {
        texto.Append(valor);
        for (var i = 0; i < valor.Length; i++) _origem.Add(letra);
    }

    /// <summary>O texto da página (<c>PDFPage.string</c>).</summary>
    public string Texto => _texto;

    /// <summary>A <i>media box</i>, em pontos.</summary>
    public double Largura => _pagina.MediaBox.Bounds.Width;
    public double Altura => _pagina.MediaBox.Bounds.Height;

    private IReadOnlyList<Letter> Letras(string trecho)
    {
        var posicao = _texto.IndexOf(trecho, StringComparison.Ordinal);
        Assert.True(posicao >= 0, $"{trecho} não está na página");
        return _origem.Skip(posicao).Take(trecho.Length).OfType<Letter>().Distinct().ToList();
    }

    /// <summary>
    /// Onde está, na página, a primeira ocorrência de <paramref name="trecho"/>:
    /// a borda esquerda (a origem da primeira letra) e o topo, em pontos, com a
    /// origem no canto inferior esquerdo — como a seleção do PDFKit.
    /// </summary>
    public (double MinX, double MaxY) Caixa(string trecho)
    {
        var letras = Letras(trecho);
        return (letras[0].StartBaseLine.X, letras.Max(l => l.BoundingBox.Top));
    }

    /// <summary>O corpo, em pontos, com que <paramref name="trecho"/> foi composto.</summary>
    public double Corpo(string trecho) => Letras(trecho)[0].PointSize;
}
