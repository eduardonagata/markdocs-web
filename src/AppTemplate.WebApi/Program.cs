using System.Reflection;
using AppTemplate.Contatos.DependencyInjection;
using AppTemplate.TestStubs;
using AppTemplate.WebApi.EndpointHandlers;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ====================================================================
// MODULOS DA APLICACAO (um Add<Slice>Module por slice)
// ====================================================================
builder.Services.AddContatosModule(builder.Configuration);

// ====================================================================
// CROSS-CUTTING DA CAMADA WEB
// ====================================================================
builder.Services.AddHttpContextAccessor();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var originsCsv = builder.Configuration["CORS_ALLOWED_ORIGINS"];
        var origins = string.IsNullOrWhiteSpace(originsCsv)
            ? Array.Empty<string>()
            : originsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        if (origins.Length > 0)
            policy.WithOrigins(origins);
    });
});

// ====================================================================
// AUTHENTICATION / AUTHORIZATION
// --------------------------------------------------------------------
// Em produção: JWT Bearer validado contra o IdP real (Firebase/Entra ID)
//              via JWT_AUTHORITY + JWT_ISSUER + JWT_AUDIENCE.
// Em dev:       JWT Bearer validado com chave simétrica (DEV_JWT_SIGNING_KEY).
//              Os JWTs são emitidos por POST /api/dev/login (TestStubs),
//              carregam claim tid = DEV_JWT_TENANT_ID, e são consumidos
//              pelos endpoints reais normalmente (sem bypass de auth).
// ====================================================================
if (builder.Environment.IsDevelopment())
{
    // TestStubs: registra DevJwtOptions + DevTokenIssuer.
    var devJwtOptions = DevJwtOptions.FromConfiguration(builder.Configuration);
    builder.Services.AddTestStubs(devJwtOptions);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Preserva nomes de claims do JWT no ClaimsPrincipal (ex: "tid",
            // "sub"). Default do handler remapeia "tid" para
            // "http://schemas.microsoft.com/identity/claims/tenantid" e
            // "sub" para ClaimTypes.NameIdentifier — quebra codigo que
            // procura pelos nomes curtos.
            options.MapInboundClaims = false;
            options.TokenValidationParameters =
                DevTokenIssuer.BuildValidationParameters(devJwtOptions);
        });
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.Authority = builder.Configuration["JWT_AUTHORITY"];
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(builder.Configuration["JWT_ISSUER"]),
                ValidateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["JWT_AUDIENCE"]),
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JWT_ISSUER"],
                ValidAudience = builder.Configuration["JWT_AUDIENCE"],
                ClockSkew = TimeSpan.FromMinutes(5),
            };
        });
}
builder.Services.AddAuthorization();

// Swagger (Development only)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "AppTemplate API", Version = "v1" });
    });
}

var app = builder.Build();

// ====================================================================
// PIPELINE HTTP
// ====================================================================
app.UseCors("Default");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Descoberta e registro automatico dos IEndpointRouteHandler deste assembly
app.MapEndpoints(Assembly.GetExecutingAssembly());

// Stubs de desenvolvimento (POST /api/dev/login, GET /api/me, GET /api/tenants).
// Ver AppTemplate.TestStubs/README.md.
if (app.Environment.IsDevelopment())
{
    app.MapTestStubsEndpoints();
}

app.Run();

public partial class Program { }
