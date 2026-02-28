# Copilot Instructions

## Project Guidelines
- Use a lazy `Target` pattern from `CreateInstance<T>` when inheriting from `BaseUnitTestCase` (or `BaseUnitTestCase<T>` when accessibility allows) for test classes in this repository.
- When modifying a public member, make sure to update the XML Documentation and the corresponding docs in the docs folder and the README file if applicable.
- Do not ignore CS1591 warnings; analyze and add missing XML comments instead.

## Documentation Guidelines
- When generating package README files, keep them short and concise, and use the docs folder for more elaborate usage patterns.