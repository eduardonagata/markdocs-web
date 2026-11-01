// /notiq/base.typ — recurso do pacote MarkDocs.Core (transcrito do NotiqTypst;
// o caminho no Acervo é o mesmo nos dois pacotes).
//
// O outro lado do acordo: o C# escreve uma árvore de dados, e este arquivo a
// interpreta. Ele **só constrói conteúdo** — heading,
// par, table. Não fixa fonte, tamanho, margem nem espaçamento: isso é do
// template. Essa separação é o que permite trocar a aparência sem tocar na
// conversão.

// 1 é o vocabulário do NotiqTypst; 2 acrescenta "imagem" (extensão do
// MarkDocs.Core: a imagem do documento, que o Acervo guarda em /imagens/).
#let versao-do-vocabulario = 2

#let inline(no) = {
  if no.tipo == "texto" { no.valor }
  else if no.tipo == "forte" { strong(no.filhos.map(inline).join()) }
  else if no.tipo == "enfase" { emph(no.filhos.map(inline).join()) }
  else if no.tipo == "riscado" { strike(no.filhos.map(inline).join()) }
  else if no.tipo == "codigo" { raw(no.valor) }
  else if no.tipo == "link" { link(no.destino, no.filhos.map(inline).join()) }
  else if no.tipo == "quebra-de-linha" { linebreak() }
  // A imagem nunca passa da largura da coluna; menor que ela, fica no tamanho
  // natural. Só existe se o documento a entregou ao Acervo. A `box` é o que a
  // deixa morar num parágrafo: sozinha, a imagem é um bloco.
  else if no.tipo == "imagem" {
    box(layout(regiao => {
      let natural = measure(image(no.caminho)).width
      image(no.caminho, alt: no.alternativo, width: calc.min(natural, regiao.width))
    }))
  }
  // Nó desconhecido é tinta vermelha, não silêncio: feio, mas visível.
  else { text(fill: red)[⟨nó desconhecido: #no.tipo⟩] }
}

#let celula(c) = c.map(inline).join()

#let tabela(no) = {
  let largura = no.alinhamentos.len()
  if largura == 0 { return none }

  let alinhamentos = no.alinhamentos.map(a => {
    if a == "centro" { center }
    else if a == "direita" { right }
    else { left }
  })

  // Uma linha curta não derruba a tabela: completa-se com célula vazia.
  let preencher(linha) = {
    let cs = linha.map(celula)
    while cs.len() < largura { cs.push([]) }
    cs.slice(0, largura)
  }

  let corpo = ()
  for linha in no.linhas { corpo += preencher(linha) }

  let colunas = (1fr,) * largura
  let alinhar = (x, y) => alinhamentos.at(x, default: left)

  if no.cabecalho.len() > 0 {
    table(
      columns: colunas,
      align: alinhar,
      table.header(..preencher(no.cabecalho)),
      ..corpo,
    )
  } else {
    table(columns: colunas, align: alinhar, ..corpo)
  }
}

#let bloco(no) = {
  if no.tipo == "titulo" { heading(level: no.nivel, no.filhos.map(inline).join()) }
  else if no.tipo == "paragrafo" { par(no.filhos.map(inline).join()) }
  else if no.tipo == "lista" {
    let itens = no.itens.map(i => i.map(bloco).join())
    if no.ordenada { enum(..itens) } else { list(..itens) }
  }
  else if no.tipo == "citacao" { quote(block: true, no.filhos.map(bloco).join()) }
  else if no.tipo == "bloco-de-codigo" {
    raw(no.texto, lang: no.linguagem, block: true)
  }
  else if no.tipo == "tabela" { tabela(no) }
  else if no.tipo == "regra" { line(length: 100%) }
  else if no.tipo == "texto-literal" { no.valor }
  else { text(fill: red)[⟨bloco desconhecido: #no.tipo⟩] }
}

#let renderizar(doc) = {
  // O vocabulário de nós é um contrato entre o C# e este arquivo. Se um dia
  // divergirem, a compilação falha com mensagem legível em vez de produzir um
  // PDF silenciosamente errado.
  assert(
    doc.versao == versao-do-vocabulario,
    message: "Vocabulário incompatível: o app emitiu " + str(doc.versao),
  )
  doc.blocos.map(bloco).join()
}
