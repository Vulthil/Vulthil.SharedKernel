# CLAUDE.md

## Git
- Do not commit without explicit permission. Allow the developers a chance to review the code and give feedback before committing.
- Do not create commit descriptions. Only suggest the one-line commit message that describes the change.
- Write the commit message and the PR title in the Conventional Commits form `<type>(<scope>): <description>` (types in `.github/CONTRIBUTING.md`); the squash merge takes its subject from one of them, and the release notes group changes by that type.
- Do not add Co-Authored-By trailers.

## Code
- Comments inside method bodies must explain *why*, not *what*: a why-comment states a constraint, invariant, or rationale the code cannot express on its own (e.g. concurrency/ordering requirements, the reason for a workaround, a justification for otherwise-surprising deliberate behavior). Narration/what-comments — restating what the next line does, section-header comments, play-by-play commentary — are not allowed. For complex logic, prefer extracting it into a separate method with a descriptive name over adding a comment.
- Library code under `src/` must call `ConfigureAwait(false)` on every `await` (including the implicit awaits in `await using`/`await foreach`) — `CA2007` is enforced as an error for `src/` only. Tests and samples are exempt and should not add it.

The rules for C# style, documentation and the public API, and tests are in `.claude/rules/`; each one loads when you work on files that match its `paths`.
