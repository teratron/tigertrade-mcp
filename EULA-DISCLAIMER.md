# EULA & Disclaimer

This project is provided **for personal, educational, and research purposes only**. It is not affiliated with, endorsed by, or associated with Tiger.com, the Tiger Trade application, or any broker named in the documentation.

## How This Tool Works

`tigertrade-mcp` is a local-only MCP (Model Context Protocol) server that integrates with the Tiger Trade desktop application through **publicly documented extension points only**:

1. **Tiger API — Local Signal Server** at `ws://localhost:7819`. This WebSocket interface is documented at the Tiger.com support portal and must be **explicitly enabled by the user** via `Settings → Local Signal Server → Enable Signal Server`. Nothing happens without that deliberate step.
2. **Tiger Trade C# Indicator SDK** (`IndicatorBase`, `IndicatorSourceBase`, `[Indicator]` attributes, the `TigerTrade.Chart.*` namespaces). Indicators are loaded by the terminal from `%USERPROFILE%\Documents\TigerTrade\Indicators\` per the documented mechanism.
3. **Independent broker APIs** (Quik, Transaq, MetaTrader 5, Rithmic, etc.). Each broker exposes its own public API. Trading happens against the broker's API directly — Tiger Trade is **not** in the order path.

This tool does **not**:

- Connect to Tiger.com's servers or APIs.
- Modify any Tiger Trade files.
- Intercept network traffic between Tiger Trade and Tiger.com.
- Reverse-engineer, decompile, or use reflection against `TigerTrade.Api.dll` or any other Tiger Trade binary. See the explicit prohibition below.

## Tiger.com EULA Article 4.3.1 — No Reverse Engineering

The Tiger.com End User License Agreement, Article 4.3.1, forbids reverse engineering, disassembly, and decompilation of the Tiger Trade software. This project is built around that constraint.

The maintainer commits to the following hard rules:

- **No reflection** against any `TigerTrade.*.dll` to discover undocumented APIs, classes, or method signatures.
- **No decompilation** of Tiger Trade binaries.
- **No use of internal / undocumented APIs.** If a capability is needed but not documented, the integration is either skipped or implemented through an external path (e.g. via the broker API, not through Tiger Trade).
- **No bundling, redistribution, or modification** of any Tiger Trade binary.

If a contributor proposes a change that would violate any of the above, it will be rejected on legal grounds regardless of technical merit.

## Your Responsibilities

By using this software, you acknowledge and agree that:

1. **You are solely responsible** for ensuring your use of this tool complies with the [Tiger.com End User License Agreement](https://www.tiger.com/terminal/end-user-license-agreement), Tiger.com's Terms of Service, your broker's API terms, your exchange's data licensing, and all applicable laws in your jurisdiction.
2. **Tiger.com / Tiger Trade reserves the right** to modify, restrict, or remove the Local Signal Server (`ws://localhost:7819`) interface or the Indicator SDK at any version bump, without notice. This tool may break at any time.
3. **You assume all risk** associated with using this tool. The maintainer is not responsible for any account bans, suspensions, license revocations, financial losses, legal actions, or other consequences resulting from its use.
4. This tool **must not be used** for, including but not limited to:
   - Redistributing, reselling, or commercially exploiting market data obtained through Tiger Trade or any broker connector.
   - Circumventing Tiger Trade's licensing or subscription model.
   - Circumventing any broker's access controls, rate limits, or risk-management systems.
   - Violating the intellectual property rights of Tiger.com, any broker, any exchange, any data vendor, or any third-party indicator author.
   - Performing market manipulation, wash trading, spoofing, or any other prohibited trading conduct.
5. **Market data** accessed through this tool remains subject to exchange and data provider licensing terms. **Do not redistribute, store, or commercially exploit any data obtained through this tool.**
6. **Live trading is off by default.** Order-placing tools (`broker_place_order`, `broker_cancel_order`, `broker_modify_order`) are not registered with the MCP server unless `--enable-trading` is passed on the command line. When enabled, every call defaults to `dry_run: true` and requires an explicit `confirm` string. **Disabling these safeguards is at your own risk.**
7. **LLM agents can and do make mistakes.** Treat any agent-generated trading action as you would treat a junior trainee's first day: with full supervision, small size, and a kill switch ready. This tool intentionally makes it hard to fire a real order accidentally — keep it that way.

## Broker Connectors — Additional Notes

Broker connectors (Quik, Transaq, MetaTrader 5, Rithmic, and any future additions) communicate **directly** with the broker's terminal or API from your local machine. This project does not proxy, intercept, store, or forward broker credentials or orders to any third party. Credentials live in your local configuration only and should be supplied via environment variables or `appsettings.Development.json` (gitignored), **never** committed to the repository.

Each broker connector is subject to that broker's own API terms. You are responsible for accepting and complying with those terms separately.

## Tiger Trade Indicator SDK — Additional Notes

If Phase 5 of the roadmap is enabled, this project ships a separate C# indicator assembly that is loaded by Tiger Trade through the documented `%USERPROFILE%\Documents\TigerTrade\Indicators\` mechanism. That assembly only:

- Reads chart data exposed by the public `IndicatorBase` / `IndicatorSourceBase` API.
- Sends that data to the MCP server process via a local named pipe.
- Optionally renders overlays through the documented `TigerTrade.Dx` rendering surface.

It does not reflect over any Tiger Trade type, does not access undocumented members, and does not modify Tiger Trade behavior.

## Attributions

This project is not affiliated with, endorsed by, or associated with:

- **Tiger.com** — Tiger Trade is a trademark of Tiger.com.
- **Anthropic** — Claude and Claude Code are trademarks of Anthropic, PBC.
- **Microsoft** — .NET and the official Model Context Protocol SDK for .NET are produced by Microsoft Corporation.
- Any broker mentioned in the documentation (including Quik, Transaq, MetaTrader 5, Rithmic).

## Use at Your Own Risk

If you are unsure whether your intended use complies with the Tiger.com EULA, your broker's API terms, or applicable law, **do not use this tool**. Consult a qualified attorney in your jurisdiction.
