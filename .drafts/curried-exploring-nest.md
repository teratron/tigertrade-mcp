# Plan: TigerTrade MCP Server

## Context

Цель — построить MCP-сервер, который даёт AI-агентам (Claude Code, Cursor, прочие MCP-клиенты) интерфейс к десктоп-платформе TigerTrade (`D:\Program Files\TigerTrade\TigerTrade.exe`).

Референс — `.references/tradingview-mcp/`. Он работает через Chrome DevTools Protocol поверх Electron-сборки TradingView. Для TigerTrade этот транспорт **не применим**: TigerTrade — native WPF .NET-приложение (стек: ActiproSoftware.\*.Wpf, SharpDX, DirectX, TigerTrade.\*.dll). CDP-флаг `--remote-debugging-port=9222` для него ничего не активирует. WebView2 присутствует, но рендерит только вспомогательные HTML-окна (новости, документация), а не торговый UI.

Открытое исследование выявило три легально доступных слоя интеграции:

1. **Tiger API (Local Signal Server)** — публично документированный WebSocket-сервер на `ws://localhost:7819`. Активируется в `Settings → Local Signal Server → Enable Signal Server`. Единственная документированная команда — `setLinkSymbol` (переключение символа в link-group). UI-control only, не торговля.
2. **TigerTrade C# Indicator SDK** — официально документированная расширяемая модель: классы `IndicatorBase`, `IndicatorSourceBase`, атрибуты `[Indicator]`, `[IndicatorSource]`, namespaces `TigerTrade.Chart.Base`, `TigerTrade.Chart.Indicators.*`. Размещение в `%USERPROFILE%\Documents\TigerTrade\Indicators\`. Даёт чтение чарта (OHLCV, кластер, индикаторы) и отрисовку overlay, но **не торговлю**.
3. **Внешние брокерские коннекторы** (Quik, Transaq, MT5, Rithmic) — у каждого свой публичный API, доступный в обход TigerTrade. Через них реализуется реальная торговля.

**Что НЕ делаем**: reflection в `TigerTrade.Api.dll` (запрещено EULA 4.3.1 — reverse engineering).

User-выбор: гибрид (чтение чарта + торговля), брокер не зафиксирован — нужна plugin-архитектура, язык — engineering choice, Indicator SDK — отложен.

## Architecture Decision: язык — C# / .NET 8

Краткое обоснование (углублённый анализ под выбор пользователя):

| Фактор | C# / .NET 8 | Python | TypeScript |
|---|---|---|---|
| TigerTrade Indicator SDK | **Native, in-process, zero IPC** | Нужен C# мост + IPC | Нужен C# мост + IPC |
| Quik (QUIK#) | **Native .NET** | quik-lua-rpc через ZMQ | через ZMQ |
| Transaq | XML connector (COM/.NET-friendly) | transaq_connector | через мост |
| MetaTrader 5 | MT5 .NET client + C++ DLL | **Official MetaTrader5 pkg** | Через мост |
| Rithmic R\|API+ | **Native .NET DLL** | pyrithmic (community) | Через мост |
| Official MCP SDK | **`ModelContextProtocol` от Microsoft** | mcp Python SDK (зрелый) | `@modelcontextprotocol/sdk` (зрелый) |
| Native Windows runtime у TigerTrade | **Совпадение runtime** | Отдельный процесс | Отдельный процесс |
| AOT/single-file публикация | **`dotnet publish -r win-x64`** | PyInstaller (хрупко) | pkg / bun (нестабильно) |

Решающий вес даёт **3 фактора**:

- Indicator SDK — это **критический будущий путь** для чтения cluster/footprint/T&S. C# даёт его без IPC. Python/TS требуют C#-моста, удваивая количество подвижных частей.
- Quik (наиболее вероятный брокер для русскоязычного пользователя) имеет первоклассную .NET-обёртку QUIK#. Из Python — через ZMQ.
- Rithmic R\|API+ — официально только C++ и .NET SDK; pyrithmic — community-only.

MT5 — единственный case, где Python имеет преимущество (официальный пакет `MetaTrader5`), но и у него есть `MtApi5` для .NET.

**Вывод**: C# / .NET 8.

## High-Level Architecture

```
┌─────────────────────────────────────────────┐
│ Claude Code / Cursor / любой MCP-клиент     │
└──────────────────┬──────────────────────────┘
                   │ MCP stdio (JSON-RPC)
┌──────────────────▼──────────────────────────┐
│ tigertrade-mcp  (C# .NET 8 console)         │
│                                             │
│  ┌─── MCP layer (ModelContextProtocol SDK)─┐│
│  │   tool registration, JSON envelope      ││
│  └─────────────────────────────────────────┘│
│  ┌─── CLI layer (Spectre.Console.Cli) ─────┐│
│  │   tt <command> <subcommand> ...         ││
│  └─────────────────────────────────────────┘│
│  ┌─── Core domain ─────────────────────────┐│
│  │   Tools/* (chart, broker, account, ...) ││
│  └─────────────────────────────────────────┘│
│  ┌─── Connector SPI ───────────────────────┐│
│  │   IBrokerConnector, IChartReader        ││
│  └────┬──────────┬───────────┬──────────┬──┘│
│       │          │           │          │   │
│  TigerLink   BrokerQuik  BrokerMt5  Indicator
│   (WS@7819)   (QUIK#)    (MtApi5)   IPC pipe
└─────────────────────────────────────────────┘
       │           │            │          │
       ▼           ▼            ▼          ▼
   TigerTrade   Quik       MT5 терминал  TigerTrade
   (порт 7819)  терминал                  индикатор
                                          (отдельная DLL)
```

Принципы:

- **Тонкое ядро + плагины коннекторов** — каждый брокер за `IBrokerConnector`, регистрируется через DI. Включение брокера — конфигом, без перекомпиляции тулзов.
- **Read-only по умолчанию** — все write-операции за фичефлагом `--enable-trading`, плюс `dry_run: true` на каждом tool по умолчанию.
- **Параллельный CLI** — каждый MCP tool доступен как `tt <area> <command>` (паттерн `tv` из референса). Удобно для отладки и pipe-friendly интеграций.
- **Compact-by-default output** — `summary: true`, `study_filter`, capping (см. контекст-патёрны референса).

## Phases & Tasks

### Phase 0 — Bootstrap (1-2 дня)

**Goal**: репозиторий, скелет, CI, документация-каркас.

- [ ] **T0.1** Создать структуру решения: `TigerTradeMcp.sln`, проекты:
  - `src/TigerTradeMcp.Server` — entrypoint (Console, AOT-ready)
  - `src/TigerTradeMcp.Core` — domain types, interfaces, response envelopes
  - `src/TigerTradeMcp.TigerLink` — Tiger API WS-клиент
  - `src/TigerTradeMcp.Cli` — CLI router
  - `tests/TigerTradeMcp.Tests` — xUnit
- [ ] **T0.2** Подключить NuGet: `ModelContextProtocol` (official SDK), `System.Net.WebSockets.Client`, `Spectre.Console.Cli`, `Microsoft.Extensions.Hosting`, `Serilog.Sinks.File`.
- [ ] **T0.3** Базовая конфигурация: `appsettings.json` (TigerLink.Url, Broker.Type, Trading.Enabled).
- [ ] **T0.4** Структурный логгер (Serilog → файл `logs/tigertrade-mcp-*.log` + stderr).
- [ ] **T0.5** Documentation skeleton: переписать корневой `README.md`, добавить `EULA-DISCLAIMER.md`, `SECURITY.md`, `CONTRIBUTING.md` по образцу референса.
- [ ] **T0.6** GitHub Actions: `dotnet test` + `dotnet publish` smoke.
- [ ] **T0.7** Корневой `CLAUDE.md` с decision tree (пустые секции, заполнять по мере реализации tools) — см. `.references/tradingview-mcp/CLAUDE.md` как образец.
- [ ] **T0.8** `.gitignore` (уже есть как открытый файл — проверить покрытие `bin/`, `obj/`, `logs/`).

### Phase 1 — MVP: Tiger API + MCP skeleton (3-5 дней)

**Goal**: рабочий MCP-сервер с 5-7 tools поверх Tiger API ws@7819. Полностью read-only, без брокеров.

- [ ] **T1.1** `TigerLink.WebSocketClient`: подключение к `ws://localhost:7819`, retry-логика (exponential backoff, 5 попыток, base 500ms — копируем `connection.js:65-87` из референса), liveness-check.
- [ ] **T1.2** `Core/SafeJson.cs`: эквивалент `safeString` из `.references/tradingview-mcp/src/connection.js:36-38` для безопасной сериализации (через `JsonSerializer.Serialize`).
- [ ] **T1.3** `Core/ToolResponse.cs`: envelope `{ content: [{ type: "text", text: <json> }], isError? }` — копируем `jsonResult()` из референса.
- [ ] **T1.4** MCP tools (минимальный набор):
  - `tiger_health_check` — статус подключения, версия терминала (через WS handshake)
  - `tiger_set_link_symbol` — обёртка над документированной командой `setLinkSymbol(exchange, market, symbol, linkGroup)`
  - `tiger_get_config` — возвращает текущий конфиг сервера (без секретов)
- [ ] **T1.5** CLI обёртка: `tt health`, `tt symbol BINANCE SPOT BTCUSDT A`, `tt config`.
- [ ] **T1.6** Tests: e2e (требует запущенный TigerTrade) + unit (mock WS).
- [ ] **T1.7** Заполнить decision tree в `CLAUDE.md` для текущих tools.
- [ ] **T1.8** Verification: запустить TigerTrade, добавить сервер в `~/.claude/.mcp.json`, проверить через Claude Code: «Switch chart to BTCUSDT on Binance Spot in link group A».

### Phase 2 — Broker Connector SPI + первый коннектор (5-7 дней)

**Goal**: интерфейс для брокеров и одна реальная реализация (выбирается по итогам пользовательского исследования). Read-only.

- [ ] **T2.1** `Core/Abstractions/IBrokerConnector.cs`:

  ```csharp
  Task<Quote> GetQuoteAsync(string symbol);
  IAsyncEnumerable<Bar> GetOhlcvAsync(string symbol, Timeframe tf, int count);
  Task<IReadOnlyList<Position>> ListPositionsAsync();
  Task<IReadOnlyList<Order>> ListOrdersAsync(OrderStatusFilter filter);
  Task<Account> GetAccountAsync();
  ```

- [ ] **T2.2** `Core/Abstractions/IBrokerFactory.cs` + DI-регистрация по `appsettings.json:Broker.Type`.
- [ ] **T2.3** `BrokerMock` — in-memory заглушка для тестов и dry-run сценариев.
- [ ] **T2.4** **ОДИН** реальный коннектор (приоритет — Quik через QUIK#, как наиболее вероятный для русскоязычного пользователя; при подтверждении другого брокера от пользователя — заменить):
  - `src/TigerTradeMcp.Brokers.Quik/QuikConnector.cs`
  - Маппинг типов Quik → доменные модели в Core
- [ ] **T2.5** MCP tools (с префиксом `broker_`):
  - `broker_get_quote { symbol }`
  - `broker_get_ohlcv { symbol, timeframe, count, summary }` — `summary: true` по умолчанию даёт ~500B вместо ~8KB (паттерн из `data_get_ohlcv` референса)
  - `broker_list_positions`
  - `broker_list_orders { status }`
  - `broker_get_account`
- [ ] **T2.6** CLI: `tt quote SBER`, `tt ohlcv SBER --tf 5m --summary`, `tt positions`, `tt orders --status active`, `tt account`.
- [ ] **T2.7** Capping & dedupe (по образцу референса):
  - OHLCV cap: 500 bars max, default 100
  - Positions dedupe by symbol
  - Orders cap: 50 most recent per request
- [ ] **T2.8** Tests с `BrokerMock` (юнит) + интеграционные за `[BrokerIntegrationFact]`.
- [ ] **T2.9** Обновить decision tree в `CLAUDE.md`.

### Phase 3 — Trading (write-операции с safety) (3-5 дней)

**Goal**: возможность ставить/отменять/изменять ордера с тройной защитой.

- [ ] **T3.1** Расширить `IBrokerConnector`:

  ```csharp
  Task<OrderId> PlaceOrderAsync(OrderRequest req, bool dryRun);
  Task CancelOrderAsync(OrderId id, bool dryRun);
  Task ModifyOrderAsync(OrderId id, OrderModification mod, bool dryRun);
  ```

- [ ] **T3.2** **Safety layer**:
  - Feature flag `--enable-trading` в CLI args сервера. Без него write-tools **не регистрируются** в MCP вообще.
  - На каждом write-tool обязательный параметр `dry_run: bool` (default `true`).
  - На каждом write-tool обязательный параметр `confirm: string` (с конкретным текстом подтверждения, иначе ошибка). Это страхует от случайных вызовов из LLM-цепочек.
  - Audit log в отдельный файл `logs/trading-audit-*.jsonl`, каждая попытка (даже dry-run) — отдельная строка.
- [ ] **T3.3** MCP tools:
  - `broker_place_order { symbol, side, qty, type, price?, stop_price?, tif, dry_run=true, confirm }`
  - `broker_cancel_order { order_id, dry_run=true, confirm }`
  - `broker_modify_order { order_id, modifications, dry_run=true, confirm }`
- [ ] **T3.4** CLI: `tt order place --symbol SBER --side buy --qty 10 --type limit --price 250 --dry-run`. Live-режим требует `--confirm` явно.
- [ ] **T3.5** Tests: безопасность фичефлага (write-tools не появляются без `--enable-trading`), dry-run не вызывает реальный API, audit log пишется.
- [ ] **T3.6** Доп. документ `TRADING-SAFETY.md` — чек-лист пользователя перед включением `--enable-trading`.

### Phase 4 — Streaming, multi-symbol, расширения чарта (5-7 дней)

**Goal**: подняться до feature-parity с tradingview-mcp в части, релевантной TigerTrade.

- [ ] **T4.1** Poll-and-diff streaming engine (`Core/Streaming/PollStream.cs`), эквивалент `src/core/stream.js` из референса. JSONL вывод в stdout.
- [ ] **T4.2** Streams: `stream_quote`, `stream_positions`, `stream_orders`, `stream_account`. Интервалы 300-1000ms, дедупликация по `JsonSerializer.Serialize`.
- [ ] **T4.3** `batch_run { symbols[], action }` — multi-symbol сканирование (паттерн `batch.js` из референса).
- [ ] **T4.4** Skills (markdown под `skills/`):
  - `skills/portfolio-review/` — get_account + positions + orders + сводка
  - `skills/multi-symbol-scan/` — batch_run по watchlist
  - `skills/order-flow-check/` — Tiger UI navigation через `setLinkSymbol` + чтение ohlcv
- [ ] **T4.5** Agent: `agents/risk-analyst.md` — специализированный агент анализа позиций и риска.

### Phase 5 — Опционально: TigerTrade Indicator SDK + IPC (отложено)

**Goal**: запускается только при подтверждённой потребности в данных, которых нет у брокера (cluster, footprint, T&S, тиковые данные).

- [ ] **T5.1** Прототип индикатора: `TigerTradeMcp.Indicator` — класс `McpDataExporter : IndicatorBase` с атрибутом `[Indicator(...)]`. Подписывается на bars/cluster/T&S, шлёт в named pipe `\\.\pipe\tigertrade-mcp`.
- [ ] **T5.2** Сборка под структуру `%USERPROFILE%\Documents\TigerTrade\Indicators\`. Прочитать `.references/tradingview-mcp/scripts/pine_pull.js` как идеологический образец.
- [ ] **T5.3** В сервере — `Core/Connectors/IndicatorPipeReader.cs`: подписка на пайп, парсинг сообщений, экспозиция в tools `chart_get_cluster`, `chart_get_t_and_s`, `chart_get_footprint`.
- [ ] **T5.4** Двунаправленность (опционально): сервер шлёт оверлеи в индикатор (горизонтальные линии, метки), индикатор рендерит на чарте через `TigerTrade.Dx`. Аналог `draw_shape` из референса.
- [ ] **T5.5** Версионирование контракта пайпа (semver в handshake).

## Critical files to create (Phase 0-1)

| Файл | Назначение |
|---|---|
| `TigerTradeMcp.sln` | Решение |
| `src/TigerTradeMcp.Server/Program.cs` | Entry point + DI + MCP host |
| `src/TigerTradeMcp.Server/Tools/TigerTools.cs` | MCP tools для Tiger API |
| `src/TigerTradeMcp.Core/ToolResponse.cs` | JSON envelope |
| `src/TigerTradeMcp.Core/SafeJson.cs` | Безопасная сериализация |
| `src/TigerTradeMcp.Core/Abstractions/IBrokerConnector.cs` | SPI (заготовка) |
| `src/TigerTradeMcp.TigerLink/WebSocketClient.cs` | WS-клиент с retry |
| `src/TigerTradeMcp.Cli/Program.cs` | CLI router |
| `tests/TigerTradeMcp.Tests/TigerLinkTests.cs` | unit + e2e |
| `README.md` | Перепись из 3-строчной заглушки |
| `EULA-DISCLAIMER.md` | Юридический disclaimer |
| `SECURITY.md` | Локальность, отсутствие сети наружу |
| `CLAUDE.md` | Decision tree для агентов |
| `appsettings.json` | Конфигурация |
| `.github/workflows/ci.yml` | CI |

## Reused references

- `.references/tradingview-mcp/src/connection.js:36-38` — `safeString` → портируется как `SafeJson.Escape`
- `.references/tradingview-mcp/src/connection.js:44-48` — `requireFinite` → портируется один-в-один
- `.references/tradingview-mcp/src/connection.js:50-87` — retry с экспоненциальной backoff → паттерн для `WebSocketClient.ConnectAsync`
- `.references/tradingview-mcp/src/core/stream.js` — poll-and-diff loop → паттерн для `PollStream<T>` в Phase 4
- `.references/tradingview-mcp/src/server.js` — структура регистрации tools и stdio-транспорт → шаблон для `Program.cs`
- `.references/tradingview-mcp/CLAUDE.md` — структура decision tree, output size table → шаблон для нашего `CLAUDE.md`
- `.references/tradingview-mcp/README.md` секции Disclaimer / How It Works → шаблон для `EULA-DISCLAIMER.md` (заменить «CDP» на «Tiger API WebSocket» и «C# Indicator SDK»)
- `.references/tradingview-mcp/RESEARCH.md` — открытые вопросы (context, granularity, human-in-the-loop) → переиспользовать как обоснование архитектуры в нашем `RESEARCH.md`

## Verification (как проверить, что работает)

**После Phase 1**:

1. `dotnet test` — все юнит-тесты зелёные.
2. Запустить TigerTrade, в настройках включить Local Signal Server (порт 7819).
3. `dotnet run --project src/TigerTradeMcp.Server` → MCP-клиент видит tools.
4. CLI smoke: `tt health` → JSON со статусом. `tt symbol BINANCE SPOT BTCUSDT A` → в терминале TigerTrade переключился символ в link-group A.
5. Из Claude Code: «Verify TigerTrade connection with tiger_health_check» → возвращает success.
6. Из Claude Code: «Switch chart to ETHUSDT on Binance Spot, link group B» → выполняется без ошибок.

**После Phase 2**:

1. С запущенным выбранным брокером: `tt quote SBER` → текущая цена.
2. `tt ohlcv SBER --tf 5m --summary` → ~500 байт компактного вывода.
3. `tt positions` → список открытых позиций.
4. Claude: «Какие у меня сейчас открытые позиции и каков нереализованный P&L?» → корректный ответ.

**После Phase 3**:

1. Без `--enable-trading` write-tools отсутствуют в MCP-инвентаре.
2. С `--enable-trading` + `dry_run: true` (default) — попытка `broker_place_order` пишет в audit log, но не отправляет в брокера.
3. С `dry_run: false` + правильным `confirm` — реальный ордер уходит.

## Risks & Mitigations

| Риск | Митигация |
|---|---|
| Tiger API ws@7819 в новых версиях TigerTrade сломан/изменён | Версионная проверка через handshake, graceful degrade, тесты с реальным терминалом в CI manual job |
| LLM случайно выставит реальный ордер | `--enable-trading` фичефлаг + `dry_run: true` default + обязательный `confirm` + audit log |
| EULA 4.3.1 запрещает reverse engineering | Идём только через документированные SDK (Tiger API ws + Indicator SDK + внешние брокерские API). `TigerTrade.Api.dll` не трогаем |
| Выбор брокера ещё не определён, риск переделки коннектора | SPI-first: `IBrokerConnector` пишется до конкретной реализации. Смена брокера = новый класс, не переписывание ядра |
| Indicator SDK добавит сложность сборки | Отложен в Phase 5; MVP работает без него |
| Output может разрастаться и съедать context LLM | Compact-by-default (`summary: true`, capping 50 labels, dedup) — паттерны из референса портируем в Phase 2 |

## Open questions (резюме для дальнейших уточнений)

1. После Phase 1 — какой брокер первой очереди (Quik / Transaq / MT5 / Rithmic)?
2. Phase 5 (Indicator SDK) — нужны ли данные cluster/footprint/T&S? Если да — переносим выше, если нет — возможно не делаем вовсе.
3. CLI distribution — `dotnet tool install -g` или standalone EXE?
