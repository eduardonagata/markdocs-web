namespace AppTemplate.Contatos.Domain.Exceptions;

public class EntidadeExternaNotFoundException : KeyNotFoundException
{
    public EntidadeExternaNotFoundException(string id)
        : base($"EntidadeExterna '{id}' nao encontrada.") { }
}
