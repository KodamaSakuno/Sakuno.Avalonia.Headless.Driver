# Installing the Avalonia Headless Driver Agent Skill

The Agent Skill is project-local. No global installation, jq, or custom driver CLI is required. The Host requires .NET 10 and aligned Avalonia 12.1.x packages; curl is an optional, replaceable HTTP client used by the examples.

## Install for Kimi Code

From this repository's root, copy the canonical Skill into the project where the Agent will operate the application:

```bash
mkdir -p .agents/skills/avalonia-headless-driver
cp src/Sakuno.Avalonia.Headless.Driver/Skill/SKILL.md \
  .agents/skills/avalonia-headless-driver/SKILL.md
```

## Install for Claude Code

Use the same canonical source and the Claude Code project directory instead:

```bash
mkdir -p .claude/skills/avalonia-headless-driver
cp src/Sakuno.Avalonia.Headless.Driver/Skill/SKILL.md \
  .claude/skills/avalonia-headless-driver/SKILL.md
```

Install into only the agent directory you use. Keep the frontmatter name `avalonia-headless-driver` unchanged so the model-invoked Skill remains discoverable.

## Prepare the consumer and use the Skill

Follow the installed `SKILL.md` to create a Console Host referencing the driver and Avalonia application, compile the project's `IHeadlessStartupAdapter`, write the local versioned JSON configuration, and run the complete startup-to-shutdown workflow. The production application entry point is not used.

The package's versioned references are:

- `openapi/v1.json` — HTTP endpoints, request and response schemas, bearer authentication, and binary PNG responses.
- `schemas/headless-driver.schema.json` — local Host configuration.

Use the packaged schema matching the installed driver version. These files are build artifacts; the running Host does not expose an OpenAPI endpoint.
