using System.Globalization;

namespace MarkDocs.Core.Nucleo;

// Os valores que a camada de emissão entende.
//
// São do núcleo, não de família nenhuma: um gerador traduz o esquema dele
// nestes tipos, e a partir daqui ninguém mais sabe de que família aquilo veio.
// São fechados de propósito — nenhum aceita sintaxe.

internal enum UnidadeDeMedida { Mm, Cm, Pt, Em, Percentual }

/// <summary>
/// Uma medida de um formato fechado: <c>"20mm"</c>, <c>"11pt"</c>,
/// <c>"0.65em"</c>, <c>"30%"</c>. Não há campo em que se possa escrever uma
/// expressão, e portanto não há escape, não há erro de digitação que compile
/// torto, e não há parser além deste.
/// </summary>
internal readonly record struct Medida(double Valor, UnidadeDeMedida Unidade)
{
    // O texto de cada unidade, da mais longa para a mais curta.
    private static readonly (string Texto, UnidadeDeMedida Unidade)[] Sufixos =
    [
        ("mm", UnidadeDeMedida.Mm), ("cm", UnidadeDeMedida.Cm),
        ("pt", UnidadeDeMedida.Pt), ("em", UnidadeDeMedida.Em),
        ("%", UnidadeDeMedida.Percentual),
    ];

    public static readonly Medida Zero = new(0, UnidadeDeMedida.Pt);

    /// <summary>O texto da unidade, como o template e o Typst o escrevem.</summary>
    public static string TextoDa(UnidadeDeMedida unidade) => Array.Find(Sufixos, s => s.Unidade == unidade).Texto;

    /// <summary>A unidade pelo texto (<c>"pt"</c>), ou <c>null</c>.</summary>
    public static UnidadeDeMedida? UnidadeDe(string texto)
    {
        foreach (var (sufixo, unidade) in Sufixos)
            if (sufixo == texto) return unidade;
        return null;
    }

    /// <summary><c>"11pt"</c> → <c>Medida(11, Pt)</c>. <c>null</c> para qualquer coisa fora do formato.</summary>
    public static Medida? Ler(string bruto)
    {
        var texto = bruto.Trim(' ', '\t');
        foreach (var (sufixo, unidade) in Sufixos)
        {
            if (!texto.EndsWith(sufixo, StringComparison.Ordinal)) continue;
            var numero = texto[..^sufixo.Length];

            // Invariante: "0.65" é sempre ponto, qualquer que seja o idioma do
            // servidor. Sem espaço, sem hexadecimal, sem infinito.
            if (!double.TryParse(numero, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture, out var valor) || !double.IsFinite(valor))
                return null;
            return new Medida(valor, unidade);
        }
        return null;
    }

    /// <summary>
    /// <c>Medida(11, Pt)</c> → <c>"11pt"</c>; <c>Medida(0.65, Em)</c> →
    /// <c>"0.65em"</c>. Inteiro sai sem casas decimais; o resto, no formato
    /// <c>%g</c> do C, como no Swift.
    /// </summary>
    public string Descricao =>
        (Valor == Math.Round(Valor) && Math.Abs(Valor) < 1e15
            ? ((long)Valor).ToString(CultureInfo.InvariantCulture)
            : FormatoG(Valor))
        + TextoDa(Unidade);

    public override string ToString() => Descricao;

    /// <summary>O <c>%g</c> do <c>printf</c>, com seis algarismos significativos.</summary>
    internal static string FormatoG(double valor)
    {
        if (valor == 0) return "0";
        var cientifico = valor.ToString("E5", CultureInfo.InvariantCulture);
        var expoente = int.Parse(cientifico[(cientifico.IndexOf('E') + 1)..], CultureInfo.InvariantCulture);

        if (expoente < -4 || expoente >= 6)
        {
            var mantissa = SemZerosAdireita(cientifico[..cientifico.IndexOf('E')]);
            return $"{mantissa}e{(expoente < 0 ? '-' : '+')}{Math.Abs(expoente):00}";
        }
        return SemZerosAdireita(valor.ToString("F" + (5 - expoente), CultureInfo.InvariantCulture));

        static string SemZerosAdireita(string s) =>
            s.Contains('.') ? s.TrimEnd('0').TrimEnd('.') : s;
    }
}

/// <summary>Uma cor hexadecimal, <c>"#1c1917"</c>. Nada de nomes, nada de funções.</summary>
internal readonly record struct Cor(string Hexadecimal)
{
    public static Cor? Ler(string bruto)
    {
        var texto = bruto.Trim(' ', '\t');
        if (!texto.StartsWith('#')) return null;
        var digitos = texto[1..];
        if (digitos.Length is not (3 or 4 or 6 or 8) || !digitos.All(char.IsAsciiHexDigit)) return null;
        return new Cor(texto);
    }
}

/// <summary>
/// Os valores são os nomes do próprio Typst, e o <c>template.json</c> os guarda
/// assim: não há tradução entre o que está em disco e o que vai para o motor.
/// </summary>
internal enum Papel { A4, A5, Carta, Oficio }

internal static class Papeis
{
    private static readonly (Papel Papel, string Texto)[] Nomes =
        [(Papel.A4, "a4"), (Papel.A5, "a5"), (Papel.Carta, "us-letter"), (Papel.Oficio, "us-legal")];

    public static string Texto(this Papel papel) => Array.Find(Nomes, n => n.Papel == papel).Texto;

    public static Papel? Ler(string texto)
    {
        foreach (var (papel, nome) in Nomes)
            if (nome == texto) return papel;
        return null;
    }
}

internal readonly record struct Margem(Medida Topo, Medida Base, Medida Esquerda, Medida Direita);

internal enum AlinhamentoHorizontal { Esquerda, Centro, Direita, Justificado }

/// <summary>
/// Um valor que o motor obtém consultando o que já compôs. É o único canal pelo
/// qual a moldura depende do documento, e é fechado.
/// </summary>
internal enum CampoDerivado { TituloDoDocumento, TipoDocumental, NumeroDaPagina, TotalDePaginas }

/// <summary>
/// Uma peça de uma fatia de cabeçalho ou rodapé. Plana — sem filhos, sem
/// aninhamento. <b>Não existe interpolação de string</b>: seria inventar uma
/// mini-linguagem, com escape e com chave não fechada.
/// </summary>
internal abstract record PecaDeConteudo
{
    private PecaDeConteudo() { }

    public sealed record Texto(string Valor) : PecaDeConteudo;
    public sealed record Campo(CampoDerivado Qual) : PecaDeConteudo;
    public sealed record Literal(string Nome) : PecaDeConteudo;
    public sealed record Imagem(string Caminho, Medida? Altura) : PecaDeConteudo;
    public sealed record Quebra : PecaDeConteudo;
}
