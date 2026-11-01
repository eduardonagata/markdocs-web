using System.Text.Json;

namespace MarkDocs.Core;

/// <summary>
/// Uma falha é sempre <b>erro de validação do template ou de conversão</b>, com
/// o campo ou o arquivo, a linha e a mensagem. Nunca é um crash: o pacote
/// garante isso na fronteira com o motor.
/// </summary>
/// <remarks>
/// Transcreve o <c>enum ErroTypst</c> do NotiqTypst: cada caso é uma subclasse
/// selada, e <see cref="Exception.Message"/> é a <c>descricaoLegivel</c>.
/// </remarks>
public abstract class ErroTypst : Exception
{
    private ErroTypst(string mensagem) : base(mensagem) { }

    /// <summary>O que a interface mostra.</summary>
    public string DescricaoLegivel => Message;

    /// <summary>Um campo do <c>template.json</c> não é utilizável.</summary>
    public sealed class TemplateInvalido(string campo, string motivo)
        : ErroTypst($"Template inválido em `{campo}`: {motivo}")
    {
        public string Campo { get; } = campo;
        public string Motivo { get; } = motivo;
    }

    /// <summary>
    /// O template exige uma família tipográfica que o app não embarca. Barrado
    /// <b>antes</b> de compor — senão o Typst substitui em silêncio e o
    /// documento sai com a tipografia errada parecendo certo.
    /// </summary>
    public sealed class FonteNaoEmbarcada(string familia)
        : ErroTypst($"A fonte “{familia}” não está embarcada, e o template a exige.")
    {
        public string Familia { get; } = familia;
    }

    /// <summary><c>template.familia</c> não está no registro de geradores.</summary>
    public sealed class FamiliaDesconhecida(string familia)
        : ErroTypst($"Este documento usa um tipo de template que esta versão do MarkDocs não conhece ({familia}).")
    {
        public string Familia { get; } = familia;
    }

    /// <summary>O template foi escrito por uma versão mais nova. Não há esquema futuro a inventar.</summary>
    public sealed class EsquemaFuturo(string familia, int esquema, int atual)
        : ErroTypst($"Este documento foi criado por uma versão mais recente do MarkDocs "
                    + $"(template {familia}, esquema {esquema}; esta versão entende até {atual}).")
    {
        public string Familia { get; } = familia;
        public int Esquema { get; } = esquema;
        public int Atual { get; } = atual;
    }

    /// <summary>
    /// O motor recusou o <c>.typ</c>. Como o <c>.typ</c> é gerado e não escrito à
    /// mão, isto costuma significar defeito no gerador.
    /// </summary>
    public sealed class Compilacao(IReadOnlyList<Diagnostico> diagnosticos)
        : ErroTypst(diagnosticos.Count == 0
            ? "O documento não pôde ser composto."
            : string.Join("\n", diagnosticos.Select(d => d.DescricaoLegivel)))
    {
        public IReadOnlyList<Diagnostico> Diagnosticos { get; } = diagnosticos;
    }

    /// <summary>O Rust entrou em pânico. É defeito nosso, e o <c>.typ</c> gerado vai junto.</summary>
    public sealed class FalhaInterna(string mensagem, string typGerado)
        : ErroTypst($"Falha interna do motor de composição: {mensagem}")
    {
        public string Mensagem { get; } = mensagem;
        public string TypGerado { get; } = typGerado;
    }

    /// <summary>O Acervo entregue ao motor não é utilizável.</summary>
    public sealed class EntradaInvalida(string motivo)
        : ErroTypst($"Entrada inválida para o motor de composição: {motivo}")
    {
        public string Motivo { get; } = motivo;
    }
}

/// <summary>Uma linha do diagnóstico devolvido pelo motor.</summary>
public sealed record Diagnostico(
    Diagnostico.Severidades Severidade,
    string? Arquivo,
    int Linha,
    int Coluna,
    string Mensagem,
    IReadOnlyList<string> Dicas)
{
    public enum Severidades { Erro, Aviso, FalhaInterna, EntradaInvalida }

    public string DescricaoLegivel
    {
        get
        {
            var texto = Mensagem;
            if (Arquivo is not null && Linha > 0)
                texto = $"{Arquivo}:{Linha}:{Coluna} — {texto}";
            else if (Arquivo is not null)
                texto = $"{Arquivo} — {texto}";
            foreach (var dica in Dicas)
                texto += $"\n  dica: {dica}";
            return texto;
        }
    }

    /// <summary>
    /// Lê o JSON que o motor devolve. Severidade desconhecida não derruba a
    /// leitura: o motor pode crescer o vocabulário, e um diagnóstico ilegível
    /// é pior que um genérico.
    /// </summary>
    internal static IReadOnlyList<Diagnostico> LerJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Array) return [];

            var lista = new List<Diagnostico>();
            foreach (var item in documento.RootElement.EnumerateArray())
            {
                var severidade = Texto(item, "severidade") switch
                {
                    "aviso" => Severidades.Aviso,
                    "falhaInterna" => Severidades.FalhaInterna,
                    "entradaInvalida" => Severidades.EntradaInvalida,
                    _ => Severidades.Erro,
                };
                var dicas = item.TryGetProperty("dicas", out var d) && d.ValueKind == JsonValueKind.Array
                    ? d.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                    : [];
                lista.Add(new Diagnostico(
                    severidade,
                    Texto(item, "arquivo"),
                    Inteiro(item, "linha"),
                    Inteiro(item, "coluna"),
                    Texto(item, "mensagem") ?? "",
                    dicas));
            }
            return lista;
        }
        catch (JsonException)
        {
            return [];
        }

        static string? Texto(JsonElement e, string nome) =>
            e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        static int Inteiro(JsonElement e, string nome) =>
            e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
    }
}
