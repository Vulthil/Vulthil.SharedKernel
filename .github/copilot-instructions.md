# Copilot Instructions

## Git Guidelines
- Do not commit without an explicit permission. Allow the developers a change to review the code to provide feedback before commiting.
- Do not create commit descriptions. Only suggest the one line commit message that describes the change.
- Do not add Co-Authored-By trailers.

## Project Guidelines
- Use a lazy `Target` pattern from `CreateInstance<T>` when inheriting from `BaseUnitTestCase` (or `BaseUnitTestCase<T>` when accessibility allows) for test classes in this repository.
- When modifying a public member, make sure to update the XML Documentation and the corresponding docs in the docs folder and the README file if applicable.
- When modifying a public member, make sure to check the Public.API files for the affected assembly and update them if necessary.
- Do not ignore CS1591 warnings; analyze and add missing XML comments instead.
- Comments inside method bodies must explain *why*, not *what*: a why-comment states a constraint, invariant, or rationale the code cannot express on its own (e.g. concurrency/ordering requirements, the reason for a workaround, a justification for otherwise-surprising deliberate behavior). Narration/what-comments — restating what the next line does, section-header comments, play-by-play commentary — are not allowed. For complex logic, prefer extracting it into a separate method with a descriptive name over adding a comment.

## Testing Guidelines
- Prefer using the Vulthil.xUnit testing framework for tests.
- When writing tests, follow the Arrange-Act-Assert pattern for better readability and maintainability.
- Prefer using the BaseUnitTestCase or BaseUnitTestCase<T> classes for test cases to leverage common setup and utilities.
- Prefer using the `CancellationToken` property on the base test classes, instead of getting the `TestContext.Current.CancellationToken` directly.
- Prefer using the AutoMocker instance for dependency injection in tests to simplify test setup and improve readability.
- Use the methods on the BaseUnitTestCase class for modifying the AutoMocker instance, such as `Use<T>(T instance)` or `Use<T>()` for registering dependencies, and `GetMock<T>()` for retrieving mocks from the AutoMocker.
- Override the CreateInstance or CreateInstance<T> methods and use the Target property to lazily create the instance under test.
- Do not add comments inside test methods except Arrange, Act and Assert; rename the test method to be descriptive of the behavior being tested instead of adding comments.

## Documentation Guidelines
- When generating package README files, keep them short and concise, and use the docs folder for more elaborate usage patterns.