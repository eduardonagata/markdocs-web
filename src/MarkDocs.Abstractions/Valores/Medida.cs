using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkDocs.Core;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace MarkDocs.Abstractions.Valores;

public enum UnidadeDeMedida { Mm, Cm, Pt, Em, Percentual }

/// <summary>
/// <c>"20mm"</c>, <c>"11pt"</c>, <c>"0.65em"</c>, <c>"30%"</c>. Não há campo em
/// que se escreva uma expressão.
/// </summary>
/// <remarks>
/// Em disco — no <c>template.json</c> e no MongoDB — uma medida é a
/// <b>string</b> <c>"11pt"</c>. Na memória, é o record <c>Medida(11, Pt)</c>. Os
/// dois atributos dizem aos dois serializadores como ela vira string e volta:
/// a declaração viaja com o tipo, e não há registro a esquecer.
/// </remarks>
[JsonConverter(typeof(MedidaJson))]
[BsonSerializer(typeof(MedidaBson))]
public readonly record struct Medida(double Valor, UnidadeDeMedida Unidade)
{
    // O texto de cada unidade, da mais longa para a mais curta.
    private static readonly (string Texto, UnidadeDeMedida Unidade)[] Sufixos =
    [
        ("mm", UnidadeDeMedida.Mm), ("cm", UnidadeDeMedida.Cm),
        ("pt", UnidadeDeMedida.Pt), ("em", UnidadeDeMedida.Em),
        ("%", UnidadeDeMedida.Percentual),
    ];

    /// <summary><c>"11pt"</c> → <c>Medida(11, Pt)</c>. Devolve false para qualquer coisa fora do formato.</summary>
    public static bool TentarLer(string? texto, out Medida medida)
    {
        medida = default;
        if (texto is null) return false;
        var limpo = texto.Trim();

        foreach (var (sufixo, unidade) in Sufixos)
        {
            if (!limpo.EndsWith(sufixo, StringComparison.Ordinal)) continue;

            // InvariantCulture: "0.65" é sempre ponto, qualquer que seja o
            // idioma do servidor. Com a cultura pt-BR, "0.65" não leria.
            var numero = limpo[..^sufixo.Length];
            if (!double.TryParse(numero, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture, out var valor) || !double.IsFinite(valor))
                return false;

            medida = new Medida(valor, unidade);
            return true;
        }
        return false;
    }

    /// <summary>A unidade de um eixo vem como texto (<c>"pt"</c>). Mesma tabela de sufixos.</summary>
    public static bool TentarDe(ValorDeEixo.Medida valor, out Medida medida)
    {
        medida = default;
        var achado = Array.Find(Sufixos, s => s.Texto == valor.Unidade);
        if (achado.Texto is null) return false;
        medida = new Medida(valor.Valor, achado.Unidade);
        return true;
    }

    /// <summary>O texto da unidade, como o template e o eixo a escrevem.</summary>
    public string TextoDaUnidade
    {
        get
        {
            var unidade = Unidade;
            return Array.Find(Sufixos, s => s.Unidade == unidade).Texto;
        }
    }

    /// <summary>
    /// <c>Medida(11, Pt)</c> → <c>"11pt"</c>; <c>Medida(0.65, Em)</c> →
    /// <c>"0.65em"</c>. Inteiro sai sem casas decimais, como no Swift.
    /// </summary>
    public override string ToString()
    {
        var numero = Valor == Math.Round(Valor) && Math.Abs(Valor) < 1e15
            ? ((long)Valor).ToString(CultureInfo.InvariantCulture)
            : Valor.ToString("G6", CultureInfo.InvariantCulture);
        return numero + TextoDaUnidade;
    }
}

public sealed class MedidaJson : JsonConverter<Medida>
{
    public override Medida Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes) =>
        Medida.TentarLer(leitor.GetString(), out var medida)
            ? medida
            : throw new JsonException($"“{leitor.GetString()}” não é uma medida");

    public override void Write(Utf8JsonWriter escritor, Medida medida, JsonSerializerOptions opcoes) =>
        escritor.WriteStringValue(medida.ToString());
}

public sealed class MedidaBson : SerializerBase<Medida>
{
    public override Medida Deserialize(BsonDeserializationContext contexto, BsonDeserializationArgs args)
    {
        var texto = contexto.Reader.ReadString();
        return Medida.TentarLer(texto, out var medida)
            ? medida
            : throw new FormatException($"“{texto}” não é uma medida");
    }

    public override void Serialize(BsonSerializationContext contexto, BsonSerializationArgs args, Medida medida) =>
        contexto.Writer.WriteString(medida.ToString());
}
