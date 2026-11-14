---
paths:
  - "src/**"
  - "docs/**"
---

# Documentation and public API

Apply these documentation standards to code modifications, additions, and refactors in this C# solution; missing pieces cause build warnings, CI failures, or stale documentation.

## 1. XML documentation for public members
Add standard C# XML documentation comments (`///`) only where the compiler would otherwise emit **CS1591** ("Missing XML comment for publicly visible type or member"). Do not suppress CS1591; add the missing comments instead.
In practice this covers `public` types and members (classes, structs, interfaces, records, enums, methods, properties, events, and fields) in assemblies that generate a documentation file.
Do not add `///` comments to members the compiler does not warn about (e.g., `internal`, `private`, or `protected` members, or members already covered by an enclosing `<inheritdoc />`), as this adds noise without satisfying any analyzer requirement.

*   **Required Tags:** Always include a `<summary>`.
*   **Contextual Tags:** Include `<param>`, `<returns>`, `<value>`, `<remarks>`, and `<exception>` tags wherever applicable.
*   **Completeness:** Do not leave XML comments empty or auto-generated without meaningful descriptions. Explain the *why* and *how*, not just the *what*.
*   **InheritDoc:** Use `<inheritdoc />` where appropriate to avoid redundancy, but ensure that the base member's documentation is comprehensive.

## 2. Public API tracking (`PublicAPI.Unshipped.txt`)
This project uses `Microsoft.CodeAnalysis.PublicApiAnalyzers` to track changes to the public API surface.

*   **Requirement:** When you add a public entry point, change a public member's signature, or remove a public member, update the `PublicAPI.Unshipped.txt` file of that project; the PublicAPI analyzer reports every mismatch.
*   **Formatting:** Ensure the fully qualified signature matches the analyzer's exact required format.

## 3. Conceptual documentation and usage patterns
The conceptual documentation is housed in the `docs` folder and is built using DocFX.

*   **Update Requirement:** When you add or change a public member, or a change alters behavior that the `docs` articles describe, update the matching markdown (`.md`) articles (usage patterns, code examples, and behavior) and the package README where it applies.
*   **Package READMEs:** Keep package README files short and concise; put more elaborate usage patterns in the `docs` folder.
*   **DocFX Configuration:** Follow the build definition in `docs/docfx.json`.
    *   Ensure any new markdown files are correctly referenced in the appropriate `toc.yml` (Table of Contents) files.
    *   Do not place files outside of the paths configured in the `docfx.json` build definition.
    *   Use DocFX-compatible Markdown (DFM) features if necessary, and ensure code snippets within the docs remain accurate and compilable.

---

## Verification checklist
Before completing your task, verify the following:
- [ ] Did I add `///` comments to every newly created or modified member that would otherwise trigger CS1591 (and avoid adding them where the compiler does not warn)?
- [ ] Did I add the exact API signature to `PublicAPI.Unshipped.txt`?
- [ ] Did I update the `docs` articles whose described behavior changed, and add usage for new public entry points?
- [ ] Are my conceptual doc changes compatible with `docs/docfx.json` and the Table of Contents?
