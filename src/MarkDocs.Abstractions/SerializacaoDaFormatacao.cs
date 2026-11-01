using System.Text.Json;
using System.Text.Json.Serialization;
using MarkDocs.Abstractions.Formatacoes;
using MarkDocs.Abstractions.Valores;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace MarkDocs.Abstractions;

/// <summary>
/// O que não tem forma de atributo. <see cref="Medida"/> e <see cref="Cor"/>
/// trazem os próprios conversores; aqui ficam as opções de JSON da ponte com o
/// <c>MarkDocs.Core</c> e o registro global do BSON.
/// </summary>
public static class SerializacaoDaFormatacao
{
    /// <summary>
    /// As opções de JSON de <c>ParaTemplateJson</c> e <c>DeTemplateJson</c>. Os
    /// conversores de valor não estão aqui: vêm dos atributos dos tipos.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,          // Pagina → "pagina"
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,                   // "peca" não precisa vir primeiro
    };

    private static readonly Lock Trava = new();
    private static bool _registrado;

    /// <summary>
    /// Chamado uma vez, no início do processo, antes de qualquer acesso ao
    /// MongoDB. Idempotente: chamar de novo não faz nada (os testes sobem o
    /// app várias vezes).
    /// </summary>
    /// <remarks>
    /// O registro BSON é global ao processo e precisa vir <b>antes do primeiro
    /// uso</b>: na primeira vez que o driver lê ou grava uma classe, ele monta
    /// e congela o mapa dela.
    /// </remarks>
    public static void RegistrarBson()
    {
        lock (Trava)
        {
            if (_registrado) return;
            _registrado = true;

            // Convenção: os nomes em camelCase, para o banco ter a mesma forma
            // do template.json. Antes de qualquer mapa ser montado.
            ConventionRegistry.Register(
                "MarkDocs: camelCase",
                new ConventionPack { new CamelCaseElementNameConvention() },
                tipo => tipo.Namespace?.StartsWith("MarkDocs.", StringComparison.Ordinal) == true);

            // Enums: [BsonSerializer] não se aplica a enum.
            BsonSerializer.RegisterSerializer(new EnumComoTextoBson<Papel>());
            BsonSerializer.RegisterSerializer(new EnumComoTextoBson<Alinhamento>());
            BsonSerializer.RegisterSerializer(new EnumComoTextoBson<Caixa>());
            BsonSerializer.RegisterSerializer(new EnumComoTextoBson<CampoDerivado>());
            BsonSerializer.RegisterSerializer(new EnumComoTextoBson<PapelDoAtivo>());

            // Discriminadores: "familia" em Formatacao, "peca" em Peca.
            BsonSerializer.RegisterDiscriminatorConvention(
                typeof(Formatacao), new ScalarDiscriminatorConvention("familia"));
            BsonSerializer.RegisterDiscriminatorConvention(
                typeof(FormatacaoDocumentoDeNegocio.Peca), new ScalarDiscriminatorConvention("peca"));

            // As famílias conhecidas: o discriminador "documento-de-negocio"
            // precisa achar a classe ao ler um campo do tipo Formatacao.
            BsonClassMap.RegisterClassMap<FormatacaoDocumentoDeNegocio>();
        }
    }
}
