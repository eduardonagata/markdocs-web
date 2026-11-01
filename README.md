# MarkDocs

Documentos de negócio a partir de Markdown. Escolhe-se um template, cola-se o
Markdown, e o documento existe — com o PDF ao lado. O Markdown é editado no
[MDXEditor](https://github.com/mdx-editor/editor), que já o mostra formatado
(e, num clique, como texto). A formatação é editada por
controles (papel, margens, tipografia, títulos, tabela), um documento formatado
vira template para os próximos, e as imagens que o Markdown cita aparecem na
tela e no papel.

O PDF é composto pelo **MarkDocs.Core**, a transcrição .NET do NotiqTypst
(motor Typst), com os mesmos templates e os mesmos testes do app Swift.

---

## Como funciona

```
 Template (preset) ──aplicar = copiar──▶ Documento ──────────────▶ PDF
   Formatacao                              Corpo (Markdown)          MarkDocs.Core
   Ativos                                  Formatacao (completa)     (Typst, em processo)
                                           Ativos (template + imagens)
```

1. **Criar.** `POST /api/documentos` com o id de um template e o Markdown. A
   formatação do template é **copiada** para o documento: o documento é
   autocontido, e mudar o template depois não o altera.
2. **Compor.** `GET /api/documentos/{id}/pdf` monta as entradas do Core — a
   família, o `template.json` gerado da formatação, os ativos baixados do
   armazenamento de objetos e o Markdown — e devolve o PDF. Compor é leitura: o
   PDF não é guardado, a fonte de verdade é o Markdown.
3. **Formatar.** O documento devolve os **eixos** de formatação que o Core
   declara (domínio, limites e valor atual). A tela desenha um controle por eixo,
   sem saber de família nenhuma; uma edição volta como ajuste
   (`PATCH /api/documentos/{id}`), é limitada ao domínio pelo próprio Core e
   gravada na formatação do documento.
4. **Reaproveitar.** `POST /api/templates` copia a formatação de um documento
   para um template novo. Os templates de fábrica — Contrato, Orçamento e
   Recibo, os mesmos do app Swift — são semeados na primeira execução.
5. **Imagens.** O Markdown cita `imagens/<nome>`, e só as imagens dos ativos do
   documento aparecem — na tela, resolvidas contra `/api/documentos/{id}/`; no
   PDF, entregues ao Acervo do Typst; numa exportação, ao lado do `.md`. Ver
   [MarkDocs.Core/Docs/Imagens.md](MarkDocs.Core/Docs/Imagens.md).

---

## Organização

Vertical slices: cada slice é um projeto .NET, e as camadas (`Domain/`,
`Application/`, `Infrastructure/`, `DependencyInjection/`) são pastas dentro
dele. O app é **single-tenant** (um database, sem `TenantId`) e, enquanto o MVP
é validado, **sem autenticação na borda**.

```
MarkDocs.Core/                     o pacote de composição — independente, extraível
├── Sources/MarkDocs.Core/         Markdown + template → PDF (P/Invoke para o motor Typst)
├── Tests/MarkDocs.Core.Tests/     os testes do NotiqTypst, com as mesmas asserções
├── Rust/typst-markdocs/           o crate do motor
├── Artefatos/runtimes/            o motor compilado (linux-x64 versionado)
└── Docs/                          Testes.md (a especificação), Imagens.md
src/
├── MarkDocs/                      Host (Sdk.Web): Program.cs — só composição
├── MarkDocs.WebApi/               a camada HTTP: IEndpointRouteHandler + DTOs
├── MarkDocs.WebUi/                a hospedagem do frontend (wwwroot + fallback do SPA)
├── MarkDocs.Abstractions/         o modelo da especificação 001: IDocumento, Formatacao, Template
└── MarkDocs.Documentos/           o slice: documentos, templates, armazenamento, composição
tests/
├── MarkDocs.Abstractions.Tests/   serialização JSON/BSON e Aplicar ≡ o Core
├── MarkDocs.Documentos.Tests/     commands e queries contra um MongoDB real
└── MarkDocs.WebApi.IntegrationTests/  a API ponta a ponta (WebApplicationFactory)
frontend/                          React + TypeScript + Fluent UI v9 (fala só com /api)
Docs/especificacao-inicial/        a especificação do modelo de dados
```

```
                MarkDocs (Host)
               ╱       │       ╲
   MarkDocs.WebApi  MarkDocs.WebUi  (frontend: só HTTP)
         │
   MarkDocs.Documentos ──▶ MarkDocs.Abstractions ──▶ MarkDocs.Core
         └──────────────────────────────────────────▶ MarkDocs.Core
```

| Projeto | Papel |
|---|---|
| **MarkDocs.Core** | Não conhece o app, o MongoDB nem o ASP.NET. API: `RegistroDeGeradores.Carregar(familia, json, ativos)` → `TemplateCarregado` (com os `Eixos`); `RenderizadorPDF.Pdf(markdown, template[, imagens])`. Ver [MarkDocs.Core/README.md](MarkDocs.Core/README.md). |
| **MarkDocs.Abstractions** | Mínimo, como a [especificação 001](Docs/especificacao-inicial/001-modelo-de-dados-web.md): `IDocumento`, `ReferenciaDeAtivo`, `Formatacao` + `FormatacaoDocumentoDeNegocio`, `Template`, `Medida`/`Cor`/enums com os conversores JSON e BSON, `SerializacaoDaFormatacao`. |
| **MarkDocs.Documentos** | `Documento` (implementa `IDocumento`) e `TemplateDeDocumento` no MongoDB; o adaptador `IArmazenamentoDeObjetos` (MinIO ou disco); o `CompositorDePdf`; os commands/queries; as fontes e os templates de fábrica. |
| **MarkDocs.WebApi** | Traduz HTTP ↔ MediatR e exceções ↔ status. Nenhuma regra de negócio. |
| **MarkDocs.WebUi** | Serve o `wwwroot/` do frontend e devolve `index.html` para as rotas do SPA; `/api/*` sem handler é 404. |
| **MarkDocs** | `Program.cs`: `AddDocumentosModule`, pipeline, `MapEndpoints(typeof(WebApiMarker).Assembly)`, `MapWebUi()`. |

As regras de código (slices, DI colocalizada por `Add<Slice>Module`, endpoints
auto-registráveis, commands/queries com MediatR, convenções de C#) estão no
[CLAUDE.md](CLAUDE.md).

### Persistência e armazenamento

| Onde | O quê |
|---|---|
| MongoDB `documentos.documentos` | os documentos: corpo, formatação (na forma do `template.json`: `tamanho: "11pt"`), ativos, datas |
| MongoDB `documentos.templates` | os templates: nome, tipo documental, formatação, ativos, se é de fábrica |
| Armazenamento de objetos | os ativos (imagens, timbres), por chave imutável `ativos/<guid>.<ext>` |

O armazenamento é um adaptador (`IArmazenamentoDeObjetos`): `minio` (MinIO ou
qualquer serviço compatível com a API do S3) ou `disco` (uma pasta local, para
desenvolvimento). Outro provedor — Azure Blob Storage, por exemplo — é uma
classe nova e um caso no `AddDocumentosModule`.

---

## API

| Método | Rota | O quê |
|---|---|---|
| `GET` | `/api/documentos` | lista os documentos (o mais recente primeiro) |
| `POST` | `/api/documentos` | cria: `{ templateId, corpo, titulo? }` — o título, se não vier, é o primeiro título do Markdown |
| `GET` | `/api/documentos/{id}` | o documento, os eixos de formatação e as imagens |
| `PATCH` | `/api/documentos/{id}` | altera: `{ titulo?, tipo?, corpo?, templateId?, ajustes?: [{ eixo, valor }] }` |
| `DELETE` | `/api/documentos/{id}` | remove o documento e as imagens dele |
| `GET` | `/api/documentos/{id}/pdf` | o PDF (`inline`), composto na hora |
| `POST` | `/api/documentos/{id}/imagens` | envia uma imagem (`multipart/form-data`, campo `arquivo`) |
| `GET` | `/api/documentos/{id}/imagens/{nome}` | uma imagem dos ativos — e só dos ativos |
| `DELETE` | `/api/documentos/{id}/imagens/{nome}` | remove uma imagem |
| `GET` | `/api/templates` | lista os templates |
| `POST` | `/api/templates` | cria um template a partir de um documento formatado: `{ documentoId, nome }` |
| `GET` | `/api/templates/{id}/amostra` | um PDF de amostra com a formatação do template |
| `GET` | `/api/version` | saúde |

Erros voltam como `{ "mensagem": "…" }`: 404 (não existe), 400 (entrada
inválida), 422 (o documento não compõe — template inválido, fonte que falta).
Em `Development`, o Swagger está em `/swagger`.

---

## Executar

### Pré-requisitos

- **.NET 10 SDK** e **Node.js 22+**.
- **MongoDB** (4.4+; não precisa de replica set).
- **MinIO** (ou outro serviço compatível com S3) — ou `ARMAZENAMENTO_PROVEDOR=disco`.
- Num **Mac**, o motor Typst da sua máquina (uma vez; pede Rust):
  `MarkDocs.Core/Scripts/construir-typst.sh`. Em Linux x64 ele já vem no repositório.

O `docker-compose.yml` sobe o MongoDB e o MinIO:

```sh
docker compose up -d mongo minio
```

O console do MinIO fica em http://localhost:9001 (usuário `markdocs`, senha
`markdocs-segredo`). O bucket é criado pelo app no primeiro uso.

### Variáveis de ambiente

Copie `.env.example` para `.env` na raiz: o `Program.cs` o carrega
(`DotNetEnv`) subindo a árvore de diretórios. Em produção, defina as variáveis
no ambiente do processo.

| Variável | Padrão | O quê |
|---|---|---|
| `DOCUMENTOS_CONNECTION_STRING` | `mongodb://localhost:27017` | a conexão com o MongoDB |
| `DOCUMENTOS_DATABASE_NAME` | `markdocs` | o database (o app é single-tenant: um só) |
| `ARMAZENAMENTO_PROVEDOR` | `minio` | `minio` (MinIO/S3) ou `disco` |
| `MINIO_ENDPOINT` | `localhost:9000` | `host:porta`, sem esquema |
| `MINIO_ACCESS_KEY` | — | a chave de acesso |
| `MINIO_SECRET_KEY` | — | o segredo |
| `MINIO_BUCKET` | `markdocs` | o bucket; criado se não existir |
| `MINIO_USAR_SSL` | `false` | `true` para HTTPS |
| `MINIO_REGIAO` | — | opcional; S3 e alguns provedores exigem |
| `ARMAZENAMENTO_DIRETORIO` | `dados/objetos` | a pasta, quando o provedor é `disco` |
| `CORS_ALLOWED_ORIGINS` | — | origens, separadas por vírgula, se o frontend for servido de outro host |
| `ASPNETCORE_URLS` | `http://localhost:5280` (dev) | onde o app escuta (`http://+:8080` no Docker) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` liga o Swagger e o SpaProxy |
| `MONGODB_TEST_URI` | — | só os testes: um MongoDB próprio em vez do efêmero |

### Desenvolvimento

```sh
npm install --prefix frontend
dotnet run --project src/MarkDocs
```

Em `Development`, o `dotnet run` sobe também o Vite (SpaProxy) e abre
http://localhost:5173, que repassa `/api` ao backend em http://localhost:5280.
Para trabalhar só no frontend com o backend já rodando: `npm run dev --prefix frontend`.

### Produção

```sh
dotnet publish src/MarkDocs -c Release -o out     # roda o build do frontend e o copia para wwwroot/
dotnet out/MarkDocs.dll
```

Ou com Docker (frontend, backend e motor numa imagem só):

```sh
docker build -t markdocs .
docker run -p 8080:8080 --env-file .env markdocs
```

Com o `docker compose`, o app sobe junto com o MongoDB e o MinIO:
`docker compose --profile app up --build` → http://localhost:8080.

---

## Testes

```sh
dotnet test MarkDocs.sln
```

| Projeto | O quê |
|---|---|
| `MarkDocs.Core/Tests/MarkDocs.Core.Tests` | os testes do NotiqTypst transcritos — as mesmas asserções — e os da extensão de imagens ([Docs/Testes.md](MarkDocs.Core/Docs/Testes.md)) |
| `tests/MarkDocs.Abstractions.Tests` | ida e volta JSON ↔ BSON dos templates de fábrica; `Formatacao.Aplicar` produz o mesmo template que `ComAjustes` do Core, para cada eixo |
| `tests/MarkDocs.Documentos.Tests` | os handlers contra um MongoDB real |
| `tests/MarkDocs.WebApi.IntegrationTests` | a API ponta a ponta, pelo Host |

Os testes que tocam o MongoDB sobem um `mongod` efêmero (o binário vem no
pacote EphemeralMongo; não precisa de Docker) — ou usam `MONGODB_TEST_URI`.
