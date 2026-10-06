# Phase 03: player-event-core - Pattern Map

**Mapped:** 2026-10-06
**Files analyzed:** 14 (5 new + 9 modified)
**Analogs found:** 12 / 14

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs` (NEW) | service (event bus + registry) | event-driven | `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` | role-match |
| `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventKind.cs` (NEW) | model (enum) | transform | `src/OriCoopPlus/OriCoopShared/PacketType.cs` + `AnimationSyncData.cs:5-34` | exact |
| `src/OriCoopPlus/OriCoopBepInEx/Events/SpiritFlameEventData.cs` (NEW) | model (DTO struct) | transform | `src/OriCoopPlus/OriCoopBepInEx/Domain/PlayerState.cs` | exact |
| `src/OriCoopPlus/OriCoopBepInEx/Patches/SpiritFlamePatch.cs` (NEW) | patch (detector) | event-driven | `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs` | exact |
| `src/OriCoopPlus/OriCoopShared/PlayerEventProtocol.cs` (NEW, optional) | config (shared codec consts) | transform | `src/OriCoopPlus/OriCoopShared/NetProtocol.cs` | exact |
| `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` (MODIFY: `SendPlayerEvent` + case 19) | service (transport) | streaming (unreliable-sequenced) | itself (`FlushSnapshot` + `PLAYER_STATE` branch + `IsNewerThanLast`) | exact (self) |
| `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs` (MODIFY: `HandlePlayerEvent`) | service (router) | event-driven | itself (`HandleSnapshot`/`HandleDirectState`) | exact (self) |
| `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` (MODIFY: `PlaySpiritFlameVisual`) | component | event-driven | itself (`ApplySnapshotDirect` + `ApplyConfirmedAnimation`) | exact (self) |
| `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs` (MODIFY: event counters) | utility | event-driven | itself (`TrackPacket`/`Record`) | exact (self) |
| `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` (MODIFY: wire publish/subscribe) | provider (lifecycle) | request-response + event-driven | itself (`Publish` + `OnPlayerSnapshotReceived` + `EnqueueMainThread`) | exact (self) |
| `src/OriCoopPlus/OriCoopBepInEx/Domain/INetworkService.cs` (MODIFY: decl + event) | interface | request-response | itself (`ConfigSyncHandler` delegate + event block) | exact (self) |
| `src/OriCoopPlus/OriCoopShared/PacketType.cs` (MODIFY: `PLAYER_EVENT = 19`) | config (contract) | transform | itself (lines 1-33) | exact (self) |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs` (MODIFY: case 19) | service (route) | streaming (unreliable-sequenced) | itself (`HandlePlayerStateAsync` + `TryParsePlayerState`) | exact (self) |
| `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs` (MODIFY: route 19) | route (dispatch) | streaming | itself (`DispatchAsync` 252-300 + `IsCriticalPacket` 554-566) | exact (self) |

## Pattern Assignments

### `Events/PlayerEventCore.cs` (NEW — service/event bus, event-driven)

**Analog:** `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` (static registry + fail-closed resolve) and `Client/RemotePlayerManager.cs` (dispatch shape).

**Why this analog:** No pub/sub bus exists in the repo. `AnimationRegistry` is the closest living pattern: static class, dictionary-backed catalog, `TryResolve*` lookups, `Resolve → null → caller keeps last` fail-closed contract. `PlayerEventCore` = same static-registry discipline, but mapping `byte eventKind → handler` instead of `hash/name → clip`, plus a `Publish` entry that builds the DTO and calls the transport.

**Static registry shape** (`AnimationRegistry.cs` lines 11-24):
```csharp
public static class AnimationRegistry
{
    private static readonly Dictionary<uint, TextureAnimationWithTransitions> s_animHashCache =
        new Dictionary<uint, TextureAnimationWithTransitions>();
    private static readonly Dictionary<string, TextureAnimationWithTransitions> s_animNameCache =
        new Dictionary<string, TextureAnimationWithTransitions>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ActionVisualState, TextureAnimationWithTransitions> s_stateClips =
        new Dictionary<ActionVisualState, TextureAnimationWithTransitions>();
```

**Fail-closed resolve contract to clone** (`AnimationRegistry.cs` lines 537-552):
```csharp
public static TextureAnimationWithTransitions Resolve(string animName, uint animHash, ActionVisualState fallbackState)
{
    TextureAnimationWithTransitions result;
    if (TryResolveExact(animName, animHash, out result))
    {
        return result;
    }
    if (TryResolveState(fallbackState, out result))
    {
        return result;
    }

    // Desconhecido: retorna null e o chamador mantém a última anim
    // (fail-closed). Sem chute para Idle genérico.
    return null;
}
```
→ `PlayerEventCore.DispatchLocal(kind, ...)`: unknown kind returns `false`/no-op + reason log; caller (`RemotePlayerManager`/`RemotePlayerPuppet`) keeps current clip. Never fall back to Idle.

**Dispatch entry shape** (`RemotePlayerManager.cs` lines 12-25):
```csharp
public void HandleSnapshot(PlayerSnapshot snapshot)
{
    if (snapshot == null || snapshot.PlayerId < 0)
    {
        return;
    }

    if (!snapshot.IsPlayerStatePacket)
    {
        return;
    }

    HandleDirectState(snapshot);
}
```
→ `HandlePlayerEvent(senderId, kind, dir, origin, timestamp)`: guard `kind` against catalog, never call publish (no echo), marshal already on main thread before touching Unity objects.

**C# 5 constraints (apply to ALL new client files):** no `$""` (use `string.Format`/`+`), no `?.`, no expression-bodied members, `Action` max 4 params (custom delegate beyond — see `INetworkService.cs:6-10`), `Object.Instantiate(x) as GameObject` explicit cast (see `RemotePuppetFactory.cs:58`). Compiled by `build.ps1` via `csc.exe /noconfig` (`build.ps1:44-48,74-96`), NOT `dotnet build`.

---

### `Events/PlayerEventKind.cs` (NEW — model/enum, transform)

**Analog:** `src/OriCoopPlus/OriCoopShared/PacketType.cs` (lines 1-33) + `ActionVisualState` (`AnimationSyncData.cs` lines 5-34).

**Enum pattern** (`PacketType.cs` lines 23-33):
```csharp
// Ori Coop Plus extended packets
SYNC_ABILITY = 10,
SYNC_LEVER = 11,
SYNC_DOOR = 12,
SYNC_BREAKABLE = 13,
SYNC_WORLDEVENT = 14,
TELEPORT_REQUEST = 15,
CONFIG_SYNC = 16,
DUMMY_ACTION = 17,
PLAYER_STATE = 18
```
→ Append `PLAYER_EVENT = 19` here (same file, same change), and define:
```csharp
namespace OriCoop
{
    public enum PlayerEventKind : byte
    {
        Unknown = 0,
        SpiritFlame = 1
        // append-only: Stomp/Bash/... get 2,3,... in future phases; 255 reserved unknown
    }
}
```
Must live in `OriCoop` namespace inside `OriCoopShared` so it compiles on both C# 5 client and .NET 8 server. Append-only rule mirrors `ActionVisualState` comment (`AnimationSyncData.cs:19-21`): "Append-only para nao quebrar o protocolo (State viaja como byte opaco)."

---

### `Events/SpiritFlameEventData.cs` (NEW — model/DTO, transform)

**Analog:** `src/OriCoopPlus/OriCoopBepInEx/Domain/PlayerState.cs` (lines 1-59).

**DTO struct pattern** (`PlayerState.cs` lines 4-28):
```csharp
public struct Vector2Data
{
    public float X;
    public float Y;

    public Vector2Data(float x, float y)
    {
        X = x;
        Y = y;
    }
}

public struct Vector3Data
{
    public float X;
    public float Y;
    public float Z;

    public Vector3Data(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
```
→ Copy verbatim conventions: public mutable fields (not properties — BinaryWriter/BinaryReader field order must stay obvious), explicit constructor, no Unity types (`Vector3`/`Quaternion` forbidden here — this is the Domain-style layer; conversion to `UnityEngine.Vector3` happens at the puppet edge, cf. `RemotePlayerManager.cs:34-35`). Suggested shape:
```csharp
public struct SpiritFlameEventData
{
    public byte Kind; // == (byte)PlayerEventKind.SpiritFlame
    public Vector3Data Direction;
    public Vector3Data Origin;
    public long TimestampTicks;
}
```
Field order frozen = wire order: `int marker(19) + byte kind + dirX/Y/Z + originX/Y/Z + long ts` (D-11).

---

### `Patches/SpiritFlamePatch.cs` (NEW — patch/detector, event-driven)

**Analog:** `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs` (lines 1-17, full file).

**Imports + filter + publish pattern** (`SeinCharacterPatch.cs` lines 1-17):
```csharp
using System;
using HarmonyLib;
using OriCoopBepInEx.Plugin;

namespace OriCoopBepInEx.Patches
{
    [HarmonyPatch(typeof(SeinCharacter), "FixedUpdate")]
    internal static class SeinCharacterPatch
    {
        private static void Postfix(SeinCharacter __instance)
        {
            if (OriCoopPlugin.Instance != null && __instance == Game.Characters.Sein)
            {
                OriCoopPlugin.Instance.Publish(PlayerStateReader.Read(__instance));
            }
        }
    }
}
```
→ Clone exactly, retargeted:
```csharp
using System;
using HarmonyLib;
using OriCoopBepInEx.Events;
using OriCoopBepInEx.Plugin;

namespace OriCoopBepInEx.Patches
{
    // Wave 0 MUST confirm exact declaring class + signature via dnSpy/ILSpy.
    // Candidates (assembly-strings probe): SeinSpiritFlameAbility / SeinStandardSpiritFlameAbility, method "OnShoot".
    [HarmonyPatch(typeof(SeinSpiritFlameAbility), "OnShoot")]
    internal static class SpiritFlamePatch
    {
        private static void Postfix(object __instance) // exact type = confirmed declaring class
        {
            if (OriCoopPlugin.Instance == null) { return; }
            // D-07: resolve owner Sein from __instance (accessor confirmed Wave 0),
            // compare against Game.Characters.Sein singleton. Puppet has no SeinCharacter ability → never publishes.
            // D-05: NO network code here — only PlayerEventCore.PublishSpiritFlame(owner).
        }
    }
}
```
**Second analog — no-op patch shape** (`Patches/SeinInputPatch.cs` lines 6-14): `internal static class` + `private static void Postfix()` with zero network logic — confirms patches stay thin.

**Third analog — registry hookup from patch** (`Patches/AnimationPrewarmPatch.cs` lines 7-14):
```csharp
[HarmonyPatch(typeof(CharacterAnimationSystem), "Start")]
internal static class AnimationPrewarmPatch
{
    private static void Postfix()
    {
        AnimationRegistry.Prewarm();
    }
}
```
→ Same one-call delegation discipline: patch detects, static core acts.

**Wave-0 gate (from RESEARCH.md):** confirm (a) which of the 3 flame classes declares the projectile-spawning method, (b) its parameters, (c) how to reach the owning `SeinCharacter` from `__instance`, (d) aim source (`ShootDirection`-like member or facing fallback). Add temporary verbose log `[EVENT] SpiritFlame detected` and verify it fires locally before wiring network (Pitfall 1).

---

### `OriCoopShared/PlayerEventProtocol.cs` (NEW, optional — config/codec consts, transform)

**Analog:** `src/OriCoopPlus/OriCoopShared/NetProtocol.cs` (lines 14-49).

**Consts-only shared pattern** (`NetProtocol.cs` lines 14-21):
```csharp
public static class NetProtocol
{
    public const ushort Magic = 0x4F43;
    public const byte Version = 2;
    public const int HeaderSize = 24;

    public const byte FlagReliable = 0x01;
    public const byte FlagAckPresent = 0x02;
```
→ Same file-level contract: `namespace OriCoop`, `public static class`, only `const` members (compiles C# 5 + .NET 8). Suggested consts: `PlayerEventBodySize` (4+1+12+12+8 = 37), field offsets (`OffKind=4`, `OffDir=5`, `OffOrigin=17`, `OffTimestamp=29`), `MinBodyLength`. If the planner prefers, these consts can live inside `PlayerEventCore.cs` instead — creating the file is optional, the consts-only discipline is not.

---

### `Networking/NetworkService.cs` (MODIFY — service/transport, streaming)

**Analog:** itself. Three sites to clone in the same file:

**1. Unreliable send to clone** (`NetworkService.cs` lines 195-221, `FlushSnapshot` body):
```csharp
using (MemoryStream stateBody = new MemoryStream())
using (BinaryWriter stateWriter = new BinaryWriter(stateBody))
{
    stateWriter.Write((int)PacketType.PLAYER_STATE);
    stateWriter.Write(snapshot.Position.X);
    ...
    WriteLegacyString(stateWriter, !string.IsNullOrEmpty(snapshot.Nick) ? snapshot.Nick : _nickname);
    stateWriter.Flush();
    // Snapshots sao unreliable por desenho (D-09): sem retry.
    SendSystem((int)PacketType.PLAYER_STATE, stateBody.ToArray(), 0);
}
```
→ New `SendPlayerEvent(byte kind, float dx, dy, dz, ox, oy, oz, long ts)` builds `int 19 + byte kind + 6×float + long` and calls `SendSystem((int)PacketType.PLAYER_EVENT, body, 0)` — flags=0, never `SendReliable` (Pitfall 2). Note the existing reliable senders that must NOT be cloned for this: `SendSkill` (269-284) and `SendSyncAbility` (322-337) both call `SendReliable` — wrong class for the pilot.

**Envelope primitive** (`NetworkService.cs` lines 539-559):
```csharp
private void SendSystem(int packetId, byte[] body, byte flags)
{
    uint seq = NextSeq();
    SendEnvelope(_assignedId, _sessionToken, packetId, seq, flags, body);
}
```
→ Reuse as-is. Pitfall 3 requirement: event seqs must never suppress snapshots. Current state: single `_sendSeq` (`644-655`) and single `_lastRelaySeq` keyed by sender only (`51`, `948-964`). Planner must add a separate seq domain for events (separate send counter + separate last-seen dict per sender on receive); server side needs a dedicated relay gate instance (see `GameHandlers` section).

**2. Receive branch to clone** (`NetworkService.cs` lines 773-797, `PLAYER_STATE` branch):
```csharp
if (packetId == (int)PacketType.PLAYER_STATE)
{
    int marker = reader.ReadInt32();
    if (marker != (int)PacketType.PLAYER_STATE)
    {
        throw new InvalidDataException("PLAYER_STATE sem marcador 18.");
    }
    if (!IsNewerThanLast(headerClientId, seq))
    {
        return;
    }
    PlayerSnapshot snapshot = new PlayerSnapshot();
    snapshot.PlayerId = headerClientId;
    ...
    RaiseSnapshot(snapshot);
}
```
→ New `else if (packetId == (int)PacketType.PLAYER_EVENT)` branch beside it: marker check → separate-domain drop-old (`IsNewerThanLastEvent`) → `ExpectRemaining`-style length checks before each read (protocol rule 4; see `ExpectMarker`/`ExpectRemaining` at 931-946) → marshal to main thread via `OriCoopPlugin.EnqueueMainThread` → `RemotePlayerManager.HandlePlayerEvent`. Identity comes from `headerClientId` — NEVER read a player id from the body (D-11).

**Validate-and-discard reference (what 19 must NOT copy):** `SKILL`/`SYNC_ABILITY` receivers (`881-905`) read-and-drop with `ExpectMarker` + `ExpectRemaining` + `ReadBytes(8)` — correct framing discipline, but 19 needs a real dispatch, not discard.

**3. Wrap-safe compare to clone** (`NetworkService.cs` lines 948-964):
```csharp
private bool IsNewerThanLast(int senderId, uint seq)
{
    uint last;
    if (_lastRelaySeq.TryGetValue(senderId, out last))
    {
        unchecked
        {
            uint diff = seq - last;
            if ((int)diff <= 0)
            {
                return false;
            }
        }
    }
    _lastRelaySeq[senderId] = seq;
    return true;
}
```
→ Duplicate as `IsNewerThanLastEvent` with its own `Dictionary<int, uint>` (Pitfall 3). Also clear the new dict alongside `_lastRelaySeq.Clear()` in the `MsgWelcome` handler (line 736).

**Error-handling discipline:** receive loop catches `InvalidDataException`/`EndOfStreamException` per-packet (`ReceiveLoop` 497-504) — throwing from the new branch is safe and idiomatic; malformed 19-body → `throw new InvalidDataException("PLAYER_EVENT ...")`, never let it escape the loop.

**Legacy string codec** (only if a string is ever added — pilot has none): `WriteLegacyString` (`980-990`, `int32 length` + ASCII, never `BinaryWriter.Write(string)` which emits LEB128).

---

### `Client/RemotePlayerManager.cs` (MODIFY — service/router, event-driven)

**Analog:** itself, full file (84 lines). Clone `HandleDirectState` (lines 27-58):

```csharp
public void HandleDirectState(PlayerSnapshot snapshot)
{
    if (snapshot == null || snapshot.PlayerId < 0)
    {
        return;
    }

    Vector3 pos = new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z);
    Vector3 vel = new Vector3(snapshot.Velocity.X, snapshot.Velocity.Y, 0f);

    RemotePlayerPuppet puppet;
    if (!_puppets.TryGetValue(snapshot.PlayerId, out puppet) || puppet == null)
    {
        puppet = RemotePuppetFactory.CreatePuppet(snapshot.PlayerId, snapshot.Nick, pos);
        if (puppet != null)
        {
            _puppets[snapshot.PlayerId] = puppet;
            puppet.SnapTo(pos);
        }
    }

    if (puppet != null)
    {
        if (!string.IsNullOrEmpty(snapshot.Nick))
        {
            puppet.UpdateNickname(snapshot.Nick);
        }
        puppet.ApplySnapshotDirect(pos, vel, snapshot.Animation.FacingLeft,
            snapshot.Animation.State, snapshot.Animation.Name,
            snapshot.Animation.AnimNameHash, snapshot.Nick);
    }
}
```
→ New `HandlePlayerEvent(int senderId, byte kind, Vector3 origin, Vector3 direction, long timestamp)`: same guard shape (`senderId < 0 → return`), same lazy-puppet lookup via `_puppets` (create at `origin` if missing — reuse `RemotePuppetFactory.CreatePuppet` + `SnapTo` lines 38-46), then `PlayerEventCore.DispatchLocal` → `puppet.PlaySpiritFlameVisual(origin, direction)` for known kinds; unknown kind → keep last anim + reason log (D-15). Must NEVER call publish (D-07 no-echo). Called on Unity main thread (via `EnqueueMainThread`, see Plugin section).

---

### `Client/RemotePlayerPuppet.cs` (MODIFY — component, event-driven)

**Analog:** itself. Two sites:

**1. Hysteresis + fail-closed entry** (`RemotePlayerPuppet.cs` lines 94-136 `ApplySnapshotDirect` + 138-202 `ApplyConfirmedAnimation`):
```csharp
public void ApplySnapshotDirect(Vector3 position, Vector3 velocity, bool facingLeft, ActionVisualState state, string animName, uint animHash, string nick)
{
    ...
    if (state == _confirmedState)
    {
        _pendingState = _confirmedState;
        ApplyConfirmedAnimation(animName, animHash, _confirmedState);
        LogAnimTransition(state, "manteve");
        return;
    }
    ...
}
```
and fail-closed (`175-185`):
```csharp
// Clipe desconhecido: mantém a anim atual (fail-closed, sem
// fallback para Idle genérico que virava sprite aleatório).
if (targetClip == null)
{
    if (OriCoopPlugin.IsAnimVerbose())
    {
        OriCoopPlugin.LogInfo(string.Format("[ANIM] P{0} recv={1} aplicado=manteve-atual motivo=desconhecido",
            PlayerId, confirmedState));
    }
    return;
}

if (!IsPlayingClip(_animator, targetClip))
{
    _animator.SetAnimation(targetClip, true);
    ...
}
```
→ `PlaySpiritFlameVisual(origin, direction)`: (1) same attack clip via `_animator.SetAnimation(clip, true)` guarded by `IsPlayingClip` (`263-277`, compares `CurrentTextureAnimationTransitions == clip` — never compare `CurrentAnimation` directly); resolve clip via `AnimationRegistry` exact → state fallback → fail-closed return. (2) muzzle particle visual-only clone at origin, auto-destroy. (3) SFX via transient scene-level `AudioSource` — NOT under puppet (Pitfall 4: `RemotePuppetFactory.CleanPuppetComponents` destroys ALL `AudioSource`, lines 248-256). (4) fake projectile: pooled pure-visual `GameObject` (mesh+animator only, no collider/rigidbody/damage scripts), straight-line `Update` motion, auto-despawn; instantiate **inactive**, strip to visual-only BEFORE first activation (Pitfall 5 — same inactive-clone discipline as the factory). Structure (payload, handler, spawn point) stays ready for real physics in the future entities phase (D-14).

**2. Observability hook** (`196-201`):
```csharp
if (_lastAnimName != animName)
{
    _lastAnimName = animName;
    ReplicationObservability.TrackPacket(PlayerId, animHash, animName ?? confirmedState.ToString(), true);
}
```
→ Same call shape for event transitions (sent → received → applied + motive), gated by `OriCoopPlugin.IsAnimVerbose()` like `LogAnimTransition` (`279-289`).

**Watchdog constraint:** `RemoteVisualController` (`RemoteVisualController.cs:18-64, 83-136`) re-enables renderers and isolates materials per-puppet — new VFX children must be renderers the watchdog tolerates (it skips `NameTag/Shadow/ghostTrail/mistErase` names at lines 31-38; name fake-projectile parts to avoid the skip list so they stay visible).

---

### `Diagnostics/ReplicationObservability.cs` (MODIFY — utility, event-driven)

**Analog:** itself, full file (84 lines).

**Ring + counters pattern** (`ReplicationObservability.cs` lines 19-33, 50-82):
```csharp
public static void Record(string line)
{
    if (string.IsNullOrEmpty(line))
    {
        return;
    }
    lock (s_ringSync)
    {
        while (s_ring.Count >= RingCapacity)
        {
            s_ring.Dequeue();
        }
        s_ring.Enqueue(line);
    }
}
```
```csharp
public static void TrackPacket(int playerId, uint animHash, string actionState, bool applied)
{
    s_packetsReceivedCounter++;
    ...
    if (Time.time - s_lastLogTime >= 3.0f)
    {
        s_lastLogTime = Time.time;
        EmitMetricsSummary(playerId, animHash, actionState);
    }
}
```
→ Add parallel event counters (`TrackPlayerEvent(kind, applied)` + `s_eventsReceived/Applied/Dropped`) and `Record("[EVENT] ...")` lines with transition reasons (sent → received → applied + motive), mirroring the anim `[ANIM]` vocabulary. Throttled summary via the same 3 s `EmitMetricsSummary` shape. Keep `RingCapacity = 200`, keep `lock (s_ringSync)`.

---

### `Plugin/OriCoopPlugin.cs` (MODIFY — provider/lifecycle, request-response + event-driven)

**Analog:** itself. Three sites:

**1. Publish entry** (`OriCoopPlugin.cs` lines 288-297):
```csharp
public void Publish(PlayerSnapshot snapshot)
{
    if (snapshot != null && _network != null)
    {
        snapshot.PlayerId = _playerId.Value;
        snapshot.Nick = !string.IsNullOrEmpty(_localNick) ? _localNick : (_nickname != null ? _nickname.Value : "Ori_Player");
        _localPosition = snapshot.Position;
        _network.SendPlayerSnapshot(snapshot);
    }
}
```
→ `PlayerEventCore` calls either a new `PublishEvent(kind, dir, origin, ts)` here or `_network.SendPlayerEvent(...)` directly (planner decides; D-05 says core owns assembly, plugin owns lifecycle). Keep the null-guard shape.

**2. Main-thread marshal** (`OriCoopPlugin.cs` lines 38-47 + 299-313):
```csharp
public static void EnqueueMainThread(Action action)
{
    if (Instance != null && action != null)
    {
        lock (Instance._mainThreadActions)
        {
            Instance._mainThreadActions.Enqueue(action);
        }
    }
}
```
```csharp
private void OnPlayerSnapshotReceived(PlayerSnapshot snapshot)
{
    lock (_remotePlayers)
    {
        _remotePlayers[snapshot.PlayerId] = snapshot;
    }

    lock (_mainThreadActions)
    {
        _mainThreadActions.Enqueue(delegate
        {
            _remotePlayerManager.HandleSnapshot(snapshot);
        });
    }
}
```
→ New `OnPlayerEventReceived(...)` handler: same shape — stash nothing game-mutating on the network thread, `Enqueue(delegate { _remotePlayerManager.HandlePlayerEvent(...); })`; drained in `Update()` (`654-669`) with per-action try/catch. Note: `Action` single-param `delegate` closure is C# 5-safe (≤4 params rule only bites at 5+; `ConfigSyncHandler` exists for the 8-param case).

**3. Event wiring** (`OriCoopPlugin.cs` lines 132-141 `Connect` + 272-281 `Awake`):
```csharp
_network = new NetworkService(host, port, -1, nick);
_network.PlayerSnapshotReceived += OnPlayerSnapshotReceived;
_network.TeleportRequested += OnTeleportRequested;
...
_network.Start();
```
→ Subscribe/unsubscribe the new `PlayerEventReceived` event in the same four places (`Connect`, `Disconnect` 157-164, `Awake`, `OnDestroy`): `+=` on connect/awake, `-=` on disconnect/destroy.

**Sein lookup helper** (`OriCoopPlugin.cs` lines 490-539 `FindLocalSein`): singleton-first (`Game.Characters.Sein`), then `FindObjectsOfType<SeinCharacter>()`, then `GameObject.Find` fallbacks — reuse for resolving the local Sein / spawning VFX reference points; never `GameObject.Find("Sein")` alone (wrong name post-load: `Sein(Clone)`).

---

### `Domain/INetworkService.cs` (MODIFY — interface, request-response)

**Analog:** itself (lines 1-39).

**Custom-delegate + event block** (`INetworkService.cs` lines 5-21):
```csharp
// Delegate proprio em vez de Action<...> com 8 parametros: o mscorlib do
// Unity 5.3 (perfil .NET 3.5) nao garante Action com mais de 4 parametros.
// Ordem canonica do CONFIG_SYNC 16: AllowTeleport, ShareAbilities,
// ShareStoryOnly, ShareWorldEvents, ShareDoorsAndLevers, ShowNicknames,
// ClientColors, EntitySync.
public delegate void ConfigSyncHandler(bool allowTeleport, bool shareAbilities, bool shareStoryOnly, bool shareWorldEvents, bool shareDoorsAndLevers, bool showNicknames, bool clientColors, bool entitySync);

public interface INetworkService : IDisposable
{
    event Action<PlayerSnapshot> PlayerSnapshotReceived;
    ...
    void SendPlayerSnapshot(PlayerSnapshot snapshot);
```
→ Add `event Action<PlayerEventData...> PlayerEventReceived;` (if the event args need >4 params, declare a `PlayerEventHandler` custom delegate with the canonical field order in the comment, same as `ConfigSyncHandler`) + `void SendPlayerEvent(byte kind, float dirX, float dirY, float dirZ, float originX, float originY, float originZ, long timestampTicks);` — 8 params → MUST be a custom delegate or a single DTO param (DTO preferred: `SendPlayerEvent(SpiritFlameEventData data)` keeps the interface to 1 param). `NetworkService` implements both.

---

### `OriCoopShared/PacketType.cs` (MODIFY — config/contract, transform)

**Analog:** itself (lines 1-33, quoted in full under `PlayerEventKind` above).

**Change:** append `PLAYER_EVENT = 19` after `PLAYER_STATE = 18` with comment `// PlayerEventCore pilot (fase 3): unreliable sequenced, D-09 class`. Never touch `POSITION = 1`, `ANIM = 2`, removed ID 3/5, or the dead-core comment block (lines 14-21) — banned IDs stay banned (`docs/protocol.md` rule 1). Same-build + docs rule: this edit ships in the SAME change as both wire ends + `docs/protocol.md` update (D-10 one-way break).

---

### Server `Net/Game/GameHandlers.cs` (MODIFY — service/route, streaming)

**Analog:** itself. Three sites:

**1. Handler to clone** (`GameHandlers.cs` lines 191-227 `HandlePlayerStateAsync`):
```csharp
public async Task HandlePlayerStateAsync(Session.Session sender, uint seq, byte[] payload, byte[] originalDatagram, CancellationToken ct)
{
    float x; ... string nick;
    if (!TryParsePlayerState(payload, out x, ...))
    {
        _log.Log(ServerLogLevel.Warning, "GAME", "PLAYER_STATE truncado de " + sender.Id + " (drop)");
        return;
    }
    lock (_sync)
    {
        _lastKnown[sender.Id] = new LastRemoteState { ... ReceivedAt = DateTime.UtcNow, };
    }
    if (!_relay.ShouldRelay(sender, seq))
    {
        return;
    }
    await _transport.RelayUnreliableAsync(originalDatagram, sender, ct).ConfigureAwait(false);
}
```
→ New `HandlePlayerEventAsync`: `TryParsePlayerEvent` (marker + length) → drop + `Warning` on truncation → dedicated seq-gate instance (NOT the shared `_relay` — Pitfall 3: separate `PlayerStateRelay`-equivalent per packet class, field `_eventRelay`) → `RelayUnreliableAsync(originalDatagram, sender, ct)` byte-identical. No `fromId` stamping (unlike `SKILL` at 291-306 which rebuilds with `sender.Id` — 19 must NOT copy that; identity stays in the envelope header).

**2. Codec pair to clone** (`GameHandlers.cs` lines 562-630 `BuildPlayerStatePayload` + `TryParsePlayerState`):
```csharp
public static bool TryParsePlayerState(
    byte[] payload, out float x, out float y, out float z, out byte state,
    out byte flags, out int animHash, out float speedX, out float speedY, out string nick)
{
    ...
    if (payload == null || payload.Length < 34)
    {
        return false;
    }
    if (BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.PLAYER_STATE)
    {
        return false;
    }
    x = ReadFloat(payload, 4);
    ...
}
```
→ `TryParsePlayerEvent(payload, out kind, out dx/dy/dz, out ox/oy/oz, out ts)`: minimum length = 4+1+12+12+8 = 37; marker check `!= (int)PacketType.PLAYER_EVENT → false`; `BinaryPrimitives` LE reads (modern C# allowed on server). Add `BuildPlayerEventPayload` mirror for SmokeProbe round-trip tests.

**3. Dispatch registration** (`GameHandlers.cs` lines 94-106 `IsGamePacket` + 161-189 `DispatchAsync`):
```csharp
public static bool IsGamePacket(int packetId)
{
    return packetId == (int)PacketType.TELEPORT_REQUEST
        || packetId == (int)PacketType.SYNC_ABILITY
        ...
}
```
→ Planner decides: either extend `IsGamePacket` + `DispatchAsync` switch with `case (int)PacketType.PLAYER_EVENT`, or mirror the `PLAYER_STATE` dedicated path in `NetServerHost` (see next section). Either way the handler ends in `RelayUnreliableAsync`, never `BroadcastReliableAsync` (Pitfall 2).

**Defensive-read idiom** (all `On*Async` handlers, e.g. 238-243): `payload == null || payload.Length < N || marker != ID → Warning-log + drop`. Copy for 19.

---

### Server `Net/NetServerHost.cs` (MODIFY — route/dispatch, streaming)

**Analog:** itself. Two sites:

**1. Dispatch branch** (`NetServerHost.cs` lines 252-281):
```csharp
if (header.PacketId == (int)PacketType.PLAYER_STATE)
{
    if (!sender.IsReady)
    {
        _log.Log(ServerLogLevel.Debug, "SESSAO", "Snapshot de " + sender.Id + " antes do Confirm (drop)");
        return Task.CompletedTask;
    }
    // Camada Game (02-03): registra LastKnown + relay unreliable.
    if (Game != null)
    {
        return Game.HandlePlayerStateAsync(sender, header.Seq, payload, datagram.Data, ct);
    }
    ...
}
```
→ New packet-19 branch in the same position: `IsReady` gate → `Game.HandlePlayerEventAsync(...)`. The `IsGamePacket → DispatchAsync` path (lines 273-281) is the alternative landing zone — pick ONE, not both.

**2. Critical-list exclusion** (`NetServerHost.cs` lines 548-566):
```csharp
/// Criticos (D-10, lista em <see cref="IsCriticalPacket"/>): chat -5,
/// CONFIG_SYNC 16, TELEPORT_REQUEST 15, SYNC_ABILITY 10, SYNC_LEVER 11,
/// SYNC_DOOR 12, SYNC_WORLDEVENT 14, SKILL 7, COLOR 6, DISCONNECT 4.
/// PLAYER_STATE 18 e Ping 104 nunca geram pendencia (unreliable).
private static bool IsCriticalPacket(int packetId)
{
    return packetId == ChatPacketId
        || packetId == (int)PacketType.CONFIG_SYNC
        ...
}
```
→ Do NOT add 19 here. 19 is unreliable-class (D-09): no `AckTracker` state, no retry. Update the doc comment to mention 19 alongside 18.

**Blind-relay primitive** (`NetServerHost.cs` lines 633-636 + 303-309 `RelayAsync`): reemits `originalDatagram` bytes to all `IsReady` except sender — reused unchanged via `IGameTransport.RelayUnreliableAsync`.

---

## Shared Patterns

### C# 5 / .NET 3.5 client discipline
**Source:** `build.ps1:44-96` (csc.exe `/noconfig`, refs from game `Managed/`), `Domain/INetworkService.cs:5-10`, `Client/RemotePuppetFactory.cs:58`
**Apply to:** ALL new/modified client files (`Events/*`, `Patches/SpiritFlamePatch.cs`, `NetworkService.cs` additions, puppet/manager additions)
```
- string.Format / + only (no $""); explicit null checks (no ?. / ?[]); no expression-bodied members
- Action max 4 params → custom delegate (ConfigSyncHandler precedent) or single-DTO param
- UnityEngine.Object.Instantiate(x) as GameObject — explicit cast required
- using() + BinaryWriter/BinaryReader field-by-field;出一律 little-endian (BinaryWriter default)
- Verify with build.ps1 (csc.exe), never dotnet build, for client changes
```

### Envelope + marker-led body (wire contract)
**Source:** `OriCoopShared/NetProtocol.cs:14-49` + `Networking/NetworkService.cs:662-682 BuildEnvelope` + `GameHandlers.cs:562-580 BuildPlayerStatePayload`
**Apply to:** `SendPlayerEvent`, client case-19 branch, server `TryParsePlayerEvent`, SmokeProbe case
```csharp
// Client send primitive (NetworkService.cs:539-543, 657-682):
SendSystem((int)PacketType.PLAYER_EVENT, body, 0); // flags=0 → unreliable, no retry
// Body layout (D-11 frozen order, 37 bytes):
writer.Write((int)PacketType.PLAYER_EVENT); // leading marker 19 — legacy-identical convention
writer.Write((byte)PlayerEventKind.SpiritFlame);
writer.Write(dirX); writer.Write(dirY); writer.Write(dirZ);
writer.Write(ox); writer.Write(oy); writer.Write(oz);
writer.Write(timestampTicks); // long, DateTime.UtcNow.Ticks
// clientId travels ONLY in envelope header (NetProtocol.OffClientId=8); never in body
```

### Unreliable-sequenced receive + wrap-safe drop-old (separate seq domains)
**Source:** `Networking/NetworkService.cs:948-964 IsNewerThanLast` + `Server/Net/Game/PlayerStateRelay.cs:19-31 ShouldRelay`
**Apply to:** client case-19 branch (new `IsNewerThanLastEvent` + own dict), server `HandlePlayerEventAsync` (dedicated `PlayerStateRelay` instance `_eventRelay`)
```csharp
// Client (NetworkService.cs:948-964):
unchecked { uint diff = seq - last; if ((int)diff <= 0) { return false; } }
// Server (PlayerStateRelay.cs:20-31):
unchecked { uint diff = seq - sender.LastRecvSeq; if ((int)diff > 0) { sender.LastRecvSeq = seq; return true; } return false; }
// REQUIREMENT (Pitfall 3): event seqs and snapshot seqs MUST use separate domains —
// separate send counter client-side, separate last-seen dict per sender client-side,
// separate relay-gate instance server-side. Event bursts must never suppress movement.
```

### Blind relay (server never parses/merges game bytes)
**Source:** `Server/Net/Game/GameHandlers.cs:222-226` + `Server/Net/NetServerHost.cs:303-309 RelayAsync` / `633-636`
**Apply to:** `HandlePlayerEventAsync`
```csharp
await _transport.RelayUnreliableAsync(originalDatagram, sender, ct).ConfigureAwait(false);
// originalDatagram = untouched bytes including sender's header+seq; server does NO parse/merge,
// stamps NO fromId (unlike SKILL/SYNC_* reliable relays). Targets = IsReady except sender (PlayerStateRelay.Targets).
```

### Local-player-only publish filter (no echo)
**Source:** `Patches/SeinCharacterPatch.cs:10-16`
**Apply to:** `SpiritFlamePatch.cs`, `PlayerEventCore.Publish*`, `RemotePlayerManager.HandlePlayerEvent`
```csharp
if (OriCoopPlugin.Instance != null && __instance == Game.Characters.Sein)
{
    OriCoopPlugin.Instance.Publish(PlayerStateReader.Read(__instance));
}
// Remote handling must NEVER call publish — puppet/remote never republishes (D-07, anti echo infinito).
```

### Fail-closed unknown catalog entries
**Source:** `Client/AnimationRegistry.cs:537-552 Resolve` + `Client/RemotePlayerPuppet.cs:175-185`
**Apply to:** `PlayerEventCore.DispatchLocal`, `HandlePlayerEvent`, `PlaySpiritFlameVisual`
```csharp
if (targetClip == null)
{
    // mantém a anim atual (fail-closed); loga motivo quando verbose. Nunca Idle genérico.
    return;
}
```
→ Unknown `eventKind` (0/255/future): keep last anim, `Record`/`LogInfo` reason when `IsAnimVerbose()`, no exception, no fallback clip.

### Network → main-thread marshal
**Source:** `Plugin/OriCoopPlugin.cs:38-47 EnqueueMainThread` + `299-313 OnPlayerSnapshotReceived` + `654-669 Update drain`
**Apply to:** `OnPlayerEventReceived` → `HandlePlayerEvent` → puppet visual calls
```csharp
lock (_mainThreadActions)
{
    _mainThreadActions.Enqueue(delegate
    {
        _remotePlayerManager.HandleSnapshot(snapshot);
    });
}
// All Unity object touches (puppet clip set, particle instantiate, AudioSource) happen inside the
// drained delegate on the Unity main thread — never on the network receive thread.
```

### Puppet visual-only construction (inactive-clone discipline)
**Source:** `Client/RemotePuppetFactory.cs:46-79` (clone visual subtree only) + `171-264 CleanPuppetComponents` (whitelist strip) + `Client/RemoteVisualController.cs:138-173 StripExtraLights`
**Apply to:** `PlaySpiritFlameVisual` fake projectile + muzzle VFX
```
- Build fake projectile from visual-only parts (mesh + animator); NO collider/rigidbody/damage scripts
- Instantiate INACTIVE, strip to whitelist BEFORE first activation, then activate (Pitfall 5)
- SFX via transient scene-level GameObject + AudioSource (PlayOneShot + auto-destroy), NOT parented under
  the cleaned puppet (Pitfall 4 — CleanPuppetComponents destroys ALL AudioSources, lines 248-256)
- Puppet whitelist keeps ONLY: SpriteAnimatorWithTransitions, CharacterSpriteMirror, RemotePlayerPuppet,
  RemoteVisualController (+ disabled CharacterAnimationSystem). Everything else is destroyed.
```

### Defensive wire reads + per-packet error containment
**Source:** `Networking/NetworkService.cs:931-946 ExpectMarker/ExpectRemaining` + `497-504 ReceiveLoop catch` + `GameHandlers.cs:238-243` payload guards
**Apply to:** client case-19 branch, server `TryParsePlayerEvent`
```csharp
// Client:
private static void ExpectMarker(BinaryReader reader, int packetId, string name) // NetworkService.cs:931-938
private static void ExpectRemaining(BinaryReader reader, int count, string name) // 940-946
// → length-check before EVERY field (protocol rule 4); throw InvalidDataException on truncation
//    (caught by ReceiveLoop per-packet — sync never stops).
// Server:
if (payload == null || payload.Length < 37 || ReadInt32LE(payload,0) != (int)PacketType.PLAYER_EVENT)
{ _log.Log(Warning, "GAME", "PLAYER_EVENT truncado de " + sender.Id + " (drop)"); return; }
```

### Observability + logging vocabulary
**Source:** `Diagnostics/ReplicationObservability.cs:19-82` + `Client/RemotePlayerPuppet.cs:279-289 LogAnimTransition` + `Plugin/OriCoopPlugin.cs:93-111 LogInfo/LogWarning/IsAnimVerbose`
**Apply to:** detector patch (temporary `[EVENT] SpiritFlame detected`), core publish/dispatch, puppet visual
```csharp
// Verbose-gated transition log (RemotePlayerPuppet.cs:285-288):
string line = string.Format("[ANIM] P{0} recv={1} confirmado={2} motivo={3}", PlayerId, receivedState, _confirmedState, reason);
ReplicationObservability.Record(line);
OriCoopPlugin.LogInfo(line);
// → Mirror as [EVENT] P{id} kind={kind} fase={sent|received|applied} motivo={...}
// Metrics: TrackPacket precedent (throttled 3 s summary) → TrackPlayerEvent equivalent.
```

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Events/PlayerEventCore.cs` publish-half (bus fan-out `Publish → transport`) | service | event-driven | No existing client-side event bus; `Publish(PlayerSnapshot)` in `OriCoopPlugin.cs:288-297` covers snapshot enqueue only. Planner: clone `Publish` null-guard + `_network.Send*` call shape, new `PublishSpiritFlame` entry per D-05. |
| Fake-projectile pooled mover (`Update` straight-line motion + lifetime/despawn) | component | event-driven | No pooled/moving visual exists in `src/` (grep: zero `SeinSpirit`/`SpiritFlame` hits; puppets only lerp in `RemotePlayerPuppet.cs:291-315 Update`). Planner: model on puppet `Update` extrapolation idiom (`goal += _velocity * age`, `dt`-scaled) + `SnapTo`/destroy discipline; keep visual-only. |
| Transient scene-level SFX player | utility | event-driven | No shared SFX helper exists; whitelist actively destroys `AudioSource`s on puppets. Planner: new tiny helper (scene `GameObject` + `AudioSource.PlayOneShot` + auto-destroy) per Pitfall 4, or explicit whitelist addition — former preferred. |
| SmokeProbe packet-19 relay case | test | batch | No existing case for 19 (cases cover handshake/relay/ping/reliable/timeout/token/game). Planner: clone existing relay case, assert byte-identical relay + no echo + drop-old + no `SysAck`. |
| `docs/protocol.md` packet-19 row + `docs/operations.md` pilot checklist | docs | — | Content is new by definition; follow `docs/protocol.md` rules 1-3 (never reuse ID, client+server together, preserve field order) and AGENTS.md docs-index rule (`docs/README.md` update). |

## Metadata

**Analog search scope:** `src/OriCoopPlus/OriCoopBepInEx/{Patches,Networking,Client,Plugin,Domain,Diagnostics}`, `src/OriCoopPlus/OriCoopShared`, `src/OriCoopDedicatedServer/.../Net/{Game,}`, `build.ps1`, phase CONTEXT + RESEARCH
**Files scanned:** 18 source files (full reads: PacketType, NetProtocol, AnimationSyncData, SeinCharacterPatch, PlayerStateReader, NetworkService, OriCoopPlugin, RemotePlayerManager, RemotePlayerPuppet, RemotePuppetFactory, AnimationRegistry, RemoteVisualController, ReplicationObservability, INetworkService, PlayerState, PlayerStateRelay, GameHandlers, NetServerHost dispatch section; skim: SeinInputPatch, AnimationPrewarmPatch, build.ps1)
**Pattern extraction date:** 2026-10-06
**Project constraints honored:** client C# 5 / .NET 3.5 via `build.ps1`+`csc.exe` (no `$""`, no `?.`, `Action` ≤ 4 params, explicit `Instantiate` cast); server .NET 8 via `dotnet build`; `docs/` updated in same change (AGENTS.md); same-build pair + one-way break (D-10); manual 2-client validation, no client unit tests.
