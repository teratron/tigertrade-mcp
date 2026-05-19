# Tiger Trade MCP Server

Personal AI assistant bridge for the [Tiger Trade](https://www.tiger.com/) desktop terminal. Exposes a Model Context Protocol (MCP) server that lets Claude Code, Cursor, and other MCP-compatible agents read chart state, query broker accounts, and (optionally, behind explicit safety flags) submit orders against external broker connectors.

> [!WARNING]
> **This tool is not affiliated with, endorsed by, or associated with Tiger.com or Tiger Trade.** It integrates with your locally running Tiger Trade desktop application **only via publicly documented extension points**. Review [`EULA-DISCLAIMER.md`](EULA-DISCLAIMER.md) before use.

> [!IMPORTANT]
> **Requires a working Tiger Trade installation and any subscriptions / market data agreements the platform itself requires.** This tool does not bypass any Tiger.com paywall, license check, or access control.

> [!NOTE]
> **All data processing occurs locally.** No Tiger Trade data is transmitted, stored, or redistributed externally by this tool. Broker connectors talk directly to their respective broker APIs from your machine.

> [!CAUTION]
> Live trading is **off by default**. Write-tools (place / cancel / modify order) are not registered with the MCP server unless `--enable-trading` is passed on the command line, and even then every call defaults to `dry_run: true` and requires an explicit `confirm` text. See [`TRADING-SAFETY.md`](TRADING-SAFETY.md) (planned) before flipping the switch.

## Status

Early-stage. Phase 0 (project skeleton, build, configuration, logging) is in progress. See [`.claude/plans/`](.claude/plans/) and `AGENTS.md` for the roadmap. No MCP tools beyond `server_info` are wired up yet.

## How It Works (and why it stays inside Tiger's terms)

Tiger Trade is a native WPF .NET application — **not** an Electron app. The Chrome DevTools Protocol approach used by [TradingView MCP](.references/tradingview-mcp/) (the design reference for this project) does not apply here. Instead, this server uses three legally documented integration paths, each chosen explicitly:

1. **Tiger API (Local Signal Server)** — a documented WebSocket server on `ws://localhost:7819` that the Tiger Trade terminal exposes when you enable `Settings → Local Signal Server → Enable Signal Server`. Used for UI control (e.g. switching the active symbol in a link-group).
2. **Tiger Trade C# Indicator SDK** — the documented `IndicatorBase` / `IndicatorSourceBase` extension model (`TigerTrade.Chart.*` namespaces). Used for chart reads (OHLCV, cluster, T&S) and overlay drawing. Loaded by the terminal from `%USERPROFILE%\Documents\TigerTrade\Indicators\`.
3. **External broker connectors** — Quik, Transaq, MetaTrader 5, Rithmic, etc. Each broker exposes its own public API independent of Tiger Trade. Real trading happens via those APIs, not through Tiger Trade itself.

Reverse engineering or reflection against `TigerTrade.Api.dll` (or any other proprietary Tiger Trade assembly) is **explicitly forbidden** by the Tiger.com EULA, Article 4.3.1, and is therefore explicitly forbidden in this project. See [`EULA-DISCLAIMER.md`](EULA-DISCLAIMER.md).

## What This Tool Does Not Do

- Connect to Tiger.com's servers or APIs.
- Store, transmit, or redistribute any market data.
- Bypass any Tiger Trade paywall, license check, or access control.
- Reverse-engineer, decompile, or reflect over `TigerTrade.Api.dll` or any other Tiger Trade binary.
- Execute trades through Tiger Trade itself (trading happens via configurable broker connectors).
- Operate without a working local Tiger Trade installation.

## Architecture

```text
Claude Code / Cursor / any MCP client
              |
              | MCP stdio (JSON-RPC)
              v
+---------- tigertrade-mcp (C# .NET 10) ----------+
|  MCP layer (ModelContextProtocol SDK)           |
|  CLI layer (Spectre.Console.Cli, binary: `tt`)  |
|  Tools/* (server_info, tiger_*, broker_*, ...)  |
|  Connector SPI: IBrokerConnector, IChartReader  |
+--+-----------+-----------+------------+---------+
   |           |           |            |
TigerLink   BrokerQuik  BrokerMt5  Indicator IPC
(ws@7819)   (QUIK#)     (MtApi5)   (named pipe)
   |           |           |            |
   v           v           v            v
Tiger Trade  Quik       MT5 term.   Tiger Trade
terminal     terminal               (separate DLL)
```

Design principles:

- **Thin core + connector plugins.** Each broker hides behind `IBrokerConnector` and is selected via `appsettings.json:Broker.Type`. Adding a broker does not touch MCP tool code.
- **Read-only by default.** All write-operations are gated by `--enable-trading`, default `dry_run: true`, and require an explicit `confirm` text.
- **CLI parity.** Every MCP tool is also reachable as a `tt <area> <command>` invocation (pattern borrowed from the `tv` CLI in [`tradingview-mcp`](.references/tradingview-mcp/)).
- **Compact output.** `summary: true`, `study_filter`, capping, and dedup are first-class to keep LLM context small.

## Prerequisites

- **Tiger Trade desktop application** (Windows) with `Settings → Local Signal Server → Enable Signal Server` switched on for Tiger API features.
- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download)).
- **Claude Code** (or any MCP-compatible client) for MCP tools.
- **A broker terminal and credentials** (Quik / Transaq / MT5 / Rithmic) for trading features — optional until Phase 2.

## Quick Start

> Not ready yet. Will be filled in after Phase 1 (`tiger_health_check`, `tiger_set_link_symbol`, `tiger_get_config`) lands.

For now you can build and smoke-test the skeleton:

```powershell
dotnet build TigerTradeMcp.slnx
dotnet run --project src/TigerTradeMcp.Server
```

The server registers a single tool, `server_info`, which returns its name, version, PID, and OS.

## CLI

> Not ready yet. Planned binary name: `tt`. Every MCP tool will also be a `tt <command>` subcommand, pipe-friendly with JSON output.

## Configuration

Defaults live in [`src/TigerTradeMcp.Server/appsettings.json`](src/TigerTradeMcp.Server/appsettings.json). Override via:

- `appsettings.Development.json` for local dev (gitignored — `appsettings.*.json` is in `.gitignore` once secrets are introduced).
- Environment variables with prefix `TTMCP_` (e.g. `TTMCP_TigerLink__Url=ws://localhost:7820`).

Sections:

| Section    | Purpose                                                                                              |
| ---------- | ---------------------------------------------------------------------------------------------------- |
| `TigerLink`| URL, connect timeout, reconnect parameters for the Tiger API WebSocket                               |
| `Broker`   | Active broker connector type and per-broker settings (Phase 2)                                       |
| `Trading`  | Live trading switch, confirm-text, audit log path (Phase 3)                                          |
| `Serilog`  | Structured logging — stdout is kept clean for MCP stdio; everything goes to stderr and rolling file  |

## Logging

`stdout` is reserved for the MCP JSON-RPC stream. All log output goes to `stderr` (via `standardErrorFromLevel: "Verbose"` in `Serilog.Sinks.Console`) and to a rolling file under `logs/tigertrade-mcp-*.log`. Trading actions (when Phase 3 lands) additionally write to `logs/trading-audit-*.jsonl`.

## Roadmap

| Phase | Goal                                                                    | Status      |
| ----- | ----------------------------------------------------------------------- | ----------- |
| 0     | Project skeleton, build, configuration, logging, docs, CI               | in progress |
| 1     | MVP: Tiger API (ws@7819) + MCP skeleton (health, set-symbol, get-config)| pending     |
| 2     | Broker connector SPI + first real broker (read-only)                    | pending     |
| 3     | Trading (write) with triple-guard safety                                | pending     |
| 4     | Streaming, multi-symbol, skills, agents                                 | pending     |
| 5     | Optional: Tiger Trade Indicator SDK + IPC for cluster / footprint / T&S | deferred    |

## Reference Project

[`.references/tradingview-mcp/`](.references/tradingview-mcp/) is the design reference for the MCP surface: granular tools, compact-by-default output, CLI parity, decision-tree style `CLAUDE.md`. Its transport (CDP / Electron) is **not** applicable here; only its design patterns are reused.

## Disclaimer

See [`EULA-DISCLAIMER.md`](EULA-DISCLAIMER.md) for the full legal disclaimer, including Tiger.com EULA Article 4.3.1 compliance notes and broker-API responsibilities.

## Security

See [`SECURITY.md`](SECURITY.md) for the local-only / no-outbound-network model, credential handling, and how to report security issues.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for development setup, branch conventions, language policy (English in code, Russian in chat for the maintainer), and the safety contract that contributions must preserve.

## License

MIT &mdash; see [`LICENSE`](LICENSE) (to be added). The license applies to the source code of this project only. It does not grant any rights to Tiger.com's software, data, trademarks, or intellectual property.
