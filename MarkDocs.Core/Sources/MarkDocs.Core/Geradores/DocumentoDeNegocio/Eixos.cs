using MarkDocs.Core.Nucleo;
using static MarkDocs.Core.Geradores.DocumentoDeNegocio.TemplateDeNegocio;

namespace MarkDocs.Core.Geradores.DocumentoDeNegocio;

/// <summary>
/// Os eixos de formatação da família <c>documento-de-negocio</c>.
/// </summary>
/// <remarks>
/// Este arquivo é a <b>única</b> tradução entre o esquema privado da família e
/// a forma apagada que o app desenha. Duas direções, inversas uma da outra:
/// <c>Eixos(de:)</c> vai do esquema aos eixos; <c>Aplicando</c> vai dos
/// ajustes ao esquema. Eixo novo é <b>um caso em cada uma</b>, mais o campo no
/// esquema. Nada em <c>Nucleo/Emissao/</c>, e nenhuma linha de Typst.
/// </remarks>
internal sealed partial class DocumentoDeNegocio
{
    /// <summary>
    /// Os níveis de título que esta família admite. <b>A faixa é da família,
    /// não do template</b>: declarar 1 a 3 fixo evita o buraco de um nível
    /// presente no Markdown e ausente do inspetor. E o Markdown <b>não
    /// participa disso</b>.
    /// </summary>
    public static readonly IReadOnlyList<int> NiveisDeTitulo = [1, 2, 3];

    /// <summary>
    /// O rótulo de "sem família própria". Não é nome de fonte nenhuma, e é por
    /// isso que pode ser um sentinela.
    /// </summary>
    public const string Herdado = "— herdar do corpo —";

    // ─── Esquema → eixos ────────────────────────────────────────────────

    public static IReadOnlyList<EixoDeFormatacao> Eixos(TemplateDeNegocio template, IReadOnlyList<string> fontes)
    {
        var eixos = new List<EixoDeFormatacao>
        {
            new("pagina.papel", "Papel", "Tamanho",
                new DominioDeEixo.Escolha([
                    new Opcao("a4", "A4"),
                    new Opcao("a5", "A5"),
                    new Opcao(Papel.Carta.Texto(), "Carta"),
                    new Opcao(Papel.Oficio.Texto(), "Ofício"),
                ]),
                new ValorDeEixo.Escolha(template.Pagina.Papel.Texto())),
        };

        // A margem é declaração de quem monta o template, de quantos
        // milímetros o conteúdo deve respeitar — o app não sabe onde está a
        // tinta do timbre e não tenta descobrir.
        foreach (var (sufixo, rotulo, valor) in new[]
                 {
                     ("topo", "Margem superior", template.Pagina.Margem.Topo),
                     ("base", "Margem inferior", template.Pagina.Margem.Base),
                     ("esquerda", "Margem esquerda", template.Pagina.Margem.Esquerda),
                     ("direita", "Margem direita", template.Pagina.Margem.Direita),
                 })
        {
            eixos.Add(new($"pagina.margem.{sufixo}", "Papel", rotulo,
                new DominioDeEixo.Medida(0, 60, 1, "mm"),
                Valor(valor)));
        }

        // Texto
        eixos.AddRange([
            new("texto.familia", "Texto", "Fonte",
                new DominioDeEixo.FamiliaTipografica(fontes.ToList()),
                new ValorDeEixo.FamiliaTipografica(template.Texto.Familia)),
            new("texto.tamanho", "Texto", "Corpo",
                new DominioDeEixo.Medida(7, 18, 0.5, "pt"),
                Valor(template.Texto.Tamanho)),
            new("texto.cor", "Texto", "Cor do texto",
                new DominioDeEixo.Cor(),
                new ValorDeEixo.Cor(template.Texto.Cor.Hexadecimal)),
            new("paragrafo.justificado", "Texto", "Justificar",
                new DominioDeEixo.Alternancia(),
                new ValorDeEixo.Alternancia(template.Paragrafo.Justificado)),
            new("paragrafo.entrelinha", "Texto", "Entrelinha",
                new DominioDeEixo.Medida(0.4, 1.6, 0.05, "em"),
                Valor(template.Paragrafo.Entrelinha)),
            new("paragrafo.espacoEntre", "Texto", "Espaço entre parágrafos",
                new DominioDeEixo.Medida(0, 2, 0.05, "em"),
                Valor(template.Paragrafo.EspacoEntre)),
        ]);

        // Títulos: a faixa é da família, os valores vêm do array.
        foreach (var nivel in NiveisDeTitulo)
        {
            var declarado = template.Titulos.FirstOrDefault(t => t.Nivel == nivel);
            var grupo = $"Título {nivel}";

            eixos.AddRange([
                // A primeira opção é "herdar do corpo", que é o que um nível
                // sem entrada no array mostra até alguém tocá-lo.
                new($"titulos.{nivel}.familia", grupo, "Fonte",
                    new DominioDeEixo.FamiliaTipografica([Herdado, .. fontes]),
                    new ValorDeEixo.FamiliaTipografica(declarado?.Familia ?? Herdado)),
                new($"titulos.{nivel}.tamanho", grupo, "Tamanho",
                    new DominioDeEixo.Medida(8, 32, 0.5, "pt"),
                    new ValorDeEixo.Medida(declarado?.Tamanho.Valor ?? TamanhoHerdado(template), "pt")),
                new($"titulos.{nivel}.alinhamento", grupo, "Alinhamento",
                    new DominioDeEixo.Escolha([
                        new Opcao("esquerda", "À esquerda"),
                        new Opcao("centro", "Centralizado"),
                        new Opcao("direita", "À direita"),
                    ]),
                    new ValorDeEixo.Escolha(TextoDe(declarado?.Alinhamento) ?? "esquerda")),
                new($"titulos.{nivel}.caixa", grupo, "Caixa",
                    new DominioDeEixo.Escolha([
                        new Opcao("normal", "Normal"),
                        new Opcao("alta", "Versais"),
                    ]),
                    new ValorDeEixo.Escolha(declarado?.Caixa == Caixa.Alta ? "alta" : "normal")),
            ]);
        }

        // Tabela
        eixos.AddRange([
            new("tabela.espessura", "Tabela", "Espessura do filete",
                new DominioDeEixo.Medida(0, 3, 0.1, "pt"),
                Valor(template.Tabela.Espessura)),
            new("tabela.respiro", "Tabela", "Respiro das células",
                new DominioDeEixo.Medida(0, 12, 0.5, "pt"),
                Valor(template.Tabela.Respiro)),
            new("tabela.repetirCabecalho", "Tabela", "Repetir cabeçalho nas páginas",
                new DominioDeEixo.Alternancia(),
                new ValorDeEixo.Alternancia(template.Tabela.RepetirCabecalho)),
        ]);

        return eixos;
    }

    private static ValorDeEixo.Medida Valor(Medida medida) =>
        new(medida.Valor, Medida.TextoDa(medida.Unidade));

    private static string? TextoDe(AlinhamentoHorizontal? alinhamento) => alinhamento switch
    {
        null => null,
        AlinhamentoHorizontal.Esquerda => "esquerda",
        AlinhamentoHorizontal.Centro => "centro",
        AlinhamentoHorizontal.Direita => "direita",
        _ => "justificado",
    };

    /// <summary>
    /// O tamanho que um nível não declarado mostra no inspetor. Sem entrada no
    /// array, o Typst daria ao título o tamanho padrão dele; mostrar o corpo é
    /// o mais honesto: é o que o leitor vê se nada for tocado.
    /// </summary>
    private static double TamanhoHerdado(TemplateDeNegocio template) => template.Texto.Tamanho.Valor;

    // ─── Ajustes → esquema ──────────────────────────────────────────────

    /// <summary>
    /// Aplica a camada, <b>limitando cada valor ao domínio</b>. É aqui que "um
    /// controle não consegue produzir um valor inválido" deixa de depender da
    /// interface: mesmo um ajuste editado à mão entra pelo mesmo funil, e um
    /// valor fora da faixa é aparado, não recusado.
    /// </summary>
    public static TemplateDeNegocio Aplicando(IReadOnlyDictionary<string, ValorDeEixo> ajustes, TemplateDeNegocio original)
    {
        var template = original.Copiar();
        var dominios = Eixos(original, original.Fontes).ToDictionary(e => e.Id, e => e.Dominio);

        foreach (var (id, valor) in ajustes)
        {
            // `id` desconhecido é ignorado em silêncio: ajuste ausente cai no
            // valor do template, então degrada em vez de quebrar.
            if (!dominios.TryGetValue(id, out var dominio)) continue;
            Aplicar(Limitado(valor, dominio), template, id);
        }
        return template;
    }

    private static ValorDeEixo Limitado(ValorDeEixo valor, DominioDeEixo dominio) => (valor, dominio) switch
    {
        (ValorDeEixo.Medida m, DominioDeEixo.Medida d) =>
            m with { Valor = Math.Min(Math.Max(m.Valor, d.Min), d.Max) },

        // Fora do conjunto, o ajuste é descartado: devolver o valor cru
        // deixaria uma string arbitrária chegar ao emissor.
        (ValorDeEixo.Escolha e, DominioDeEixo.Escolha d) =>
            d.Opcoes.Any(o => o.Id == e.Id) ? e : new ValorDeEixo.Escolha(d.Opcoes.FirstOrDefault()?.Id ?? ""),

        // Esta é a validação que mais importa: uma fonte não embarcada seria
        // substituída pelo Typst em silêncio, produzindo um documento com a
        // tipografia errada parecendo certo.
        (ValorDeEixo.FamiliaTipografica f, DominioDeEixo.FamiliaTipografica d) =>
            d.Disponiveis.Contains(f.Familia) ? f : new ValorDeEixo.FamiliaTipografica(Herdado),

        // Alternância, cor, ou domínio e valor de tipos diferentes: o ajuste
        // segue, e quem o aplica o ignora se não for deste eixo.
        _ => valor,
    };

    private static void Aplicar(ValorDeEixo valor, TemplateDeNegocio template, string id)
    {
        switch (id)
        {
            case "pagina.papel":
                if (valor is ValorDeEixo.Escolha e && Papeis.Ler(e.Id) is { } papel) template.Pagina.Papel = papel;
                break;
            case "pagina.margem.topo":
                if (MedidaDe(valor) is { } topo) template.Pagina.Margem.Topo = topo;
                break;
            case "pagina.margem.base":
                if (MedidaDe(valor) is { } @base) template.Pagina.Margem.Base = @base;
                break;
            case "pagina.margem.esquerda":
                if (MedidaDe(valor) is { } esquerda) template.Pagina.Margem.Esquerda = esquerda;
                break;
            case "pagina.margem.direita":
                if (MedidaDe(valor) is { } direita) template.Pagina.Margem.Direita = direita;
                break;

            case "texto.familia":
                if (valor is ValorDeEixo.FamiliaTipografica f && f.Familia != Herdado) template.Texto.Familia = f.Familia;
                break;
            case "texto.tamanho":
                if (MedidaDe(valor) is { } tamanho) template.Texto.Tamanho = tamanho;
                break;
            case "texto.cor":
                if (valor is ValorDeEixo.Cor c && Cor.Ler(c.Hexadecimal) is { } cor) template.Texto.Cor = cor;
                break;

            case "paragrafo.justificado":
                if (valor is ValorDeEixo.Alternancia j) template.Paragrafo.Justificado = j.Ligado;
                break;
            case "paragrafo.entrelinha":
                if (MedidaDe(valor) is { } entrelinha) template.Paragrafo.Entrelinha = entrelinha;
                break;
            case "paragrafo.espacoEntre":
                if (MedidaDe(valor) is { } espaco) template.Paragrafo.EspacoEntre = espaco;
                break;

            case "tabela.espessura":
                if (MedidaDe(valor) is { } espessura) template.Tabela.Espessura = espessura;
                break;
            case "tabela.respiro":
                if (MedidaDe(valor) is { } respiro) template.Tabela.Respiro = respiro;
                break;
            case "tabela.repetirCabecalho":
                if (valor is ValorDeEixo.Alternancia r) template.Tabela.RepetirCabecalho = r.Ligado;
                break;

            default:
                AplicarEmTitulo(valor, template, id);
                break;
        }
    }

    /// <summary>
    /// Os eixos de título, que têm o nível no <c>id</c>. <b>Tocar um nível
    /// ausente materializa a entrada</b> no array: é o que faz o inspetor
    /// continuar sendo função do template, sem buraco, e sem que o conteúdo
    /// seja consultado em momento nenhum.
    /// </summary>
    private static void AplicarEmTitulo(ValorDeEixo valor, TemplateDeNegocio template, string id)
    {
        var partes = id.Split('.');
        if (partes.Length != 3 || partes[0] != "titulos"
            || !int.TryParse(partes[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var nivel)
            || !NiveisDeTitulo.Contains(nivel))
            return;

        var titulo = template.Titulos.FirstOrDefault(t => t.Nivel == nivel);
        if (titulo is null)
        {
            titulo = new Titulo
            {
                Nivel = nivel,
                Familia = null,
                Tamanho = template.Texto.Tamanho,
                Alinhamento = null,
                Caixa = null,
                Forte = true,
                Cor = null,
                EspacoAntes = new Medida(1, UnidadeDeMedida.Em),
                EspacoDepois = new Medida(0.5, UnidadeDeMedida.Em),
            };
            template.Titulos.Add(titulo);
            template.Titulos.Sort((a, b) => a.Nivel.CompareTo(b.Nivel));
        }

        switch (partes[2])
        {
            case "familia":
                // O sentinela volta a `null`, que é "herda do corpo" no esquema.
                if (valor is ValorDeEixo.FamiliaTipografica f)
                    titulo.Familia = f.Familia == Herdado ? null : f.Familia;
                break;
            case "tamanho":
                if (MedidaDe(valor) is { } tamanho) titulo.Tamanho = tamanho;
                break;
            case "alinhamento":
                if (valor is ValorDeEixo.Escolha a)
                    titulo.Alinhamento = a.Id switch
                    {
                        "esquerda" => AlinhamentoHorizontal.Esquerda,
                        "centro" => AlinhamentoHorizontal.Centro,
                        "direita" => AlinhamentoHorizontal.Direita,
                        "justificado" => AlinhamentoHorizontal.Justificado,
                        _ => null,
                    };
                break;
            case "caixa":
                if (valor is ValorDeEixo.Escolha c)
                    titulo.Caixa = c.Id switch { "normal" => Caixa.Normal, "alta" => Caixa.Alta, _ => null };
                break;
        }
    }

    private static Medida? MedidaDe(ValorDeEixo valor) =>
        valor is ValorDeEixo.Medida m && Medida.UnidadeDe(m.Unidade) is { } unidade
            ? new Medida(m.Valor, unidade)
            : null;
}
