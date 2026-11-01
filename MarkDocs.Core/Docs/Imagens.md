# Imagens no Markdown

- Status: implementado no MarkDocs.Core (vocabulário 2) e no app web; **ainda
  não existe no NotiqTypst**.
- Resolve a pendência da especificação 001 (`Docs/especificacao-inicial/001-modelo-de-dados-web.md`,
  "Pendência"): *"Imagem no Markdown ainda não existe no NotiqTypst, nem,
  portanto, no MarkDocs.Core."*

## O problema

Um documento cita imagens no Markdown — uma planta, uma foto do imóvel, um
gráfico. A mesma citação precisa funcionar em três lugares, sem que o Markdown
seja reescrito em nenhum deles:

1. **no PDF**, composto pelo motor Typst, que não tem I/O e só enxerga o Acervo
   que lhe é entregue;
2. **na web**, onde o Markdown é renderizado no navegador;
3. **numa exportação**, em que o documento vira uma pasta que qualquer
   visualizador de Markdown abre.

E só as imagens que o documento declara podem aparecer: nada de URL externa,
nada de caminho que aponte para outro documento ou para o bucket inteiro.

## A convenção: um caminho relativo e fixo

O Markdown cita a imagem por **`imagens/<nome>`**:

```markdown
![Planta do escritório](imagens/planta.png)
```

`<nome>` é o `Nome` de um ativo do documento com papel `Imagem`
(`ReferenciaDeAtivo(Id, Nome, PapelDoAtivo.Imagem)`). É uma componente de
caminho só — letras, dígitos, `.`, `-` e `_`, sem espaço e sem começar por ponto
(`ImagensDoDocumento.NomeValido`). O app reduz o nome do arquivo enviado a essa
forma: `Planta Baixa (1).PNG` vira `planta-baixa-1.png`.

O caminho é **relativo ao documento**, e é isso que o faz valer nos três lugares:
cada lugar decide o que é "a pasta do documento".

| Onde | A pasta do documento | `imagens/planta.png` resolve para |
|---|---|---|
| PDF (Acervo do Typst) | a raiz do Acervo, onde está `/documento.typ` | `/imagens/planta.png` |
| Web | o endereço do documento na API, `/api/documentos/{id}/` | `/api/documentos/{id}/imagens/planta.png` |
| Exportação | a pasta exportada, onde está `documento.md` | `./imagens/planta.png` |

A forma `./imagens/planta.png` também é aceita. Qualquer outro destino — URL
externa, caminho absoluto, `..`, subpasta, nome que não está nos ativos — cai no
comportamento do NotiqTypst: a imagem vira o **texto alternativo**. Nada quebra,
e nada que o documento não declare chega ao papel ou à tela.

## No PDF: o MarkDocs.Core

O Acervo ganha um diretório irmão de `/documento.typ`:

```
/documento.typ          o esqueleto, com o literal do conteúdo
/template.typ           o que o gerador emitiu
/notiq/base.typ         o intérprete do vocabulário
/template/timbre.svg    os ativos do template
/imagens/planta.png     as imagens do documento        ← novo
```

As mudanças seguem a receita do pacote para um elemento novo do vocabulário
("três lugares, e a versão sobe"):

1. **`InlineDoc.Imagem(Nome, Alternativo)`** em `ArvoreDeMarkdown`. O conversor
   recebe os nomes das imagens entregues; uma imagem citada por
   `imagens/<nome>` com o nome no conjunto vira `Imagem`, e qualquer outra
   continua sendo `Texto(alternativo)`.
2. **`(tipo: "imagem", caminho: "/imagens/<nome>", alternativo: "…")`** em
   `ValorTypst`. O caminho e o texto alternativo são strings entre aspas, pelo
   mesmo `LiteralTypst`: nenhum nome vira sintaxe.
3. **O caso `"imagem"` em `base.typ`**: a imagem dentro de uma `box` (sozinha,
   ela é um bloco, e o Typst a descartaria dentro de um parágrafo), com a largura
   limitada à da coluna — uma imagem larga ocupa a coluna, uma pequena fica no
   tamanho natural.
4. **`ValorTypst.VersaoDoVocabulario` = 2**, e o `#let versao-do-vocabulario`
   do `base.typ` junto.

A superfície pública ganha uma sobrecarga, e nada muda nas existentes:

```csharp
renderizador.Pdf(markdown, template);             // como no NotiqTypst
renderizador.Pdf(markdown, template, imagens);    // imagens: nome → bytes
```

O PDF continua sendo função das entradas — agora (Markdown, template, ajustes,
**imagens do documento**). Sem imagens, a saída é a do NotiqTypst byte a byte,
e todos os testes transcritos passam sem alteração (`Testes.md`, §8).

## Na web: o app

- **Os ativos.** `POST /api/documentos/{id}/imagens` grava a imagem no
  armazenamento de objetos com uma chave nova (objeto no bucket é imutável) e a
  acrescenta aos `Ativos` do documento com papel `Imagem`. Um nome que já existe
  é substituído, e o objeto antigo sai do bucket.
- **O endereço.** `GET /api/documentos/{id}/imagens/{nome}` serve a imagem
  **só se** `{nome}` está nos ativos do documento — o bucket nunca é consultado
  por um caminho que o documento não declare. É uma subseção do endereço do
  documento, de propósito: é contra `/api/documentos/{id}/` que o relativo
  `imagens/<nome>` se resolve.
- **A tela.** A visualização do Markdown (react-markdown) resolve
  `imagens/<nome>` contra a base do documento e mostra só as imagens dos ativos;
  as demais aparecem como o texto alternativo, como no PDF. Colar ou arrastar
  uma imagem no editor a envia e insere `![…](imagens/<nome>)` no cursor.
- **O PDF.** O `CompositorDePdf` baixa as imagens dos ativos e as entrega ao
  renderizador junto com os ativos do template.
- **Aplicar um template** troca a formatação e os ativos de template, e
  **mantém as imagens** — elas são do documento. Pela mesma razão, um template
  criado a partir de um documento não leva as imagens dele.

O endereço não carrega o tipo documental (`Contrato`, `Recibo`): o tipo é um
rótulo editável, e um endereço que muda com ele quebraria links. O "tipo" do
endereço é o recurso — `/api/documentos/{id}`; um tipo de documento com classe
própria (um `ContratoDeLocacao`) teria o seu, `/api/contratos-de-locacao/{id}`,
e a mesma subseção `imagens/`.

## Numa exportação

Fora do MVP, mas a convenção já a resolve: exportar é gravar `documento.md` e,
ao lado, a pasta `imagens/` com cada ativo de papel `Imagem` pelo seu `Nome`.
O Markdown não muda uma vírgula.

## O caminho de volta para o NotiqTypst

A especificação 001 pede que a mudança aconteça **primeiro no Swift**, para os
dois pacotes continuarem passando os mesmos testes. Aqui ela veio antes, porque
a pergunta era como montar a renderização de ponta a ponta. Portar é repetir os
mesmos quatro passos no Swift:

1. `case imagem(nome: String, alternativo: String)` em `InlineDoc`, e
   `ArvoreDeMarkdown.converter(_:imagens:)` recebendo os nomes entregues (com
   `converter(_:)` delegando com o conjunto vazio).
2. O caso `.imagem` em `ValorTypst`, e `versaoDoVocabulario = 2`.
3. O `base.typ` desta pasta (`Sources/MarkDocs.Core/Conteudo/Recursos/base.typ`)
   copiado, com o caminho `/notiq/base.typ` que os dois pacotes já compartilham.
4. `Acervo` com o diretório `/imagens/`, e
   `RenderizadorPDF.compor(markdown:template:imagens:)`.

E os testes de `Tests/MarkDocs.Core.Tests/Extensoes/ImagensTests.cs`
transcritos para o `NotiqTypstTests`. A partir daí, a extensão deixa de ser
extensão.
