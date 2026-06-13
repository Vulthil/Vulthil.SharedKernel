using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

/// <summary>
/// Marker for EF Core interceptors that the outbox attaches to an outbox-enabled <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// Implementations are resolved from DI and added to the context options when outbox processing is enabled, so a
/// provider package (e.g. relational) can contribute a transaction-commit interceptor without the provider-agnostic
/// base needing to reference relational-only interceptor types.
/// </summary>
public interface IOutboxInterceptor : IInterceptor;
