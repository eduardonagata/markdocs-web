# Template TODO

Este arquivo existe para registrar ajustes que devem voltar para o repositório template.

Quando usar este projeto (criado via "Use this template") você identificar que uma decisão, convenção ou scaffold deveria ser padrão, **anote aqui** em vez de esquecer. Periodicamente — ou quando terminar um slice — leve as entradas desta lista para um commit no repo template.

Se estiver trabalhando **no próprio repo template**, apague as entradas conforme forem incorporadas.

---

## Como registrar uma entrada

Use o formato abaixo. O "porquê" é o mais importante — é o que permite decidir se a mudança cabe no template genérico ou se é específica deste projeto.

```markdown
### <título curto>

- **O quê:** descrição objetiva do ajuste
- **Onde:** arquivo(s) / skill / seção do README afetada
- **Por quê:** o atrito real encontrado ao usar o template
- **Status:** pendente | incorporado no template em <commit/data>
```

---

## Entradas

<!-- Adicione novas entradas abaixo. Exemplo:

### Regra de autorização padrão nos endpoints

- **O quê:** a skill de endpoints deveria gerar `.RequireAuthorization("DefaultPolicy")` em vez de `.RequireAuthorization()` sem política nomeada
- **Onde:** `.claude/skills/api-endpoints-development/SKILL.md`, Passo 4
- **Por quê:** todos os projetos estão caindo em .RequireAuthorization() genérico e depois refatorando para políticas nomeadas
- **Status:** pendente
-->

### O código do template não segue a separação Host / WebApi do CLAUDE.md

- **O quê:** o CLAUDE.md descreve `<Produto>` (Host, Sdk.Web, só `Program.cs`) separado de `<Produto>.WebApi` (class library com os handlers e o `WebApiMarker`), mas o código do template tinha o `Program.cs` dentro de `AppTemplate.WebApi` (Sdk.Web) e descobria os endpoints com `Assembly.GetExecutingAssembly()`.
- **Onde:** `src/AppTemplate.WebApi/`, README §2/§3.1 e a skill `api-endpoints-development`.
- **Por quê:** no MarkDocs foi preciso refazer a topologia à mão (Host `MarkDocs` + `MarkDocs.WebApi` + `MarkDocs.WebUi`), como no servico-dfe. O template deveria nascer com ela.
- **Status:** pendente

### Leitura de configuração: README e CLAUDE.md dizem coisas diferentes

- **O quê:** o README (§9) manda ler variáveis planas (`CONTATOS_CONNECTION_STRING`) explicitamente no `Add<Slice>Module`; o CLAUDE.md ("Configuracao") mostra `services.Configure<T>(configuration.GetSection("<Slice>"))`, que pede variáveis no formato `Slice__Propriedade`.
- **Onde:** README §9.2 e CLAUDE.md, seção "Configuracao".
- **Por quê:** o MarkDocs seguiu o README (variáveis planas), mas a contradição obriga a escolher a cada slice.
- **Status:** pendente
