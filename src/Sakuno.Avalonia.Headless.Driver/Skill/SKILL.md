---
name: avalonia-headless-driver
description: Operate and verify Avalonia 12.1.x applications through the Sakuno Headless Driver. Use when starting a project-owned Headless Host, inspecting semantic UI snapshots, performing UI actions or waits, capturing screenshots, or closing and resetting a Headless Session.
---

# Avalonia Headless Driver

Use the local HTTP API for observation-action-verification of logical Avalonia controls. Keep the project-owned Host process alive for the whole scenario.

## Prerequisites and consumer setup

Use .NET 10 and aligned Avalonia 12.1.x packages. The Host runs on Windows, macOS, and Linux without a display server. curl is optional and replaceable with any equivalent HTTP client; the Bash examples use `python3` only for JSON parsing. Neither jq nor a custom or globally installed driver CLI is required.

Create a Console Host that references both `Sakuno.Avalonia.Headless.Driver` and the Avalonia application. Compile an `IHeadlessStartupAdapter` into the Host or an assembly it references. The adapter establishes the test application and initial UI; it does not call the production entry point.

```csharp
using Avalonia.Controls;
using Sakuno.Avalonia.Headless.Driver;

public sealed class HeadlessStartupAdapter : IHeadlessStartupAdapter
{
    public Type ApplicationType => typeof(App);
    public TopLevel CreateInitialWindow() => new MainWindow();
}
```

Set the Host's `Program.cs` to:

```csharp
using Sakuno.Avalonia.Headless.Driver.Hosting;

return await HeadlessHost.RunAsync(args);
```

Create a local, versioned JSON configuration. `startupAdapter` is the assembly-qualified name of the compiled adapter; the driver does not load request-supplied assemblies.

```json
{
  "version": 1,
  "startupAdapter": "MyApp.HeadlessStartupAdapter, MyApp",
  "rendering": true,
  "actionTimeoutSeconds": 5,
  "waitTimeoutSeconds": 10,
  "idleTimeoutSeconds": 600,
  "snapshot": {
    "maxDepth": 4,
    "maxNodes": 500,
    "maxTextLength": 500
  }
}
```

The package contains the configuration schema at `schemas/headless-driver.schema.json` and the HTTP contract at `openapi/v1.json`. Consult those schemas for every field and response shape. The running Host does not serve `/openapi/v1.json`.

## Start, authenticate, and claim the session

Start the built Host with `dotnet <host.dll> --config <config.json>`. Read exactly one stdout line and parse it as JSON with fields `url`, `token`, `processId`, and `protocolVersion`. Treat stdout as the readiness channel and stderr as diagnostics. Keep the token secret and do not copy it into logs or artifacts.

Run the following blocks in the same Bash shell. Set `HOST_DLL` and `CONFIG` to the built Host and local configuration paths.

```bash
set -euo pipefail

HOST_DLL="path/to/MyApp.Headless.Host.dll"
CONFIG="path/to/headless-driver.json"
ARTIFACT_DIR="artifacts/headless-driver"
mkdir -p "$ARTIFACT_DIR"

READY_FIFO="$ARTIFACT_DIR/host.stdout"
rm -f "$READY_FIFO"
mkfifo "$READY_FIFO"
dotnet "$HOST_DLL" --config "$CONFIG" >"$READY_FIFO" 2>"$ARTIFACT_DIR/host.stderr" &
HEADLESS_HOST_PID=$!
IFS= read -r READY <"$READY_FIFO"
read -r BASE_URL TOKEN PROCESS_ID PROTOCOL_VERSION < <(
  python3 - "$READY" <<'PY'
import json
import sys

ready = json.loads(sys.argv[1])
print(ready["url"], ready["token"], ready["processId"], ready["protocolVersion"])
PY
)

api() {
  curl --fail-with-body --silent --show-error --write-out '\n' \
    --header "Authorization: Bearer $TOKEN" "$@"
}
```

In PowerShell, invoke `curl.exe`, not the `curl` alias, with the equivalent arguments.

Check `GET /v1/capabilities` with bearer authentication for compatibility and available features before session work:

```bash
CAPABILITIES=$(api "$BASE_URL/v1/capabilities")
python3 - "$PROTOCOL_VERSION" "$CAPABILITIES" <<'PY'
import json
import sys

protocol = sys.argv[1]
capabilities = json.loads(sys.argv[2])
if protocol.split(".", 1)[0] != "1" or capabilities["protocolVersion"] != protocol:
    raise SystemExit("Unsupported or mismatched protocol version")
print(json.dumps(capabilities, indent=2))
PY
```

Claim the only session once. A second `POST /v1/sessions` in the same process returns `session_active`.

```bash
SESSION=$(api --request POST "$BASE_URL/v1/sessions")
read -r SESSION_ID WINDOW_ID < <(
  python3 - "$SESSION" <<'PY'
import json
import sys

session = json.loads(sys.argv[1])
print(session["sessionId"], session["windows"][0]["id"])
PY
)
```

## Observe, act, and verify

1. Observe the Semantic Tree and choose repeatable Element Queries from control semantics. Re-resolve targets for each operation; add criteria or `windowId` when a query is ambiguous.
2. Execute one action at a time. Text mode `replace` replaces content without implicitly pressing Enter; `append` and the separate `clear` action have distinct semantics.
3. Verify the business result with a condition wait or a new snapshot. An action response reports only executed input and processed UI work, never business success.

The sample application uses `UsernameTextBox`, `PasswordTextBox`, `LoginButton`, and `StatusText` as AutomationIds. This complete login example observes first, replaces both text values, clicks once, and verifies the resulting status:

```bash
api "$BASE_URL/v1/sessions/$SESSION_ID/snapshot" | python3 -m json.tool

api --request POST \
  --header 'Content-Type: application/json' \
  --data '{"type":"text","query":{"automationId":"UsernameTextBox"},"text":"agent","mode":"replace"}' \
  "$BASE_URL/v1/sessions/$SESSION_ID/actions"

api --request POST \
  --header 'Content-Type: application/json' \
  --data '{"type":"text","query":{"automationId":"PasswordTextBox"},"text":"correct-password","mode":"replace"}' \
  "$BASE_URL/v1/sessions/$SESSION_ID/actions"

api --request POST \
  --header 'Content-Type: application/json' \
  --data '{"type":"click","query":{"automationId":"LoginButton"}}' \
  "$BASE_URL/v1/sessions/$SESSION_ID/actions"

api --request POST \
  --header 'Content-Type: application/json' \
  --data '{"query":{"automationId":"StatusText"},"property":"text","comparison":"equals","expected":"Authenticated","timeoutSeconds":10}' \
  "$BASE_URL/v1/sessions/$SESSION_ID/wait"
```

A successful click does not prove authentication. The final wait on `StatusText` is the verification; a wait timeout means the expected business condition remained false.

## Capture a screenshot and shut down

Save the binary PNG with the HTTP client into an Agent-owned artifacts directory. The API accepts no output path. Pixel dimensions and render scaling are returned in `X-Pixel-Width`, `X-Pixel-Height`, and `X-Render-Scaling`.

```bash
curl --fail-with-body --silent --show-error \
  --header "Authorization: Bearer $TOKEN" \
  --output "$ARTIFACT_DIR/login.png" \
  "$BASE_URL/v1/sessions/$SESSION_ID/windows/$WINDOW_ID/screenshot"
```

Close the session exactly once. The DELETE response completes before the Host exits.

```bash
api --request DELETE "$BASE_URL/v1/sessions/$SESSION_ID"
wait "$HEADLESS_HOST_PID"
```

To reset or recover application state, start a fresh Host process and claim a new session. The previous session identity is stale and returns HTTP 410; do not reuse it.

## Boundaries and failures

- The Host binds only to loopback on a random port and requires the per-process bearer token for every `/v1` request.
- Keep requests sequential. Overlapping session work is rejected with HTTP 409 and `session_busy`, not queued.
- Requests are bounded to 64 KiB. Action and wait deadlines default to 5 and 10 seconds, are configurable locally, and cannot exceed 60 seconds. Authenticated inactivity closes the Host after 10 minutes by default.
- Snapshots are bounded and may be truncated; use their subtree paths for bounded expansion. Password values and text-action payloads remain redacted from observations, responses, and diagnostics.
- Use only the versioned UI contract. Requests cannot override security settings, evaluate code, run shell commands, load assemblies, or read or write arbitrary files. Local client tooling, not the driver, owns screenshot files.
