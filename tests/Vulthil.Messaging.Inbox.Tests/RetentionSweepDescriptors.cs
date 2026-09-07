using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Vulthil.Messaging.Inbox.Tests;

/// <summary>
/// Recognises the inbox retention sweep among the hosted-service registrations. The sweep's hosted service type lives
/// in Vulthil.Extensions.Retention and is internal there, so it is identified by the shape of its factory registration:
/// a hosted service built for the idempotency store type.
/// </summary>
internal static class RetentionSweepDescriptors
{
    public static bool IsInboxRetentionSweep(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationFactory?.GetType().GenericTypeArguments is [_, { IsGenericType: true } sweepType]
        && sweepType.GetGenericArguments()[0] == typeof(IIdempotencyStore);
}
