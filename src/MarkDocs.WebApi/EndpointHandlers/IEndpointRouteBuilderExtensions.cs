using System.Reflection;

namespace MarkDocs.WebApi.EndpointHandlers;

public static class IEndpointRouteBuilderExtensions
{
    /// <summary>Registra todo <see cref="IEndpointRouteHandler"/> concreto, com construtor sem parâmetros, do assembly.</summary>
    public static void MapEndpoints(this IEndpointRouteBuilder app, Assembly assembly)
    {
        var handlerType = typeof(IEndpointRouteHandler);
        var handlers = assembly.GetTypes().Where(t =>
            t.IsClass
            && !t.IsAbstract
            && !t.IsGenericType
            && t.GetConstructor(Type.EmptyTypes) != null
            && handlerType.IsAssignableFrom(t));

        foreach (var type in handlers)
        {
            var instance = (IEndpointRouteHandler)Activator.CreateInstance(type)!;
            instance.MapEndpoints(app);
        }
    }
}
