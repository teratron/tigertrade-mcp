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

- **Target stack**: C# / .NET 10 (rationale documented in the plan file). Solution uses the new `.slnx` XML format. Shared MSBuild settings live in `Directory.Build.props` — do not duplicate `TargetFramework`, `Nullable`, `ImplicitUsings`, or `LangVersion` into individual `.csproj` files.
- **No reverse engineering**: do not load, decompile, or use reflection on `TigerTrade.Api.dll` or any other proprietary TigerTrade assembly. Tiger.com EULA Article 4.3.1 explicitly forbids this. Only documented extension points are allowed:
  - Tiger API (Local Signal Server) at `ws://localhost:7819`
  - TigerTrade C# Indicator SDK (`IndicatorBase`, `IndicatorSourceBase`, `[Indicator]`, namespaces `TigerTrade.Chart.*`)
  - External broker connector APIs (Quik, Transaq, MetaTrader 5, Rithmic) used independently of TigerTrade
- **Plugin SPI for brokers**: every broker integration sits behind `IBrokerConnector` and is registered via DI. Adding a broker must not require changes to MCP tool code.
- **No outbound network listeners**: the server speaks MCP stdio, loopback WebSocket, broker-local channels, and a local named pipe. No HTTP / gRPC / TCP listener bound to a non-loopback interface.
- **Stdout is reserved for MCP**: every logging sink writes to stderr or a file. Serilog console sink uses `standardErrorFromLevel: "Verbose"`. Never `Console.WriteLine` from production code.

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

## 7. Decision Tree — Which Tool When

The MCP surface is built up phase-by-phase. Each row maps a typical user request to the tool sequence the agent should use. Empty rows are placeholders for tools that have not been implemented yet.

### Phase 1 — Tiger API (UI control, read-only)

| User says...                                       | Use this tool sequence                                                |
| -------------------------------------------------- | --------------------------------------------------------------------- |
| "Is Tiger Trade connected?"                        | `tiger_health_check`                                                  |
| "Switch chart to BTCUSDT on Binance Spot, group A" | `tiger_set_link_symbol` with `exchange/market/symbol/linkGroup` args  |
| "What is the server configured to do?"             | `tiger_get_config` → returns non-secret config snapshot                |
| "Inspect server metadata (version, PID, OS)"       | `server_info`                                                          |

### Phase 2 — Broker (read-only)

| User says...                                | Use this tool sequence                                                  |
| ------------------------------------------- | ----------------------------------------------------------------------- |
| "What is the current price of SBER?"        | `broker_get_quote { symbol }`                                            |
| "Give me a price summary of SBER 5m"        | `broker_get_ohlcv { symbol, timeframe, summary: true }`                  |
| "What positions do I have open?"            | `broker_list_positions`                                                  |
| "Show me active orders"                     | `broker_list_orders { status: "active" }`                                |
| "Show me my account state"                  | `broker_get_account`                                                     |
| "Give me a portfolio review"                | `broker_get_account` → `broker_list_positions` → `broker_list_orders`    |

### Phase 3 — Trading (write, guarded)

| User says...                                | Use this tool sequence                                                                         |
| ------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| "Place a limit buy 10 SBER at 250"          | `broker_place_order { side: buy, qty: 10, type: limit, price: 250, dry_run: true, confirm: … }` |
| "Cancel order 12345"                        | `broker_cancel_order { order_id: "12345", dry_run: true, confirm: … }`                          |
| "Modify order 12345 to price 251"           | `broker_modify_order { order_id, modifications, dry_run: true, confirm: … }`                    |

For Phase 3 calls the agent **must** ask the human user explicitly before flipping `dry_run` to `false`, and **must** include the exact `confirm` text configured in `appsettings.json:Trading.RequireConfirmText`.

### Phase 4 — Streaming, multi-symbol

| User says...                          | Use this tool sequence                                |
| ------------------------------------- | ----------------------------------------------------- |
| "Stream the SBER price"               | `stream_quote { symbol }` (JSONL output)               |
| "Watch my positions live"             | `stream_positions`                                    |
| "Scan all my watchlist for trends"    | `batch_run { symbols: [...], action: "ohlcv_summary" }`|

### Phase 5 — Indicator SDK (deferred)

Tools `chart_get_cluster`, `chart_get_t_and_s`, `chart_get_footprint`, `chart_draw_overlay` — populate this section only after Phase 5 ships.

## 8. Context Management Rules

These rules keep agent context lean. Apply them whenever a tool returns data.

1. **Always pass `summary: true`** on `broker_get_ohlcv` unless the user explicitly needs bar-by-bar data.
2. **Always pass `study_filter`** when targeting one indicator (Phase 5 only).
3. **Cap counts** — OHLCV default 100, max 500; orders 50 most recent; positions deduplicated by symbol; pine labels (Phase 5) capped at 50.
4. **Never poll in a loop from an MCP tool.** Streaming tools (`stream_*`) are the only place where polling is allowed, and only inside the dedicated stream engine.
5. **Use `tiger_get_config`** to inspect server state instead of re-asking the user. Do not call repeatedly within one task.

## 9. Completion Checklist (per task)

Before declaring any task done, verify:

- [ ] All code, identifiers, comments are in English; chat replies are in Russian
- [ ] No horizontal rules in markdown (except footers)
- [ ] `.gitignore` updated for any new generated artifact, secret file, or tool cache
- [ ] New durable rules added to this `AGENTS.md` (not to `CLAUDE.md` directly)
- [ ] No use of `TigerTrade.Api.dll` via reflection or decompilation
- [ ] No new outbound network listeners
- [ ] If touching write-broker code, safety guards (feature flag, dry-run, confirm, audit log) are intact
- [ ] `dotnet build TigerTradeMcp.slnx` is clean; `dotnet test` is green
