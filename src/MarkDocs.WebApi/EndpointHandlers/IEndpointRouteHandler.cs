namespace MarkDocs.WebApi.EndpointHandlers;

public interface IEndpointRouteHandler
{
    void MapEndpoints(IEndpointRouteBuilder app);
}
