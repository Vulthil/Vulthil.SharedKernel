---
paths:
  - "tests/**"
  - "samples/**/*.Tests/**"
---

# Tests

- Prefer the Vulthil.xUnit testing framework for tests.
- Follow the Arrange-Act-Assert pattern for better readability and maintainability.
- Prefer deriving test classes from `BaseUnitTestCase` or `BaseUnitTestCase<T>` (when accessibility allows) to use the common setup and utilities.
- Override the `CreateInstance` or `CreateInstance<T>` methods and use the `Target` property to lazily create the instance under test.
- Prefer the `CancellationToken` property on the base test classes over getting `TestContext.Current.CancellationToken` directly.
- Prefer the AutoMocker instance for dependency injection in tests, and change it through the methods on the base class: `Use<T>(T instance)` or `UseReal<T>()` to register dependencies, and `GetMock<T>()` to retrieve mocks.
- Do not add comments inside test methods except Arrange, Act and Assert; rename the test method to be descriptive of the behavior being tested instead of adding comments.
- Do not use `Host.CreateApplicationBuilder()` for a test builder that never becomes a host the test disposes (for example one that only feeds `BuildServiceProvider()` or a configurator); use `Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings())` instead. The default builder watches `appsettings.json` for changes, and Linux CI allows only 128 file watchers (inotify instances) per user across all test processes that run at the same time.
