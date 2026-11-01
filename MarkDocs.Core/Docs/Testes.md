# Testes do MarkDocs.Core

Este documento é a **especificação do comportamento** do pacote, escrita a partir
dos testes que a verificam. Cada afirmação aqui aponta para um teste concreto
(`Suite › teste`), e o teste é a prova dela: quem quer saber o que o pacote faz
lê este documento; quem quer saber *como* se verifica abre o teste com aquele
nome.

A propriedade que os testes fixam é a mesma que organiza o pacote:

> **O PDF é função de (Markdown, template, ajustes).** Não há outra entrada, e
> nada que não esteja nessas três chega ao papel.

Este documento é a **transcrição** de `NotiqTypst/Docs/Testes.md` (repositório
notiq-docs): os testes do MarkDocs.Core fazem **as mesmas asserções** que os do
NotiqTypst, sobre a mesma tabela de casos e os mesmos templates de teste. O que
muda é o idioma — Swift Testing vira xUnit, PDFKit vira PdfPig, swift-markdown
vira Markdig — e o que muda de comportamento está dito onde muda (§6 e §8).

A regra de manutenção é uma só: **comportamento que muda, muda junto com o
teste e com este documento, no mesmo commit.** Um agente que altera o pacote e
quebra um teste aqui descrito está alterando a especificação, não corrigindo um
detalhe — e tem de dizer isso.

---

## 1. Como os testes observam o pacote

O caminho de `RenderizadorPDF.Compor(markdown, template)` tem quatro pontos
observáveis, e cada suíte escolhe o mais barato que prova o que ela afirma:

```
Markdown ──→ [BlocoDoc] ──→ literal de dados ──┐
              (árvore)      (/documento.typ)   ├──→ Acervo ──→ motor ──→ PDF
template.json + ajustes ──→ /template.typ ─────┘
```

| Ponto | O que se lê | Custo | Quem usa |
|---|---|---|---|
| A árvore | `ArvoreDeMarkdown.Converter(markdown)` | C# puro | Conteúdo |
| O literal | `ValorTypst.Literal(blocos)` | C# puro | Conteúdo |
| O `.typ` do template | `TemplateCarregado.EmitirTyp()` | C# puro | Gerador |
| O Acervo | os arquivos entregues ao motor | C# puro | Integração |
| O elemento Typst | uma *sonda* compilada pelo motor | motor | Conteúdo, Integração |
| O PDF | `DocumentoPdf` (PdfPig) | motor | Integração |

### 1.1 O único ajuste de produção que os testes pedem

Para que a suíte de integração verifique **exatamente o que o motor recebe** —
e não uma remontagem paralela feita pelo teste —, a montagem do Acervo é um
método `internal`, como no NotiqTypst:

```csharp
internal Acervo Acervo(string markdown, TemplateCarregado template, IReadOnlyDictionary<string, byte[]>? imagens = null)
```

`Compor` o chama. O equivalente do `@testable import` é o
`<InternalsVisibleTo Include="MarkDocs.Core.Tests" />` do `.csproj`: os testes
leem a árvore, o literal, o `.typ` e o Acervo sem que nada disso seja público.

### 1.2 A asserção sobre Typst é sobre a **regra do elemento**, não sobre o arquivo

Comparar o `/template.typ` inteiro com um texto esperado quebraria a cada
espaço. Os testes do gerador isolam a regra do elemento afetado e afirmam sobre
o argumento que interessa:

| Elemento | Regra que o teste isola |
|---|---|
| página | `set page(` |
| texto | `set text(` |
| parágrafo | `set par(` |
| título nível *N* | `show heading.where(level: N)` |
| tabela | `set table(`, `set table.header(`, `show table.cell.where(y: 0)` |

O apoio `Apoio.Regra(prefixo, typ)` devolve a regra a partir do prefixo, e a asserção
fica no formato *"a regra do título 2 contém `align(center,`"*.

Complementa isso a **asserção diferencial** (§4.8): ajustar um eixo muda a regra
do elemento dele, **e só ela**. É o que prova que um controle do inspetor não
tem efeito colateral em outro elemento.

### 1.3 A sonda: verificar o elemento Typst sem olhar pixel

**O problema.** A pergunta da suíte 1 é: *este Markdown vira mesmo um `heading`
de nível 2 no Typst?* Quem transforma o literal de dados em `heading` é o
`base.typ`, e ele roda **dentro do motor**. Do lado Swift só se vê o literal,
`(tipo: "titulo", nivel: 2, …)`, e isso prova apenas metade do caminho. O PDF
também não serve de resposta: um parágrafo em negrito e grande se parece com um
título, mas não é um.

**A resposta.** É preciso perguntar ao próprio Typst. O Typst sabe dizer de que
tipo é um conteúdo (`c.func()`) e quais campos ele tem (`c.level`), e tem
`assert`. A sonda é um Acervo montado pelo teste em que **o `/documento.typ`
termina com asserções, e não com `#renderizar(documento)`**.

#### O que é gerado pelo programa e o que é escrito à mão

Para comparar, este é o `/documento.typ` que o `RenderizadorPDF` monta em
produção:

```typst
#import "/notiq/base.typ": renderizar          // fixo
#import "/template.typ": estilo                // fixo

#let documento = (versao: 1, blocos: (…))      // GERADO a partir do Markdown

#show: estilo                                  // fixo
#renderizar(documento)                         // fixo
```

E este é o da sonda, para o Markdown `## Objeto`:

```typst
#import "/notiq/base.typ": renderizar          // fixo, igual à produção
#import "/template.typ": estilo                // fixo, igual à produção

#let documento = (versao: 1, blocos: (…))      // GERADO, pelo mesmo código da produção

#let c = renderizar(documento)                 // fixo, escrito em Sonda.cs
#assert.eq(c.func(), heading)                  // montado a partir do caso de teste
#assert.eq(c.level, 2)                         // montado a partir do caso de teste
```

Arquivo por arquivo:

| Parte do Acervo de sonda | De onde vem | Igual à produção? |
|---|---|---|
| A linha `#let documento = …` | **Gerada pelo programa.** O teste passa o **Markdown**, e `ArvoreDeMarkdown.converter` + `ValorTypst.literal` produzem o literal, pelo mesmo caminho de `RenderizadorPDF`. O teste nunca escreve o literal à mão. | sim |
| `/notiq/base.typ` | O recurso do pacote, lido por `Acervo.Base()`. | sim, byte a byte |
| `/template.typ` | Uma linha fixa de `Sonda.cs`: `#let estilo(body) = body`, um estilo que não faz nada. Assim nenhuma regra de família interfere no que o conteúdo produz. | **não**, de propósito |
| As duas importações e `#let c = renderizar(documento)` | Texto fixo em `Sonda.cs`. | sim; só a última linha muda |
| As linhas `#assert.eq(…)` | **Montadas por `Sonda.cs`** a partir do que a linha de `CasosDeConteudo` declara em Swift, por exemplo `.elemento("heading", campos: ["level": "2"])`. | não existem em produção |

Resumindo: **o conteúdo é sempre gerado a partir do Markdown; o Typst escrito à
mão é só o andaime da pergunta**, e ele mora num arquivo só, `Sonda.cs`.
Quem acrescenta um caso à tabela escreve Markdown e uma expectativa em Swift,
nunca Typst. É a mesma disciplina do pacote, em que o Typst só é escrito em
`Nucleo/Emissao/`, aplicada aos testes.

#### Como o resultado volta ao teste

A sonda é composta por `MotorTypst.Compor`, acessível por `InternalsVisibleTo`:

- **Todas as asserções valem:** o motor devolve um PDF, que é descartado. O
  teste passa.
- **Uma asserção falha:** o motor recusa o documento e lança
  `ErroTypst.Compilacao`. A mensagem do Typst (*"equality assertion failed:
  value par was not equal to heading"*) vira a mensagem da falha no xUnit
  (`Sonda.Falha`). É o diagnóstico inteiro: diz o que se esperava e o que veio.

Um detalhe que a sonda precisa tratar: `renderizar` junta os blocos com
`.join()`. Com um bloco só, `c` **é** o elemento; com vários, é uma sequência, e
as asserções olham `c.children`. Os casos da tabela têm um bloco cada, de
propósito, para que a pergunta seja sempre sobre um elemento.

#### O que a sonda não substitui

A sonda verifica o **conteúdo isolado da aparência**, e por isso troca o
`/template.typ`. Já a suíte de integração (§5) usa o `/documento.typ` de
produção sem alteração nenhuma, com o template real, e é lá que se confirma que
as duas metades funcionam juntas.

### 1.4 Do Swift Testing ao xUnit

Cada recurso do Swift Testing tem um equivalente, e a tradução é mecânica:

| No NotiqTypst (Swift Testing) | No MarkDocs.Core (xUnit) | Para quê |
|---|---|---|
| `@Suite` aninhadas | classes aninhadas (`ConteudoTests+Blocos`) | Espelhar a hierarquia deste documento — e, no gerador, a do inspetor. |
| `@Test(arguments:)` | `[Theory]` + `[MemberData]`/`[InlineData]` | Uma tabela de casos é o contrato; o teste é um só. |
| `zip(_:_:)` em `arguments:` | `[InlineData(entrada, esperado)]` | Pares (entrada, esperado) sem produto cartesiano. |
| `CustomTestStringConvertible` | o caso entra pelo **nome** (`"título nível 2"`) e é procurado na tabela | Cada caso aparece no navegador de testes pelo nome legível. |
| `#require` | `Assert.True(x is not null, …)` antes de usar | Falhar cedo quando o eixo ou a regra procurada nem existe. |
| `guard case .x = erro` | `Assert.IsType<ErroTypst.X>(erro)` | Afirmar o **caso** de `ErroTypst` e o campo, não só que algo lançou. |
| Tags (`.conteudo`, `.gerador`, `.integracao`, `.motor`) | `[Trait("Tag", "motor")]` | Filtrar: `dotnet test --filter Tag=motor`. |
| `withKnownIssue` | `ProblemaConhecido.Registrar(motivo, confere)` | Registrar os pontos em aberto da §6 sem cimentá-los: quando forem corrigidos, o teste falha e avisa. |

O `#expect` do Swift continua depois de falhar; o `Assert` do xUnit para no
primeiro. Num teste que passa não há diferença, e num que falha a primeira
falha já é o diagnóstico.

O PDF que o Swift lê com o PDFKit é lido com o PdfPig (`Apoio/LeitorDePdf.cs`):
o texto da página reconstruído com espaços e quebras, a caixa de um trecho e o
corpo das letras. A origem das coordenadas é a mesma (canto inferior
esquerdo), e as tolerâncias são as mesmas.

---

## 2. Organização

```
Tests/MarkDocs.Core.Tests/
├── Apoio/
│   ├── Apoio.cs              fontes, templates de teste, Apoio.Regra(prefixo, typ)
│   ├── Sonda.cs              o Acervo de sonda da §1.3
│   ├── CasosDeConteudo.cs    a tabela de casos da §3, compartilhada
│   ├── LeitorDePdf.cs        o que o PDFKit faz nos testes Swift, com o PdfPig
│   └── ProblemaConhecido.cs  o withKnownIssue, e as tags
├── Conteudo/
│   └── ConteudoTests.cs                     Suíte 1 — geral, fixa
├── Geradores/
│   └── DocumentoDeNegocio/
│       └── DocumentoDeNegocioTests.cs       Suíte 2 — uma por gerador
├── Integracao/
│   └── IntegracaoTests.cs                   Suíte 3
├── Extensoes/
│   └── ImagensTests.cs                      §8 — sem par no NotiqTypst
└── Recursos/                                as quatro faces de Libertinus Serif
```

| Suíte | Afirma | Muda quando |
|---|---|---|
| **1. Conteúdo** | Cada elemento de Markdown vira **um** elemento Typst, sempre o mesmo, em qualquer família. | O vocabulário cresce (e `VersaoDoVocabulario` sobe). |
| **2. Gerador `documento-de-negocio`** | Cada controle do inspetor altera **o elemento Typst dele**, com o valor que mostra. | O esquema da família ou os eixos mudam. |
| **3. Integração** | Pelo `RenderizadorPDF`: o motor recebe o que as suítes 1 e 2 afirmam, e o PDF sai como o inspetor promete. | Qualquer uma das duas, ou o motor. |

A suíte 1 é **geral**: não conhece família nenhuma. Família nova ganha a própria
suíte 2, no próprio diretório, e não toca as outras — a mesma fronteira de
conhecimento que o pacote tem em `Geradores/<Familia>/`.

### Os templates de teste

| Nome | O que é | Para quê |
|---|---|---|
| `Apoio.TemplateMinimo` | O JSON atual de `Apoio`: dois níveis de título, rodapé "Página X de Y". | O ponto de partida dos testes do gerador. |
| `Apoio.TemplateDaCaptura` | O mínimo + os ajustes que aparecem na captura do inspetor (A4; margens 30/32/34/56 mm; Libertinus Serif 9,5 pt, `#000000`, justificado, entrelinha 0,65 em, 2 em entre parágrafos; Título 1 24,5 pt centralizado em versais; Título 2 21 pt à esquerda; Título 3 11 pt centralizado; filete 0,5 pt, respiro 5 pt, cabeçalho repetido). | Ancorar a integração numa tela que qualquer um reconhece. |

Os templates de fábrica do app (`contrato-padrao.json` etc.) **não** entram:
são do app, e o pacote não os conhece.

---

## 3. Suíte 1 — Conteúdo: Markdown → Typst

`@Suite("Conteúdo: Markdown → Typst", .tags(.conteudo))`

O vocabulário é fechado e **um só** para toda família. A suíte afirma o
mapeamento 1:1 em três camadas, e as três são dirigidas **pela mesma tabela de
casos** (`CasosDeConteudo`): cada linha é um Markdown, a árvore esperada, o
`tipo` no literal e o elemento Typst. Acrescentar um elemento ao vocabulário é
acrescentar uma linha a essa tabela, e as três camadas passam a cobri-lo.

### 3.1 Blocos

| Caso | Markdown | Árvore (`BlocoDoc`) | `tipo` no literal | Elemento Typst |
|---|---|---|---|---|
| título 1–6 | `#` … `######` | `.titulo(nivel: n, …)` | `"titulo"` | `heading(level: n)` |
| parágrafo | `Texto.` | `.paragrafo` | `"paragrafo"` | `par` |
| lista | `- a` | `.lista(ordenada: false)` | `"lista"` | `list` |
| lista numerada | `1. a` | `.lista(ordenada: true)` | `"lista"` | `enum` |
| lista aninhada | `- a` / `  - b` | item contém outra `.lista` | `"lista"` | `list` dentro de `list` |
| citação | `> q` | `.citacao` | `"citacao"` | `quote(block: true)` |
| bloco de código | ` ```swift ` | `.blocoDeCodigo(linguagem: "swift")` | `"bloco-de-codigo"` | `raw(block: true, lang: "swift")` |
| código sem linguagem | ` ``` ` | `linguagem: nil` | `linguagem: none` | `raw(lang: none)` |
| tabela | linhas entre barras verticais, com `---` sob o cabeçalho | `.tabela(cabecalho:linhas:alinhamentos:)` | `"tabela"` | `table` com `table.header` |
| regra | `---` | `.regra` | `"regra"` | `line(length: 100%)` |
| HTML | `<div>x</div>` | `.textoLiteral` | `"texto-literal"` | o texto cru, visível |

Testes:

| Teste | Afirma |
|---|---|
| `blocoViraArvore(_:)` — `arguments: CasosDeConteudo.blocos` | O Markdown produz exatamente a árvore da linha. |
| `blocoViraLiteral(_:)` — mesmos argumentos | O literal tem o `tipo` da linha e os campos do bloco. |
| `blocoViraElemento(_:)` — mesmos argumentos, `.tags(.motor)` | A sonda confirma o elemento Typst e os campos dele (`level`, `block`, `lang`, número de colunas). |
| `alinhamentosDaTabela` | `:--`, `:-:`, `--:` e sem marcação viram esquerda, centro, direita, esquerda; no Typst, `left`, `center`, `right`. |
| `tabelaColunasIguais` | Toda tabela tem colunas `1fr` — a largura é do papel, não do conteúdo. |
| `tabelaLinhaCurtaECompletada` / `tabelaLinhaLongaECortada` | Uma linha com menos células ganha células vazias; com mais, é cortada na largura do cabeçalho. Nenhuma das duas derruba a composição. |

### 3.2 Inlines

| Caso | Markdown | Árvore (`InlineDoc`) | Elemento Typst |
|---|---|---|---|
| texto | `abc` | `.texto` | o texto |
| ênfase | `*a*`, `_a_` | `.enfase` | `emph` |
| forte | `**a**` | `.forte` | `strong` |
| riscado | `~~a~~` | `.riscado` | `strike` |
| código | `` `a` `` | `.codigo` | `raw` (inline) |
| link | `[a](https://x)` | `.link(destino:filhos:)` | `link("https://x", …)` |
| quebra forçada | `a  ⏎b`, `a\⏎b` | `.quebraDeLinha` | `linebreak()` |
| quebra simples | `a⏎b` | `.texto(" ")` | espaço — quem quebra a linha é o motor |
| imagem | `![alt](f.png)` | `.texto("alt")` | o texto alternativo |
| HTML inline | `<b>` | `.texto("<b>")` | o texto cru |
| link sem destino | `[a]()` | `.enfase` | `emph` |

Testes: `inlineViraArvore(_:)`, `inlineViraLiteral(_:)` e
`inlineViraElemento(_:)`, sobre `CasosDeConteudo.inlines`, no mesmo desenho dos
blocos. Mais:

| Teste | Afirma |
|---|---|
| `inlinesAninham` | `**texto _misto_**` produz `forte[texto, enfase[misto]]`, e a ordem dos filhos se conserva. |
| `imagemNaoTemArquivo` | Imagem nunca vira `image(…)`: o Acervo não tem os arquivos do autor, e o motor não tem I/O. |

### 3.3 O literal de dados: nada é injetado

A propriedade que sustenta a camada: o texto do usuário só aparece **dentro de
string entre aspas**.

#### O problema que o literal resolve

O Typst tem dois modos de leitura. Em **marcação**, que é o modo normal de um
`.typ`, vários caracteres comuns são sintaxe: `#` abre código, `$` abre
matemática, `*` e `_` abrem negrito e ênfase, `@` faz uma referência, `[` e `]`
delimitam conteúdo. Se o conversor escrevesse o texto do usuário direto em
marcação, o texto deixaria de ser texto:

| O usuário digitou | Em marcação, o Typst entenderia |
|---|---|
| `Silva & Cia #2` | `#2` como código: imprime `2` e o `#` some |
| `#let x = 1` | uma definição de variável; nada aparece no papel |
| `custo $x$` | `x` como fórmula matemática |
| `ver @ref` | uma referência a um rótulo que não existe, e a composição falha |
| `5 * 3 * 2` | `3` em negrito |

Escapar todos esses caracteres em marcação é possível, mas a lista é longa,
depende do contexto e muda entre versões do Typst. Basta esquecer um para
abrir uma brecha.

#### Como o literal resolve

O conversor não escreve marcação. Escreve **dados**: dicionários, arranjos e
strings, que o `base.typ` lê e transforma em conteúdo. Para o parágrafo
`Silva & Cia #2`, o que vai para o `/documento.typ` é:

```typst
#let documento = (
  versao: 1,
  blocos: ((tipo: "paragrafo", filhos: ((tipo: "texto", valor: "Silva & Cia #2"),)),),
)
```

O texto digitado está só em `valor: "…"`. Dentro de uma string entre aspas, o
Typst não interpreta nada: `#`, `$`, `*` e `@` são caracteres como quaisquer
outros. Só dois têm significado ali dentro:

- `"`, que fecha a string;
- `\`, que inicia uma sequência de escape.

Por isso o escape tem exatamente dois caracteres, e a lista não depende da
versão do motor.

#### O que acontece se o escape falhar

O risco que sobra é uma aspa sem escape fechar a string antes da hora. Se o
usuário digitasse `a", tipo: "regra` e a aspa passasse crua, o literal ficaria
assim:

```typst
(tipo: "texto", valor: "a", tipo: "regra")
```

O texto do usuário teria virado **estrutura**: um campo a mais no dicionário.
Os testes abaixo existem para impedir isso. Cada um fecha uma porta pela qual o
texto poderia sair de dentro das aspas.

#### Os testes

Em todos eles, *a sonda confirma* quer dizer que o literal foi compilado pelo
motor de verdade (§1.3) e que o Typst devolveu o texto que recebeu. Não basta o
literal parecer certo em Swift.

| Teste | Afirma |
|---|---|
| `nadaEhInjetado(_:)` — `arguments:` `#`, `$x$`, `"`, `\`, `<tag>`, `@ref`, `~`, `#let x = 1`, `[`, `]`, `*`, `_`, `` ` ``, emoji, árabe | O literal tem aspas balanceadas, e a sonda confirma que o texto chega ao Typst **igual ao digitado**. |
| `escapeDeDoisCaracteres` | Só `\` e `"` são escapados; `Silva & Cia #2` atravessa intacto. |
| `controlesSaoEscapados` | `\n`, `\r`, `\t` e controles abaixo de `0x20` viram sequência de escape, e não terminam a string. |
| `arranjoDeUmElemento` | Arranjo de um item leva vírgula final — `(x,)` —, senão o Typst o leria como parêntese. |
| `literalDeclaraVersao` | Todo literal começa com `versao: ValorTypst.VersaoDoVocabulario`. |

O que cada um protege:

- **`nadaEhInjetado`** é o teste principal. Cada argumento é um caractere que
  seria sintaxe em marcação (a primeira tabela desta seção), mais emoji e
  árabe, que testam se texto fora do ASCII atravessa sem ser cortado. O teste
  verifica duas coisas: que toda string aberta no literal é fechada, e que o
  Typst devolve o texto **igual ao digitado**.
- **`escapeDeDoisCaracteres`** verifica o outro lado: escapar demais também é
  defeito. Se o conversor escapasse `&` ou `#`, dentro da string a contrabarra
  sobraria no papel ou viraria uma sequência de escape inválida. Só `\` e `"`
  podem mudar.
- **`controlesSaoEscapados`** cobre os caracteres invisíveis. Uma quebra de
  linha crua dentro da string, vinda de um parágrafo com várias linhas ou de um
  bloco de código, quebraria o literal. Por isso ela vira `\n`, e o mesmo vale
  para `\r`, `\t` e os demais controles abaixo de `0x20`.
- **`arranjoDeUmElemento`** cobre uma armadilha da sintaxe, e não do texto. No
  Typst, `(x)` é só `x` entre parênteses, e `(x,)` é um arranjo com um item. Um
  documento com um único parágrafo geraria `blocos: (…)` sem a vírgula, e o
  `base.typ` receberia um dicionário onde esperava uma lista.
- **`literalDeclaraVersao`** garante que todo literal carrega a versão do
  vocabulário. É ela que permite ao `base.typ` recusar com mensagem legível um
  literal gerado por uma versão diferente do conversor (§3.4).

  *Carregar a versão* quer dizer que o primeiro campo do literal é um número:

  ```typst
  #let documento = (
    versao: 1,        // ← a versão do vocabulário
    blocos: (…),
  )
  ```

  O **vocabulário** é a lista de tipos que o literal pode conter: `"titulo"`,
  `"paragrafo"`, `"tabela"` e os demais. Dois lados do código precisam
  concordar sobre ela. O Swift (`ValorTypst`) produz os tipos e declara
  `versaoDoVocabulario = 1`. O `base.typ` sabe transformar cada tipo em
  elemento Typst e declara `#let versao-do-vocabulario = 1`. Antes de desenhar
  qualquer coisa, `renderizar` compara os dois números:

  ```typst
  assert(
    doc.versao == versao-do-vocabulario,
    message: "Vocabulário incompatível: o app emitiu " + str(doc.versao),
  )
  ```

  Um exemplo: alguém acrescenta notas de rodapé. O Swift passa a emitir
  `tipo: "nota"` e a versão sobe para 2, mas a pessoa esquece de ensinar o
  `base.typ` a tratar `"nota"`. Sem a versão, o `base.typ` recebe um tipo que
  não conhece e desenha o aviso vermelho de bloco desconhecido no meio do PDF,
  o que pode passar despercebido. Com a versão, ele vê "recebi 2, conheço 1" e
  a composição para com *"Vocabulário incompatível: o app emitiu 2"*. O erro
  aparece na hora e com a causa escrita.

  Os três testes que cuidam disso se completam. `literalDeclaraVersao` garante
  que o campo existe, porque sem ele não há o que comparar.
  `versoesConcordam` (§3.4) garante que os dois números são iguais no código
  atual. `versaoDivergenteFalhaLegivel` (§3.4) garante que, se forem
  diferentes, a falha vem com a mensagem legível.

  A versão não protege documentos antigos: o literal não é guardado em disco,
  é gerado de novo a cada composição, e o Swift e o `base.typ` são distribuídos
  juntos no mesmo pacote. O que ela pega é um erro de quem desenvolve: mudar um
  lado do contrato e esquecer o outro.

### 3.4 O `base.typ`: o contrato do vocabulário

| Teste | Afirma |
|---|---|
| `versaoDivergenteFalhaLegivel` — `.tags(.motor)` | Um literal com versão diferente da do `base.typ` falha com `ErroTypst.Compilacao` contendo *"Vocabulário incompatível"*, em vez de um PDF silenciosamente errado. |
| `versoesConcordam` | `ValorTypst.VersaoDoVocabulario` é igual ao `versao-do-vocabulario` lido do recurso `base.typ`. |
| `noDesconhecidoEVisivel` — `.tags(.motor)` | Um `tipo` que o `base.typ` não conhece vira texto vermelho *"⟨bloco desconhecido: …⟩"* — feio, mas visível, nunca silêncio. |
| `baseNaoDecideAparencia` | O `base.typ` não contém `set` nem `show`: só constrói conteúdo. |

---

## 4. Suíte 2 — Gerador `documento-de-negocio`

`@Suite("Gerador documento-de-negocio", .tags(.gerador))`

A suíte é organizada **como o inspetor de formatação**: um grupo por seção da
tela, um teste por controle. Cada teste parte de `Apoio.TemplateMinimo`, aplica
o ajuste por `ComAjustes(ajustes)` — o mesmo caminho que o app usa — e afirma três
coisas:

1. **o valor inicial** do eixo é o que o `template.json` declara;
2. **o ajuste chega à regra** do elemento, com o valor formatado como o inspetor
   o mostra;
3. **o domínio é respeitado**: fora da faixa, o valor é aparado, não recusado.

O passo 3 é coberto uma vez, de forma transversal, em §4.8; os grupos
descrevem os passos 1 e 2.

### 4.1 Catálogo — o inspetor é função dos eixos

| Teste | Afirma |
|---|---|
| `inventarioDoInspetor` | A lista de eixos é **exatamente** esta, nesta ordem, com estes grupos, rótulos e domínios. É o teste que muda quando um controle entra ou sai da tela. |
| `valoresIniciaisVemDoTemplate` | Cada eixo nasce com o valor do `template.json`, e não de um padrão compilado. |
| `faixaDeTitulosEDaFamilia` | Os níveis 1 a 3 aparecem mesmo quando o array `titulos` só declara um — e um `####` no Markdown não faz nascer controle nenhum. |
| `nivelAusenteMostraHerdado` | Um nível sem entrada mostra fonte "— herdar do corpo —", tamanho igual ao do corpo, à esquerda, caixa normal. |

O inventário que `inventarioDoInspetor` fixa:

| Grupo | Rótulo | `id` | Domínio |
|---|---|---|---|
| Papel | Tamanho | `pagina.papel` | escolha: A4 (`a4`), A5 (`a5`), Carta (`us-letter`), Ofício (`us-legal`) |
| Papel | Margem superior / inferior / esquerda / direita | `pagina.margem.{topo,base,esquerda,direita}` | 0–60 mm, passo 1 |
| Texto | Fonte | `texto.familia` | famílias declaradas em `fontes` |
| Texto | Corpo | `texto.tamanho` | 7–18 pt, passo 0,5 |
| Texto | Cor do texto | `texto.cor` | cor |
| Texto | Justificar | `paragrafo.justificado` | alternância |
| Texto | Entrelinha | `paragrafo.entrelinha` | 0,4–1,6 em, passo 0,05 |
| Texto | Espaço entre parágrafos | `paragrafo.espacoEntre` | 0–2 em, passo 0,05 |
| Título *N* (1–3) | Fonte | `titulos.N.familia` | "herdar do corpo" + famílias declaradas |
| Título *N* | Tamanho | `titulos.N.tamanho` | 8–32 pt, passo 0,5 |
| Título *N* | Alinhamento | `titulos.N.alinhamento` | À esquerda, Centralizado, À direita |
| Título *N* | Caixa | `titulos.N.caixa` | Normal, Versais |
| Tabela | Espessura do filete | `tabela.espessura` | 0–3 pt, passo 0,1 |
| Tabela | Respiro das células | `tabela.respiro` | 0–12 pt, passo 0,5 |
| Tabela | Repetir cabeçalho nas páginas | `tabela.repetirCabecalho` | alternância |

Vinte e seis eixos. Os testes dos grupos a seguir são, cada um, uma linha desta
tabela.

### 4.2 Papel → `set page(…)`

`@Suite("Papel")`

| Teste | Controle | Campo do JSON | Ajuste de exemplo | A regra passa a conter |
|---|---|---|---|---|
| `tamanhoDoPapel(_:)` — `arguments:` as quatro opções | Tamanho | `pagina.papel` | Carta | `paper: "us-letter"` — o `id` da opção, o valor no `template.json` e o nome no Typst são o mesmo texto |
| `margem(_:)` — `arguments:` os quatro lados | Margem superior… | `pagina.margem.topo`… | 30 mm | `margin: (top: 30mm, …)` |

Os quatro lados formam um caso parametrizado (lado, chave Typst): `topo → top`,
`base → bottom`, `esquerda → left`, `direita → right`. Ajustar um lado **não**
altera os outros três.

### 4.3 Texto → `set text(…)` e `set par(…)`

`@Suite("Texto")`

| Teste | Controle | Campo do JSON | Ajuste de exemplo | A regra passa a conter |
|---|---|---|---|---|
| `fonteDoCorpo` | Fonte | `texto.familia` | uma família declarada | `set text(font: "…")` |
| `tamanhoDoCorpo` | Corpo | `texto.tamanho` | 9,5 pt | `size: 9.5pt` |
| `corDoTexto` | Cor do texto | `texto.cor` | `#000000` | `fill: rgb("#000000")` |
| `justificar(_:)` — `arguments: [true, false]` | Justificar | `paragrafo.justificado` | desligado | `set par(justify: false, …)` |
| `entrelinha` | Entrelinha | `paragrafo.entrelinha` | 0,65 em | `leading: 0.65em` |
| `espacoEntreParagrafos` | Espaço entre parágrafos | `paragrafo.espacoEntre` | 2 em | `spacing: 2em` |

E dois comportamentos do grupo:

| Teste | Afirma |
|---|---|
| `corInvalidaEIgnorada` | `texto.cor` com valor que não é hexadecimal (`"red"`, `"#zzz"`) não altera a regra: a cor do template continua. |
| `medidaComDecimalNaoArredonda` | 9,5 pt sai `9.5pt`, e 11 pt sai `11pt` — o formato do papel é o formato do inspetor. |

**Por que a cor do template é sempre válida.** Uma cor entra no template por
dois caminhos, e cada um trata o valor inválido de um jeito:

| Caminho | Quando acontece | Cor inválida | Teste |
|---|---|---|---|
| **Carga** do `template.json` | ao abrir o template, por `RegistroDeGeradores.Carregar` | **erro**: `TemplateInvalido(Campo: "cor")`, e o template não é carregado | `corInvalida(_:)`, §4.9 |
| **Ajuste** por `ComAjustes(ajustes)` | quando a pessoa mexe no inspetor, ou ao ler o `ajustes.json` | **ignorada**: o campo fica como estava | `corInvalidaEIgnorada`, aqui |

A ordem é sempre a mesma: primeiro carrega, depois ajusta. Um ajuste só pode
ser aplicado sobre um `TemplateCarregado`, e um `TemplateCarregado` só existe
se a carga deu certo. Na carga, toda cor passa pelo tipo `Cor`, que só aceita
`#` seguido de 3, 4, 6 ou 8 dígitos hexadecimais e lança erro em qualquer outro
valor. Por isso, quando um ajuste chega, a cor do template já foi validada. "A
cor do template continua" quer dizer que fica a cor que já passou pela carga,
e essa é válida por construção.

Os dois caminhos reagem de forma diferente porque o erro tem origens
diferentes:

- **Na carga**, quem erra é quem montou o template. Uma cor inválida ali é
  defeito da papelaria. Seguir em frente produziria um documento com a cor
  errada e sem aviso, então a carga para e diz o campo.
- **No ajuste**, o valor é uma preferência por cima de um template que já
  funciona. Pode ser um `ajustes.json` editado à mão, ou escrito por uma versão
  do app com outro formato de cor. Recusar o ajuste inteiro faria uma
  preferência malformada impedir a composição de um documento que compõe sem
  ela. Ignorar o valor segue a regra de todo ajuste (§4.8: valor fora do
  domínio é aparado, `id` desconhecido é ignorado): o ajuste degrada, não
  quebra.

### 4.4 Título 1, 2 e 3 → `show heading.where(level: N)`

`@Suite("Títulos")` — todos os testes parametrizados sobre `nivel in 1...3`, de
modo que o navegador mostra, por exemplo, *"alinhamento — Título 2"*.

| Teste | Controle | Campo do JSON | Ajuste de exemplo | A regra do nível passa a conter |
|---|---|---|---|---|
| `fonteDoTitulo(_:)` | Fonte | `titulos[n].familia` | uma família declarada | `text(…, font: "…")` |
| `fonteHerdada(_:)` | Fonte = "herdar do corpo" | `familia` ausente | herdar | `text(` **sem** `font:` — o título usa a família de `set text` |
| `tamanhoDoTitulo(_:)` | Tamanho | `titulos[n].tamanho` | 24,5 pt | `text(size: 24.5pt, …)` |
| `alinhamentoDoTitulo(_:)` — × as três opções | Alinhamento | `titulos[n].alinhamento` | Centralizado | `align(center, …)` |
| `caixaDoTitulo(_:)` — × Normal, Versais | Caixa | `titulos[n].caixa` | Versais | `upper(it.body)`; Normal → `it.body` |

E o que é próprio dos títulos:

| Teste | Afirma |
|---|---|
| `ajusteNaoVazaParaOutroNivel(_:)` | Ajustar o Título 2 não muda a regra dos níveis 1 e 3. |
| `nivelAusenteNaoEmiteRegra` | Um nível sem entrada no array não tem `show heading.where(level: 3)`: o papel usa o padrão do Typst. |
| `tocarNivelAusenteMaterializa` | Ajustar qualquer controle de um nível ausente cria a entrada, com tamanho do corpo, negrito, 1 em antes e 0,5 em depois, e a regra passa a existir. |
| `niveisAcimaDeTresSaoIgnorados` | `titulos.4.tamanho` é ignorado como `id` desconhecido; `####` no Markdown segue o padrão do Typst. |

### 4.5 Tabela → `set table(…)` e `set table.header(…)`

`@Suite("Tabela")`

| Teste | Controle | Campo do JSON | Ajuste de exemplo | A regra passa a conter |
|---|---|---|---|---|
| `espessuraDoFilete` | Espessura do filete | `tabela.espessura` | 0,5 pt | `stroke: (x, y) => (bottom: 0.5pt + rgb(…))` |
| `filetesSoNaBase` | — | — | — | o traço é só `bottom:`: não há filete vertical |
| `respiroDasCelulas` | Respiro das células | `tabela.respiro` | 5 pt | `inset: 5pt` |
| `repetirCabecalho(_:)` — `arguments: [true, false]` | Repetir cabeçalho nas páginas | `tabela.repetirCabecalho` | ligado | `set table.header(repeat: true)` |

### 4.6 Só no `template.json`: campos sem controle no inspetor

`@Suite("Campos sem controle")`

O esquema tem campos que a papelaria fixa e a tela não oferece. Os testes aqui
alteram o **JSON** e conferem a regra — é o caminho de quem monta um template.

| Teste | Campo do JSON | Valor | A regra passa a conter |
|---|---|---|---|
| `idiomaDoTexto` | `texto.idioma` | `"en"` | `set text(…, lang: "en")` |
| `corDoFilete` | `tabela.filete` | `#cccccc` | `+ rgb("#cccccc")` |
| `cabecalhoDaTabelaForte(_:)` — `[true, false]` | `tabela.cabecalhoForte` | `false` | **sem** `show table.cell.where(y: 0): strong` |
| `tituloForte(_:)` — `[true, false, ausente]` | `titulos[n].forte` | `false` | o corpo do título sem `strong(…)`; ausente vale `true` |
| `corDoTitulo` | `titulos[n].cor` | `#57534e` | `text(…, fill: rgb("#57534e"))`; ausente, sem `fill:` |
| `espacoDoTitulo` | `titulos[n].espacoAntes` / `espacoDepois` | 22 pt / 8 pt | `block(above: 22pt, below: 8pt, width: 100%, …)` |
| `estiloDeTexto` | `estilosDeTexto.nota` | 9 pt, `#57534e`, `caixa: alta`, `espacamento` | `#let est-nota(c) = text(size: 9pt, fill: …, tracking: …, upper(c))` |

### 4.7 Moldura: cabeçalho, rodapé e fundo

`@Suite("Moldura")` — é o que produz o "Página 1 de 2" da captura.

| Teste | Afirma |
|---|---|
| `semMolduraSemParametros` | Sem `moldura`, `set page` não tem `header:`, `footer:` nem `background:`, e as declarações de faixa não são emitidas. |
| `rodapeComCampos` | O rodapé de `Apoio.TemplateMinimo` emite `faixa(none, none, est-nota("Página " + campo-numero-da-pagina + " de " + campo-total-de-paginas))`. |
| `todasEABase` | `todas` vale para primeira e demais; `primeira` ou `demais` declarada substitui `todas` **inteira** naquela variante — a resolução é rasa. |
| `variantesIguaisSemContext` | Primeira e demais iguais emitem o nome da faixa direto; diferentes, um `context { if counter(page).get().first() == 1 … }`. |
| `afastamento` | `rodape.afastamento` vira `footer-descent:`; `cabecalho.afastamento`, `header-ascent:`. |
| `fundoDoTimbre` | `fundo.arquivo` vira `background: image("/template/<arquivo>", width: 100%, height: 100%)`. |
| `identidade(_:)` — `nome`, `linhas`, `logotipo` | `nome` é texto; `linhas` alternam texto e `linebreak()`; `logotipo` é `image` com a altura declarada. |
| `tipoDocumentalResolvidoNoGerador` | `campo: tipoDocumental` sai como texto literal (`tipoDocumental`, ou `nome` se ausente), nunca como consulta ao motor. |
| `pecaDesconhecidaOmitida` | Uma `peca` que o gerador não conhece some do papel, sem erro — é o `template.json` mais novo aberto por um app mais antigo. |
| `textoDaFaixaEscapado` | `"valor": "Silva & Cia #2 \"x\""` sai como literal escapado, pelo mesmo `LiteralTypst`. |

#### `todasEABase`: o que quer dizer "a resolução é rasa"

A moldura não é igual em todas as páginas. É comum a primeira página não ter
cabeçalho (o título já está no corpo) e as seguintes repetirem o título no
alto. Por isso `moldura.paginas` aceita três chaves, chamadas **variantes**:

| Variante | Vale para |
|---|---|
| `todas` | qualquer página que não tenha variante própria |
| `primeira` | só a página 1 |
| `demais` | da página 2 em diante |

Cada variante é um objeto com até três partes: `fundo`, `cabecalho` e
`rodape`. Na emissão, o gerador precisa de exatamente duas variantes, a da
primeira página e a das demais, e as resolve assim
(`Gerador.Resolver`):

```csharp
var primeira = declarada.Paginas.GetValueOrDefault("primeira") ?? todas;
var demais   = declarada.Paginas.GetValueOrDefault("demais")   ?? todas;
```

Ou seja: se `primeira` foi declarada, ela é usada; senão, usa-se `todas`. O
mesmo vale para `demais`.

**Rasa** quer dizer que a troca é do objeto inteiro, não campo a campo. Quando
`demais` existe, `todas` é ignorada nas demais páginas, **inclusive nas partes
que `demais` não declarou**. Não há mescla.

Um exemplo:

```json
"paginas": {
  "todas":  { "rodape":    { … "Página X de Y" … } },
  "demais": { "cabecalho": { … título do documento … } }
}
```

| Página | Variante usada | Cabeçalho | Rodapé |
|---|---|---|---|
| 1 | `todas` (não há `primeira`) | — | "Página X de Y" |
| 2 em diante | `demais` | título | **nenhum** |

Quem esperava uma mescla ("as demais herdam o rodapé de `todas` e acrescentam
o cabeçalho") vê o rodapé sumir da página 2 em diante. Para ter os dois, a
variante `demais` precisa repetir o rodapé. É o que faz o `contrato-padrao.json`
do app: o rodapé aparece em `todas` e de novo, idêntico, em `demais`.

A regra é rasa de propósito (doc 3, §8). O doc 3 não detalha o porquê, mas a
vantagem se vê no exemplo. Com mescla, quem lê o JSON precisaria
juntar mentalmente duas variantes para saber o que sai numa página, e surgiria
uma pergunta sem resposta óbvia: como declarar "nas demais, **sem** rodapé"? Na
regra rasa, cada variante declarada descreve sozinha a sua página, e a
ausência de uma parte significa que ela não existe.

O teste `todasEABase` fixa isso:
- só `todas` declarada: primeira e demais saem iguais a `todas`;
- `primeira` (ou `demais`) declarada: aquela variante sai exatamente como
  declarada, sem nenhuma parte de `todas`, mesmo quando lhe falta algo que
  `todas` tem;
- a outra variante, não declarada, continua saindo igual a `todas`.

### 4.8 Ajustes: as regras que valem para todo eixo

`@Suite("Ajustes")`

| Teste | Afirma |
|---|---|
| `umEixoUmaRegra(_:)` — `arguments:` os 26 eixos | Ajustar o eixo muda a regra do elemento dele e **nenhuma outra linha** do `/template.typ`. Cada eixo traz, no caso de teste, o prefixo da regra que pode mudar. |
| `foraDaFaixaEAparado(_:)` — `arguments:` os eixos de medida × {abaixo do mínimo, acima do máximo} | O valor vira o limite do domínio — `999 pt` no corpo sai `18pt`. Um `ajustes.json` editado à mão passa pelo mesmo funil. |
| `escolhaForaDoConjunto` | Uma escolha que não está nas opções vira a **primeira** opção (papel `"tabloide"` → A4; alinhamento `"justificado"` → à esquerda). |
| `fonteNaoEmbarcadaNaoEntra(_:)` — corpo e títulos | Uma família fora de `fontes` não chega à regra: no corpo, a família do template continua; no título, cai em "herdar do corpo". |
| `valorDeOutroTipoEIgnorado` | `.alternancia(true)` em `texto.tamanho` não altera nada. |
| `idDesconhecidoEIgnorado` | Um `id` que não existe é ignorado em silêncio, e os outros ajustes do mesmo lote valem. |
| `originalIntacto` | `comAjustes` devolve **outro** template; o original continua com os valores da papelaria. |
| `ajusteVazioDevolveAPapelaria` | `comAjustes([:])` emite o mesmo `/template.typ` que o template sem ajuste. |
| `encadearAssaNaBase` | Aplicar sobre um template já ajustado faz do ajuste o novo padrão — por isso o app guarda o template do pacote e aplica sempre sobre ele. |
| `eixoEEmissaoConcordam(_:)` — os 26 eixos | Depois do ajuste, o valor que o eixo exibe e o que a regra contém são o mesmo número na mesma unidade: o inspetor nunca mostra o que o papel não tem. |

### 4.9 Carga e validação do `template.json`

`@Suite("Carga")` — todos afirmam o **caso** de `ErroTypst` e o `campo`.

| Teste | Entrada | Resultado |
|---|---|---|
| `esquemaAusente` | JSON sem `esquema` | `TemplateInvalido(campo: "esquema")` |
| `esquemaFuturo` | `esquema: 2` | `EsquemaFuturo(familia: "documento-de-negocio", esquema: 2, atual: 1)` |
| `jsonMalformado` | campo obrigatório faltando | `TemplateInvalido(campo: "template.json")` |
| `medidaInvalida(_:)` | `"20"`, `"calc(1pt+2pt)"` | `TemplateInvalido(campo: "medida")` |
| `papelForaDoVocabulario(_:)` | `"letter"`, `"legal"`, `"carta"` | `TemplateInvalido(campo: "template.json")` — o papel é guardado com o nome do Typst |
| `corInvalida(_:)` | `"red"`, `"#zzz"` | `TemplateInvalido(Campo: "cor")` |
| `familiaCitadaNaoDeclarada` | título com `familia` fora de `fontes` | `TemplateInvalido(campo: "fontes")` |
| `nivelRepetido` | dois títulos de nível 1 | `TemplateInvalido(campo: "titulos")` |
| `varianteDePaginaInvalida` | `paginas.ultima` | `TemplateInvalido(campo: "moldura.paginas")` |
| `ativoAusente(_:)` | `fundo` ou `imagem` sem o ativo | `TemplateInvalido` com o caminho da variante |
| `estiloInexistente` | faixa com `estilo` fora de `estilosDeTexto` | `TemplateInvalido` |
| `campoDerivadoInvalido` | `campo: "data"` | `TemplateInvalido` |
| `identidadeNaoDeclarada` | peça `identidade` sem a seção | `TemplateInvalido` |
| `afastamentoDivergente` | 10 mm na primeira, 20 mm nas demais | `TemplateInvalido(campo: "moldura.rodape.afastamento")` |
| `familiaDesconhecida` | `RegistroDeGeradores.Carregar(familia: "abnt", …)` | `FamiliaDesconhecida("abnt")` |

E um teste de propriedade: `cargaEhPura` — carregar o mesmo JSON duas vezes
emite o mesmo `/template.typ`, e a ordem de `estilosDeTexto` no JSON não muda a
saída (as declarações saem em ordem alfabética).

---

## 5. Suíte 3 — Integração: pelo `RenderizadorPDF`

`@Suite("Integração", .tags(.integracao, .motor))`

Aqui a entrada é sempre `RenderizadorPDF` — `acervo(markdown:template:)` para ver
o que o motor recebe, `compor(markdown:template:)` para ver o que ele devolve.
Nada é remontado pelo teste.

### 5.1 O Acervo

`@Suite("Acervo")`

| Teste | Afirma |
|---|---|
| `quatroCaminhos` | O motor recebe `/documento.typ`, `/template.typ`, `/notiq/base.typ` e os ativos sob `/template/` — e nada mais. |
| `esqueletoNaoMudaEntreTemplates` | Com o mesmo Markdown, `/documento.typ` é idêntico para dois templates diferentes: duas importações, o literal, `#show: estilo`, `#renderizar(documento)`. |
| `templateEOQueOGeradorEmite` | `/template.typ` é exatamente `template.emitirTyp()`, já com os ajustes. |
| `baseEORecurso` | `/notiq/base.typ` é o recurso do pacote, byte a byte. |

### 5.2 Cada elemento, emitido e vestido

`@Suite("Elementos")`

| Teste | Afirma |
|---|---|
| `elementoChegaAoMotor(_:)` — `arguments: CasosDeConteudo.blocos` | O literal de `/documento.typ` contém o `tipo` do elemento, e a composição com `Apoio.TemplateMinimo` não lança. |
| `elementoTemRegra(_:)` — título 1–3, parágrafo, tabela | Para cada elemento que a família estiliza, `/template.typ` tem a regra dele (§1.2). |
| `elementosSemRegraUsamOPadrao` — lista, citação, código, regra | A família **não** estiliza esses elementos: não há `set`/`show` para eles, e o papel usa o padrão do Typst. É uma afirmação do estado atual — quando um ganhar eixo, este teste muda. |
| `textoChegaAoPapel` | O texto de um documento com todos os elementos aparece no PDF extraído, na ordem do Markdown. |
| `nadaEhInjetadoAtePDF(_:)` — os mesmos argumentos de §3.3 | O texto perigoso aparece **igual** no texto extraído do PDF. |

### 5.3 O inspetor no papel

`@Suite("Formatação no PDF")` — o que a captura promete, conferido no PDF com
o PdfPig (no Swift, PDFKit), com tolerância de 0,5 mm ou 0,5 pt.

| Teste | Ajuste | Afirma no PDF |
|---|---|---|
| `papelNoPDF(_:)` — `zip` das quatro opções com as medidas | Tamanho | A *media box* tem 210 × 297 (A4), 148 × 210 (A5), 215,9 × 279,4 (Carta), 215,9 × 355,6 mm (Ofício). |
| `margemEsquerdaNoPDF` | Margem esquerda 34 mm | O texto do corpo começa a 34 mm da borda. |
| `margemSuperiorNoPDF` | Margem superior 30 mm | A primeira linha fica 30 mm abaixo de onde estaria com margem 0. A medida é diferencial porque o Typst põe a altura das versais na margem, e a caixa do glifo passa um pouco acima dela. |
| `tamanhoNoPDF(_:)` — corpo e títulos 1–3 | Corpo 9,5 pt; Título 1 24,5 pt | A fonte do trecho correspondente, lida no `attributedString` da página, tem o tamanho ajustado. |
| `versaisNoPDF` | Título 1 em Versais | O título sai em maiúsculas no texto extraído; o Markdown continua em caixa normal. |
| `cabecalhoRepetidoNoPDF(_:)` — `[true, false]` | Repetir cabeçalho | Com uma tabela de 80 linhas, as páginas de continuação começam (ou não) pelo cabeçalho. |
| `rodapeNoPDF` | — | Cada página termina com "Página *i* de *n*". |
| `aCapturaDoInspetor` | `Apoio.TemplateDaCaptura` sobre o contrato da captura | A4, duas páginas, "CONTRATO DE LOCAÇÃO" em versais, "Página 1 de 2". |

### 5.4 Robustez: compõe sempre, ou diz por quê

`@Suite("Robustez")`

| Teste | Afirma |
|---|---|
| `markdownArbitrarioCompila(_:)` — vazio, `#`, `$x^2$`, aspas, contrabarra, `<script>`, `@ref`, `#let estilo(x) = 1`, árabe, emoji, lista de seis níveis, tabela vazia, código, citação, `***`, link e imagem | Qualquer `String` produz um PDF. |
| `linhaEnorme` | Uma linha de dez mil caracteres compõe. |
| `todoValorDoDominioCompila(_:)` — os eixos de medida × {mínimo, máximo}; as escolhas × cada opção | Nenhum valor que o inspetor consegue produzir quebra o motor — inclusive margem 0 e filete 0. |
| `determinismo` | O mesmo Markdown com o mesmo template produz o mesmo PDF, byte a byte (o `today()` é fixo). |
| `composicaoNaoMudaOTemplate` | Compor não altera o `TemplateCarregado` nem os eixos dele. |
| `fonteQueFaltaBarradaAntes` | Um template cuja família não foi embarcada lança `FonteNaoEmbarcada(familia:)` com o nome dela, **antes** de chamar o motor. |
| `avisosNaoBloqueiam` | Um documento que gera aviso do motor devolve PDF e `avisos` não vazio. |

---

## 6. Pontos em aberto

Comportamentos que escrever estes testes revelou e que **não devem ser
cimentados** como especificação. Cada um entra como `withKnownIssue` — no
MarkDocs.Core, `ProblemaConhecido.Registrar` — no teste indicado: o
comportamento atual fica registrado, e no dia em que for corrigido o teste passa
a acusar a correção, e o registro sai.

Os sete são os mesmos do NotiqTypst, e seis valem igual aqui. O 6 **não
ocorre** no MarkDocs.Core (ver o próprio item).

1. **O limite do domínio ignora a unidade.** `paragrafo.espacoEntre` tem domínio
   0–2 **em**, mas os templates declaram `"8pt"` ou `"10pt"`. O eixo exibe
   `.medida(10, "pt")`, fora da faixa, e o inspetor mostra o controle no máximo
   como "2em" — é o que aparece na captura — enquanto o papel tem 10 pt. Ao
   tocar o controle, o valor vira `em` e o espaçamento salta. O mesmo vale para
   margens declaradas em `cm`. *Teste:* `Ajustes › eixoEEmissaoConcordam`.
2. **A lista numerada perde o número inicial.** `3. item` começa em 1: a árvore
   não guarda o início, e o `base.typ` chama `enum(..itens)` sem `start:`.
   *Teste:* `Conteúdo › blocoViraElemento`, caso "lista numerada a partir de 3".
3. **`alinhamento: "justificado"` num título é aceito e sai à esquerda.** O
   esquema admite o valor, o domínio do eixo não, e a emissão o converte em
   `left` sem avisar. *Teste:* `Carga`, caso próprio.
4. **Escolha inválida e família inválida degradam de formas diferentes.** Escolha
   fora do conjunto vira a primeira opção (papel inválido → A4, mesmo que o
   template fosse Carta); família inválida mantém a do template. O segundo é o
   comportamento que "degrada, não quebra" pede. *Teste:* `Ajustes ›
   escolhaForaDoConjunto`.

Os três seguintes apareceram ao implementar os testes. Um quarto — Carta e
Ofício emitiam `paper: "letter"` e `"legal"`, que o Typst 0.15 não conhece, e
a composição falhava — foi corrigido no mesmo trabalho, sem tradução: o
`template.json` e o `ajustes.json` passam a guardar os nomes do próprio Typst,
`us-letter` e `us-legal`, e o nome curto é recusado na carga
(`Carga › papelForaDoVocabulario`).

5. **Variantes iguais emitem `context` assim mesmo.** Com só `todas`
   declarada, primeira e demais têm a mesma faixa, mas o gerador declara
   `rodape-primeira` e `rodape-demais` e compara os **nomes**, que sempre
   diferem. Sai uma consulta ao contador em toda página para decidir sempre a
   mesma coisa. O papel fica certo; o custo é que é desnecessário. *Teste:*
   `Moldura › variantesIguaisSemContext`.
6. **A aspa reta vira aspa tipográfica.** `"` chega ao papel como `“`: o Typst
   aplica aspas inteligentes também ao texto que veio de uma string. Não é
   injeção — nada virou sintaxe —, mas o texto não é igual ao digitado. Pode
   ser o comportamento desejado; precisa ser decidido. *Teste:* `Elementos ›
   nadaEhInjetadoAtePDF`, caso `"`.

   **No MarkDocs.Core a aspa chega reta, igual ao digitado** — e por isso o
   caso `"` não é problema conhecido aqui: a asserção é a mesma, e passa. O
   glifo desenhado no PDF é a aspa reta, e o texto extraído também. A
   diferença está antes do Typst: o Markdig não aplica aspas tipográficas, e o
   Typst não as aplica a texto que vem de uma string. No NotiqTypst, a aspa
   curva vem do caminho do Swift (o swift-markdown ou o PDFKit), não do motor.
7. **Texto sem glifo nas fontes embarcadas some sem aviso.** Emoji e árabe não
   existem na Libertinus. O emoji desaparece do papel e o árabe sai
   incompleto, e `avisos` vem vazio — o modo de falha "parece certo" que o doc
   2 §7 quer evitar para fontes. *Teste:* `Elementos › nadaEhInjetadoAtePDF`,
   casos emoji e árabe.

---

## 7. Divergências de forma entre o Markdig e o swift-markdown

O MarkDocs.Core usa o Markdig onde o NotiqTypst usa o swift-markdown (cmark-gfm).
As diferenças de forma que importam ao vocabulário são absorvidas em
`Conteudo/ArvoreDeMarkdown.cs`, e a tabela de casos da §3 fixa que a árvore sai
igual à do Swift:

| O cmark-gfm entrega | O Markdig entrega | O conversor faz |
|---|---|---|
| código e HTML com `\n` no fim de cada linha | as linhas sem o `\n` | acrescenta o `\n` |
| linhas de tabela completadas ou cortadas na largura do cabeçalho | as células como vieram | completa com célula vazia e corta, como o GFM |
| `[` sem par como texto | um delimitador de link que não fechou | vira texto (`[`) |
| definições de link fora da árvore | um grupo de definições | ignora |

Uma diferença **não** foi reproduzida, de propósito: o NotiqTypst analisa o
Markdown com `parseBlockDirectives`, e uma linha que começa com `@nome` vira uma
diretiva de bloco — o texto dela some do papel. No MarkDocs.Core, `@ref` é texto
de parágrafo. Nenhum teste distingue os dois, e o comportamento daqui é o que a
§3.3 pede (o texto atravessa).

---

## 8. Extensão: as imagens do documento

**Sem par no NotiqTypst.** O vocabulário do MarkDocs.Core é o 2: acrescenta
`"imagem"`. A convenção, o desenho e o caminho de volta para o Swift estão em
[`Imagens.md`](Imagens.md).

A extensão não altera nenhum teste transcrito: sem imagens entregues ao
renderizador — o caso de todos eles —, a imagem continua sendo o texto
alternativo, o Acervo tem os mesmos quatro caminhos, e o PDF é byte a byte o
mesmo (`ImagensTests › SemImagensOPdfEOMesmo`). `versoesConcordam` e
`literalDeclaraVersao` comparam o número do C# com o do `base.typ`, e os dois
sobem juntos.

| Teste (`Extensoes/ImagensTests.cs`) | Afirma |
|---|---|
| `NomeValido(_:)` | Um nome é uma componente de caminho: letras, dígitos, `.`, `-`, `_`; sem espaço, sem barra, sem começar por ponto. |
| `NomeCitado(_:)` | `imagens/x.png` e `./imagens/x.png` citam `x.png`; URL externa, caminho absoluto, `..` e subpasta não citam nada. |
| `ImagemEntregueViraImagem(_:)` | Com o nome entregue, `![Planta](imagens/planta.png)` vira `InlineDoc.Imagem`. |
| `ImagemNaoEntregueContinuaTexto(_:)` | Fora dos nomes entregues, a imagem continua sendo o texto alternativo — como no NotiqTypst. |
| `LiteralCitaOCaminhoNoAcervo` | O literal leva `tipo: "imagem"`, o caminho `/imagens/<nome>` e o texto alternativo, entre aspas. |
| `AcervoGuardaAImagemAoLadoDoDocumento` | A imagem entra no Acervo em `/imagens/<nome>`, ao lado de `/documento.typ`. |
| `NomeInvalidoERecusado` | Um nome inválido entregue ao renderizador é `EntradaInvalida`. |
| `SondaVeUmaImagem` | No Typst, a imagem é uma `box` dentro do parágrafo (sozinha, seria um bloco e o Typst a descartaria). |
| `ImagemChegaAoPapel` | O PDF tem a imagem. |
| `ImagemNuncaPassaDaColuna` | Uma imagem larga ocupa a coluna de texto; uma pequena fica no tamanho natural. |
| `SemImagensOPdfEOMesmo` | Sem imagens, o PDF é o mesmo da sobrecarga sem o parâmetro. |

---

## 9. Como rodar

```sh
dotnet test MarkDocs.Core/MarkDocs.Core.sln
dotnet test MarkDocs.Core/MarkDocs.Core.sln --filter "Tag=gerador"
dotnet test MarkDocs.Core/MarkDocs.Core.sln --filter "FullyQualifiedName~Integracao"
```

O motor nativo vem de `Artefatos/runtimes/<rid>/native/`. A fatia `linux-x64`
está versionada; num Mac, rode antes `Scripts/construir-typst.sh` (pede Rust).
`Tag=motor` separa o que atravessa o Rust do que é C# puro.

Hoje são **461 casos** — os 433 da transcrição (os mesmos 116 testes do
NotiqTypst, contados por caso de cada tabela) e os 28 da extensão de imagens —,
e passam com 7 casos registrados como problema conhecido: os pontos 1–5 e 7 da
§6 (o 7 em dois casos, emoji e árabe).

### De onde veio cada teste

Os nomes seguem os do NotiqTypst em PascalCase (`blocoViraArvore` →
`BlocoViraArvore`), na mesma suíte e na mesma ordem. Quem procura um teste deste
documento procura o mesmo nome com a inicial maiúscula.
