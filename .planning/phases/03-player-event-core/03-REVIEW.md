---
phase: 03-player-event-core
reviewed: 2026-10-06T00:00:00Z
depth: deep
files_reviewed: 13
files_reviewed_list:
  - src/OriCoopPlus/OriCoopShared/PacketType.cs
  - src/OriCoopPlus/OriCoopShared/PlayerEventProtocol.cs
  - src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventKind.cs
  - src/OriCoopPlus/OriCoopBepInEx/Events/SpiritFlameEventData.cs
  - src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs
  - src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs
  - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
  - src/OriCoopPlus/OriCoopBepInEx/Domain/INetworkService.cs
  - src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs
  - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs
  - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs
  - src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs
  - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs
  - src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs
  - src/OriCoopDedicatedServer/SmokeProbe/Program.cs
findings:
  critical: 0
  warning: 8
  info: 3
  total: 11
status: issues-found
---

# Phase 03-player-event-core: Code Review Report

**Reviewed:** 2026-10-06
**Depth:** deep (per-file + cross-file: wire-codec consistency client/server/probe, relay auth/no-echo chain, thread boundaries)
**Files Reviewed:** 15 (13 src + server/probe; docs commits excluded per scope)
**Status:** issues-found (warnings only, advisory non-blocking)

## Summary

Reviewed the full phase-3 src diff (`410b73b..HEAD`: tracer `3f63fc7`, visuals `5d0e472`, wire hardening `5731005`, observability `83e9e60`, probe `dbe0330`). The security-critical core is **sound**: the frozen 37B layout is byte-consistent across all three codecs (client `SendPlayerEvent` sequential writes, server `BuildPlayerEventPayload`, probe `BuildPlayerEventBody`, offsets `kind=4/dir=5/origin=17/ts=29` = 4+1+24+8 = 37); sender identity is authenticated (`SessionManager.ValidatePacket`: clientId+token+fixed-endpoint, `NetServerHost` lines 211/231); no-echo holds on three independent layers (server `Targets` excludes sender, client `RaisePlayerEvent` suppresses own id, manager receive path contains zero publish calls); server relay re-emits original bytes verbatim with no `fromId` stamping; packet 19 stays out of `IsCriticalPacket`; unknown kinds fail closed end-to-end; the puppet path is visual-only by construction (MeshFilter/MeshRenderer/ParticleSystem/transient AudioSource only, positive keep-list strip); and the client code is C# 5-clean (no `$""`, no `?.`, no `nameof`, `Action` arities <= 2, `string.Format`, explicit casts). No BLOCKERs. All 8 warnings are real but non-ship-blocking; WR-02 and WR-05 are the two worth fixing first.

## Warnings

### WR-01: Unconditional per-shot LogInfo on the game thread (log spam under no-throttle fire)

**File:** `src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs:55`
**Issue:** The `[EVENT] SpiritFlame detected` line logs unconditionally on every local shot via `OriCoopPlugin.LogInfo`, doing string formatting + file I/O on the game thread per shot. Combined with the deliberate D-12 no-throttle discipline, hold-fire spam turns this into a log-flooding path (disk growth, frame cost). The parallel `fase=enviado` line in `PublishSpiritFlame` is already verbose-gated, so in verbose runs every shot also logs twice.
**Fix:** Gate it exactly like the other EVENT lines; keep unconditional only until the 2-client validation passes, then flip:
```csharp
if (OriCoopPlugin.IsAnimVerbose())
{
    OriCoopPlugin.LogInfo(string.Format("[EVENT] SpiritFlame detected dir=({0:F1},{1:F1},{2:F1}) origin=({3:F1},{4:F1},{5:F1})",
        direction.X, direction.Y, direction.Z, originData.X, originData.Y, originData.Z));
}
```

### WR-02: Event telemetry counters only move when verbose — D-12 flood observation is blind in default runs, plus double-count on handler exception

**File:** `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs:58-65,72-91`
**Issue:** `TrackPlayerEvent` (which is the only writer of `EvRecv/EvApplied/EvDropped`) is called exclusively inside `IsAnimVerbose()` gates for the sent path and the known-kind received path. In a default non-verbose production run, `EvRecv/EvApplied` stay at 0 no matter how hard shots are spammed — defeating the stated D-12 purpose of these counters (throttle calibration needs numbers, not feelings). Two compounding defects: (a) the handler-exception drop at line ~91 calls `TrackPlayerEvent(kind, false)` unconditionally, so dropped failures are counted but successful traffic is not — the ratio is meaningless; (b) in verbose mode a throwing handler produces TWO `TrackPlayerEvent` calls for one event (one `applied=true` "recebido" before the call, one `applied=false` after), and since `TrackPlayerEvent` unconditionally does `s_eventsReceivedCounter++`, one event yields `Received=2, Applied=1, Dropped=1`.
**Fix:** Always count, gate only the log/record lines; count received once at dispatch entry:
```csharp
public static void DispatchLocal(int senderId, byte kind, SpiritFlameEventData data)
{
    PlayerEventHandler handler;
    if (s_handlers.TryGetValue(kind, out handler) && handler != null)
    {
        ReplicationObservability.TrackPlayerEvent(kind, true); // received, always
        if (OriCoopPlugin.IsAnimVerbose()) { /* Record + LogInfo only */ }
        try { handler(senderId, data); }
        catch (Exception ex)
        {
            OriCoopPlugin.LogWarning("[EVENT] handler kind=" + kind + " falhou: " + ex.Message);
            ReplicationObservability.TrackPlayerEventDropped(kind); // needs a dropped-only entry, no Received++
        }
        return;
    }
    ReplicationObservability.TrackPlayerEvent(kind, false); // unknown-kind drop, always
    if (OriCoopPlugin.IsAnimVerbose()) { /* reason line only */ }
}
```
Same treatment for the publish (sent) path: count outside the verbose gate.

### WR-03: No finite-value validation on attacker-controlled direction/origin floats (NaN puppet poisoning)

**File:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs:75-93`, `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:560-565`
**Issue:** The server relays the 37B body as opaque bytes by design (correct), so any LAN peer with a valid session can send `NaN`/`Infinity` direction/origin in their own events and the victim applies them unchecked: `HandlePlayerEvent` builds `origin` and calls `puppet.SnapTo(origin)` on lazy-create (NaN position corrupts the puppet transform + Unity error spam until the next snapshot corrects it ~0.4 s later), `SpawnMuzzleParticle` positions a clone at NaN, and `SpawnFakeProjectile`/`FakeFlameMover.Launch` propagate NaN (`NaN < 0.0001f` is false, so the zero-guard does not catch NaN; `Normalize()` of NaN stays NaN). One crafted packet per ~0.4 s keeps a victim's puppet for that sender invisible/broken — transient but remotely triggerable griefing with zero skill. `float.IsNaN/IsInfinity` exist in .NET 2.0/3.5 and are C# 5-safe.
**Fix:** Validate at the top of `HandlePlayerEvent` (single choke point covers SnapTo, particle, projectile):
```csharp
if (float.IsNaN(data.Origin.X) || float.IsNaN(data.Origin.Y) || float.IsNaN(data.Origin.Z)
    || float.IsInfinity(data.Origin.X) || float.IsInfinity(data.Origin.Y) || float.IsInfinity(data.Origin.Z)
    || float.IsNaN(data.Direction.X) || float.IsNaN(data.Direction.Y) || float.IsNaN(data.Direction.Z)
    || float.IsInfinity(data.Direction.X) || float.IsInfinity(data.Direction.Y) || float.IsInfinity(data.Direction.Z))
{
    if (OriCoopPlugin.IsAnimVerbose()) { OriCoopPlugin.LogInfo("[EVENT] P" + senderId + " drop motivo=coordenada-invalida"); }
    return;
}
```

### WR-04: Client drop-old gate is updated before body validation (fail-open ordering; server does parse-then-gate)

**File:** `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:870-902`
**Issue:** In the case-19 branch, `IsNewerThanLastEvent(headerClientId, seq)` records the sequence number in `_lastEventSeq` BEFORE the `ExpectRemaining` length checks and field reads. A well-formed-length packet carrying a huge seq therefore permanently occupies the sender's seq slot and all subsequent legitimate events from that sender are dropped until Welcome-clear/reconnect. The server's own handler does it in the safe order (`TryParsePlayerEvent` first, `_eventRelay.ShouldRelay` second — `GameHandlers.cs`). Practical reachability is limited (the server pre-filters truncated bodies so the client gate almost never sees garbage, and sender ids are token-bound so an attacker can only burn their own slot), and the snapshot path shares the same ordering as a pre-existing idiom — but the new code should follow the server's parse-then-gate order, not the client's legacy order.
**Fix:** Move the `IsNewerThanLastEvent` call to after the timestamp read (or record-then-validate: validate first, gate second), mirroring `HandlePlayerEventAsync`.

### WR-05: Probe no-echo / duplicate / stale assertions read a single datagram — background reliable traffic can arrive first and produce false passes

**File:** `src/OriCoopDedicatedServer/SmokeProbe/Program.cs:605-650`
**Issue:** Each negative assertion (`eco para o remetente`, `seq repetida foi repassada`, `seq antiga foi repassada`) performs ONE `Receive` with a 600 ms timeout and passes if that single datagram is not a 19-packet. But the phase's own 03-03 summary documents live background traffic during probe runs (server `COLOR`/`CONFIG_SYNC` reliable retries while the probe drains without ACKing). If a reliable-retry datagram arrives first in any of those three windows, the check passes without ever observing whether the forbidden 19 relay happened. The positive assertion (B receives intact) uses a proper drain-loop; the negative ones do not. (Note: the shape was cloned from the pre-existing `TestRelay`, which shares the weakness — this finding applies to the new case and ideally the old one too.)
**Fix:** Drain-loop each negative window for the full timeout, failing on ANY 19-packet observed:
```csharp
DateTime noEchoUntil = DateTime.UtcNow.AddMilliseconds(600);
try
{
    while (DateTime.UtcNow < noEchoUntil)
    {
        byte[] echo = clientA.Receive(ref remote);
        if (TryParseHeader(echo, out int pid, out _, out _, out _, out _) && pid == PlayerEventId)
        { Console.WriteLine("FAIL player-event (eco para o remetente)"); return false; }
    }
}
catch (SocketException) { }
```

### WR-06: Harmony patch targets only the base `SeinSpiritFlameAbility.ThrowSpiritFlames` — subclass overrides would silently bypass detection

**File:** `src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs:24-26`
**Issue:** Wave-0 confirmed the base declaration `ThrowSpiritFlames(SpiritFlame)` and the `m_sein` field on all three flame ability classes, but nothing in the reviewed diff shows that `SeinStandardSpiritFlameAbility` / `SeinChargeFlameAbility` do NOT override `ThrowSpiritFlames`. A Harmony patch on the base `MethodInfo` does not fire when the game dispatches a virtual override on a subclass instance. If the standard-flame subclass overrides the fire method, standard shots — the pilot's entire subject — are silently never published, with zero signal (the unconditional detect line just never appears, indistinguishable from "player didn't shoot"). The charge-burst gap is acknowledged in the summaries; the standard-override question is not.
**Fix:** Re-run the metadata probe checking `GetMethod("ThrowSpiritFlames", DeclaredOnly)` on both subclasses; if either declares an override, add a second `[HarmonyPatch]` target (same Postfix) for it. One-line verification closes this.

### WR-07: Bare `catch { }` blocks in new client code swallow all exceptions without a trace

**File:** `src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs:44-47,88-90`, `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:428-430,439-441,446-448,461-466` (Spawn/Play helpers)
**Issue:** Multiple new empty catches (owner transform read, `ResolveOwner` fallback, particle/sound/projectile spawns) discard every exception type including `NullReferenceException`/`MissingComponentException` that would indicate real wiring bugs (e.g., keyword resolvers silently missing every visual — the "fail-closed silence" then hides a broken install as "working"). Some sites log a verbose reason before returning (good), but the `catch {}` bodies themselves record nothing, so a transient Unity failure during `SetActive`/`Instantiate`/`AddComponent` is invisible even in verbose mode. In Unity/.NET 3.5 a parameterless `catch` also swallows non-CLS exceptions.
**Fix:** At minimum, verbose-gate a log line inside each catch:
```csharp
catch (Exception ex)
{
    if (OriCoopPlugin.IsAnimVerbose()) { OriCoopPlugin.LogInfo("[EVENT] SpawnMuzzleParticle falhou: " + ex.GetType().Name); }
    try { UnityEngine.Object.Destroy(clone); } catch { }
}
```

### WR-08: `TakePooledFake` pool-cap check is dead code; `pool-cheio` branch unreachable

**File:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:590-640`
**Issue:** The `for` loop drains the entire pool (removing each entry) and returns the first non-null entry, so reaching line 631 (`if (s_fakePool.Count >= FakePoolCap) return null;`) means the pool is necessarily empty and the condition is always false. Additionally `BuildFakeProjectile` unconditionally returns a non-null `new GameObject`, so `TakePooledFake` can never return null and the `VerboseEvent("pool-cheio")` branch in `SpawnFakeProjectile` (line 568) is unreachable. No leak results (growth is bounded on the `ReturnPooledFake` side, line 654), but the cap reads as enforced-on-take when it is not, misleading future tuning.
**Fix:** Either check the live-shot count before building (track active count incremented in `SpawnFakeProjectile`, decremented in `ReturnPooledFake`) or delete the dead branch and document that the cap is enforced on return.

## Info

### IN-01: Static telemetry counters and ring queue have no locking (lost increments under spam)

**File:** `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs:79-135`
**Issue:** `TrackPlayerEvent`/`TrackPacket` mutate `s_events*`/`s_packets*` counters and `EmitMetricsSummary` reads-then-resets them with no `lock (s_ringSync)`, while writers run on the game thread (publish), the Unity main thread (dispatch), and the network thread (packets). Under hold-fire spam, increments are lost and a summary can interleave with a track (counters reset mid-burst). Pre-existing pattern (packet counters were already unlocked), so this only extends it — but WR-02's fix will touch exactly these lines, making it cheap to add the lock then.
**Fix:** Wrap counter mutation + summary emit/reset in `lock (s_ringSync)`.

### IN-02: Remote puppet does not face the shot direction when playing the attack clip

**File:** `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:410-450`
**Issue:** `PlaySpiritFlameVisual` sets the `AimThrow` clip and fires the fake projectile along the event direction, but never mirrors the puppet (`CharacterSpriteMirror.FaceLeft`) toward `sign(direction.x)`. Since the event direction is facing-derived (±X) and the puppet's mirror state comes from the last snapshot, a leftward remote shot can play on a right-facing sprite while the bolt flies left. Cosmetic only.
**Fix:** In `HandleSpiritFlame` (or the visual entry), set the mirror from the event direction before playing the clip.

### IN-03: Comment overclaims the exception type for truncated case-19 bodies (no behavior bug)

**File:** `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:883-890`
**Issue:** The comment states truncation "lanca InvalidDataException ... contida pelo catch por-pacote", but the marker `reader.ReadInt32()` preceding the first `ExpectRemaining` throws `EndOfStreamException` on bodies shorter than 4 bytes. Both are caught by `ReceiveLoop` (lines 541-548), so behavior is fail-closed as intended — the comment is just imprecise.
**Fix:** Amend the comment to name both exception types, or add an explicit `ExpectRemaining(reader, 4, ...)` before the marker read.

---

_Reviewed: 2026-10-06_
_Reviewer: the agent (gsd-code-reviewer)_
_Depth: deep_

**Verdict:** no blockers — wire contract, relay auth/no-echo, puppet visual-only guarantees and C# 5 compliance all verified; 8 warnings + 3 infos, fix WR-02/WR-05 first; safe to proceed, re-verify after the 2-client manual run.
