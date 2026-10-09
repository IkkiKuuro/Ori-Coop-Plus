---
last_mapped_commit: 9816477729158db1007480b98cb16f4d49eb5cfb
last_mapped_at: 2026-10-09
---
# Coding Conventions

**Analysis Date:** 2026-10-09

## Language Dialects (know your target before writing)

The repo is C# with **two dialects that must not be mixed**:

- **Client plugin** (`src/OriCoopPlus/OriCoopBepInEx/`): compiles against `net35` with `LangVersion 7.3` in `OriCoopBepInEx.csproj`, but the real limit is **C# 5 semantics** — `build.ps1` invokes the .NET Framework 4 `csc.exe` with `/langversion:default`. Forbidden in this project: string interpolation `$""`, null-conditional `?.`, `Action<...>` with more than 4 parameters, `Object.Instantiate` without explicit cast. Preferred: explicit types over `var`, `get { return ...; }` property bodies.
- **Server + probe** (`src/OriCoopDedicatedServer/OriCoopDedicatedServer/`, `src/OriCoopDedicatedServer/SmokeProbe/`): `net8.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`. Modern C# is allowed here only.
- **Shared contract** (`src/OriCoopPlus/OriCoopShared/*.cs`): compiles into **both** projects via `<Compile Include="..\OriCoopShared\*.cs" Link="Shared\%(Filename)%(Extension)" />`. Write it as C# 5: consts, enums, plain classes (see `NetProtocol.cs` header comment: "Apenas consts: compila em C# 5 (.NET 3.5, cliente Unity) e em .NET 8 (servidor)").

Scripts are PowerShell (`src/OriCoopPlus/OriCoopBepInEx/build.ps1`, `scripts/*.ps1`). Comments and log messages are predominantly Portuguese; keep that language for consistency.

## Naming Patterns

**Files:**
- One public type per file; file name equals the type name: `ServerBoot.cs`, `SessionManager.cs`, `NetworkService.cs`, `AckTracker.cs`.
- Role suffixes are meaningful — use them for new files: `*Patch.cs` (Harmony patches, e.g. `Patches/SpiritFlamePatch.cs`), `*Manager.cs` (`Client/RemotePlayerManager.cs`), `*Store.cs` (`Net/Game/ConfigStore.cs`), `*Factory.cs` (`Client/RemotePuppetFactory.cs`), `*Registry.cs` (`Net/Game/Commands/CommandRegistry.cs`), `*Helper.cs` (`UI/NativeUIHelper.cs`), `*Protocol.cs` (`OriCoopShared/NetProtocol.cs`), `*Data.cs` (`Events/SpiritFlameEventData.cs`), `*Core.cs` (`Events/PlayerEventCore.cs`).

**Types:**
- PascalCase classes/structs/enums; interfaces take the `I` prefix: `INetworkService`, `ILogger`, `IGameTransport`, `IServerContext`, `IPlayerStateSink`.
- Default to `public sealed class` for concrete classes (see `ServerBoot`, `GameHandlers`, `NetworkService`, `FileConsoleLogger`). Use `public static class` for stateless cores (`PlayerEventCore`, `NetProtocol`).
- Named delegates instead of bare `Action<,>` where the signature is a public contract: `public delegate void PlayerEventHandler(int senderId, SpiritFlameEventData data);` and `ConfigSyncHandler` on `INetworkService`.

**Members:**
- Methods, properties, public consts: PascalCase (`HandleHello`, `MaxRetries`, `FlagReliable`, `MsgHello`).
- Private fields: `_camelCase` with underscore prefix, `private readonly` where possible: `_sessions`, `_sync`, `_lastSentPos`, `_pending`.
- Local variables and parameters: camelCase (`serverVer`, `rejectReason`, `nick`).
- Private consts: PascalCase (`ChatPacketId`, `PlayerStateId`, `DummyId` in `Program.cs`/`NetworkService.cs`/`GameHandlers.cs`).

**Enums — two styles, both intentional:**
- Wire-protocol enums: `UPPER_CASE` members (`PacketType.PLAYER_STATE = 18`, `POSITION = 1`).
- Domain/diagnostic enums: PascalCase members (`ServerLogLevel.Warning`, `PlayerEventKind.SpiritFlame`).
- Removed protocol IDs stay in the enum as comments marked `// LEGACY_REMOVED` / "nunca reutilizar" (`PacketType.cs`) — never recycle a packet ID.

**Namespaces:**
- Server code uses file-scoped namespaces: `namespace OriCoopDedicatedServer;`
- Client and shared code use block-scoped namespaces: `namespace OriCoop { ... }`, `namespace OriCoopBepInEx.Networking { ... }`.
- Shared contract lives in root namespace `OriCoop` (`OriCoopShared/*.cs`); server namespaces are rooted at `OriCoopDedicatedServer`; client at `OriCoopBepInEx`.

## Code Style

**Formatting:**
- No linter/formatter config exists (no `.editorconfig`, `.prettierrc`, `eslint.config.*`, `StyleCop`, or `Directory.Build.props` in the repo). Conventions are enforced by reading neighbors, not by tooling.
- **Allman braces** — opening brace on its own line. This is universal across server, client, and shared code.
- **4-space indentation** is the norm. Exception: the server entry point `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs` uses tabs — match the file you edit.
- One blank line between members; keep lines under ~100 characters.
- Explicit types over `var` in client/shared code (C# 5 discipline); the server uses `var` in local flows (`Program.cs` argument parsing, `out var` in `TryParse*` calls).
- Class header comment or XML `<summary>` immediately after the opening brace explaining the type's single responsibility and any decision IDs (see `ServerBoot.cs`, `SessionManager.cs`, `GameHandlers.cs`).

**Linting:** none configured. Builds use default warning levels; recent history records "0 aviso, 0 erro" as the expected build outcome (`docs/operations.md`, 2026-10-05 entry). Treat new build warnings as failures.

## Import Organization

There is no enforced order or tooling; the observed pattern is:
1. `System*` usings
2. Project namespaces (`OriCoop`, `OriCoopDedicatedServer.*` / `OriCoopBepInEx.*`)
3. Third-party/game (`BepInEx`, `HarmonyLib`, `UnityEngine`) — in `Plugin/OriCoopPlugin.cs` these appear between System and project usings; match the neighboring file.

**Path aliases:** none exist. Shared code is not a separate assembly or ProjectReference — it is `<Compile Include="..\OriCoopShared\*.cs" Link="Shared\...">` glob-compiled into both projects. When adding a shared type, drop the file in `src/OriCoopPlus/OriCoopShared/` and it appears in both builds automatically; it must remain C# 5-compatible.

**Solution layout:** `src/OriCoopPlus/OriCoopPlus.sln` contains only `OriCoopBepInEx` + `OriCoopDedicatedServer`. `SmokeProbe` is built/run directly with `dotnet run --project` and is deliberately not in the solution.

## Error Handling

**Strategy:** guard-and-reject at boundaries with machine-usable reasons; throw only for programming/lifecycle errors; never swallow silently without a comment.

**Patterns, in order of preference:**
1. **Guard clauses at entry** — validate constructor/public-method arguments: `if (string.IsNullOrEmpty(host)) throw new ArgumentException("Network host is required.", "host");` and `if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("port");` (`Networking/NetworkService.cs`).
2. **Required dependencies throw**: `_log = log ?? throw new ArgumentNullException("log");` (`SessionManager.cs`, `ConfigStore.cs`, `UdpTransport.cs`, `GameHandlers.cs`).
3. **Lifecycle misuse throws** `InvalidOperationException`: `throw new InvalidOperationException("Transporte ja iniciado.");` (`Net/Transport/UdpTransport.cs`).
4. **Try-pattern with `out reason`** for anything driven by untrusted network input: `ValidatePacket(int clientId, uint token, IPEndPoint remote, out Session session, out string reason)`, `TryGet(int clientId, out Session session)`, `TryParseHello(..., out string rejectReason)` (`Net/Session/SessionManager.cs`). Callers log the reason and drop the datagram — never crash the receive loop on bad input.
5. **Defensive parsing** — length-check every field before reading it. Documented contract in `Net/Game/GameHandlers.cs`: "Leitura defensiva: checagem de length antes de cada campo (T-02-08, D-03); descarte logado."
6. **Empty `catch (Exception) { }` is allowed only for best-effort/cosmetic operations**, each accompanied by a comment: log-append failure (`Net/Diagnostics/ServerLogger.cs`), `SIO_UDP_CONNRESET` IOControl failure (`Net/Transport/UdpTransport.cs`, `Networking/NetworkService.cs`), `KillServer` teardown (`SmokeProbe/Program.cs`). Do not copy this pattern for real failures.
7. **`try/finally` for state restoration** — restore `ReceiveTimeout` (`SmokeProbe/Program.cs` `ExpectReject`), kill the spawned server (`RunAll`), backup/restore `serverconfig.json` (`TestGame`).
8. **`SocketException` inside receive loops is the expected polling timeout**, not an error — swallow it and re-check the deadline (all `TestX` methods in `SmokeProbe/Program.cs`, client receive loop in `Networking/NetworkService.cs`).
9. **No custom exception types exist**; do not introduce them for ordinary protocol failures.

## Logging

**Server (injectable abstraction, never `Console.WriteLine` in library code):**
- Interface `ILogger` with `void Log(ServerLogLevel level, string tag, string message)`; levels `Debug=0, Info=1, Warning=2, Error=3` (`Net/Diagnostics/ServerLogger.cs`).
- `FileConsoleLogger` writes `[HH:mm:ss][LEVEL][TAG] message` to console and `Logs/server.log` under a `lock`; default minimum level `Info` (`new FileConsoleLogger(path)`).
- Tags in use: `"SERVER"`, `"NET"`, `"CMD"` (see `Program.cs` `RunHost`). Use an existing tag when one fits; new tags are SCREAMING_SNAKE or short caps.
- Every logger is constructor-injected — passing `null` throws by design.

**Client (BepInEx console + Unity `LogOutput.log`):**
- Static wrappers only: `OriCoopPlugin.LogInfo/LogWarning/LogError` (`Plugin/OriCoopPlugin.cs`), each null-guards `Instance`.
- Structured log-line vocabulary — reuse these prefixes: `[EVENT] kind=... fase=enviado|recebido|aplicado` (`Events/PlayerEventCore.cs`), `[OBSERVABILITY][NET-METRICS]` with counters `EvRecv/EvApplied/EvDropped` (`Diagnostics/ReplicationObservability.cs`), `[ANIM]` for animation transitions.
- Telemetry goes through `ReplicationObservability.Record(line)` (ring buffer + counters), not raw log calls.

## Comments

**When to comment:** explain WHY, never WHAT. Required comment topics:
- Protocol/wire-format constraints (byte offsets, endianness, "nunca reutilizar" IDs) — see `OriCoopShared/NetProtocol.cs` header and `PacketType.cs`.
- Decision/plan traceability: cite the decision ID (`D-01`...`D-16`), task ID (`T-02-08`), or phase/plan (`fase 3, plano 02-03`) next to non-obvious code.
- Platform quirks: Windows ICMP `ConnectionReset`, `SIO_UDP_CONNRESET`, Unity 5.3.2f1 entrypoint issues.
- Rationale for structural rules: "sem estatico global mutavel" (`ServerBoot`, `GameHandlers`, `ConfigStore` class docs), sequence-domain separation (`_lastEventSeq` in `Networking/NetworkService.cs`).

**JSDoc/TSDoc:** XML `/// <summary>` doc comments on all public types, public members, and non-obvious private methods in server/shared/client public surface. Multi-line `<summary>` blocks carrying the design contract are the norm on core types (`Net/Session/SessionManager.cs`, `Net/Game/GameHandlers.cs`, `Events/PlayerEventCore.cs`).

**Repo-wide doc rule (from `AGENTS.md`):** any change to code, protocol, commands, config, build, install, or diagnostics must update the corresponding page in `docs/` before the task is done; unverified behavior must be marked **"a confirmar"** — never present intended behavior as observed. Manual/automated tests are registered in `docs/operations.md`.

## Function Design

**Size:** small, single-purpose; extract helpers rather than nest. Example vocabulary to reuse: `HelloConfirm`, `BuildEnvelope`, `TryParseHeader`, `ExpectSysAck`, `Tail` (`SmokeProbe/Program.cs`).

**Async:** server async methods take `CancellationToken` and end in `Async`: `StartAsync(ct)`, `StopAsync()`, `UnicastReliableAsync(..., ct)`, `RunReceiveLoopAsync(ct)` (`Net/Game/ServerBoot.cs`, `Net/Game/IGameTransport.cs`, `Net/Transport/UdpTransport.cs`). Never start a task without a token path.

**Events for decoupling:** subscribers register on `event Action<...>` contracts (`INetworkService.PlayerSnapshotReceived`, `ConfigStore.Changed`) — raise events **outside** locks (see `ConfigStore.Changed` doc comment) and dispatch to Unity's main thread via `OriCoopPlugin.EnqueueMainThread` (`Plugin/OriCoopPlugin.cs`).

**Parameters/returns:** out-params for multi-value protocol results (`Try*`, `Validate*`); structs/DTOs for wire payloads (`PlayerSnapshot`, `SpiritFlameEventData`, `Vector3Data`); relay of raw bytes allowed and preferred for opaque bodies ("o servidor reemite os bytes sem reconstrucao" — `Networking/NetworkService.cs`).

## Module Design

**Exports:** explicit small public surface; entry points are `internal static class Program` (server and probe). One responsibility per type; composition root is `ServerBoot` (wires `SessionManager` + `ConfigStore` + `NetServerHost` + `DummyBot` + `GameHandlers`).

**Barrel files:** none — and do not add them. Include only what each file uses.

**State discipline:**
- Server: no mutable global statics — instances and DI are the rule, stated in class doc comments (`ServerBoot.cs`, `GameHandlers.cs`, `ConfigStore.cs`). Thread safety via `ConcurrentDictionary` + `lock (_sync)` (each class has its own private `_sync` object) + bounded `Channel<ReceivedDatagram>` for the receive path (`UdpTransport.cs`).
- Client: a small number of statics is accepted for the plugin lifecycle (`OriCoopPlugin.Instance`, `PlayerEventCore` handlers dictionary, `s_transport`); new shared mutable state should be an instance member injected where possible.

**Serialization:**
- Wire: manual `BinaryWriter`/`BinaryReader` field-by-field at fixed offsets from `NetProtocol` (`OffMagic 0`, `OffSeq 4`, `OffPacketId 16`, ...), little-endian, 24-byte header — never change an offset without updating `NetProtocol.cs`, `docs/protocol.md`, and the SmokeProbe case in the same change.
- `serverconfig.json`: `System.Text.Json` (`Net/Game/ConfigStore.cs`); the 8 bools have a canonical order defined by `ConfigStore.FlagNames` — append-only, never reorder.

**Version discipline:** client and server must be the **same build** — breaking wire changes are declared "quebra one-way" in comments and `docs/protocol.md`, with no legacy fallback (see the network contract comments in `Networking/NetworkService.cs` and `PacketType.cs`).

---

*Convention analysis: 2026-10-09*
