---
last_mapped_commit: 9816477729158db1007480b98cb16f4d49eb5cfb
last_mapped_at: 2026-10-09
---
# Testing Patterns

**Analysis Date:** 2026-10-09

## Test Framework

**Unit test framework: none.** There is no xUnit/NUnit/MSTest reference in any `.csproj`, no `*.test.*`/`*Test*.cs` file, and no CI pipeline (no `.github/`, no YAML files anywhere in the repo). Do not add one without discussing it — the project's automated strategy is purpose-built.

**Automated harness — SmokeProbe:**
- Location: `src/OriCoopDedicatedServer/SmokeProbe/` (`SmokeProbe.csproj`, `Program.cs`, ~1260 lines)
- Type: `net8.0` console exe (`OutputType Exe`), **BCL-only, zero NuGet references** ("harness headless BCL-only (sem NuGet)" — `Program.cs` header comment)
- Strategy: spawns the real dedicated server with `dotnet run` and drives **real UDP datagrams over loopback**, asserting protocol behavior on the wire. Not in `OriCoopPlus.sln`; run directly by project path.

**Assertion style:** no assertion library. Each `TestX` returns `bool`, prints `PASS <case> (...)` / `FAIL <case> (...)` lines; `FailUnless(bool ok, string reason)` throws `InvalidOperationException(reason)` on failure (`Program.cs`, line 216).

**Run Commands:**

```powershell

# Full automated suite: spawns a fresh server on the test port, runs every case, tears it down

dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7779 --test all

# Single mode against an already-running server

dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7777 --test handshake

# Comma-separated modes

dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7777 --test timeout,token

# Modes that manage their own server (do NOT use against a running server on that port)

dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7779 --test full
dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7779 --test game
```

**Output contract (exit codes):**
- `SMOKE_OK` printed → exit `0`
- `SMOKE_FAIL: <reason>` (or `SMOKE_FAIL: <ExceptionType> <message>` from the top-level catch) → exit `1`; on failure the tail (30 lines) of the server's captured stdout/stderr is printed for diagnosis.
- Isolated modes print their token first: `HANDSHAKE_OK`, `RELAY_OK`, `EVENT_OK`, `PING_OK`, `RELIABLE_OK`, `TIMEOUT_OK`, `TOKEN_OK`, `FULL_OK`, `GAME_OK`, then `SMOKE_OK`.
- `--test` accepts: `all|handshake|relay|event|ping|reliable|timeout|token|full|game`. `all`, `full`, and `game` require a fresh server (they assert deterministic IDs and spawn/restore config themselves); the rest attach to a running `--port`.

**Build verification (run before any suite):**

```powershell
dotnet build .\src\OriCoopPlus\OriCoopPlus.sln --configuration Release
dotnet build .\src\OriCoopDedicatedServer\OriCoopDedicatedServer\OriCoopDedicatedServer.csproj --configuration Release
powershell -ExecutionPolicy Bypass -File .\src\OriCoopPlus\OriCoopBepInEx\build.ps1
```

Expected outputs: `src\OriCoopPlus\OriCoopBepInEx\bin\Release\OriCoopBepInEx.dll` and `src\OriCoopDedicatedServer\OriCoopDedicatedServer\bin\Release\net8.0\OriCoopDedicatedServer.exe` (see `docs/operations.md`).

## Test File Organization

**Location:** a single harness project separate from production code — `src/OriCoopDedicatedServer/SmokeProbe/Program.cs`. There are no co-located test files and no `tests/` directory.

**Naming:**
- Test methods: `Test<Subject>` returning `bool` — `TestHandshake`, `TestRelay`, `TestPlayerEvent`, `TestPing`, `TestReliable`, `TestToken`, `TestTimeout`, `TestServerFull`, `TestGame`, `TestChatRules`, `TestGamePhase1/2`.
- Helpers: verb-first — `HelloConfirm`, `ExpectReject`, `ExpectPong`, `ExpectSysAck`, `SendAck`, `SendDisconnect`, `DrainAndAck`, `BuildEnvelope`, `TryParseHeader`, `ReadI32/ReadU32/ReadI64`, `Tail`, `SpawnServer`, `KillServer`, `WaitForServer`.
- Payload builders: `BuildHelloPayload`, `BuildPlayerStateBody`, `BuildPlayerEventBody`, `BuildChatPayload`, `TicksBytes`, `WriteGameConfig`.

**Structure:**

```
src/OriCoopDedicatedServer/SmokeProbe/
├── SmokeProbe.csproj        # net8.0, no PackageReference
└── Program.cs               # internal static class Program; Main -> RunAll | RunSingle
```

## Test Structure

**Suite organization** — `Main` parses `--port`/`--test`, then `RunAll(port)` spawns one fresh server (`--net2 --auto --port P --max-players 10`, note `--net2` is a no-op flag kept for script compatibility) and runs the ordered checklist, and `RunSingle(test, port)` fans out comma-separated modes:

```csharp
private static int RunAll(int port)
{
    string serverProject = FindServerProject();
    var serverLog = new StringBuilder();
    using (var server = SpawnServer(serverProject, port, 10, serverLog))
    {
        try
        {
            WaitForServer(port, TimeSpan.FromSeconds(120));
            FailUnless(TestInvalidMagic(port), "invalid-magic sem Reject com mensagem");
            FailUnless(TestHandshake(port, 1), "handshake falhou");
            FailUnless(TestRelay(port), "relay falhou");
            FailUnless(TestPlayerEvent(port), "player-event relay falhou");
            FailUnless(TestPing(port), "ping falhou");
            FailUnless(TestReliable(port), "reliable falhou");
            FailUnless(TestTestToken(port), ...);   // token
            FailUnless(TestTimeout(port), "timeout falhou");
            FailUnless(TestServerFull(serverProject, port + 11), "server-full falhou");
            FailUnless(TestGame(serverProject, port + 21), "game falhou");
            Console.WriteLine("SMOKE_OK");
            return 0;
        }
        catch (Exception ex) { /* prints SMOKE_FAIL + server log tail */ return 1; }
        finally { KillServer(server); }
    }
}
```

**Case pattern:** two-client wire scenario with deadline polling — the canonical shape every new case should copy (`TestRelay`):

```csharp
using (var clientA = NewClient())
using (var clientB = NewClient())
{
    var server = new IPEndPoint(IPAddress.Loopback, port);
    uint seqA = 0, seqB = 0;
    if (!HelloConfirm(clientA, server, "Relay_A", ref seqA, out int idA, out uint tokenA) || idA <= 0) { ... FAIL ... return false; }
    if (!HelloConfirm(clientB, server, "Relay_B", ref seqB, out int idB, out uint tokenB) || idB <= 0 || idB == idA) { ... FAIL ... return false; }

    byte[] body = BuildPlayerStateBody();
    uint stateSeq = ++seqA;
    byte[] state = BuildEnvelope(0, stateSeq, idA, tokenA, PlayerStateId, body);
    clientA.Send(state, state.Length, server);

    var remote = new IPEndPoint(IPAddress.Any, 0);
    DateTime deadline = DateTime.UtcNow.AddSeconds(5);
    bool relayOk = false;
    while (DateTime.UtcNow < deadline && !relayOk)
    {
        try
        {
            byte[] reply = clientB.Receive(ref remote);
            if (TryParseHeader(reply, out int packetId, out uint rseq, out int rclient, out _, out byte[] rbody)
                && packetId == PlayerStateId && rclient == idA && rseq == stateSeq && BytesEqual(rbody, body))
            { relayOk = true; }
        }
        catch (SocketException) { }   // polling timeout, not an error
    }
    ...
}
```

**Patterns:**
- Setup: `HelloConfirm(client, server, nick, ref seq, out id, out token)` performs the 3-way handshake (Hello→Welcome→Confirm) and returns assigned `clientId` + session `token`.
- Every case drains its slots with `SendDisconnect(...)` at the end so `--test all` does not accumulate sessions up to the cap.
- Deadline-based waits everywhere (`DateTime.UtcNow.AddSeconds(3|5|16)`); `SocketException` swallowed inside the loop and re-checked against the deadline.

## Mocking

**Framework: none — and by design.** There are no fakes, stubs, test doubles, or injection seams for tests. The harness exercises the **real server process and real sockets**:

- `SpawnServer(serverProject, port, maxPlayers, log)` starts `dotnet run --project <csproj> --configuration Release -- --net2 --auto --port P --max-players N` with `UseShellExecute = false`, redirected stdout/stderr/stderr captured into a `StringBuilder` under a lock via `OutputDataReceived`/`ErrorDataReceived` + `BeginOutputReadLine`.
- `WaitForServer(port, timeout)` polls with a garbage datagram until the server replies `Reject 106` — proof the core is up (readiness gate).
- **What is deliberately NOT mocked:** UDP transport, session/token validation, the envelope codec, retry/SysAck logic, config persistence (`serverconfig.json` is read/written by the real `ConfigStore`), the command registry. Byte-level assertions (`BytesEqual`) would be meaningless against a fake.
- **What is controlled instead:** timeouts and expectations (`ExpectSysAck(client, seq, 800)` — passes when no SysAck arrives, used to assert unreliable packets never create pending retries), endpoint identity (`NewClient()` per scenario, one port per logical client), and config side effects.

## Fixtures and Factories

**Test data = byte builders, not object factories.** The wire contract is the fixture:

- `BuildPlayerStateBody()` / `BuildPlayerEventBody()` — fixed known bodies. `TestPlayerEvent` asserts the contract shape inline: `body.Length != 37 || ReadI32(body, 0) != PlayerEventId` → FAIL ("corpo fora do contrato 37B/marcador 19").
- `BuildChatPayload(string)` — e.g. `new string('y', 400)` to exercise the 350-char truncation; `"a<b>c>d"` to exercise `<>` sanitization; nick `"Evil<Nick>"` to exercise nick sanitization.
- `TicksBytes(l)` / `DateTime.UtcNow.Ticks` — ping payloads with echo-round-trip identity assertions.
- `WriteGameConfig(path, tp, ab, story, world, doors, names, cc, es)` — writes the real `serverconfig.json` (hand-built JSON string, ASCII) that the server loads, proving persistence `AllowTeleport=false` across restarts.

**Location:** all fixtures are private methods inside `Program.cs`. There is no shared fixture directory; extend in place when adding a case.

**Restore discipline:** `TestGame` snapshots `serverconfig.json` bytes, and its `finally` block restores or deletes the file — any new test that mutates persistent state must do the same.

## Coverage

**Requirements: none enforced — no coverage tooling exists** (no `coverlet`, no `--collect` runs, no percentage gate). Quality is enforced by breadth of the wire-level checklist instead: the current suite covers framing, handshake, relay identity/fidelity, event domain isolation, ping RTT, reliable retry/SysAck, token/endpoint security, session timeout sweeper, capacity limits (SERVER_FULL), chat rules, and game-layer config/teleport/dummy flows.

**Evidence trail:** results are recorded manually in `docs/operations.md` ("Registro de Validacao..." sections with `SMOKE_OK` / `GAME_OK` / `PERSIST_OK` outcomes and DLL byte sizes), and UAT checkpoints are committed with `test(03):` prefixed commits (e.g. `9816477 test(03): catalog re-test errors - delivery ok, clip unknown, no render`).

## Test Types

**Integration / wire smoke (automated, this is the primary safety net):**
- Scope: the whole server process end-to-end over UDP loopback — envelope framing (`0x4F43`/v2 24B header), handshake, relay, reliability, sessions, chat, game layer.
- Negative cases included (robustness is first-class): invalid magic (8B garbage → `Reject` with reason, no session created), `Confirm` with wrong token → rejected, `Confirm` on ghost session id → rejected, `Ping` from swapped endpoint → discarded, stale/duplicate `seq` → dropped (drop-old, wrap-safe `(int)(seq-last) > 0` gate), unreliable packet must NOT emit SysAck, server full → `Reject 106 SERVER_FULL` without session, silent client → DISCONNECT within ~10s (16s test budget), help response must not leak to other clients.
- Fidelity assertions: relay preserves `clientId` + `seq` + body **byte-identical** (`BytesEqual`), no echo to sender, ID allocation 1 then 2 and never 0/999, ping echo < 1000 ms, retry delivers ≥ 2 copies at ~250 ms.

**In-game UAT (manual, documented):**
- `docs/operations.md` — "Checklist de teste manual" (11 steps: build, install, server banner, connect 1–2 clients, name/color/disconnect/reconnect, per-option toggle tests, `/dummy`, teleport via `T` and `/tp`, HUD) plus LAN test procedure and a render/animation validation protocol (frustum extremes, cutscene locks, animation transition sequences with `Dropped: 0` expectation).
- `docs/anim-test-battery.md` — T0–T4 battery for the animation sync rework, one block per change.
- Phase pilot checklists — e.g. Spirit Flame C1–C6 (`docs/operations.md`, "Piloto Spirit Flame 03-03"): A-shoots-B-sees, symmetry, no-echo, spam-under-movement, unknown-type holds pose, disconnect/reconnect; each round's result is recorded as "a confirmar" until executed.
- **Pre-gate rule:** the automated suite must print `SMOKE_OK` before a pilot is taken into the game — "sem ele verde, nao levar o piloto para o jogo" (`docs/operations.md`).

## Common Patterns

**Async/deadline polling** (used everywhere instead of blocking waits):

```csharp
DateTime deadline = DateTime.UtcNow.AddSeconds(5);
while (DateTime.UtcNow < deadline)
{
    try { byte[] reply = client.Receive(ref remote); /* assert */ }
    catch (SocketException) { /* timeout slice; re-check deadline */ }
}
```

**Error/negative testing:**

```csharp
// Wrong token must be rejected, not answered
byte[] badConfirm = BuildEnvelope(0, ++seqA, idA, tokenA + 1, MsgConfirm, Array.Empty<byte>());
clientA.Send(badConfirm, badConfirm.Length, server);
if (!ExpectReject(clientA, 3000)) { Console.WriteLine("FAIL handshake (Confirm com token errado nao foi recusado)"); return false; }

// Asserting ABSENCE: unreliable packets must never generate SysAck (short window)
if (ExpectSysAck(clientA, probeSeq, 800)) { Console.WriteLine("FAIL player-event (packet-19 gerou SysAck/pendencia)"); return false; }
```

**Process lifecycle hygiene:**

```csharp
using (var server = SpawnServer(serverProject, port, 10, serverLog))
{
    try { /* cases */ }
    catch (Exception ex) { Console.WriteLine("SMOKE_FAIL: " + ex.GetType().Name + " " + ex.Message); /* + log tail */ }
    finally { KillServer(server); }   // Kill + WaitForExit(5000) inside try/catch
}
```

**Where to add new automated coverage:**
1. Add `private static bool TestNewThing(int port)` in `src/OriCoopDedicatedServer/SmokeProbe/Program.cs` following the A/B client + deadline pattern; reuse `BuildEnvelope`/`TryParseHeader`/`HelloConfirm` rather than new helpers when possible.
2. Add a `FailUnless(TestNewThing(port), "motivo");` line in `RunAll` (respect ordering — cases share one server), and a mode branch + `<TOKEN>_OK` line in `RunSingle`.
3. If the case needs its own server or config state, copy `TestServerFull`/`TestGame`'s spawn/restore-in-`finally` pattern.
4. Register the run (command + result) in `docs/operations.md` per the `AGENTS.md` rule; mark in-game-dependent confirmation as "a confirmar".

**What is not automated** (expect manual UAT instead): client plugin behavior (Harmony patches, puppet rendering, animation resolution, native UI), anything requiring the Unity runtime or the game install, and two-client visual checks. These live as checklists in `docs/operations.md` and `docs/anim-test-battery.md`.

---

*Testing analysis: 2026-10-09*
