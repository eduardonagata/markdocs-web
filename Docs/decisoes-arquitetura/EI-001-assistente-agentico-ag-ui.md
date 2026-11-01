# EI-001: O assistente — um agente que age sobre a tela, via AG-UI

| | |
|---|---|
| **Status** | Implementado |
| **Data** | 2026-11-01 |
| **Depende de** | [Especificação 001](../especificacao-inicial/001-modelo-de-dados-web.md) (o modelo de documentos e templates); o editor MDXEditor (o texto que o agente edita é o do editor). |
| **Escopo** | Um assistente conversacional disponível em todas as telas, aberto por um botão ou por ⌘J / Ctrl+J. No backend, um agente do [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) exposto em [AG-UI](https://docs.ag-ui.com/) por `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`, em `POST /api/agente`, com ferramentas **de leitura** (listar e consultar documentos, listar templates). No frontend, `@ag-ui/client` e um painel próprio (Fluent UI), com as ferramentas **de edição** — todas executadas no navegador, sobre o que está na tela. Modelo pela OpenAI ou pelo Microsoft Foundry, escolhido por variáveis de ambiente. |
| **Não cobre** | Persistência do histórico da conversa no servidor (§9: a arquitetura está pronta, o *onde* é decisão em aberto). Autenticação (o app ainda não tem; §11). Edição de formatação eixo a eixo pelo agente (ele aplica templates; ajustar um eixo continua sendo do inspetor). Envio de imagens pelo agente. |

---

## 1. A ideia em uma frase

**O agente não tem uma API própria para mudar documentos: ele usa a tela do usuário.** Tudo o que altera um documento é uma ferramenta declarada pelo frontend e executada no navegador, sobre o editor aberto. Por isso o que o agente faz aparece na interface na hora — destacado —, e tudo o que grava no servidor passa pelas mãos do usuário.

Isso dá três propriedades de graça:

1. **Sincronia.** Não há um segundo estado a reconciliar: o agente edita o mesmo texto que o usuário vê, pelo mesmo editor. Não existe "o agente salvou e a tela ficou velha".
2. **Controle.** Alterar a tela não grava. Gravar — salvar, criar um documento, aplicar um template — é uma ferramenta que pede aprovação, e o salvamento automático do editor fica suspenso enquanto há alterações do agente não revisadas.
3. **Contexto.** O documento aberto (inclusive o que não foi salvo) e o trecho selecionado vão ao agente a cada execução, pelo `context` do AG-UI. "Isto aqui" e "este documento" significam o que o usuário está vendo.

---

## 2. Visão geral

```
 Navegador                                                      Servidor (MarkDocs.Documentos)
 ─────────────────────────────────────────────                  ─────────────────────────────────────────────
 AssistenteProvider (em Casca, acima das rotas)                 POST /api/agente   (MapAgente → MapAGUIServer)
   HttpAgent (@ag-ui/client)  ── RunAgentInput ──────────────▶    ChatClientAgent  "markdocs-assistente"
     messages  (a conversa, guardada no cliente)                    Instructions  (Application/Agente/Instrucoes.md)
     tools     (as ferramentas da tela aberta)                      Tools         listarDocumentos · consultarDocumento ·
     context   (tela, documento, Markdown, seleção)                               listarTemplates   (executadas aqui)
                                                                    ↓
   ◀──────────────────────────── eventos SSE (AG-UI) ──────────   FunctionInvokingChatClient
     TEXT_MESSAGE_*      → o texto da resposta                      ↓
     TOOL_CALL_* + RESULT → ferramenta do backend, já executada    ContextoDaTelaChatClient  (context → instruções)
     TOOL_CALL_* sem RESULT → ferramenta da tela: executar aqui     ↓
     RUN_FINISHED { pendingToolCallIds }                           IChatClient  (OpenAI | Foundry, ClienteDoModelo)

   executa as ferramentas pendentes (com aprovação, se gravam)
   acrescenta as mensagens `tool` e roda de novo  ────────────▶   (continuação: o servidor vê os resultados e segue)
```

| Peça | Onde | O que faz |
|---|---|---|
| `Assistente` (`Application/Agente/`) | slice `MarkDocs.Documentos` | Monta o `ChatClientAgent`: instruções, ferramentas do backend, `AllowMultipleToolCalls = false` (§4.3). |
| `FerramentasDoAgente` | idem | As três ferramentas de leitura. Falha de regra volta como texto, não como exceção (§4.1). |
| `Instrucoes.md` | idem (recurso do assembly) | O *system prompt*, em pt-BR. |
| `ContextoDaTelaChatClient` (`Infrastructure/Agente/`) | idem | Leva o `context` do AG-UI para as instruções de cada chamada (§5). |
| `ClienteDoModelo`, `AgenteOptions` | idem | O `IChatClient` da OpenAI ou do Foundry, pelas variáveis `AGENTE_*` (§8). |
| `MapAgente` (`Hosting/`) | idem | Esconde o `MapAGUIServer` da camada HTTP; sem configuração, responde 503 com o que falta. |
| `EventosAguiSemNulos` (`Hosting/`) | idem | Compatibilidade entre os SDKs .NET e TypeScript do AG-UI 1.0 (§10). |
| `AgenteHandler` | `MarkDocs.WebApi` | `app.MapAgente("/api/agente")` e `GET /api/agente/status`. A WebApi não referencia o Agent Framework. |
| `AssistenteProvider` (`frontend/src/assistente/Assistente.tsx`) | frontend | A conversa (`HttpAgent`), o laço de ferramentas, as aprovações, o registro de ferramentas e de contexto. |
| `PainelDoAssistente` (`Painel.tsx`) | frontend | O painel: mensagens, rastros das ações, cartões de aprovação, composer com os chips de contexto. |
| `useAssistenteNoDocumento` | frontend | O que a página do documento entrega ao agente: contexto e as ferramentas de edição. |
| `FerramentasGlobais` | frontend | Ferramentas válidas em qualquer tela: abrir, criar documento, mudar de tela. |
| `marcas.ts` | frontend | O destaque do que o agente alterou (CSS Custom Highlight API). |

### 2.1 Por que o agente mora no slice `Documentos`

As ferramentas de backend leem documentos e templates — o agregado do slice `MarkDocs.Documentos`. Um slice `MarkDocs.Agente` à parte precisaria referenciar `Documentos` (uma `PublicApi` com listar/consultar), e o CLAUDE.md pede que dependência entre slices seja evitada e combinada antes. Como hoje há **um** slice de domínio, o agente fica nele, como o `Notiq` do servico-dfe guarda o agente dele: tudo do Agent Framework confinado a um `.csproj`, e a WebApi só chama `MapAgente`.

Quando surgir um segundo slice de domínio que o agente precise ler, o caminho é extrair `MarkDocs.Agente` como orquestrador e mover para ele `Application/Agente/`, `Infrastructure/Agente/` e `Hosting/`, com as leituras de cada slice atrás de uma interface (`Application/PublicApi/` nos slices lidos). O frontend não muda: ele só conhece `/api/agente`.

---

## 3. A execução, passo a passo

1. O usuário escreve. O `AssistenteProvider` acrescenta a mensagem ao `HttpAgent` e chama `runAgent` com:
   - `messages` — a conversa inteira, guardada no cliente;
   - `tools` — as ferramentas **registradas pela tela aberta agora** (as globais e, na página do documento, as de edição);
   - `context` — a tela, o documento aberto, o Markdown atual e o trecho selecionado (§5).
2. O `MapAGUIServer` converte a entrada (`ToChatRequestContext` do SDK AG-UI .NET): as ferramentas do cliente viram declarações embrulhadas em `ApprovalRequiredAIFunction`, de modo que o `FunctionInvokingChatClient` **não** as executa — devolve a chamada.
3. O modelo responde. Se chamou uma ferramenta do backend, ela roda no servidor e o stream traz `TOOL_CALL_START/ARGS/END` **e** `TOOL_CALL_RESULT`. Se chamou uma ferramenta da tela, o stream traz a chamada **sem** resultado e termina com `RUN_FINISHED`, cujo `pendingToolCallIds` (derivado pelo cliente) lista as chamadas que a tela precisa responder.
4. O provider executa cada chamada pendente — pedindo aprovação quando a ferramenta grava — e acrescenta uma mensagem `tool` com o resultado.
5. Roda de novo, com o contexto **recalculado** (o Markdown já com a edição). O SDK do servidor reconhece a continuação (`ProcessContinuation`) e entrega os resultados ao modelo, que segue. O laço para quando não há mais ferramentas pendentes (no máximo 12 rodadas por mensagem).

"Parar" aborta a execução e recusa as aprovações pendentes; "Nova conversa" troca o `HttpAgent` (outro `threadId`).

---

## 4. As ferramentas

| Ferramenta | Onde executa | Grava? | Aprovação | O que faz |
|---|---|---|---|---|
| `listarDocumentos(filtro)` | backend | não | — | Os documentos (id, título, tipo, template, data), filtrados por um trecho do título ou do tipo (sem caixa nem acento). |
| `consultarDocumento(documentoId)` | backend | não | — | Título, tipo, imagens citáveis e o Markdown **salvo** de um documento. |
| `listarTemplates()` | backend | não | — | Os templates (id, nome, tipo, se é de fábrica). |
| `substituirTrecho(trechoAtual, novoTexto)` | tela (documento) | não | — | Troca um trecho exato do Markdown atual. A forma preferida de editar. |
| `inserirTexto(texto, onde, referencia)` | tela (documento) | não | — | Acrescenta um bloco no início, no fim, ou antes/depois do bloco que contém a referência. |
| `reescreverDocumento(markdown)` | tela (documento) | não | — | Substitui o Markdown inteiro (destaca as linhas novas, por LCS). |
| `salvarDocumento(titulo)` | tela (documento) | **sim** | **sim** | Grava o texto da tela (e um título novo, se vier). |
| `aplicarTemplate(templateId)` | tela (documento) | **sim** | **sim** | Aplica um template: troca a formatação inteira; o texto não muda. |
| `criarDocumento(templateId, markdown, titulo)` | tela (global) | **sim** | **sim** | Cria um documento e o abre. |
| `abrirDocumento(documentoId)` | tela (global) | não | — | Navega até o documento; ele passa a ser o do contexto. |
| `irPara(tela)` | tela (global) | não | — | Lista de documentos, templates ou novo documento. |

### 4.1 Regras comuns

- **Falha de regra volta como texto** para o modelo ler e corrigir na mesma rodada — trecho não encontrado, trecho ambíguo (aparece mais de uma vez), documento que não existe, alterações não salvas impedindo sair da tela. O rastro na conversa fica marcado como falha. O que é lançado é falha de infraestrutura.
- **Os argumentos são todos obrigatórios**, com vazio significando "não informado" (`filtro: ""`, `titulo: ""`, `referencia: ""`). Um campo sempre presente é uma decisão a menos no caminho da chamada — a lição do notiq-docs com argumentos opcionais.
- **Localizar um trecho** é exato; se não achar, tenta de novo com os espaços e quebras de linha afrouxados. Mais de uma ocorrência é recusada, pedindo mais texto ao redor.
- **Ferramenta nova da tela** entra em dois lugares: a definição (em `useAssistenteNoDocumento.ts` ou `FerramentasGlobais.tsx`, ou num hook novo de outra página) e uma linha em `assistente/rotulos.ts`, que dá o rótulo do rastro. Ferramenta nova do backend: um método em `FerramentasDoAgente` e uma linha em `Todas()`.

### 4.2 Aprovação (human-in-the-loop)

A aprovação é feita **no frontend**, porque é lá que estão as ferramentas que gravam. Uma ferramenta com `aprovacao` monta um pedido (título, o que vai acontecer, o verbo do botão e, quando faz sentido, "Ver as alterações", que abre o modo de diferenças do editor). O provider abre o painel se estiver fechado, mostra o cartão logo abaixo do rastro da chamada e **espera** a resposta — a execução no servidor já terminou; a espera não segura conexão nenhuma. Aprovado, executa; recusado, devolve ao modelo que nada foi gravado e que não deve insistir.

O editor tem a mesma regra pelo outro lado: **enquanto houver alterações do agente na tela, o salvamento automático fica suspenso**. A barra de revisão, presa ao pé do editor, oferece *Diferenças* (o modo de diferenças do MDXEditor, contra a versão gravada), *Desfazer* (volta ao texto de antes da primeira alteração do agente) e *Manter e salvar* — que é, ela também, uma aprovação. Sair da página com alterações pendentes pede confirmação (descartar, salvar e sair, ou ficar).

Se um dia houver ferramentas do **backend** que gravem, o caminho nativo é o do próprio AG-UI 1.0: embrulhá-las em `ApprovalRequiredAIFunction`; o servidor termina a execução com `RUN_FINISHED { outcome: interrupt }`, e o cliente retoma com `resume: [{ interruptId, payload: { toolCall, approved } }]` (o SDK .NET já decodifica isso em `ToolApprovalResponseContent`). Hoje não é necessário.

### 4.3 Uma chamada de ferramenta por resposta

`ChatOptions.AllowMultipleToolCalls = false`. Numa resposta que misturasse uma ferramenta da tela com uma do backend, o `FunctionInvokingChatClient` devolveria as duas ao cliente; o Agent Framework guarda a do backend na sessão para executá-la na continuação — mas, sem histórico no servidor (§9), a sessão da continuação é outra, e a chamada ficaria órfã (o provedor recusa a próxima requisição). Uma chamada por vez evita o caso inteiro, ao custo de algumas idas e voltas a mais. Quando o histórico for persistido, a opção pode ser revista.

---

## 5. O contexto da tela

A cada execução — inclusive em cada continuação — o frontend manda:

| `description` | `value` | Quem informa |
|---|---|---|
| A tela aberta | o caminho e o título da página | o provider |
| O documento aberto na tela — o alvo das edições… | id, título, tipo, template de origem, as imagens citáveis (`imagens/<nome>`), se há alterações do agente não salvas | `useAssistenteNoDocumento` |
| O Markdown atual do documento aberto (o texto a editar) | o Markdown do editor, **com** o que não foi salvo | idem |
| Trecho selecionado pelo usuário (Markdown) — o pedido é sobre ele | `getSelectionMarkdown()` do MDXEditor | o provider, quando há seleção anexada |

**O documento aberto é sempre o alvo**, a não ser que o usuário diga outro: as instruções dizem isso, e o composer mostra o título dele como um chip. A seleção entra de três formas: o botão *Perguntar ao assistente* que aparece junto do texto selecionado na folha, o atalho ⌘J com texto selecionado, ou — depois de anexada — o chip removível no composer. Ela vale para a mensagem em que foi anexada.

No servidor, `ContextoDaTelaChatClient` lê o `RunAgentInput` que o SDK guarda em `ChatOptions.AdditionalProperties` e acrescenta o contexto às **instruções** daquela chamada. Não vira mensagem, de propósito: o contexto é um retrato da tela *agora*, e como instrução ele não entra no histórico — que é o que permite persistir a conversa (§9) sem guardar retratos velhos do documento.

---

## 6. A interface: a camada de quem age em seu nome

A pergunta de design era como dar ao usuário a sensação de que o agente atua sobre a interface **em nome dele, numa camada acima**. A resposta junta quatro práticas já consolidadas:

1. **Uma folha sobre a superfície do app** — o painel lateral dos copilotos (Microsoft 365 Copilot, Notion AI, GitHub Copilot Chat), tratado como papel: opaco, com margem, cantos arredondados e sombra próprios — visivelmente uma camada sobre o app, e não uma coluna dele. Abre à direita pelo botão *Assistente* no topo ou por ⌘J / Ctrl+J (o atalho do Notion AI); Esc fecha. O app não se rearranja por causa dele: a folha se sobrepõe ao que estiver à direita (os controles da página, o PDF), e fechá-la devolve tudo. O botão de expandir, no canto superior direito, a estende sobre quase toda a tela — sobra mais espaço no topo e à esquerda do que à direita e embaixo, e a superfície por baixo escurece um tom, sem deixar de ser o app; a conversa e o campo passam a uma coluna de leitura. O mesmo botão a devolve ao tamanho reduzido, e *Mostrar* ou *Ver as alterações* também — para que o que o agente fez fique à vista. Em telas estreitas, a folha ocupa a tela inteira.
2. **Presença do agente onde ele age** — como os agentes que operam navegadores (o "Claude está ativo nesta aba", o Operator) emolduram a área controlada. Enquanto uma ferramenta da tela executa, a lente do editor ganha uma moldura e o rótulo *O assistente está editando*. O botão do topo pulsa enquanto ele trabalha e marca quando há uma aprovação esperando com o painel fechado.
3. **A tinta do agente no documento, até a revisão** — como as sugestões do Google Docs e os "Keep / Undo" das edições inline do Copilot e do Cursor. O que o agente alterou fica destacado na folha (fundo e sublinhado na cor dele), e a barra de revisão decide: diferenças, desfazer, manter e salvar. O destaque usa a CSS Custom Highlight API: faixas pintadas sobre o texto renderizado, sem tocar no DOM nem no Markdown.
4. **Rastro e aprovação na conversa** — cada ação aparece como uma linha (*Alterou um trecho do documento — destacado no documento · Mostrar*), e as que gravam trazem o cartão de aprovação logo abaixo. A conversa é o registro do que foi feito.

**A cor.** O app é grafite sobre papel, sem acento cromático. O agente tem uma cor própria (`--assistente`, um violeta dessaturado) usada **só** para dizer "isto é do assistente": a moldura de presença, o destaque do que ele alterou, os rastros, o cartão de aprovação. É estado, não ação — os botões continuam grafite.

---

## 7. A interface por dentro

- **O composer** segue o do ChatGPT: o campo e o botão de enviar numa caixa só, de cantos bem arredondados. O texto fica em cima e cresce com o que se digita; embaixo, à esquerda, os chips do contexto (o documento aberto, o trecho anexado) e, à direita, enviar — ou parar, enquanto o agente trabalha. Enter envia, Shift+Enter quebra a linha.
- **A tela é função das mensagens.** O painel desenha `agent.messages` (assinatura `onMessagesChanged` do `HttpAgent`): mensagens do usuário, texto do assistente (Markdown, por `react-markdown`) e, para cada `toolCall`, um rastro. O estado de cada chamada da tela (executando, aguardando, ok, falha, recusada) e o resumo dela ficam num mapa ao lado, pelo id da chamada.
- **Registro, não acoplamento.** As páginas registram ferramentas (`registrarFerramentas`) e fontes de contexto (`registrarFonte`) no provider ao montar e as retiram ao desmontar. O provider não conhece página nenhuma; uma página nova que queira o agente faz o mesmo.
- **O provider fica acima das rotas** (em `Casca`): a conversa sobrevive à navegação, e o agente pode abrir um documento e continuar trabalhando nele.

---

## 8. Configuração

Variáveis planas, lidas em `AddDocumentosModule` (como as demais do app):

| Variável | Padrão | O quê |
|---|---|---|
| `AGENTE_PROVEDOR` | `openai` | `openai` — a API da OpenAI, ou um serviço compatível em `AGENTE_ENDPOINT`; `foundry` — um modelo implantado no Microsoft Foundry (Azure OpenAI), pela API v1. |
| `AGENTE_MODELO` | — | OpenAI: o id do modelo (`gpt-4.1`). Foundry: o nome da implantação. |
| `AGENTE_CHAVE_API` | — | Obrigatória na OpenAI. No Foundry, opcional: sem ela, a autenticação é por Entra ID (`DefaultAzureCredential` — `az login` em desenvolvimento, identidade gerenciada em produção). |
| `AGENTE_ENDPOINT` | — | Foundry: o endereço do recurso (`https://<recurso>.openai.azure.com` ou `https://<recurso>.services.ai.azure.com`; o `/openai/v1/` é acrescentado). OpenAI: opcional, um endereço compatível (gateway, proxy). |

Exemplos:

```sh
# OpenAI
AGENTE_PROVEDOR=openai
AGENTE_MODELO=gpt-4.1
AGENTE_CHAVE_API=sk-...

# Microsoft Foundry, com chave
AGENTE_PROVEDOR=foundry
AGENTE_ENDPOINT=https://meu-recurso.openai.azure.com
AGENTE_MODELO=gpt-4.1-markdocs        # o nome da implantação
AGENTE_CHAVE_API=...

# Microsoft Foundry, sem chave (Entra ID)
AGENTE_PROVEDOR=foundry
AGENTE_ENDPOINT=https://meu-recurso.services.ai.azure.com
AGENTE_MODELO=gpt-4.1-markdocs
```

Os dois provedores usam o mesmo SDK (o da OpenAI, por `Microsoft.Extensions.AI.OpenAI`): o Foundry expõe os modelos OpenAI pela API v1, compatível — muda o endereço e, sem chave, a credencial. Outro provedor (Anthropic, um modelo local) é um caso novo em `ClienteDoModelo` que devolva um `IChatClient`; o agente, as ferramentas e o endpoint não mudam.

**Sem configuração mínima**, o app sobe igual: `GET /api/agente/status` responde `{ disponivel: false, oQueFalta }`, `POST /api/agente` responde 503 com a mesma explicação, e o painel a mostra no lugar do composer.

---

## 9. Histórico da conversa — decisão em aberto

**Hoje:** a conversa vive no cliente. O `HttpAgent` guarda as mensagens e as reenvia a cada execução (é o modelo padrão do AG-UI); o servidor não registra `AgentSessionStore` nenhum, e o `MapAGUIServer` usa um store que não guarda nada — cada requisição monta a sessão a partir das mensagens recebidas. Recarregar a página começa uma conversa nova. O `threadId` do AG-UI já é estável por conversa ("Nova conversa" gera outro).

**Para persistir**, o mecanismo é o nativo do Agent Framework, e o ponto de encaixe já existe — está comentado em `AddAgente` (`DependencyInjection/ServiceCollectionExtensions.cs`):

```csharp
services.AddAIAgent(Assistente.Nome, ...)
        .WithSessionStore((sp, nome) => new SessoesNoMongo(...));   // um AgentSessionStore
```

O `MapAGUIServer` procura o `AgentSessionStore` registrado pelo nome do agente, usa o `threadId` como chave, carrega a sessão antes da execução e a grava depois. O que fica para decidir junto com o *onde* (MongoDB, Redis, Cosmos DB, Azure Storage):

1. **Quem é a fonte da conversa.** Com o histórico no servidor, o cliente deve mandar só as mensagens novas — ou o histórico guardado e o reenviado se somam. As alternativas são o cliente enviar apenas o delta (e carregar a conversa pelo `threadId` ao abrir, com um endpoint de leitura) ou um `ChatHistoryProvider` que deduplique pelo `MessageId`.
2. **Isolamento.** Com autenticação, registrar um `AgentIsolationKeyProvider` (por exemplo `UseClaimsBasedAgentIsolation`): o `MapAGUIServer` já embrulha o store em `IsolationKeyScopedAgentSessionStore`, e o `threadId` deixa de bastar para ler a conversa de outro usuário.
3. **Uma chamada de ferramenta por resposta** (§4.3) pode ser revista: com a sessão persistida, o Agent Framework guarda a chamada do backend que acompanha uma da tela e a executa na continuação.

O contexto da tela já foi desenhado para isso: ele vai nas instruções, não nas mensagens (§5), e não entra no histórico.

---

## 10. Compatibilidade entre os SDKs do AG-UI

O SDK .NET (`AGUI.Server` 1.0.0, trazido pelo pacote de hosting) escreve os campos opcionais ausentes como `null` — `"usage": null`, `"pendingToolCallIds": null` —, e o cliente TypeScript (`@ag-ui/client` 1.0.3) valida o stream com esquemas em que opcional é *ausente*, e recusa o evento. `EventosAguiSemNulos` (`Hosting/`) acrescenta um modificador ao `JsonSerializerOptions` do ASP.NET que omite nulos **só** nos tipos de `AGUI.Abstractions`; o JSON do resto da API não muda. Um teste de integração verifica que nenhum evento sai com campo nulo. Quando os dois SDKs concordarem, a classe sai.

As versões estão alinhadas na mesma geração do protocolo (1.0): `Microsoft.Agents.AI` 1.25.0, `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` 1.25.0-preview, `@ag-ui/client` 1.0.3. O pacote de hosting está em *preview* porque o protocolo ainda evolui; ao atualizar, manter os `Microsoft.Agents.AI*` alinhados entre si e rodar os testes do §12.

---

## 11. Segurança

- **Sem autenticação**, como o resto da API (MVP em validação). Quando ela entrar, entra no Host, e o `MapAgente` recebe `RequireAuthorization()` como os demais endpoints — e o isolamento de sessões do §9.
- **O backend só lê.** Nenhuma ferramenta do servidor altera dados; toda gravação passa por uma ação aprovada pelo usuário no navegador, com o mesmo endpoint REST que a tela usa.
- **O conteúdo dos documentos é dado, não instrução.** Um documento pode conter texto que tente instruir o agente; o pior que ele consegue é propor uma edição na tela ou pedir uma gravação — e as duas passam pelo usuário.
- **Custo.** Não há teto de uso de modelo. Se for preciso, o lugar é um `DelegatingChatClient` em volta do `ClienteDoModelo` (como o `BudgetEnforcingChatClient` do servico-dfe).

---

## 12. Testes

| Onde | O quê |
|---|---|
| `tests/MarkDocs.WebApi.IntegrationTests/AgenteTests.cs` | A borda AG-UI, pelo Host, com um modelo roteirizado no lugar do LLM (`ModeloRoteirizado`, trocado no DI pelo nome do agente): a ferramenta do backend é executada no servidor e o resultado volta no stream; a da tela volta ao cliente **sem** resultado, e a continuação com o resultado da tela chega ao modelo; o contexto vai nas instruções e não vira mensagem; os eventos não levam campos nulos; o status. Sem `AGENTE_*`, status `disponivel: false` e 503. |
| `tests/MarkDocs.Documentos.Tests/AgenteTests.cs` | A configuração mínima por provedor e o que falta; o endereço v1 do Foundry; o cliente dos dois provedores criado sem rede; o contexto nas instruções; as instruções como recurso; as ferramentas de leitura contra um MongoDB real (filtro sem caixa nem acento, documento inexistente como texto). |

O frontend foi verificado no navegador contra um servidor compatível com `/v1/chat/completions` com roteiro fixo (`AGENTE_ENDPOINT` apontando para ele): editar um trecho (destaque, moldura, barra de revisão), salvar com aprovação, perguntar sobre uma seleção, desfazer, ver as diferenças, criar um documento a partir da lista.

---

## 13. Próximos passos

- Decidir onde persistir o histórico (§9).
- Ferramentas de formatação eixo a eixo (o agente aplica templates; não ajusta, por exemplo, só as margens).
- Medir, com o modelo escolhido, a taxa de chamadas corretas das ferramentas de edição em pedidos vagos — e ajustar as instruções com essa medida, não sem ela.
- Quando houver autenticação: `RequireAuthorization`, isolamento de sessões e teto de uso.
