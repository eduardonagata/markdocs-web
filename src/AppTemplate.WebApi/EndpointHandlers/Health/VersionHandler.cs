namespace AppTemplate.WebApi.EndpointHandlers.Health;

public class VersionHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/version", GetVersion)
            .AllowAnonymous()
            .Produces<VersionResponse>(StatusCodes.Status200OK);
    }

    internal static IResult GetVersion()
    {
        return Results.Ok(new VersionResponse
        {
            Version = "0.1.0",
            Timestamp = DateTimeOffset.UtcNow,
        });
    }
}

public record VersionResponse
{
    public required string Version { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
