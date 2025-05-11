using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure<TDbContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> optionsBuilder)
        where TDbContext : DbContext
    {
        services.AddSingleton<DomainEventsToOutboxMessageSaveChangesInterceptor>();

        services.AddDbContext<TDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<DomainEventsToOutboxMessageSaveChangesInterceptor>();
            var newAction = optionsBuilder + (o => o.AddInterceptors(interceptor));
            newAction(options);
        });

        return services;
    }

    public static IServiceCollection AddInfrastructureWithUnitOfWork<TDbContext, TUnitOfWork>(this IServiceCollection services, Action<DbContextOptionsBuilder> optionsBuilder)
        where TDbContext : DbContext, TUnitOfWork
        where TUnitOfWork : class, IUnitOfWork
    {
        services.AddInfrastructure<TDbContext>(optionsBuilder);
        services.AddScoped<TUnitOfWork>(sp => sp.GetRequiredService<TDbContext>());

        return services;
    }

    public static IServiceCollection AddInfrastructureWithUnitOfWork<TDbContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> optionsBuilder)
        where TDbContext : DbContext, IUnitOfWork
    {
        services.AddInfrastructureWithUnitOfWork<TDbContext, IUnitOfWork>(optionsBuilder);
        return services;
    }

    public static IServiceCollection AddOutboxProcessing<TDbContext>(this IServiceCollection services, Action<OutboxProcessingOptions>? optionsAction = null)
        where TDbContext : DbContext, ISaveOutboxMessages
    {
        optionsAction ??= (o) => { };
        services.AddOptions<OutboxProcessingOptions>()
            .Configure(optionsAction)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ISaveOutboxMessages>(sp => sp.GetRequiredService<TDbContext>());
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }
}
