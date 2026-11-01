using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarkDocs.Core.Tests;

/// <summary>
/// O que as três suítes compartilham: as fontes, os templates de teste e a
/// leitura de regras do <c>/template.typ</c> (§§1.2 e 2 de <c>Docs/Testes.md</c>).
/// </summary>
public static class Apoio
{
    // ─── Fontes ─────────────────────────────────────────────────────────

    /// <summary>
    /// As quatro faces que os templates de teste exigem. O motor não vê as
    /// fontes do sistema: só usa o que entregamos.
    /// </summary>
    public static IReadOnlyList<FonteEmbarcada> Fontes { get; } =
        new[] { "LibertinusSerif-Regular", "LibertinusSerif-Bold", "LibertinusSerif-Italic", "LibertinusSerif-BoldItalic" }
            .Select(nome => Path.Combine(AppContext.BaseDirectory, "Recursos", nome + ".otf"))
            .Where(File.Exists)
            .Select(caminho => new FonteEmbarcada("Libertinus Serif", File.ReadAllBytes(caminho)))
            .ToList();

    public static RenderizadorPDF Renderizador() => new(Fontes);

    /// <summary>Um PNG de 1 × 1 pixel: o menor ativo que o motor aceita como imagem.</summary>
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    // ─── Templates de teste ─────────────────────────────────────────────

    /// <summary><c>Apoio.templateMinimo</c>: dois níveis de título e rodapé "Página X de Y".</summary>
    public const string TemplateMinimoJson = """
    {
      "familia": "documento-de-negocio",
      "esquema": 1,
      "id": "teste",
      "nome": "Template de teste",
      "fontes": ["Libertinus Serif"],
      "pagina": {
        "papel": "a4",
        "margem": { "topo": "28mm", "base": "24mm", "esquerda": "22mm", "direita": "22mm" }
      },
      "texto": { "familia": "Libertinus Serif", "tamanho": "11pt", "cor": "#1c1917", "idioma": "pt" },
      "paragrafo": { "justificado": true, "entrelinha": "0.65em", "espacoEntre": "8pt" },
      "titulos": [
        { "nivel": 1, "tamanho": "20pt", "alinhamento": "centro", "caixa": "alta",
          "espacoAntes": "0pt", "espacoDepois": "24pt" },
        { "nivel": 2, "tamanho": "13pt", "alinhamento": "esquerda",
          "espacoAntes": "24pt", "espacoDepois": "8pt" }
      ],
      "tabela": {
        "filete": "#d6d3d1", "espessura": "0.5pt", "respiro": "4pt",
        "cabecalhoForte": true, "repetirCabecalho": true
      },
      "estilosDeTexto": {
        "nota": { "tamanho": "9pt", "cor": "#57534e" }
      },
      "moldura": {
        "paginas": {
          "todas": {
            "rodape": {
              "afastamento": "14mm",
              "estilo": "nota",
              "direita": [
                { "peca": "texto", "valor": "Página " },
                { "peca": "campo", "campo": "numeroDaPagina" },
                { "peca": "texto", "valor": " de " },
                { "peca": "campo", "campo": "totalDePaginas" }
              ]
            }
          }
        }
      }
    }
    """;

    public static TemplateCarregado TemplateMinimo() => Carregar(Encoding.UTF8.GetBytes(TemplateMinimoJson));

    /// <summary>Os ajustes que aparecem na captura do inspetor.</summary>
    public static Dictionary<string, ValorDeEixo> AjustesDaCaptura => new()
    {
        ["pagina.papel"] = new ValorDeEixo.Escolha("a4"),
        ["pagina.margem.topo"] = new ValorDeEixo.Medida(30, "mm"),
        ["pagina.margem.base"] = new ValorDeEixo.Medida(32, "mm"),
        ["pagina.margem.esquerda"] = new ValorDeEixo.Medida(34, "mm"),
        ["pagina.margem.direita"] = new ValorDeEixo.Medida(56, "mm"),
        ["texto.familia"] = new ValorDeEixo.FamiliaTipografica("Libertinus Serif"),
        ["texto.tamanho"] = new ValorDeEixo.Medida(9.5, "pt"),
        ["texto.cor"] = new ValorDeEixo.Cor("#000000"),
        ["paragrafo.justificado"] = new ValorDeEixo.Alternancia(true),
        ["paragrafo.entrelinha"] = new ValorDeEixo.Medida(0.65, "em"),
        ["paragrafo.espacoEntre"] = new ValorDeEixo.Medida(2, "em"),
        ["titulos.1.tamanho"] = new ValorDeEixo.Medida(24.5, "pt"),
        ["titulos.1.alinhamento"] = new ValorDeEixo.Escolha("centro"),
        ["titulos.1.caixa"] = new ValorDeEixo.Escolha("alta"),
        ["titulos.2.tamanho"] = new ValorDeEixo.Medida(21, "pt"),
        ["titulos.2.alinhamento"] = new ValorDeEixo.Escolha("esquerda"),
        ["titulos.3.tamanho"] = new ValorDeEixo.Medida(11, "pt"),
        ["titulos.3.alinhamento"] = new ValorDeEixo.Escolha("centro"),
        ["tabela.espessura"] = new ValorDeEixo.Medida(0.5, "pt"),
        ["tabela.respiro"] = new ValorDeEixo.Medida(5, "pt"),
        ["tabela.repetirCabecalho"] = new ValorDeEixo.Alternancia(true),
    };

    /// <summary><c>Apoio.templateDaCaptura</c>: o mínimo com os ajustes da captura.</summary>
    public static TemplateCarregado TemplateDaCaptura() => TemplateMinimo().ComAjustes(AjustesDaCaptura);

    public static TemplateCarregado Carregar(byte[] json, IReadOnlyDictionary<string, byte[]>? ativos = null) =>
        RegistroDeGeradores.Carregar("documento-de-negocio", json, ativos ?? new Dictionary<string, byte[]>());

    // ─── Variações do JSON ──────────────────────────────────────────────

    /// <summary>
    /// O JSON do mínimo com alterações por caminho pontuado —
    /// <c>"titulos.0.familia"</c>, <c>"moldura"</c>. <c>null</c> remove o campo.
    /// Os valores são qualquer coisa que o <c>System.Text.Json</c> serialize:
    /// strings, números, listas, objetos anônimos.
    /// </summary>
    public static byte[] Json(params (string Caminho, object? Valor)[] alteracoes)
    {
        var raiz = JsonNode.Parse(TemplateMinimoJson);
        foreach (var (caminho, valor) in alteracoes)
            raiz = Definir(valor, raiz, caminho.Split('.'));
        return Encoding.UTF8.GetBytes(raiz!.ToJsonString());
    }

    public static TemplateCarregado TemplateMinimo(params (string Caminho, object? Valor)[] alteracoes) =>
        Carregar(Json(alteracoes));

    private static JsonNode? Definir(object? valor, JsonNode? no, ReadOnlySpan<string> caminho)
    {
        if (caminho.IsEmpty) return valor is null ? null : JsonSerializer.SerializeToNode(valor);
        var chave = caminho[0];
        var resto = caminho[1..];

        if (no is JsonArray lista && int.TryParse(chave, out var indice))
        {
            if (indice < lista.Count)
            {
                var atual = lista[indice];
                lista[indice] = null;
                lista[indice] = Definir(valor, atual, resto);
            }
            else
            {
                lista.Add(Definir(valor, new JsonObject(), resto));
            }
            return lista;
        }

        var dicionario = no as JsonObject ?? new JsonObject();
        if (resto.IsEmpty && valor is null)
        {
            dicionario.Remove(chave);
        }
        else
        {
            var atual = dicionario[chave] ?? new JsonObject();
            dicionario.Remove(chave);
            dicionario[chave] = Definir(valor, atual, resto);
        }
        return dicionario;
    }

    // ─── Erros ──────────────────────────────────────────────────────────

    /// <summary>O erro lançado, ou <c>null</c> se nada foi lançado.</summary>
    public static ErroTypst? Erro(Func<object?> corpo)
    {
        try
        {
            corpo();
            return null;
        }
        catch (ErroTypst erro)
        {
            return erro;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>O <c>Campo</c> de um <c>TemplateInvalido</c>, ou <c>null</c> para qualquer outro caso.</summary>
    public static string? CampoInvalido(Func<object?> corpo) =>
        Erro(corpo) is ErroTypst.TemplateInvalido invalido ? invalido.Campo : null;

    // ─── Regras do `/template.typ` (§1.2) ───────────────────────────────

    /// <summary>
    /// A regra que começa com <paramref name="prefixo"/>, inteira: vai até a
    /// quebra de linha em que parênteses, colchetes e chaves estão todos
    /// fechados. Texto entre aspas não conta.
    /// </summary>
    public static string? Regra(string prefixo, string typ)
    {
        var inicio = typ.IndexOf(prefixo, StringComparison.Ordinal);
        if (inicio < 0) return null;

        var profundidade = 0;
        var emTexto = false;
        var escapado = false;

        for (var indice = inicio; indice < typ.Length; indice++)
        {
            var caractere = typ[indice];
            if (emTexto)
            {
                if (escapado) escapado = false;
                else if (caractere == '\\') escapado = true;
                else if (caractere == '"') emTexto = false;
            }
            else
            {
                switch (caractere)
                {
                    case '"': emTexto = true; break;
                    case '(' or '[' or '{': profundidade++; break;
                    case ')' or ']' or '}': profundidade--; break;
                    case '\n' when profundidade <= 0: return typ[inicio..indice];
                }
            }
        }
        return typ[inicio..];
    }

    /// <summary>
    /// As linhas não vazias do <c>.typ</c> depois de tirar a regra — é o que a
    /// asserção diferencial compara (§4.8).
    /// </summary>
    public static IReadOnlyList<string> Linhas(string typ, string semARegra)
    {
        var restante = typ;
        if (Regra(semARegra, typ) is { } regra)
        {
            var posicao = restante.IndexOf(regra, StringComparison.Ordinal);
            if (posicao >= 0) restante = restante.Remove(posicao, regra.Length);
        }
        return restante.Split('\n')
            .Select(l => l.Trim(' ', '\t'))
            .Where(l => l.Length > 0)
            .ToList();
    }

    // ─── Documentos ─────────────────────────────────────────────────────

    /// <summary>O documento das medições: três páginas, com uma tabela de 80 linhas.</summary>
    public static string DocumentoDeTresPaginas => $$"""
        # Contrato de prestação de serviços

        Conforme tratado, segue o contrato para **Silva & Cia #2**, com as
        cláusulas que *as partes* acordaram. O valor combinado é de 1/3 do
        orçamento inicial, a ser pago conforme o cronograma abaixo.

        ## Objeto

        O presente instrumento tem por objeto a prestação de serviços de
        consultoria técnica, descritos em detalhe no anexo. A contratada se
        obriga a executar os serviços com zelo e diligência, observando os
        prazos pactuados e as normas técnicas aplicáveis a cada etapa.

        > O descumprimento de qualquer cláusula sujeita a parte infratora às
        > penalidades previstas na cláusula décima.

        ## Cronograma

        {{TabelaLonga(80)}}

        ---

        ## Disposições finais

        1. O contrato vigora por doze meses.
        2. A renovação depende de termo aditivo.
           - Com aviso prévio de trinta dias.
           - Por escrito.
        3. Foro da comarca de São Paulo.

        São Paulo, data da assinatura.
        """ + "\n";

    /// <summary>Uma tabela cujo cabeçalho ("Descrição") não se repete em linha nenhuma.</summary>
    public static string TabelaLonga(int linhas)
    {
        var texto = new StringBuilder("| Etapa | Descrição | Prazo | Valor |\n|---|---|:-:|--:|\n");
        for (var i = 1; i <= linhas; i++)
            texto.Append($"| {i} | Etapa de execução número {i} | {i * 3} dias | R$ {i * 150},00 |\n");
        return texto.ToString();
    }
}
