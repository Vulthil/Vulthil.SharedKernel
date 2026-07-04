# Vulthil.SharedKernel.Outbox

[![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Outbox)](https://www.nuget.org/packages/Vulthil.SharedKernel.Outbox)

The transactional **outbox engine** for `Vulthil.SharedKernel`: the message-capture model (`OutboxMessage`), the
relay processor and background service, pluggable sinks (`IOutboxDispatcher`), the commit-time relay signal, and the
provider-agnostic strategy contracts (`IOutboxStrategy` / `BaseOutboxStrategy`).

It is intentionally persistence-light — it carries the outbox engine without the rest of
`Vulthil.SharedKernel.Infrastructure` — so a messaging bridge (such as `Vulthil.Messaging.Outbox`) can depend on the
engine alone. The full infrastructure package (`Vulthil.SharedKernel.Infrastructure`) references this engine and adds
the `DbContext` base, the database-infrastructure configurator, and the DI wiring (`EnableOutboxProcessing`).

See the [Outbox Pattern](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/outbox-pattern.md)
article for the design and usage.
