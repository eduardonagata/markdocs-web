namespace MarkDocs.Core;

/// <summary>
/// Um controle de formatação, <b>como dado</b>.
/// </summary>
/// <remarks>
/// A tela de formatação é função dos eixos declarados pela família, e o PDF é
/// função de (Markdown, template, ajustes). Não há um segundo estado a manter
/// em sincronia.
///
/// A consequência que decide o desenho: <b>o app não escreve interface por
/// família.</b> Ele não sabe o que é <c>documento-de-negocio</c>; sabe desenhar
/// um controle para um eixo cujo domínio é uma medida, e outro para um eixo
/// cujo domínio é uma escolha. Família nova chega com eixos novos e a tela já
/// os mostra.
/// </remarks>
/// <param name="Id">
/// <b>Opaco para o app</b>: <c>"titulos.2.tamanho"</c> é string para quem
/// desenha e endereço para quem interpreta. O app nunca decompõe, nunca
/// interpreta e nunca monta um.
/// </param>
/// <param name="Grupo">Para agrupar o inspetor: "Papel", "Texto", "Título 2", "Tabela".</param>
/// <param name="Rotulo">O rótulo do controle.</param>
/// <param name="Dominio">O domínio, que <b>carrega os limites</b>.</param>
/// <param name="Valor">O que <b>este</b> template diz hoje, já migrado e já com os ajustes aplicados.</param>
public sealed record EixoDeFormatacao(
    string Id,
    string Grupo,
    string Rotulo,
    DominioDeEixo Dominio,
    ValorDeEixo Valor);

/// <summary>
/// O domínio de um eixo. <b>Um controle não consegue produzir um valor
/// inválido</b>: a validação deixa de ser recusa depois do fato e passa a ser
/// inalcançável por construção.
/// </summary>
public abstract record DominioDeEixo
{
    private DominioDeEixo() { }

    public sealed record Medida(double Min, double Max, double Passo, string Unidade) : DominioDeEixo;

    public sealed record Escolha(IReadOnlyList<Opcao> Opcoes) : DominioDeEixo
    {
        public bool Equals(Escolha? outra) =>
            outra is not null && Opcoes.SequenceEqual(outra.Opcoes);

        public override int GetHashCode() =>
            Opcoes.Aggregate(17, (h, o) => HashCode.Combine(h, o));
    }

    public sealed record Alternancia : DominioDeEixo;

    public sealed record Cor : DominioDeEixo;

    /// <summary>
    /// Só as famílias <b>embarcadas</b>. É o que impede escolher uma fonte que
    /// o Typst substituiria em silêncio, produzindo um documento com a
    /// tipografia errada parecendo certo.
    /// </summary>
    public sealed record FamiliaTipografica(IReadOnlyList<string> Disponiveis) : DominioDeEixo
    {
        public bool Equals(FamiliaTipografica? outra) =>
            outra is not null && Disponiveis.SequenceEqual(outra.Disponiveis);

        public override int GetHashCode() =>
            Disponiveis.Aggregate(17, (h, f) => HashCode.Combine(h, f));
    }
}

public sealed record Opcao(string Id, string Rotulo);

/// <summary>O valor de um eixo. Cinco casos fechados.</summary>
public abstract record ValorDeEixo
{
    private ValorDeEixo() { }

    /// <summary><c>10.5</c>, <c>"pt"</c>.</summary>
    public sealed record Medida(double Valor, string Unidade) : ValorDeEixo;

    /// <summary>O <c>Id</c> de uma <see cref="Opcao"/>: <c>"centro"</c>.</summary>
    public sealed record Escolha(string Id) : ValorDeEixo;

    public sealed record Alternancia(bool Ligado) : ValorDeEixo;

    /// <summary><c>"#1c1917"</c>.</summary>
    public sealed record Cor(string Hexadecimal) : ValorDeEixo;

    /// <summary><c>"Libertinus Serif"</c>.</summary>
    public sealed record FamiliaTipografica(string Familia) : ValorDeEixo;
}
