using System.Text;

namespace MarkDocs.Core.Nucleo;

/// <summary>O escape, e é um só.</summary>
/// <remarks>
/// É a mesma função que o conversor de conteúdo e os geradores de template
/// usam: nenhuma família pode esquecer de escapar. E é curta de propósito —
/// num literal de string Typst, só <c>\</c> e <c>"</c> são significantes. Os
/// controles entram porque uma quebra de linha crua terminaria a string.
/// </remarks>
internal static class LiteralTypst
{
    public static string Texto(string bruto)
    {
        var saida = new StringBuilder(bruto.Length + 2);
        saida.Append('"');

        foreach (var escalar in bruto.EnumerateRunes())
        {
            switch (escalar.Value)
            {
                case '\\': saida.Append("\\\\"); break;
                case '"': saida.Append("\\\""); break;
                case '\n': saida.Append("\\n"); break;
                case '\r': saida.Append("\\r"); break;
                case '\t': saida.Append("\\t"); break;
                default:
                    if (escalar.Value < 0x20 || escalar.Value == 0x7F)
                        saida.Append("\\u{").Append(escalar.Value.ToString("X")).Append('}');
                    else
                        saida.Append(escalar.ToString());
                    break;
            }
        }

        return saida.Append('"').ToString();
    }
}
