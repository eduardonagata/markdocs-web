namespace MarkDocs.Core;

/// <summary>
/// O que o núcleo recebe. <b>Não tem nada da família dentro.</b>
/// </summary>
/// <remarks>
/// É a forma apagada que faz o isolamento valer: o gerador analisa o JSON no
/// seu próprio tipo privado, migra, e devolve isto — cujo <c>EmitirTyp</c> é
/// uma closure sobre aquele tipo. O núcleo nunca vê o esquema de família
/// nenhuma, e por isso não precisa mudar quando uma nasce.
/// </remarks>
public sealed class TemplateCarregado
{
    private readonly Func<string> _emitirTyp;
    private readonly Func<IReadOnlyDictionary<string, ValorDeEixo>, TemplateCarregado> _aplicarAjustes;

    public TemplateCarregado(
        string familia,
        int esquema,
        string nome,
        IReadOnlyList<string> fontesExigidas,
        IReadOnlyDictionary<string, byte[]> ativos,
        IReadOnlyList<EixoDeFormatacao>? eixos,
        Func<string> emitirTyp,
        Func<IReadOnlyDictionary<string, ValorDeEixo>, TemplateCarregado>? aplicarAjustes = null)
    {
        Familia = familia;
        Esquema = esquema;
        Nome = nome;
        FontesExigidas = fontesExigidas;
        Ativos = ativos;
        Eixos = eixos ?? [];
        _emitirTyp = emitirTyp;
        _aplicarAjustes = aplicarAjustes ?? (_ => throw new ErroTypst.TemplateInvalido(
            "ajustes", "esta família ainda não declara eixos de formatação"));
    }

    public string Familia { get; }
    public int Esquema { get; }
    public string Nome { get; }
    public IReadOnlyList<string> FontesExigidas { get; }
    public IReadOnlyDictionary<string, byte[]> Ativos { get; }

    /// <summary>
    /// Os controles de formatação que <b>este</b> template oferece, já com os
    /// valores que ele declara hoje. <b>A família decide quais eixos existem;
    /// o template decide em que valor cada um começa.</b>
    /// </summary>
    public IReadOnlyList<EixoDeFormatacao> Eixos { get; }

    /// <summary>Fecha sobre o template já tipado e migrado, dentro do gerador.</summary>
    internal string EmitirTyp() => _emitirTyp();

    /// <summary>
    /// Aplica uma camada de ajustes e devolve <b>outro</b>
    /// <see cref="TemplateCarregado"/>. Cada valor é limitado ao domínio do
    /// eixo; um <c>id</c> desconhecido é ignorado em silêncio — degrada em vez
    /// de quebrar.
    /// </summary>
    public TemplateCarregado ComAjustes(IReadOnlyDictionary<string, ValorDeEixo> ajustes) =>
        _aplicarAjustes(ajustes);
}
