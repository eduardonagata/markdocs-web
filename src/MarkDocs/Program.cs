using DotNetEnv;
using MarkDocs.Documentos.DependencyInjection;
using MarkDocs.WebApi;
using MarkDocs.WebApi.EndpointHandlers;
using MarkDocs.WebUi.Hosting;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ─── Módulos (um Add<Slice>Module por slice) ──────────────────────────────
builder.Services.AddDocumentosModule(builder.Configuration);

// ─── Camada web ───────────────────────────────────────────────────────────
// Single-tenant e sem autenticação na borda, por ora (MVP em validação).
builder.Services.AddProblemDetails();

var origens = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (origens.Length > 0)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod()));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "MarkDocs API", Version = "v1" }));
}

var app = builder.Build();

app.UseExceptionHandler();
if (origens.Length > 0) app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapEndpoints(typeof(WebApiMarker).Assembly);

// O frontend por último: /api/* tem prioridade sobre o fallback do SPA.
app.MapWebUi();

app.Run();

public partial class Program;
