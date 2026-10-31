namespace AppTemplate.Contatos.Infrastructure.Persistence.Configuration;

public class ContatosOptions
{
    /// <summary>String de conexao com o replica set MongoDB.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
