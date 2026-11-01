using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace MarkDocs.Abstractions.Valores;

/// <summary><c>"#1c1917"</c>: 3, 4, 6 ou 8 dígitos hexadecimais. Nada de nomes, nada de funções.</summary>
[JsonConverter(typeof(CorJson))]
[BsonSerializer(typeof(CorBson))]
public readonly record struct Cor(string Hexadecimal)
{
    public static bool TentarLer(string? texto, out Cor cor)
    {
        cor = default;
        var limpo = texto?.Trim();
        if (limpo is null || !limpo.StartsWith('#')) return false;

        var digitos = limpo[1..];
        if (digitos.Length is not (3 or 4 or 6 or 8) || !digitos.All(char.IsAsciiHexDigit))
            return false;

        cor = new Cor(limpo);
        return true;
    }

    public override string ToString() => Hexadecimal;
}

public sealed class CorJson : JsonConverter<Cor>
{
    public override Cor Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes) =>
        Cor.TentarLer(leitor.GetString(), out var cor)
            ? cor
            : throw new JsonException($"“{leitor.GetString()}” não é uma cor");

    public override void Write(Utf8JsonWriter escritor, Cor cor, JsonSerializerOptions opcoes) =>
        escritor.WriteStringValue(cor.ToString());
}

public sealed class CorBson : SerializerBase<Cor>
{
    public override Cor Deserialize(BsonDeserializationContext contexto, BsonDeserializationArgs args)
    {
        var texto = contexto.Reader.ReadString();
        return Cor.TentarLer(texto, out var cor)
            ? cor
            : throw new FormatException($"“{texto}” não é uma cor");
    }

    public override void Serialize(BsonSerializationContext contexto, BsonSerializationArgs args, Cor cor) =>
        contexto.Writer.WriteString(cor.ToString());
}
