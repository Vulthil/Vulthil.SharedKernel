# Vulthil.SharedKernel.Outbox

[![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Outbox)](https://www.nuget.org/packages/Vulthil.SharedKernel.Outbox)

The transactional **outbox engine** for `Vulthil.SharedKernel`: the message-capture model (`OutboxMessage`), the
relay processor and background service, pluggable dispatchers (`IOutboxDispatcher`), the commit-time relay signal,
and the persistence-agnostic `IOutboxStore` seam.

It is intentionally persistence-light — free of any EF Core dependency — so a messaging bridge (such as
`Vulthil.Messaging.Outbox`) can depend on the engine alone. The EF Core implementation lives in
`Vulthil.SharedKernel.Outbox.EntityFrameworkCore`, and the full infrastructure package
(`Vulthil.SharedKernel.Infrastructure`) references the engine and adds the `DbContext` base, the
database-infrastructure configurator, and the DI wiring (`EnableOutboxProcessing`).

See the Outbox Pattern article on the [documentation site](https://vulthil.github.io/Vulthil.SharedKernel/)
for the design and usage.
