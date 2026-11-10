# Agent Guidance

## .NET Project Setup

- Use the .NET CLI to create projects, add them to the solution, and add package or project dependencies.
- Specify the target framework explicitly when creating xUnit v3 projects, for example `dotnet new xunit3 --framework net10.0`.
- Keep SDK selection unpinned and use an installed SDK that supports the project's target framework; add `global.json` only when a separate reproducibility requirement calls for it.

## Commit Granularity

Make each commit a single, coherent intent that is complete, reviewable, and verified. Use behavior and reviewer understanding—not file count, task count, or elapsed time—as the boundary.

- Include the implementation, tests, documentation, configuration, and generated sources required for that intent. Leave each commit buildable with its relevant tests passing.
- Split independent behaviors, unrelated fixes, broad cleanup, formatting, and dependency updates into separate commits when they can be reviewed and verified independently.
- Prefer a pure refactor before the behavior change that needs it. Keep the refactor behavior-preserving and verify it separately.
- Split a large feature into vertical, working increments when possible. Include each increment's contract, implementation, and tests; avoid commits that contain only an unusable layer.
- Commit OpenSpec planning artifacts separately from implementation when they represent a distinct review step. Include task-checkbox updates with the implementation they record.
- Keep generated artifacts with their source changes only when those artifacts are intended for version control. Exclude build outputs, logs, and local test artifacts.
- Use no fixed line-count target. A small change may require several files; a large mechanical change may belong in one commit. If a commit contains multiple unrelated intents, split it.

## Commit Messages

Write commit subjects as short English imperative sentences. Start with a verb and name the primary change, for example `Add session lifecycle management`.
