using MarkDocs.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace MarkDocs.Abstractions;

/// <summary>
/// A formatação de um documento. Há uma classe base e <b>uma classe concreta
/// por família</b> do <c>MarkDocs.Core</c>.
/// </summary>
/// <remarks>
/// A base não conhece família nenhuma: limita o valor ao domínio que o eixo
/// trouxe e entrega à classe concreta, que sabe onde gravá-lo. Os limites vêm
/// do <c>MarkDocs.Core</c> (<see cref="TemplateCarregado.Eixos"/>); nenhum número
/// de limite é escrito aqui.
/// </remarks>
public abstract class Formatacao
{
    public abstract string Familia { get; }
    public int Esquema { get; set; }

    /// <summary>
    /// As famílias tipográficas que esta formatação oferece e exige. Gravado,
    /// não derivado: é também o domínio do eixo <c>texto.familia</c>, e
    /// derivá-la reduziria a escolha de fonte à fonte já escolhida.
    /// </summary>
    public List<string> Fontes { get; set; } = [];

    /// <summary>
    /// Recebe o eixo inteiro, e não só o id, para que os limites venham do
    /// <c>MarkDocs.Core</c> e não sejam repetidos aqui.
    /// </summary>
    public void Aplicar(EixoDeFormatacao eixo, ValorDeEixo valor) =>
        AplicarLimitado(eixo.Id, Limitar(valor, eixo.Dominio));

    protected abstract void AplicarLimitado(string eixo, ValorDeEixo valor);

    /// <summary>O sentinela de "herdar do corpo", que cada família define.</summary>
    protected abstract string Herdado { get; }

    /// <summary>Transcrição de <c>limitado(_:a:)</c> em <c>Eixos.swift</c>.</summary>
    private ValorDeEixo Limitar(ValorDeEixo valor, DominioDeEixo dominio) => (valor, dominio) switch
    {
        (ValorDeEixo.Medida m, DominioDeEixo.Medida d) =>
            m with { Valor = Math.Clamp(m.Valor, d.Min, d.Max) },
        (ValorDeEixo.Escolha e, DominioDeEixo.Escolha d) =>
            d.Opcoes.Any(o => o.Id == e.Id) ? e : new ValorDeEixo.Escolha(d.Opcoes.FirstOrDefault()?.Id ?? ""),
        (ValorDeEixo.FamiliaTipografica f, DominioDeEixo.FamiliaTipografica d) =>
            d.Disponiveis.Contains(f.Familia) ? f : new ValorDeEixo.FamiliaTipografica(Herdado),
        _ => valor,
    };

    /// <summary>
    /// Cópia profunda: aplicar um template não pode compartilhar objetos com
    /// ele. Pede <see cref="SerializacaoDaFormatacao.RegistrarBson"/>.
    /// </summary>
    public Formatacao Copiar() =>
        (Formatacao)BsonSerializer.Deserialize(this.ToBson(GetType()), GetType());

    /// <summary>
    /// O <c>template.json</c> que o <c>MarkDocs.Core</c> carrega. <c>id</c>,
    /// <c>nome</c> e <c>tipoDocumental</c> vêm do documento, porque não são
    /// formatação.
    /// </summary>
    public abstract byte[] ParaTemplateJson(string nome, string? tipoDocumental);
}
