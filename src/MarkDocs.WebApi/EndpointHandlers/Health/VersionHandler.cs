namespace MarkDocs.WebApi.EndpointHandlers.Health;

public class VersionHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/version", GetVersion)
            .WithTags("Saúde")
            .Produces<VersionResponse>(StatusCodes.Status200OK);
    }

    internal static IResult GetVersion(TimeProvider relogio) =>
        Results.Ok(new VersionResponse
        {
            Version = typeof(VersionHandler).Assembly.GetName().Version?.ToString(3) ?? "0.1.0",
            Timestamp = relogio.GetUtcNow(),
        });
}

public record VersionResponse
{
    public required string Version { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
