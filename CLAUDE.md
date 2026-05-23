# CLAUDE.md

@.github/copilot-instructions.md

## Scoped instructions

For each `.github/instructions/*.instructions.md`, read only its YAML
frontmatter first. Load the body only if its `apply-to` glob (paths relative
to repo root) matches a file in scope for the current task. Missing or
malformed frontmatter → treat as universal and load. Re-triage when the set
of files in scope changes.
