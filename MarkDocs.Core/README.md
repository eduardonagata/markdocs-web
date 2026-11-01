# MarkDocs.Core

A transcrição .NET do **NotiqTypst** (repositório notiq-docs). Em uma frase:
**Markdown e um template entram, os bytes de um PDF paginado saem.**

```
Markdown ───────────────────┐
                            ├─→ RenderizadorPDF.Pdf(markdown, template) → byte[]
template.json + ativos ─────┘     (RegistroDeGeradores.Carregar → TemplateCarregado)
```

É um pacote independente e reaproveitável: não conhece MongoDB, ASP.NET,
`IDocumento` nem o app. Por enquanto é desenvolvido dentro do MarkDocs, como o
NotiqTypst dentro do notiq-docs — e esta pasta inteira (código, testes, motor e
documentação) sai do repositório sem levar nada do app.

## A interface

```csharp
using MarkDocs.Core;

// Uma vez por processo: as fontes que os documentos usam. O motor não vê as do sistema.
var renderizador = new RenderizadorPDF(fontes);             // IEnumerable<FonteEmbarcada>

// O template: a família escolhe o gerador; o JSON é o template.json; os ativos, nome → bytes.
var template = RegistroDeGeradores.Carregar("documento-de-negocio", json, ativos);

byte[] pdf = renderizador.Pdf(markdown, template);
Composicao c = renderizador.Compor(markdown, template);    // o PDF e os avisos do motor

// A formatação editável, como dado: um controle por eixo, com domínio e valor.
IReadOnlyList<EixoDeFormatacao> eixos = template.Eixos;
var ajustado = template.ComAjustes(new Dictionary<string, ValorDeEixo>
{
    ["texto.tamanho"] = new ValorDeEixo.Medida(10.5, "pt"),
});

// Extensão do MarkDocs.Core: as imagens que o Markdown cita por imagens/<nome>.
byte[] comImagens = renderizador.Pdf(markdown, template, imagens);  // nome → bytes
```

| Tipo | O que é |
|---|---|
| `RenderizadorPDF`, `FonteEmbarcada`, `Composicao` | a composição |
| `RegistroDeGeradores`, `IGeradorDeTemplate` | as famílias de template (hoje, `documento-de-negocio`) |
| `TemplateCarregado` | a forma apagada do template: nome, fontes exigidas, ativos, eixos, `ComAjustes` |
| `EixoDeFormatacao`, `DominioDeEixo`, `ValorDeEixo`, `Opcao` | os controles de formatação, como dado |
| `ErroTypst` (`TemplateInvalido`, `FonteNaoEmbarcada`, `FamiliaDesconhecida`, `EsquemaFuturo`, `Compilacao`, `FalhaInterna`, `EntradaInvalida`) | toda falha é erro tipado, nunca crash |
| `Diagnostico` | uma linha do diagnóstico do motor |
| `ImagensDoDocumento` | a convenção `imagens/<nome>` ([Docs/Imagens.md](Docs/Imagens.md)) |

## A organização

A mesma do NotiqTypst — **a fronteira de diretório é a fronteira de
conhecimento**:

| Diretório | O que é | Conhece |
|---|---|---|
| `Sources/MarkDocs.Core/Publico/` | a API pública | tudo |
| `Sources/MarkDocs.Core/Nucleo/` | `Acervo` (o *World*), `MotorTypst` (P/Invoke) | caminhos e bytes |
| `Sources/MarkDocs.Core/Nucleo/Emissao/` | **o único lugar que escreve Typst** | nada de família |
| `Sources/MarkDocs.Core/Conteudo/` | `ArvoreDeMarkdown` (Markdig), `ValorTypst`, `base.typ` | nada de template |
| `Sources/MarkDocs.Core/Geradores/<Familia>/` | o esquema, as migrações, os eixos, o gerador | só a própria família |
| `Tests/MarkDocs.Core.Tests/` | os testes do NotiqTypst, transcritos | — |
| `Rust/typst-markdocs/` | o crate do motor (Typst 0.15.1), fora do build do .NET | — |
| `Artefatos/runtimes/<rid>/native/` | o motor compilado, versionado | — |
| `Scripts/construir-typst.sh` | compila o motor para um RID | — |

As regras do NotiqTypst valem iguais: **nunca se escreve marcação Typst** fora
de `Nucleo/Emissao/` (o conteúdo vira um *literal de dados*, e o texto do usuário
só aparece entre aspas); família nova é um diretório em `Geradores/` e uma linha
em `RegistroDeGeradores`; elemento novo do vocabulário mexe em
`ArvoreDeMarkdown`, `ValorTypst` e `base.typ`, e `VersaoDoVocabulario` sobe.

O que muda em relação ao Swift é só o necessário:

| No NotiqTypst | No MarkDocs.Core |
|---|---|
| swift-markdown (cmark-gfm) | Markdig, com tabelas, riscado e links automáticos; as diferenças de forma são absorvidas em `ArvoreDeMarkdown` ([Docs/Testes.md](Docs/Testes.md), §7) |
| `staticlib` num `.xcframework`, ligado pelo Xcode | `cdylib` em `runtimes/<rid>/native/`, carregado por `NativeLibrary` |
| `enum` com valores associados | `record` abstrato com casos aninhados (`ValorDeEixo.Medida`…) e exceções seladas (`ErroTypst.TemplateInvalido`…) |
| `Codable` | `System.Text.Json` com as regras do `JSONDecoder` (campo obrigatório, enum exato, erro por campo) |
| vocabulário 1 | vocabulário 2: + `"imagem"` ([Docs/Imagens.md](Docs/Imagens.md)) |

O caminho do intérprete no Acervo continua `/notiq/base.typ`: é o contrato do
vocabulário, e os testes dos dois pacotes o fixam.

## Os testes

```sh
dotnet test MarkDocs.Core/MarkDocs.Core.sln
```

São os testes do NotiqTypst com **as mesmas asserções**: a mesma tabela de casos,
os mesmos templates de teste, a mesma sonda que pergunta ao próprio Typst que
elemento um literal produziu. [Docs/Testes.md](Docs/Testes.md) é a especificação
do comportamento, transcrita de `NotiqTypst/Docs/Testes.md`, com as divergências
ditas onde ocorrem. Hoje: 461 casos (433 transcritos, 28 da extensão de imagens).

## O motor nativo

O Typst é Rust. O crate `Rust/typst-markdocs` é o `typst-notiq` do NotiqTypst com
os nomes trocados e `crate-type = ["cdylib"]`: uma função
(`markdocs_typst_compilar`), dados brutos, um par alocar/liberar, `catch_unwind`
em volta de tudo, e nenhum I/O — qualquer caminho fora do Acervo não existe.

**A fatia `linux-x64` está versionada** (é a do Docker e a da CI): ninguém
precisa de Rust para trabalhar no app em Linux. Num Mac, compile a fatia da
máquina uma vez:

```sh
MarkDocs.Core/Scripts/construir-typst.sh            # gera Artefatos/runtimes/osx-arm64/native/
```

O `.csproj` copia o que houver em `Artefatos/runtimes/` para o `bin/` e para o
diretório publicado, e o `MotorTypst` procura a biblioteca em
`runtimes/<rid>/native/` ao lado do assembly.

**Subir a versão do Typst** é tarefa própria, nunca efeito colateral de outra:
mudar a versão pinada em `Rust/typst-markdocs/Cargo.toml` (a mesma do
NotiqTypst), rodar o script em Linux e commitar o `.so`, migrar
`Nucleo/Emissao/` se a sintaxe mudou, e rodar os testes.

## Extrair o pacote

Mover a pasta `MarkDocs.Core/` para um repositório próprio basta: ela tem a
própria solução (`MarkDocs.Core.sln`), os testes não dependem de nada fora dela,
e o `.csproj` já declara o empacotamento do motor em `runtimes/` (`dotnet pack`
gera um NuGet com a biblioteca nativa no lugar que o .NET procura).
