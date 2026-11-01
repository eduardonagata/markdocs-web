namespace MarkDocs.WebApi.EndpointHandlers.Documentos.Dto;

public sealed record CriarDocumentoRequest(string TemplateId, string Corpo, string? Titulo = null);

/// <summary>Só o que vier é alterado. <c>TemplateId</c> aplica um template inteiro, antes dos ajustes.</summary>
public sealed record AtualizarDocumentoRequest(
    string? Titulo = null,
    string? Tipo = null,
    string? Corpo = null,
    string? TemplateId = null,
    IReadOnlyList<AjusteRequest>? Ajustes = null);

/// <summary>Uma edição de formatação: o id do eixo, como veio em <see cref="EixoResponse"/>, e o valor novo.</summary>
public sealed record AjusteRequest(string Eixo, ValorDeEixoDto Valor);

public sealed record DocumentoResumoResponse(
    string Id,
    string Titulo,
    string Tipo,
    string? TemplateDeOrigem,
    DateTimeOffset CriadoEm,
    DateTimeOffset AlteradoEm);

public sealed record DocumentoResponse(
    string Id,
    string Titulo,
    string Tipo,
    string Corpo,
    string Familia,
    string? TemplateDeOrigem,
    DateTimeOffset CriadoEm,
    DateTimeOffset AlteradoEm,
    IReadOnlyList<EixoResponse> Eixos,
    IReadOnlyList<ImagemResponse> Imagens);

/// <summary>
/// Uma imagem do documento. <c>Referencia</c> é o que o Markdown escreve
/// (<c>imagens/planta.png</c>), e é relativa: resolvida contra o endereço do
/// documento, dá <c>Url</c>.
/// </summary>
public sealed record ImagemResponse(string Nome, string Referencia, string Url);

public sealed record CriarTemplateRequest(string DocumentoId, string Nome);

public sealed record TemplateResponse(
    string Id,
    string Nome,
    string Tipo,
    string Familia,
    bool DeFabrica,
    string? DocumentoDeOrigem,
    DateTimeOffset CriadoEm);

public sealed record ErroResponse(string Mensagem);
