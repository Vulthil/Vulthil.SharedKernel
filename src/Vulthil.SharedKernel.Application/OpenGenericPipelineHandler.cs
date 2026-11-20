using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Application;

/// <summary>
/// Builds the scoped registration of an open-generic pipeline handler, after checking that the type is an open
/// generic type definition that implements the expected open-generic handler interface.
/// </summary>
internal static class OpenGenericPipelineHandler
{
    /// <summary>
    /// Returns the scoped <see cref="ServiceDescriptor"/> that registers <paramref name="pipelineHandler"/> as
    /// <paramref name="openHandlerInterface"/>.
    /// </summary>
    /// <param name="pipelineHandler">The candidate pipeline handler type.</param>
    /// <param name="openHandlerInterface">The open-generic handler interface it must implement.</param>
    /// <returns>The registration of <paramref name="pipelineHandler"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="pipelineHandler"/> is not an open generic type definition, or does not implement
    /// <paramref name="openHandlerInterface"/>.
    /// </exception>
    public static ServiceDescriptor CreateDescriptor(Type pipelineHandler, Type openHandlerInterface)
    {
        ArgumentNullException.ThrowIfNull(pipelineHandler);

        if (!pipelineHandler.IsGenericTypeDefinition)
        {
            throw new InvalidOperationException($"{pipelineHandler.Name} must be an open generic type.");
        }

        if (!ImplementsOpenInterface(pipelineHandler, openHandlerInterface))
        {
            throw new InvalidOperationException($"{pipelineHandler.Name} must implement {openHandlerInterface.FullName}.");
        }

        return new ServiceDescriptor(openHandlerInterface, pipelineHandler, ServiceLifetime.Scoped);
    }

    private static bool ImplementsOpenInterface(Type pipelineHandler, Type openHandlerInterface) =>
        pipelineHandler.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerInterface);
}
