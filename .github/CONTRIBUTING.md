# Contributing to Vulthil.SharedKernel

First off, thank you for taking the time to contribute! This project is a collection of
opinionated .NET building blocks, and contributions of all kinds — bug reports, feature
ideas, documentation improvements, and code — are genuinely appreciated.

This document explains how to get set up, the conventions we follow, and how to submit
your changes for review.

## Ways to Contribute

- **Report a bug** — open a [bug report](https://github.com/Vulthil/Vulthil.SharedKernel/issues/new?template=bug_report.yml).
- **Request a feature** — open a [feature request](https://github.com/Vulthil/Vulthil.SharedKernel/issues/new?template=feature_request.yml).
- **Improve the docs** — fixes and clarifications to the README, `docs/` articles, or XML
  documentation are always welcome.
- **Submit code** — pick up an open issue or propose a change via a pull request.

If you plan to work on a non-trivial change, please open an issue first so we can discuss
the approach before you invest significant effort.

## Prerequisites

- [.NET SDK 10.0.300](https://dotnet.microsoft.com/download) or later (see [`global.json`](../global.json)).
- A RabbitMQ instance (or Docker) is only required for the messaging integration tests.

## Getting Started

```bash
# 1. Fork and clone the repository
git clone https://github.com/<your-username>/Vulthil.SharedKernel.git
cd Vulthil.SharedKernel

# 2. Restore local tools and build the solution
dotnet tool restore
dotnet build Vulthil.SharedKernel.slnx -c Release

# 3. Run the test suite
dotnet test Vulthil.SharedKernel.slnx -c Release
```

## Development Workflow

1. Create a topic branch off `main` (e.g. `feat/outbox-retry`, `fix/consumer-ack`).
2. Make your change in small, focused commits.
3. Ensure the build is clean and all tests pass locally.
4. Push your branch and open a pull request against `main`.

### Commit Messages

This repository follows the [Conventional Commits](https://www.conventionalcommits.org/)
specification — the changelog and version bumps are generated from commit history. Use a
single-line message in the form `<type>(<scope>): <description>`, for example:

```
feat(messaging): add publisher confirms to RabbitMQ transport
fix(results): preserve error metadata when mapping
docs(readme): clarify package responsibilities
```

Recognized types: `feat`, `fix`, `docs`, `perf`, `refactor`, `test`, `chore`, `ci`.

## Coding Guidelines

The full conventions live in [`.github/copilot-instructions.md`](copilot-instructions.md)
and [`.github/instructions/`](instructions). The essentials:

- Use **file-scoped namespaces** that match the directory structure.
- Prefer **immutable types** and use `record` for immutable data.
- Do **not** add comments inside methods. For complex logic, extract a well-named method
  instead.
- When you change a **public member**, update its **XML documentation**, the relevant docs
  in the [`docs/`](../docs) folder, and the README if applicable.
- Do not suppress **CS1591** — add the missing XML comments instead.
- Update the **Public API** files (`PublicAPI.*.txt`) for the affected assembly when public
  surface changes.

## Testing Guidelines

- Use the **Vulthil.xUnit** framework and follow the **Arrange–Act–Assert** pattern.
- Derive from `BaseUnitTestCase` or `BaseUnitTestCase<T>` to leverage shared setup.
- Lazily create the system under test by overriding `CreateInstance`/`CreateInstance<T>`
  and exposing it through the `Target` property.
- Use the **AutoMocker** instance for dependencies: `Use<T>(...)` to register and
  `GetMock<T>()` to retrieve mocks.
- Prefer the `CancellationToken` property on the base test class over reading
  `TestContext.Current.CancellationToken` directly.
- Do not add comments inside test methods other than `Arrange`, `Act`, and `Assert`;
  name the test method to describe the behavior under test.

## Submitting a Pull Request

Before opening a PR, please confirm:

- [ ] The solution builds without warnings (`dotnet build -c Release`).
- [ ] All tests pass (`dotnet test -c Release`).
- [ ] Public API, XML documentation, and docs are updated where applicable.
- [ ] Commits follow the Conventional Commits format.

Fill out the [pull request template](PULL_REQUEST_TEMPLATE.md) so reviewers have the
context they need. Continuous integration runs the build and full test suite on every PR;
all checks must pass before a change can be merged.

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](../LICENSE) that covers this project.
