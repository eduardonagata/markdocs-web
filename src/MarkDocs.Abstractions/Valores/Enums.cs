using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace MarkDocs.Abstractions.Valores;

// Em disco, cada caso é o identificador em camelCase (`Centro` → "centro",
// `TituloDoDocumento` → "tituloDoDocumento"). `Papel` é a exceção: os nomes são
// os do Typst, e dois não derivam do identificador.

[JsonConverter(typeof(EnumComoTextoJson<Papel>))]
public enum Papel
{
    [JsonStringEnumMemberName("a4")] A4,
    [JsonStringEnumMemberName("a5")] A5,
    [JsonStringEnumMemberName("us-letter")] Carta,
    [JsonStringEnumMemberName("us-legal")] Oficio,
}

[JsonConverter(typeof(EnumComoTextoJson<Alinhamento>))]
public enum Alinhamento { Esquerda, Centro, Direita, Justificado }

[JsonConverter(typeof(EnumComoTextoJson<Caixa>))]
public enum Caixa { Normal, Alta }

[JsonConverter(typeof(EnumComoTextoJson<CampoDerivado>))]
public enum CampoDerivado { TituloDoDocumento, TipoDocumental, NumeroDaPagina, TotalDePaginas }

/// <summary>
/// JSON: o conversor de enum do .NET, com camelCase. Existe só porque um
/// atributo não aceita argumentos de construtor.
/// </summary>
public sealed class EnumComoTextoJson<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where T : struct, Enum;

/// <summary>
/// A tabela de texto de um enum: <c>[JsonStringEnumMemberName]</c> quando
/// existe, senão camelCase — a mesma regra nos dois serializadores.
/// </summary>
public static class TextoDeEnum<T> where T : struct, Enum
{
    public static readonly IReadOnlyDictionary<T, string> ParaTexto = Enum.GetValues<T>().ToDictionary(
        v => v,
        v => typeof(T).GetField(v.ToString())!
                 .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
             ?? JsonNamingPolicy.CamelCase.ConvertName(v.ToString()));

    private static readonly Dictionary<string, T> DeTexto =
        ParaTexto.ToDictionary(par => par.Value, par => par.Key);

    public static bool TentarLer(string? texto, out T valor)
    {
        valor = default;
        return texto is not null && DeTexto.TryGetValue(texto, out valor);
    }
}

/// <summary>
/// BSON: a mesma regra do JSON, para o banco gravar exatamente o que o JSON
/// grava. <b>É a exceção ao atributo</b>: <c>[BsonSerializer]</c> não se aplica
/// a enum, e por isso este serializador é registrado na inicialização
/// (<see cref="SerializacaoDaFormatacao.RegistrarBson"/>).
/// </summary>
public sealed class EnumComoTextoBson<T> : SerializerBase<T> where T : struct, Enum
{
    public override T Deserialize(BsonDeserializationContext contexto, BsonDeserializationArgs args)
    {
        var texto = contexto.Reader.ReadString();
        return TextoDeEnum<T>.TentarLer(texto, out var valor)
            ? valor
            : throw new FormatException($"“{texto}” não é um {typeof(T).Name}");
    }

    public override void Serialize(BsonSerializationContext contexto, BsonSerializationArgs args, T valor) =>
        contexto.Writer.WriteString(TextoDeEnum<T>.ParaTexto[valor]);
}
