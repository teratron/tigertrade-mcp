# Agents Instructions

This file is the single source of truth for AI-agent rules in this project.
A hardlink to `CLAUDE.md` (and `GEMINI.md`, `CODEX.md` etc. as needed) is created from this file by the user.

## 1. Language Policy

- **Code, identifiers, comments, technical docs, commit messages, PR descriptions, error messages, logs**: English only.
- **Chat interaction, planning, design discussions, conversational reviews**: Russian.

## 2. File Hygiene

- **`.gitignore`**: keep up to date as the project evolves. Whenever a new generated artifact, build output, secrets file, or tool-specific cache appears, add it to `.gitignore` immediately in the same change.
- **`AGENTS.md`** (this file): the canonical place for project-wide rules. Any durable rule, convention, or constraint goes here. Do not duplicate rules into `CLAUDE.md` directly — `CLAUDE.md` is a hardlink target managed by the user.
- **Markdown formatting**: no horizontal rules (`---`) except in document footers. Prefer headings and blank lines for separation. No hardcoded absolute paths in links (`file:///C:/...`); use relative paths or backticked filenames.

## 3. Architecture Constraints

- **Target stack**: C# / .NET 8 (rationale documented in the plan file).
- **No reverse engineering**: do not load, decompile, or use reflection on `TigerTrade.Api.dll` or any other proprietary TigerTrade assembly. Tiger.com EULA Article 4.3.1 explicitly forbids this. Only documented extension points are allowed:
  - Tiger API (Local Signal Server) at `ws://localhost:7819`
  - TigerTrade C# Indicator SDK (`IndicatorBase`, `IndicatorSourceBase`, `[Indicator]`, namespaces `TigerTrade.Chart.*`)
  - External broker connector APIs (Quik, Transaq, MetaTrader 5, Rithmic) used independently of TigerTrade
- **Plugin SPI for brokers**: every broker integration sits behind `IBrokerConnector` and is registered via DI. Adding a broker must not require changes to MCP tool code.

## 4. Safety Rules for Trading Code

- All write-operations (place/modify/cancel order) must be:
  - Gated by a `--enable-trading` server feature flag. Without it, write-tools are not registered in the MCP inventory at all.
  - Default to `dry_run: true`.
  - Require an explicit `confirm: string` parameter with literal acknowledgement text.
  - Logged to `logs/trading-audit-*.jsonl` (every attempt, including dry-runs).
- Never bypass these guards "for testing convenience". Use `BrokerMock` for tests.

## 5. Reference Project

- `.references/tradingview-mcp/` is the design reference for MCP-surface patterns: granular tools, compact-by-default output (`summary: true`, `study_filter`), CLI parity, decision-tree style `CLAUDE.md`.
- The TradingView transport (CDP / Electron) is **not** applicable to TigerTrade. Reuse design patterns, not implementation.

## 6. Reference Materials

- Implementation plan: `C:\Users\Oleg\.claude\plans\curried-exploring-nest.md`
- Tiger API docs: <https://support.tiger.com/english/development-for-tiger.trade-windows/tiger-api>
- Indicator SDK docs: <https://support.tiger.com/english/development-for-tiger.trade-windows>
- EULA: <https://www.tiger.com/terminal/end-user-license-agreement>

## 7. Completion Checklist (per task)

Before declaring any task done, verify:

- [ ] All code, identifiers, comments are in English; chat replies are in Russian
- [ ] No horizontal rules in markdown (except footers)
- [ ] `.gitignore` updated for any new generated artifact, secret file, or tool cache
- [ ] New durable rules added to this `AGENTS.md` (not to `CLAUDE.md` directly)
- [ ] No use of `TigerTrade.Api.dll` via reflection or decompilation
- [ ] If touching write-broker code, safety guards (feature flag, dry-run, confirm, audit log) are intact
