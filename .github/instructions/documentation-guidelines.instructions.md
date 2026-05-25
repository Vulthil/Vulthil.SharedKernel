---
apply-to: "src/**/*.*"
---

# Documentation Guidelines for AI Agents

When assisting with this C# solution, you MUST adhere to the following documentation standards for all code modifications, additions, and refactors. Failure to maintain these standards will result in build warnings, CI/CD failures, or degraded documentation.

## 1. XML Documentation for Public Members
All `public` types and members (classes, structs, interfaces, records, enums, methods, properties, events, and fields) REQUIRE standard C# XML documentation comments (`///`). 

*   **Required Tags:** Always include a `<summary>`. 
*   **Contextual Tags:** Include `<param>`, `<returns>`, `<value>`, `<remarks>`, and `<exception>` tags wherever applicable.
*   **Completeness:** Do not leave XML comments empty or auto-generated without meaningful descriptions. Explain the *why* and *how*, not just the *what*.
*   **InheritDoc:** Use `<inheritdoc />` where appropriate to avoid redundancy, but ensure that the base member's documentation is comprehensive.

## 2. Public API Tracking (`PublicAPI.Unshipped.txt`)
This project uses `Microsoft.CodeAnalysis.PublicApiAnalyzers` to track changes to the public API surface.

*   **Requirement:** Whenever you create a new public entry point, modify an existing public member's signature, or remove a public member, you MUST update the corresponding `PublicAPI.Unshipped.txt` file in that specific project.
*   **Formatting:** Ensure the fully qualified signature matches the analyzer's exact required format. 

## 3. Conceptual Documentation and Usage Patterns
The conceptual documentation is housed in the `docs` folder and is built using DocFX.

*   **Update Requirement:** When modifying existing logic or adding new public entry points, you MUST review the `docs` folder and update the relevant markdown (`.md`) articles to reflect the new usage patterns, code examples, and behaviors.
*   **DocFX Configuration:** You must respect the `docs/docfx.json` file. 
    *   Ensure any new markdown files are correctly referenced in the appropriate `toc.yml` (Table of Contents) files.
    *   Do not place files outside of the paths configured in the `docfx.json` build definition.
    *   Use DocFX-compatible Markdown (DFM) features if necessary, and ensure code snippets within the docs remain accurate and compilable.

---

## Agent Verification Checklist
Before completing your task, verify the following:
- [ ] Did I add `///` comments to all newly created or modified `public` members?
- [ ] Did I add the exact API signature to `PublicAPI.Unshipped.txt`?
- [ ] Did I add/update usage examples in the `docs` folder?
- [ ] Are my conceptual doc changes compatible with `docs/docfx.json` and the Table of Contents?