namespace MarkDocs.Documentos.Domain.Exceptions;

/// <summary>O recurso pedido não existe. O endpoint traduz para 404.</summary>
public abstract class NaoEncontradoException(string mensagem) : Exception(mensagem);

public sealed class DocumentoNaoEncontradoException(string id)
    : NaoEncontradoException($"O documento “{id}” não existe.");

public sealed class TemplateNaoEncontradoException(string id)
    : NaoEncontradoException($"O template “{id}” não existe.");

public sealed class ImagemNaoEncontradaException(string nome)
    : NaoEncontradoException($"A imagem “{nome}” não está nos ativos do documento.");

public sealed class ObjetoNaoEncontradoException(string chave)
    : NaoEncontradoException($"O objeto “{chave}” não está no armazenamento.");
