# Contributing

Thanks for considering a contribution. This project is small, opinionated, and bound by a strict safety contract (live trading is involved). Read this guide end-to-end before opening a PR.

## Ground Rules

These are non-negotiable. Violating any of them will get a PR closed regardless of how good the code is.

1. **No reverse engineering of Tiger Trade.** No reflection over `TigerTrade.Api.dll` or any other proprietary Tiger Trade binary, no decompilation, no use of undocumented APIs. See [`EULA-DISCLAIMER.md`](EULA-DISCLAIMER.md). Only the three documented integration paths are allowed: Tiger API `ws://localhost:7819`, Tiger Trade C# Indicator SDK, and independent broker APIs.
2. **Trading safety guards stay intact.** Three independent guards (feature flag `--enable-trading`, default `dry_run: true`, mandatory `confirm` text) plus the audit log. PRs that weaken any of them — even "just for the test setup" — will be rejected. Use `BrokerMock` for tests.
3. **No outbound network listeners.** The server only speaks MCP stdio, loopback WebSocket, broker-specific local channels, and a local named pipe. No HTTP listener, no gRPC server, no public socket. See [`SECURITY.md`](SECURITY.md).
4. **No committed secrets.** Credentials live in environment variables (`TTMCP_*` prefix) or in `appsettings.Development.json` (gitignored). Never inline values in `.mcp.json`, never paste into the repo.

## Language Policy

The maintainer is Russian-speaking. The codebase is bilingual on purpose:

| Surface                                              | Language |
| ---------------------------------------------------- | -------- |
| Code: identifiers, comments, docstrings              | English  |
| Documentation: README, design notes, plan files      | English  |
| Process artifacts: commits, PR titles / descriptions | English  |
| Runtime: error messages, logs, API definitions       | English  |
| Conversational interaction with the maintainer / AI  | Russian  |

If you only speak English, your PR is welcome; just keep the technical content English (which you would anyway) and the maintainer will reply in English.

## Development Environment

Required:

- **.NET 10 SDK** (`dotnet --version` should report `10.x`). Install from <https://dotnet.microsoft.com/download>.
- A reasonably modern editor with C# support (Rider, Visual Studio 2026, or VS Code with the official C# Dev Kit extension).

Optional but recommended:

- **Tiger Trade desktop application** with `Settings → Local Signal Server → Enable Signal Server` switched on, for Phase 1 e2e tests.
- **A broker terminal** matching whichever connector you are working on, for Phase 2+ work.

## Build & Run

```powershell
# Restore + build the entire solution
dotnet build TigerTradeMcp.slnx

# Run the MCP server (stdio transport — talk to it via an MCP client, not interactively)
dotnet run --project src/TigerTradeMcp.Server

# Run tests
dotnet test
```

The solution is in the new `.slnx` XML format (default in .NET 10). All shared MSBuild settings (target framework, nullable, analyzer level) live in [`Directory.Build.props`](Directory.Build.props) — do not duplicate them into individual `.csproj` files.

## Project Layout

```text
src/
  TigerTradeMcp.Server/      MCP server entry point (Program.cs, Tools/, appsettings.json)
  TigerTradeMcp.Core/        Domain types, options, abstractions (IBrokerConnector, etc.)
  TigerTradeMcp.TigerLink/   WebSocket client for the Tiger API (Local Signal Server)
  TigerTradeMcp.Cli/         `tt` CLI executable (Spectre.Console.Cli)
tests/
  TigerTradeMcp.Tests/       xUnit + FluentAssertions + NSubstitute
```

Future broker connectors go under `src/TigerTradeMcp.Brokers.<Name>/` and register themselves through DI based on `appsettings.json:Broker.Type`.

## Branching & Commit Style

- **Branch from `master`.** Topic branches use `<area>/<short-slug>`, e.g. `tigerlink/reconnect-backoff`, `broker/quik-positions`.
- **Commit messages are English, Conventional-Commits-flavoured.** Examples:
  - `feat(tigerlink): add exponential-backoff reconnect`
  - `fix(broker-mock): correct unrealized PnL sign on shorts`
  - `chore(ci): bump dotnet to 10.0.200`
  - `docs(README): clarify trading-safety guards`
- **Small commits.** One logical change per commit. Squash on merge if a series of WIP commits accumulated.

## Pull Request Workflow

1. Confirm the change is in scope. If you are unsure, open an issue first describing the intended change. For anything trading-related, mention it explicitly — these PRs are reviewed more carefully.
2. Branch from `master`, push, open a draft PR early. Drafts welcome.
3. Mark PR ready when:
   - `dotnet build TigerTradeMcp.slnx` is clean (no warnings beyond the agreed `WarningsAsErrors`).
   - `dotnet test` is green.
   - New code is covered by tests (`BrokerMock` for trading code, mock WebSocket for TigerLink).
   - You ran the completion checklist below.
4. CI runs build + test. Re-pushes are fine; do not force-push to a PR with active review comments without flagging.

## Completion Checklist

Tick all of these before marking a PR ready:

- [ ] Code, identifiers, comments, technical docs, commit messages are in English.
- [ ] No horizontal rules (`---`) in markdown except in document footers.
- [ ] `.gitignore` covers every newly generated artifact, secret file, or tool cache introduced by the change.
- [ ] No reflection or decompilation against `TigerTrade.*.dll`.
- [ ] No new outbound network listeners.
- [ ] If touching trading code: feature flag, default `dry_run: true`, mandatory `confirm`, and audit log all intact.
- [ ] If a new durable rule emerged from this change, it was added to [`AGENTS.md`](AGENTS.md), not duplicated into `CLAUDE.md`.
- [ ] `dotnet build` and `dotnet test` both clean locally.

## Code Style

The project uses the C# analyzers shipped with the .NET 10 SDK (`AnalysisLevel = latest-recommended`) and treats nullable annotations as errors (`WarningsAsErrors = nullable`). The repository does not ship an `.editorconfig` override — defaults apply. If you have an opinion that disagrees with the analyzer defaults, raise it as an issue before changing settings.

A few project-specific conventions:

- **Tools are static where possible.** MCP tools are plain `static` methods on classes marked `[McpServerToolType]`. Use DI inside the method body via `[FromKeyedServices]` or method-injected parameters where the MCP SDK supports it. Do not introduce instance state in tool classes.
- **Names mirror the MCP tool name.** A tool called `broker_get_quote` lives in a method named `GetQuote` inside a class `BrokerTools` registered with the matching MCP `Name`.
- **Compact output is the default.** Every tool that returns data takes a `summary: bool = true` parameter (or analogous capping) and returns JSON via `JsonSerializer.Serialize`. See `ServerInfoTool` for the minimal pattern.
- **Stderr-only logging.** Serilog is wired to send all log levels to stderr (and to a rolling file). Do not introduce sinks that write to stdout — the MCP stdio transport claims stdout exclusively.

## Reporting Bugs

Open a GitHub issue with: affected version (commit SHA), reproduction steps, what you expected, what actually happened, and relevant log excerpts (scrub credentials first).

For security issues, see [`SECURITY.md`](SECURITY.md) — do not file public issues.

## License

By contributing, you agree that your contributions are licensed under the project's MIT license (see [`LICENSE`](LICENSE), to be added).
