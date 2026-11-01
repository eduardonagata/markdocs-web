using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MarkDocs.Core.Nucleo;

/// <summary>A fronteira com o Rust.</summary>
/// <remarks>
/// Você nunca chama isto diretamente: entra-se por
/// <see cref="RenderizadorPDF.Pdf(string, TemplateCarregado)"/>, depois que o
/// conversor e o Acervo fizeram a parte deles.
///
/// As três regras da fronteira, do lado .NET:
/// <list type="number">
///   <item>Nenhum ponteiro do Rust sobrevive à chamada: os bytes do PDF são
///   copiados para um <c>byte[]</c> e <c>markdocs_typst_liberar</c> roda num
///   <c>finally</c>.</item>
///   <item>Nenhum ponteiro do .NET sobrevive à chamada: os buffers ficam
///   fixados (<c>GCHandle</c>) só durante ela.</item>
///   <item>Nenhum pânico atravessa: o Rust devolve código 2, que vira
///   <see cref="ErroTypst.FalhaInterna"/>.</item>
/// </list>
/// </remarks>
internal static unsafe partial class MotorTypst
{
    private const string Biblioteca = "typst_markdocs";

    // Antes da primeira chamada nativa, e uma vez só.
    static MotorTypst() => BibliotecaNativa.Registrar();

    [StructLayout(LayoutKind.Sequential)]
    private struct Arquivo
    {
        public byte* Caminho;
        public byte* Dados;
        public nuint Tamanho;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Fonte
    {
        public byte* Dados;
        public nuint Tamanho;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Entrada
    {
        public Arquivo* Arquivos;
        public nuint ArquivosLen;
        public byte* CaminhoDeEntrada;
        public Fonte* Fontes;
        public nuint FontesLen;
        /// <summary>Segundos desde 1970. Torna <c>today()</c> determinístico.</summary>
        public long Instante;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Saida
    {
        public byte* Pdf;
        public nuint PdfLen;
        public byte* Diagnostico;
    }

    [LibraryImport(Biblioteca, EntryPoint = "markdocs_typst_compilar")]
    private static partial int Compilar(Entrada* entrada, Saida* saida);

    [LibraryImport(Biblioteca, EntryPoint = "markdocs_typst_liberar")]
    private static partial void Liberar(Saida* saida);

    public static Composicao Compor(Acervo acervo, IReadOnlyList<FonteEmbarcada> fontes, DateTimeOffset instante)
    {
        var arquivos = acervo.Arquivos.ToList();
        var fixados = new List<GCHandle>();
        var textosC = new List<IntPtr>();

        try
        {
            var arquivosC = new Arquivo[arquivos.Count];
            for (var i = 0; i < arquivos.Count; i++)
            {
                var caminho = Marshal.StringToCoTaskMemUTF8(arquivos[i].Key);
                textosC.Add(caminho);
                arquivosC[i] = new Arquivo
                {
                    Caminho = (byte*)caminho,
                    Dados = Fixar(arquivos[i].Value, fixados),
                    Tamanho = (nuint)arquivos[i].Value.Length,
                };
            }

            var fontesC = new Fonte[fontes.Count];
            for (var i = 0; i < fontes.Count; i++)
            {
                fontesC[i] = new Fonte
                {
                    Dados = Fixar(fontes[i].Dados, fixados),
                    Tamanho = (nuint)fontes[i].Dados.Length,
                };
            }

            var entradaC = Marshal.StringToCoTaskMemUTF8(Acervo.CaminhoDeEntrada);
            textosC.Add(entradaC);

            fixed (Arquivo* ponteiroDeArquivos = arquivosC)
            fixed (Fonte* ponteiroDeFontes = fontesC)
            {
                var entrada = new Entrada
                {
                    Arquivos = ponteiroDeArquivos,
                    ArquivosLen = (nuint)arquivosC.Length,
                    CaminhoDeEntrada = (byte*)entradaC,
                    Fontes = ponteiroDeFontes,
                    FontesLen = (nuint)fontesC.Length,
                    Instante = instante.ToUnixTimeSeconds(),
                };
                var saida = new Saida();

                try
                {
                    var codigo = Compilar(&entrada, &saida);
                    var diagnosticos = Diagnostico.LerJson(TextoDeC(saida.Diagnostico));

                    switch (codigo)
                    {
                        case 0:
                            if (saida.Pdf is null) throw new ErroTypst.Compilacao(diagnosticos);
                            // Copiado para um byte[] antes do `finally` liberar o original.
                            var pdf = new ReadOnlySpan<byte>(saida.Pdf, checked((int)saida.PdfLen)).ToArray();
                            return new Composicao(pdf, diagnosticos);

                        case 2:
                            throw new ErroTypst.FalhaInterna(
                                diagnosticos.FirstOrDefault()?.Mensagem ?? "pânico no motor",
                                Encoding.UTF8.GetString(acervo.Arquivos.GetValueOrDefault(Acervo.CaminhoDeEntrada) ?? []));

                        case 3:
                            throw new ErroTypst.EntradaInvalida(
                                diagnosticos.FirstOrDefault()?.Mensagem ?? "acervo inutilizável");

                        default:
                            throw new ErroTypst.Compilacao(diagnosticos);
                    }
                }
                finally
                {
                    // Regra 1: nenhum ponteiro do Rust sobrevive à chamada.
                    Liberar(&saida);
                }
            }
        }
        finally
        {
            foreach (var alca in fixados) alca.Free();
            foreach (var texto in textosC) Marshal.FreeCoTaskMem(texto);
        }
    }

    private static byte* Fixar(byte[] dados, List<GCHandle> fixados)
    {
        if (dados.Length == 0) return null;
        var alca = GCHandle.Alloc(dados, GCHandleType.Pinned);
        fixados.Add(alca);
        return (byte*)alca.AddrOfPinnedObject();
    }

    private static string? TextoDeC(byte* ponteiro) =>
        ponteiro is null ? null : Marshal.PtrToStringUTF8((IntPtr)ponteiro);
}

/// <summary>
/// Onde a biblioteca nativa é procurada: em <c>runtimes/&lt;rid&gt;/native/</c>
/// ao lado do assembly — o layout que o csproj copia para <c>bin/</c> e para o
/// diretório publicado. Sem isto o .NET só a acharia se ela viesse de um
/// pacote NuGet listado no <c>deps.json</c>.
/// </summary>
internal static class BibliotecaNativa
{
    internal static void Registrar() =>
        NativeLibrary.SetDllImportResolver(typeof(BibliotecaNativa).Assembly, Resolver);

    private static IntPtr Resolver(string nome, Assembly assembly, DllImportSearchPath? caminho)
    {
        if (nome != "typst_markdocs") return IntPtr.Zero;

        var arquivo = OperatingSystem.IsWindows() ? "typst_markdocs.dll"
            : OperatingSystem.IsMacOS() ? "libtypst_markdocs.dylib"
            : "libtypst_markdocs.so";

        foreach (var diretorio in Diretorios(assembly))
        {
            var candidato = Path.Combine(diretorio, "runtimes", RidPortavel(), "native", arquivo);
            if (File.Exists(candidato) && NativeLibrary.TryLoad(candidato, out var alca))
                return alca;
        }

        // Cai no carregamento padrão (diretório da aplicação, deps.json, LD_LIBRARY_PATH).
        return NativeLibrary.TryLoad(nome, assembly, caminho, out var padrao) ? padrao : IntPtr.Zero;
    }

    private static IEnumerable<string> Diretorios(Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location))
            yield return Path.GetDirectoryName(assembly.Location)!;
        yield return AppContext.BaseDirectory;
    }

    /// <summary>
    /// O RID portável (<c>linux-x64</c>, <c>osx-arm64</c>…). Não usa
    /// <c>RuntimeInformation.RuntimeIdentifier</c>, que num SDK de distribuição
    /// devolve algo como <c>ubuntu.24.04-x64</c>.
    /// </summary>
    private static string RidPortavel()
    {
        var so = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var arquitetura = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
        return $"{so}-{arquitetura}";
    }
}
