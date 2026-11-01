using MarkDocs.Core.Geradores.DocumentoDeNegocio;

namespace MarkDocs.Core;

/// <summary>A indireção que escolhe o gerador pela família.</summary>
/// <remarks>
/// Entra já com uma família só, de propósito: acrescentá-la depois obrigaria a
/// adivinhar a família dos arquivos já gravados. Família nova é <b>uma
/// linha</b> aqui.
/// </remarks>
public static class RegistroDeGeradores
{
    private delegate TemplateCarregado Carregador(byte[] json, IReadOnlyDictionary<string, byte[]> ativos);

    private static readonly IReadOnlyDictionary<string, Carregador> Registrados =
        new Dictionary<string, Carregador>
        {
            [DocumentoDeNegocio.Familia] = Registro<DocumentoDeNegocio>(),
            // [TrabalhoAcademico.Familia] = Registro<TrabalhoAcademico>(),
        };

    private static Carregador Registro<T>() where T : IGeradorDeTemplate => T.Carregar;

    /// <summary>As famílias que esta versão conhece. É o que a interface lista.</summary>
    public static IReadOnlyList<string> Familias =>
        Registrados.Keys.Order(StringComparer.Ordinal).ToList();

    public static TemplateCarregado Carregar(
        string familia,
        byte[] json,
        IReadOnlyDictionary<string, byte[]>? ativos = null)
    {
        if (!Registrados.TryGetValue(familia, out var carregar))
            throw new ErroTypst.FamiliaDesconhecida(familia);
        return carregar(json, ativos ?? new Dictionary<string, byte[]>());
    }
}
