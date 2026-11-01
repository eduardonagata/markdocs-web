using System.Text.Json.Serialization;
using MarkDocs.Core;

namespace MarkDocs.WebApi.EndpointHandlers.Documentos.Dto;

/// <summary>
/// Um controle de formatação, como a tela o desenha. O <c>Id</c> é opaco: a
/// tela o devolve num ajuste sem nunca decompô-lo.
/// </summary>
public sealed record EixoResponse(string Id, string Grupo, string Rotulo, DominioResponse Dominio, ValorDeEixoDto Valor)
{
    public static EixoResponse De(EixoDeFormatacao eixo) =>
        new(eixo.Id, eixo.Grupo, eixo.Rotulo, DominioResponse.De(eixo.Dominio), ValorDeEixoDto.De(eixo.Valor));
}

/// <summary>O domínio de um eixo — e com ele, os limites. Cinco formas, pelo campo <c>tipo</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(MedidaDominio), "medida")]
[JsonDerivedType(typeof(EscolhaDominio), "escolha")]
[JsonDerivedType(typeof(AlternanciaDominio), "alternancia")]
[JsonDerivedType(typeof(CorDominio), "cor")]
[JsonDerivedType(typeof(FamiliaTipograficaDominio), "familiaTipografica")]
public abstract record DominioResponse
{
    public static DominioResponse De(DominioDeEixo dominio) => dominio switch
    {
        DominioDeEixo.Medida m => new MedidaDominio(m.Min, m.Max, m.Passo, m.Unidade),
        DominioDeEixo.Escolha e => new EscolhaDominio(e.Opcoes.Select(o => new OpcaoResponse(o.Id, o.Rotulo)).ToList()),
        DominioDeEixo.Alternancia => new AlternanciaDominio(),
        DominioDeEixo.Cor => new CorDominio(),
        DominioDeEixo.FamiliaTipografica f => new FamiliaTipograficaDominio(f.Disponiveis),
        _ => throw new InvalidOperationException($"domínio sem DTO: {dominio}"),
    };
}

public sealed record MedidaDominio(double Min, double Max, double Passo, string Unidade) : DominioResponse;
public sealed record EscolhaDominio(IReadOnlyList<OpcaoResponse> Opcoes) : DominioResponse;
public sealed record AlternanciaDominio : DominioResponse;
public sealed record CorDominio : DominioResponse;
public sealed record FamiliaTipograficaDominio(IReadOnlyList<string> Disponiveis) : DominioResponse;
public sealed record OpcaoResponse(string Id, string Rotulo);

/// <summary>O valor de um eixo, nos dois sentidos. Cinco formas, pelo campo <c>tipo</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(MedidaValor), "medida")]
[JsonDerivedType(typeof(EscolhaValor), "escolha")]
[JsonDerivedType(typeof(AlternanciaValor), "alternancia")]
[JsonDerivedType(typeof(CorValor), "cor")]
[JsonDerivedType(typeof(FamiliaTipograficaValor), "familiaTipografica")]
public abstract record ValorDeEixoDto
{
    public static ValorDeEixoDto De(ValorDeEixo valor) => valor switch
    {
        ValorDeEixo.Medida m => new MedidaValor(m.Valor, m.Unidade),
        ValorDeEixo.Escolha e => new EscolhaValor(e.Id),
        ValorDeEixo.Alternancia a => new AlternanciaValor(a.Ligado),
        ValorDeEixo.Cor c => new CorValor(c.Hexadecimal),
        ValorDeEixo.FamiliaTipografica f => new FamiliaTipograficaValor(f.Familia),
        _ => throw new InvalidOperationException($"valor sem DTO: {valor}"),
    };

    public abstract ValorDeEixo ParaCore();
}

public sealed record MedidaValor(double Valor, string Unidade) : ValorDeEixoDto
{
    public override ValorDeEixo ParaCore() => new ValorDeEixo.Medida(Valor, Unidade);
}

public sealed record EscolhaValor(string Id) : ValorDeEixoDto
{
    public override ValorDeEixo ParaCore() => new ValorDeEixo.Escolha(Id);
}

public sealed record AlternanciaValor(bool Ligado) : ValorDeEixoDto
{
    public override ValorDeEixo ParaCore() => new ValorDeEixo.Alternancia(Ligado);
}

public sealed record CorValor(string Hexadecimal) : ValorDeEixoDto
{
    public override ValorDeEixo ParaCore() => new ValorDeEixo.Cor(Hexadecimal);
}

public sealed record FamiliaTipograficaValor(string Familia) : ValorDeEixoDto
{
    public override ValorDeEixo ParaCore() => new ValorDeEixo.FamiliaTipografica(Familia);
}
