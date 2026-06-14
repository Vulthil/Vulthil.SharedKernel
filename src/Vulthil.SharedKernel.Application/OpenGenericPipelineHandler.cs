namespace Vulthil.SharedKernel.Application;

/// <summary>
/// Shared validation for open-generic pipeline handler registrations, ensuring a registered type is an open
/// generic type definition that implements the expected open-generic handler interface.
/// </summary>
internal static class OpenGenericPipelineHandler
{
    /// <summary>
    /// Validates that <paramref name="pipelineHandler"/> is a usable open-generic implementation of
    /// <paramref name="openHandlerInterface"/>.
    /// </summary>
    /// <param name="pipelineHandler">The candidate pipeline handler type.</param>
    /// <param name="openHandlerInterface">The open-generic handler interface it must implement.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="pipelineHandler"/> is not an open generic type definition, or does not implement
    /// <paramref name="openHandlerInterface"/>.
    /// </exception>
    public static void EnsureValid(Type pipelineHandler, Type openHandlerInterface)
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
    }

    private static bool ImplementsOpenInterface(Type pipelineHandler, Type openHandlerInterface) =>
        pipelineHandler.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerInterface);
}
