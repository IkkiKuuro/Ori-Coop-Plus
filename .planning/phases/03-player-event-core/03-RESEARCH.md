# Phase 03: player-event-core (PlayerEventCore + piloto Spirit Flame) - Research

**Researched:** 2026-10-06
**Domain:** BepInEx client event core (C# 5 / .NET 3.5) + dedicated server relay (.NET 8) + UDP envelope protocol v2
**Confidence:** HIGH

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Uma categoria por fase. Cada categoria de evento (ataque, VFX, SFX, anim geral, morte etc.) é uma fase completa de implementação — sem "tudo junto" nesta fase.
- **D-02:** Fase 3 = core + piloto. Nem só infraestrutura sem evento real, nem core + categoria completa: entrega o `PlayerEventCore` funcionando ponta a ponta com 1 evento piloto para validar o caminho.
- **D-03:** Piloto = Spirit Flame (ataque de projétil do Ori). Motivo: o mais visível e simples de validar com 2 clientes.
- **D-04:** Só Spirit Flame nesta fase. Stomp/Bash/ChargeJump/Dash/Glide, VFX e SFX genéricos, anims gerais e morte/respawn ficam para fases futuras. O core deve nascer extensível (registro por tipo) para recebê-los sem reescrever.
- **D-05:** Core central (`PlayerEventCore` + bus/registro). Patches Harmony só detectam e chamam o core; o core monta o evento e publica no `NetworkService`. Nada de lógica de rede espalhada nos patches.
- **D-06:** Detecção via Harmony (Prefix/Postfix nas classes de habilidade do Sein que disparam o Spirit Flame), não polling em FixedUpdate. Motivo: eventos discretos e rápidos se perdem no polling; Harmony pega o instante do disparo. (Polling continua existindo para `PLAYER_STATE` 18 — não é substituído.)
- **D-07:** Só o jogador local publica. Filtro estrito (`__instance == Game.Characters.Sein`, mesmo padrão do `SeinCharacterPatch` atual); puppet/remoto nunca publica nem republica — evita eco infinito.
- **D-08:** Reusar o código MP atual de habilidades como base/referência (fluxo de `SKILL` 7 / `SYNC_ABILITY` 10 e classes MP de Spirit/Stomp onde existirem no jogo ou no mod). Não reescrever do zero ignorando o que já funciona; o researcher deve mapear o que existe antes de propor o novo.
- **D-09:** Piloto Spirit Flame = unreliable sequenciado (mesma classe de `PLAYER_STATE` 18, D-09 da fase 2): sem ACK, sem retry. Perda se resolve no próximo tiro. Motivo: tiro é frequente e visual; latência baixa importa mais que garantia por tiro.
- **D-10:** Pacote com ID novo (não reutilizar `SKILL` 7, `SYNC_ABILITY` 10 nem estender `PLAYER_STATE` 18) — **Reversibility:** one-way — redefine o contrato publicado; exige cliente e servidor do mesmo build e `docs/protocol.md` atualizado na mesma mudança (regra D-15 da fase 2).
- **D-11:** Payload mínimo no piloto: direção + posição de origem + timestamp (+ `clientId` no header do envelope, nunca no corpo). Suficiente para o puppet replicar o visual; sem cooldown/alvo/dano nesta fase. Ordem de campos congelada após o merge (regra "preserve a ordem" de `docs/protocol.md`).
- **D-12:** Sem throttle no piloto: cada disparo envia na hora, spam livre. Se o teste com 2 clientes mostrar inundação de UDP, throttle entra como follow-up (agent's discretion calibrar limite).
- **D-13:** Fidelidade visual total sem efeito gameplay: o puppet toca o mesmo clipe + partícula + som do disparo, mas sem dano, sem colisão, sem alterar estado do jogo local. Servidor só repassa bytes (mesma regra D-11 da fase 1).
- **D-14:** Projétil fake agora, física real depois. No piloto o projétil remoto é visual falso (sem colisão/dano); a estrutura (payload, handler, ponto de spawn no puppet) já nasce preparada para a física real que virá na fase de sincronia de entidades/mundo.
- **D-15:** Fail-closed em evento desconhecido: pacote de evento fora do catálogo mantém a última anim válida, nunca chuta para Idle genérico. Coerente com D-14 da fase 1.

### Agent's Discretion
- Nome exato da classe (`PlayerEventCore` vs `PlayerEventBus`), assinatura do registro de listeners, valores de seq/throttle futuro, layout de logs e posição do handler no `NetworkService`/`RemotePlayerManager`: researcher e planner decidem a partir do código atual.
- Mapear via dnSpy/ILSpy qual classe/método real do `Assembly-CSharp` dispara o Spirit Flame (nome do método para o Harmony patch) — está **a confirmar**, não foi verificado nesta discussão.
- Detalhe do VFX/SFX do Spirit Flame no puppet (qual prefab/partícula clonar, qual `AudioClip` tocar, necessidade de `AudioSource` no puppet): descobrir no código/jogo durante research.

### Deferred Ideas (OUT OF SCOPE)
- Stomp, Bash, ChargeJump, Dash, Glide como eventos próprios — fases futuras.
- VFX genéricos e SFX genéricos como categorias próprias — fases futuras.
- Animações gerais além de movimentação — fase futura.
- Morte/respawn, dano/vida/energia — fase futura.
- Física real e dano do projétil remoto + sync de entidades/inimigos do mundo — fase futura de entidades.
- Descoberta LAN, jogo pela internet (NAT/port-forward), submenu "Ori Coop" do pause — futuras fases.

## Project Constraints (from AGENTS.md)

1. `docs/` must be updated on every relevant change (architecture, behavior, protocol, commands, config, build, install, diagnostics, game context); new page when uncovered; mark unconfirmed behavior "a confirmar"; update `docs/README.md` index; register manual/automated tests in `docs/operations.md`. A task is NOT complete if code changed and docs stayed stale.
2. After every successful build, compiled DLL/EXE MUST be deployed immediately to the Ori DE install dir (`<ORI_DIR>\BepInEx\plugins\OriCoopBepInEx.dll`, server files to `<ORI_DIR>\Server\`); close `OriDE.exe`/server before copying if locked. Known install dirs: `C:\Program Files (x86)\Steam\steamapps\common\Ori DE`, `D:\SteamLibrary\steamapps\common\Ori DE` (verified this session: only `C:\...` exists on this machine).
3. Client constraints C# 5 / .NET Framework 3.5 via `build.ps1`+`csc.exe`: no `$""` interpolation, no `?.`, `Action` max 4 params (custom delegates beyond that), `Object.Instantiate` needs explicit cast. Server is .NET 8 / modern C# via `dotnet build`.
4. No automated tests in repo — validation is manual 2-client checklist (`docs/operations.md` + `docs/anim-test-battery.md` T0–T4); server side has automated SmokeProbe (`dotnet run --project SmokeProbe -- --test all`).

## Summary

Phase 3 builds a client-side `PlayerEventCore` (event bus + per-type registry) that detects the local Ori's Spirit Flame shot via a Harmony patch, publishes a minimal event through a NEW unreliable-sequenced packet ID, lets the dedicated server blind-relay the raw bytes, and reproduces a visual-only copy (muzzle clip + particle + sound + fake projectile, zero gameplay effect) on the remote puppet. Fail-closed on unknown events, local-player-only publishing, no throttle in the pilot.

The single biggest unknown going in — the exact `Assembly-CSharp` class/method that fires Spirit Flame — is now **partially resolved by this research**: a raw-strings probe of the installed `Assembly-CSharp.dll` (this session) confirms the ability classes `SeinSpiritFlameAbility`, `SeinStandardSpiritFlameAbility`, `SeinChargeFlameAbility` (all under a `SeinAbility` base) and a candidate fire method named `OnShoot`, plus a `SeinCharacter.SpiritFlame` property (`get_SpiritFlame`/`set_SpiritFlame`) and aim helpers (`ShootDirection`, `GenerateSpiritFlameProjectileOffset`). Wave 0 of the plan must still confirm the exact declaring class + signature via dnSpy/ILSpy (or a runtime dump), but the planner no longer starts from zero — it starts from three concrete candidate targets.

On the wire side everything needed already exists as a pattern to clone: `PLAYER_STATE` 18 is the template for unreliable-sequenced send (`SendSystem(..., 0)`), blind relay (`RelayUnreliableAsync` + `PlayerStateRelay.ShouldRelay` wrap-safe drop-old), and client-side drop-old (`IsNewerThanLast`). The new packet reuses all three mechanisms with a new ID. ID **19** is recommended (next free integer after 18; IDs 1, 2, 3, 5 and negatives −1..−7 are banned forever). The current `SKILL` 7 / `SYNC_ABILITY` 10 flows are reference-only: their senders (`SendSkill`, `SendSyncAbility`) have **zero callers** in the client (verified by grep this session) and their receivers validate-and-discard — D-08 "reuse" means cloning their framing/codec conventions, not wiring into live code.

**Primary recommendation:** Add `PLAYER_EVENT = 19` with frozen field order `int marker(19) + byte eventKind + float dirX/Y/Z + float originX/Y/Z + long timestampTicks`; send via a new `SendUnreliable` client path (never `SendReliable`); relay bytes untouched via `RelayUnreliableAsync`; reproduce visual-only on the puppet through a `PlayerEventCore` registry (`eventKind → handler`), with unknown kinds keeping the last valid anim; patch `SeinSpiritFlameAbility.OnShoot` (Postfix, confirm signature in Wave 0) filtered by `__instance == Game.Characters.Sein` equivalent.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Shot detection (Harmony patch) | Client (BepInEx mod) | — | Only the modded client observes game internals; server never sees game objects |
| Local-player authority filter | Client (BepInEx mod) | — | `Game.Characters.Sein` singleton exists only in-client (D-07) |
| Event bus / registry (`PlayerEventCore`) | Client (BepInEx mod) | — | D-05: core lives in client; patches only detect, core publishes |
| UDP transport + blind relay | Server (.NET 8 dedicated) | — | D-13 + server-rewrite D-11: server reemits bytes, no parse/merge |
| Visual-only reproduction (clip/VFX/SFX/fake projectile) | Client (BepInEx mod, puppet) | — | Gameplay authority stays local; puppet is visual-only by construction (whitelist factory) |
| Fail-closed unknown-event policy | Client (BepInEx mod) | — | D-15 mirrors `AnimationRegistry.Resolve → null → keep last anim` |

## Standard Stack

### Core (no new libraries — in-repo components only)

| Component | Location / Version | Purpose | Why Standard |
|-----------|-------------------|---------|--------------|
| `PacketType` enum + new `PLAYER_EVENT = 19` | `src/OriCoopPlus/OriCoopShared/PacketType.cs` [VERIFIED: src/OriCoopPlus/OriCoopShared/PacketType.cs:1-33] — quote: `POSITION = 1, // LEGACY_REMOVED`, `ANIM = 2, // LEGACY_REMOVED`, `DISCONNECT = 4`, `COLOR = 6`, `SKILL = 7`, `SYNC_ABILITY = 10`, … `PLAYER_STATE = 18` | Single contract shared by client (C# 5) and server (.NET 8); consts-only so it compiles on both | Canonical per `docs/protocol.md` + `docs/architecture.md`; changing client+server together is the enforced rule |
| `NetProtocol` envelope (24B `0x4F43`/v2) | `src/OriCoopPlus/OriCoopShared/NetProtocol.cs` [VERIFIED: src/OriCoopPlus/OriCoopShared/NetProtocol.cs:14-49] — quote: `public const ushort Magic = 0x4F43;`, `public const byte Version = 2;`, `public const int HeaderSize = 24;`, `FlagReliable = 0x01`, `AckRetryMs = 250`, `MaxRetries = 3` | Framing for every datagram; `clientId` in header carries identity, `packetId` mirrors body marker | Only path since phase-2 cutover; no legacy fallback exists |
| `NetworkService` (client transport) | `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` | `SendSystem` unreliable path, `SendReliable`+retry, `ReadServerPacket` dispatch, wrap-safe `IsNewerThanLast`, `WriteLegacyString` | Exclusive transport; new packet plugs a send method + a receive branch here |
| `OriCoopPlugin.Publish` + main-thread queue | `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:288-297] — quote: `public void Publish(PlayerSnapshot snapshot)` + `EnqueueMainThread` at lines 39-47 | Entry point for game-thread → network-thread handoff and network → Unity-main-thread marshaling | D-05 integration point: `PlayerEventCore` publishes here / subscribes here |
| `SeinCharacterPatch` filter pattern | `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs` [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs:7-17] — quote: `[HarmonyPatch(typeof(SeinCharacter), "FixedUpdate")]` … `if (OriCoopPlugin.Instance != null && __instance == Game.Characters.Sein)` | Template for local-player-only detection (D-07) | Exact pattern D-07 mandates reusing |
| `RemotePlayerManager` + `RemotePlayerPuppet` | `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs`, `RemotePlayerPuppet.cs` | Puppet lifecycle and snapshot/animation application; hysteresis (150 ms) + fail-closed (`ApplyConfirmedAnimation` keeps current clip when `targetClip == null`) [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:175-185] | Where the pilot's visual reproduction lands |
| `RemotePuppetFactory` whitelist | `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs` [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs:182-207] — whitelist keeps only `SpriteAnimatorWithTransitions`, `CharacterSpriteMirror`, `RemotePlayerPuppet`, `RemoteVisualController` (+ disabled `CharacterAnimationSystem`); destroys colliders/rigidbodies/AudioSources/all other MonoBehaviours | Guarantees visual-only puppets (D-13) by construction | Any fake-projectile/SFX approach must survive or bypass this whitelist |
| `AnimationRegistry` resolve + fail-closed | `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs:537-552] — quote: `if (TryResolveExact(...)) return ...; if (TryResolveState(...)) return ...; return null;` with comment `fail-closed` | Exact-resolve (hash/name → same shared asset) + state fallback restricted to proven Sein clips; unknown → null → caller keeps last anim | D-15 implementation model for the event-kind catalog |
| `PlayerStateRelay.ShouldRelay` | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/PlayerStateRelay.cs` [VERIFIED: src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/PlayerStateRelay.cs:20-31] — quote: `uint diff = seq - sender.LastRecvSeq; if ((int)diff > 0)` | Server-side unreliable-sequenced gate (D-09 class) | New packet reuses the same gate object or an identical per-sender check |
| `GameHandlers` + `IGameTransport` | `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs` | Per-packet dispatch (`DispatchAsync` switch, `IsGamePacket`), blind-relay calls (`RelayUnreliableAsync` / `BroadcastReliableAsync`), legacy-identical body codecs (`BuildXxxPayload` with leading marker int) | Server-side landing zone for the new packet ID |
| Harmony 2.x (`0Harmony.dll`) | Provided by game install / `API\Client\` (see `build.ps1` ref resolution [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/build.ps1:59-67]) | Method patching (`[HarmonyPatch]`, Prefix/Postfix) | Already the mod's detection mechanism; no alternative exists in this environment |

### Supporting

| Component | Purpose | When to Use |
|-----------|---------|-------------|
| `ReplicationObservability` (ring buffer 200 + `TrackPacket`/`Record`) | Structured transition logs + in-game log viewer source | Extend with event transition reasons (sent → received → applied + motive), mirroring anim D-17 pattern |
| `DummyBot` (`ID 999`, `TriggerAbility`) | Server-local test bot; `AbilityName(15) == "SpiritFlame"` [VERIFIED: src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:717-737] — quote: `case 15: return "SpiritFlame";` | Optional: 2-client manual test aid; NOT the transport for the pilot (dummy broadcasts reliable `SYNC_ABILITY`, wrong class) |
| SmokeProbe (`--test all` → `SMOKE_OK`) | Automated server protocol regression (handshake/relay/ping/reliable/timeout/token/game) | Extend with a relay-preservation case for packet 19 (bytes identical, no echo, drop-old) |
| dnSpy / ILSpy (operator tool, not a dependency) | Confirm exact declaring class + signature of the Spirit Flame fire method | Wave 0 only; never ships with the mod |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New ID 19 | Reuse `SKILL` 7 / `SYNC_ABILITY` 10 with new fields | Rejected by D-10; also both receivers currently validate-and-discard and both are reliable-class (wrong semantics + wasted retries) |
| Unreliable-sequenced (D-09) | Reliable + ACK (D-10 class) | Rejected by D-09: fire-and-forget visual needs low latency, loss heals on next shot; reliable would add 250 ms retry stalls |
| Harmony event detection (D-06) | Polling in `FixedUpdate` | Rejected by D-06: discrete fast shots are lost between polls; polling stays only for `PLAYER_STATE` 18 snapshots |
| Central `PlayerEventCore` + registry (D-05) | Network calls directly inside Harmony patches | Rejected by D-05: scatters transport logic, untestable, breaks future categories |

**Installation:** No new packages. Build commands (unchanged):
```powershell
powershell -ExecutionPolicy Bypass -File .\src\OriCoopPlus\OriCoopBepInEx\build.ps1
dotnet build .\src\OriCoopDedicatedServer\OriCoopDedicatedServer\OriCoopDedicatedServer.csproj --configuration Release
```
Then deploy per AGENTS.md (`<ORI_DIR>\BepInEx\plugins\`, `<ORI_DIR>\Server\`, processes closed first).

**Version verification:** No ecosystem registry applies (C# game-mod project; Harmony/BepInEx/UnityEngine/`Assembly-CSharp` come from the local game install, resolved by `build.ps1`). Toolchain verified this session: `dotnet 8.0.425` present; `Assembly-CSharp.dll` present at `C:\Program Files (x86)\Steam\steamapps\common\Ori DE\oriDE_Data\Managed\`.

## Package Legitimacy Audit

No external packages are installed by this phase. The full dependency set (UnityEngine, Assembly-CSharp, `0Harmony.dll`, `BepInEx.dll`) is supplied by the local Ori DE install and referenced by `build.ps1` — nothing is fetched from any registry. No `npm`/`pip`/`cargo`/`NuGet` additions are recommended.

| Package | Registry | Verdict | Disposition |
|---------|----------|---------|-------------|
| *(none)* | — | — | No new dependencies; audit not applicable |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```text
LOCAL CLIENT (OriDE.exe + OriCoopBepInEx.dll, C# 5)
  Game.Characters.Sein (local player)
      │ fires Spirit Flame
      ▼
  [Harmony Postfix on SeinSpiritFlameAbility.OnShoot]   ← Wave 0 confirms exact target
      │ filter: ability's Sein == Game.Characters.Sein (D-07)
      ▼
  PlayerEventCore.Publish(kind=SpiritFlame, dir, origin, timestamp)   (NEW, D-05)
      │ builds body: int 19 + byte kind + 3×float dir + 3×float origin + long ts
      ▼
  NetworkService.SendPlayerEvent(...) ── unreliable, flags=0, no retry (D-09)
      │ envelope 24B: magic 0x4F43 / v2 / seq++ / clientId / token / packetId=19
      ▼ UDP
DEDICATED SERVER (.NET 8)
  UdpTransport + Channel → Session (token+endpoint check) → GameHandlers.Dispatch
      │ new case 19: length-validate → PlayerStateRelay-style seq gate → blind relay
      ▼ RelayUnreliableAsync(originalDatagram bytes, sender) → all IsReady except sender
      ▼ UDP
REMOTE CLIENT
  NetworkService.ReadServerPacket: case 19 → marker+length check → drop-old per sender
      │ marshal to Unity main thread (EnqueueMainThread)
      ▼
  RemotePlayerManager.HandlePlayerEvent → PlayerEventCore.DispatchLocal(kind, payload)
      │ registry lookup; UNKNOWN kind → keep last anim, log (D-15 fail-closed)
      ▼
  RemotePlayerPuppet.PlaySpiritFlameVisual(origin, dir)
      ├── same attack clip via SpriteAnimatorWithTransitions.SetAnimation (exact resolve)
      ├── muzzle particle (visual-only clone)
      ├── one-shot SFX (transient AudioSource — NOT on puppet, see Pitfall 4)
      └── fake projectile: pure-visual GameObject (mesh+animator only), straight-line,
          pooled, auto-despawn — spawn point + payload shape ready for real physics later (D-14)
```

### Recommended Project Structure

```text
src/OriCoopPlus/OriCoopBepInEx/
├── Events/                        # NEW — PlayerEventCore home (D-05)
│   ├── PlayerEventCore.cs         # Publish(kind,payload) + DispatchLocal + registry
│   ├── PlayerEventKind.cs         # byte enum: SpiritFlame=1, ... (append-only; unknown=0/255 → fail-closed)
│   └── SpiritFlameEventData.cs    # dir/origin/timestamp DTO (Domain-style struct, no Unity deps)
├── Patches/
│   ├── SeinCharacterPatch.cs     # existing FixedUpdate polling (PLAYER_STATE — untouched)
│   └── SpiritFlamePatch.cs       # NEW — Harmony Postfix on fire method, local filter, calls core
├── Networking/NetworkService.cs  # ADD SendPlayerEvent (unreliable) + case 19 receive branch
├── Client/RemotePlayerManager.cs  # ADD HandlePlayerEvent routing to core/puppet
├── Client/RemotePlayerPuppet.cs   # ADD PlaySpiritFlameVisual (clip+VFX+fake projectile)
└── Diagnostics/ReplicationObservability.cs  # ADD event transition counters/reasons
src/OriCoopPlus/OriCoopShared/
├── PacketType.cs                  # ADD PLAYER_EVENT = 19
└── (option) PlayerEventProtocol.cs # shared body codec consts (field offsets/counts), consts-only for C# 5
src/OriCoopDedicatedServer/.../Net/Game/
└── GameHandlers.cs                 # ADD case 19: validate + seq-gate + RelayUnreliableAsync
```

### Pattern 1: Local-only Harmony event detector (D-06 + D-07)

**What:** Postfix patch on the game's fire method; resolves the owning Sein from `__instance`, compares against the singleton, and forwards a DTO to the core. Zero network code in the patch (D-05).
**When to use:** Every future event category (Stomp, Bash, …) clones this file with its own target.
**Example:**
```csharp
// Source: established pattern in src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs:7-17
// [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs:7-17]
[HarmonyPatch(typeof(SeinSpiritFlameAbility), "OnShoot")] // <-- Wave 0 MUST confirm via dnSpy/ILSpy; fallback candidates below
internal static class SpiritFlamePatch
{
    private static void Postfix(SeinSpiritFlameAbility __instance)
    {
        // Local-player filter (D-07). Exact accessor (ability → Sein reference)
        // confirmed in Wave 0; intent: only Game.Characters.Sein publishes.
        if (OriCoopPlugin.Instance == null) { return; }
        SeinCharacter owner = ResolveOwner(__instance); // [ASSUMED] accessor shape — confirm in Wave 0
        if (owner == null || owner != Game.Characters.Sein) { return; }
        PlayerEventCore.PublishSpiritFlame(owner); // core builds DTO + NetworkService send
    }
}
```

### Pattern 2: Unreliable-sequenced game packet (D-09 class)

**What:** Body with leading marker int + frozen field order; sent with flags=0 (no `Reliable`, no pending entry); received with marker check + wrap-safe drop-old per sender; server reemits original datagram bytes untouched.
**When to use:** Pilot packet 19; template for all future high-frequency visual events.
**Example (send side shape to clone):**
```csharp
// Source: src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:195-221 (FlushSnapshot body build + SendSystem(...,0))
// [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:195-221]
using (MemoryStream body = new MemoryStream())
using (BinaryWriter writer = new BinaryWriter(body))
{
    writer.Write((int)PacketType.PLAYER_EVENT); // 19 — leading marker, legacy-identical convention
    writer.Write((byte)PlayerEventKind.SpiritFlame);
    writer.Write(dirX); writer.Write(dirY); writer.Write(dirZ);
    writer.Write(ox); writer.Write(oy); writer.Write(oz);
    writer.Write(timestampTicks);
    writer.Flush();
    SendSystem((int)PacketType.PLAYER_EVENT, body.ToArray(), 0); // flags=0 → unreliable, no retry (D-09)
}
```

### Pattern 3: Fail-closed catalog resolve (D-15)

**What:** Registry lookup returns null for unknown kinds; caller keeps the current clip and logs the reason — never falls back to a generic Idle.
**When to use:** Event-kind dispatch and any future catalog (VFX/SFX IDs).
**Example:**
```csharp
// Source: src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:175-185 + AnimationRegistry.cs:537-552
// [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:175-185]
if (targetClip == null)
{
    // fail-closed: mantém a anim atual (D-15); loga motivo quando verbose
    return;
}
```

### Anti-Patterns to Avoid

- **Network logic inside Harmony patches:** violates D-05; patches detect + call `PlayerEventCore`, nothing more.
- **Reusing `SendReliable` for the pilot:** violates D-09; reliable retries stall visual shots by 250 ms×3 and the server treats the ID as critical (ACK tracking overhead). Use a new `SendUnreliable` path.
- **`clientId`/`playerId` inside the body:** violates D-11 + legacy convention — identity travels in the envelope header (`clientId`); the server relay preserves the sender's header and reemits body bytes verbatim.
- **Spawning gameplay prefabs on the puppet:** violates D-13; the factory whitelist destroys colliders/rigidbodies/AudioSources/non-visual MonoBehaviours — a cloned real projectile would either be gutted or, worse, execute gameplay code before cleanup. Build the fake from visual-only parts.
- **Changing field order after merge:** violates D-11 + `docs/protocol.md` rule 3 ("Preserve a ordem dos campos"). Freeze order in the same change that updates `docs/protocol.md`.
- **Echo/republish on receive:** violates D-07; remote handling must never call publish (infinite echo across 2 clients).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Envelope framing / seq / ACK / retry | Custom UDP header | `NetProtocol` consts + `NetworkService.SendSystem`/`SendReliable` + server `EnvelopeCodec`/`AckTracker` | Wrap-safe seq, token/endpoint validation, sweeper, and retry timing are already hardened and SmokeProbe-covered |
| Wrap-safe "is newer" comparison | `seq > last` naive compare | `(int)(nova - ultima) > 0` pattern (`PlayerStateRelay.ShouldRelay`, `NetworkService.IsNewerThanLast`) | Naive compare breaks at uint32 wraparound; the one-line idiom is already correct in two places |
| Legacy string encoding | `BinaryWriter.Write(string)` | `WriteLegacyString` (`int32 length` + ASCII) | Bare `Write(string)` emits LEB128 prefix — the documented cause of `Could not read value of type 'string'!` |
| `Action<...>` with >4 params (C# 5/3.5) | `Action<a,b,c,d,e,...>` events | Custom delegate like `ConfigSyncHandler` [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Domain/INetworkService.cs:6-10] | Unity 5.3 mscorlib does not guarantee `Action` beyond 4 params |
| Fail-open fallbacks | "Unknown → play Idle" | `AnimationRegistry.Resolve → null → keep last` + reason log | Fail-open caused the "random enemy sprite" bug class; fail-closed is decided (D-14 fase 1, D-15 here) |
| Projectile physics for the pilot | Real rigidbody/collision/damage on remote copy | Visual-only straight-line fake (D-14) | Real physics belongs to the future entities/world-sync phase; premature physics risks desync + NRE cascades in stripped puppets |

**Key insight:** This phase is 80% wiring existing hardened mechanisms (envelope, unreliable relay, drop-old, whitelist puppets, fail-closed resolve) to one new event source. The only genuinely new code is the detector patch + registry + fake-projectile visual. Anything that looks like new infrastructure (framing, reliability, session) already exists — reuse it.

## Common Pitfalls

### Pitfall 1: Patching the wrong overload / method (silent no-fire)
**What goes wrong:** Patch compiles and applies, but the pilot never fires because the real shot path is `SeinStandardSpiritFlameAbility` override, not the base `SeinSpiritFlameAbility.OnShoot` — or `OnShoot` is the animation callback while projectiles spawn elsewhere.
**Why it happens:** Assembly-CSharp has at least three flame classes (`SeinSpiritFlameAbility`, `SeinStandardSpiritFlameAbility`, `SeinChargeFlameAbility`) [VERIFIED: Assembly-CSharp.dll strings probe, this session] — quote: `SeinAbility`, `SeinChargeFlameAbility`, `SeinSpiritFlameAbility`, `SeinStandardSpiritFlameAbility`.
**How to avoid:** Wave 0: confirm hierarchy + which method actually spawns `StandardSpiritFlameProjectile` via dnSpy/ILSpy; add a temporary verbose log line in the patch (`[EVENT] SpiritFlame detected`) and verify it fires on local shot before wiring network.
**Warning signs:** 2-client test shows movement sync but zero flame events in `LogOutput.log`.

### Pitfall 2: Reliable-send by copy-paste (latency + server critical-path mismatch)
**What goes wrong:** Cloning `SendSyncAbility` (which calls `SendReliable`) makes every shot a critical packet: 250 ms retries, ACK tracking per shot, and server `IsCriticalPacket`/`GameHandlers` reliable handling.
**Why it happens:** `SendSyncAbility`/`SendSkill` are the closest-looking examples and the easiest to copy [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:322-337].
**How to avoid:** New `SendPlayerEvent` method calling `SendSystem(id, body, 0)` (flags=0); server `IsGamePacket`/`DispatchAsync` routes 19 to `RelayUnreliableAsync`, and 19 is NOT added to `IsCriticalPacket` [VERIFIED: server critical list at src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:554-566].
**Warning signs:** Shots arrive in bursts ~250 ms apart; server log shows ACK/retry churn per shot.

### Pitfall 3: Reusing one `_lastRelaySeq` domain across packet types (cross-type starvation)
**What goes wrong:** Client `IsNewerThanLast` keys drop-old by sender only. PLAYER_STATE seqs and EVENT seqs share one counter space per sender if the same `NextSeq()` feeds both — a burst of shots can make subsequent snapshots look "old" (or vice versa), dropping movement.
**Why it happens:** `NetworkService` has a single `_sendSeq` and `_lastRelaySeq` keyed by `senderId` only [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:48-57,948-964].
**How to avoid:** Use a separate seq domain for events (separate send counter + separate last-seen dict per sender on receive, and on the server a separate relay gate per packet class — e.g. second `PlayerStateRelay` instance or a per-packetId gate). Planner decides exact shape; requirement is explicit: event seqs must never suppress snapshots.
**Warning signs:** Puppet freezes/moves in steps while shots are being fired rapidly.

### Pitfall 4: SFX silently stripped by the puppet whitelist
**What goes wrong:** Cloning the muzzle/shot prefab under the puppet results in silence: `CleanPuppetComponents` destroys ALL `AudioSource`/`AudioListener` [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs:248-264], and `StripExtraLights` removes lights/haloss.
**Why it happens:** The whitelist keeps only renderers/animators/mirror/puppet/controller.
**How to avoid:** Play the shot sound from a transient scene-level GameObject with its own `AudioSource` (PlayOneShot + auto-destroy), NOT parented under the cleaned puppet — or a shared SFX player component added to the whitelist explicitly. Same caution for light/flare eye-candy.
**Warning signs:** Visual + projectile appear on remote, no sound; no errors in log (destruction is silent by design).

### Pitfall 5: Fake projectile inherits gameplay (damage/collision) or NRE-loops
**What goes wrong:** Instantiating the real `StandardSpiritFlameProjectile` prefab brings its damage/collision scripts; even if the factory later strips them, `Awake`/`OnEnable` may run at instantiation and touch singletons or throw per-frame NREs (the documented 1–5 FPS cascade class).
**Why it happens:** Prior incidents (`SeinPrefabFactory` instantiating nested prefabs, `SeinDamageReciever` NRE loops) are recorded in `docs/operations.md` diagnóstico.
**How to avoid:** Instantiate the projectile prefab **inactive**, strip to visual-only BEFORE first activation (same inactive-clone discipline as the puppet factory), move it along a straight line in `Update`, pool or destroy after ~lifetime/range; never attach damage/collider components. D-14 explicitly blesses visual-only.
**Warning signs:** FPS collapse when remote shoots; `NullReferenceException` floods mentioning projectile/damage classes.

### Pitfall 6: Breaking C# 5 in new client code (build passes nowhere useful)
**What goes wrong:** `build.ps1` uses `csc.exe` (Framework v4.0.30319) with `/noconfig` [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/build.ps1:44-48,74-96] — `$""`, `?.`, expression-bodied members, `Action`>4 params, and uncast `Instantiate` all fail or misbehave.
**Why it happens:** Muscle memory from modern C# (server code is .NET 8 and allows everything).
**How to avoid:** Review checklist on every new client file: `string.Format` only, explicit null checks, custom delegates, `as GameObject` casts. The planner should add a build-verification step running `build.ps1` (not just `dotnet build`) for client changes.
**Warning signs:** `build.ps1` exit code ≠ 0; error text mentioning `$` or `?.`.

### Pitfall 7: Forgetting the same-build + docs rule (D-10 one-way break)
**What goes wrong:** Client sends 19 but server drops it as unknown (or vice versa); old builds interoperating produce silent desync.
**Why it happens:** New packet ID redefines the published contract — `docs/protocol.md` rules 1–3 («Nunca reutilize um ID… Mude cliente e servidor juntos… Preserve a ordem») [CITED: docs/protocol.md — Regras para mudancas].
**How to avoid:** Same change must: add `PLAYER_EVENT = 19` to shared `PacketType.cs`, implement both ends, update `docs/protocol.md` (new row + payload field-order table), build + deploy both binaries, test 2-client. No fallback for old builds, by decision.
**Warning signs:** `Reject`/unknown-packet drops in server log; one client shows shots, other doesn't.

## Code Examples

### New packet receive branch (client) — shape to clone

```csharp
// Source: src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:773-797 (PLAYER_STATE branch)
// [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:773-797]
else if (packetId == (int)PacketType.PLAYER_EVENT) // 19 — NEW branch beside PLAYER_STATE
{
    int marker = reader.ReadInt32();
    if (marker != (int)PacketType.PLAYER_EVENT)
    {
        throw new InvalidDataException("PLAYER_EVENT sem marcador 19.");
    }
    if (!IsNewerThanLastEvent(headerClientId, seq)) // separate seq domain — see Pitfall 3
    {
        return;
    }
    // read: byte kind + dir(3×float) + origin(3×float) + long timestamp, with
    // ExpectRemaining length checks before each read (protocol rule 4)
    ...
    // marshal to main thread via OriCoopPlugin.EnqueueMainThread → RemotePlayerManager.HandlePlayerEvent
}
```

### Server dispatch + blind relay (shape to clone)

```csharp
// Source: src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:191-227 (HandlePlayerStateAsync)
// [VERIFIED: src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:191-227]
if (!TryParsePlayerEvent(payload, out kind, out dx, out dy, out dz, out ox, out oy, out oz, out ts))
{
    _log.Log(ServerLogLevel.Warning, "GAME", "PLAYER_EVENT truncado de " + sender.Id + " (drop)");
    return;
}
if (!_eventRelay.ShouldRelay(sender, seq)) // dedicated gate instance — see Pitfall 3
{
    return;
}
await _transport.RelayUnreliableAsync(originalDatagram, sender, ct).ConfigureAwait(false);
```

### Puppet visual reproduction entry (shape to clone)

```csharp
// Source: src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:94-136 (ApplySnapshotDirect hysteresis gate)
// [VERIFIED: src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:94-136]
public void PlaySpiritFlameVisual(Vector3 origin, Vector3 direction)
{
    // 1. same attack clip via _animator.SetAnimation(clip, true) — resolve via AnimationRegistry exact
    // 2. muzzle particle: visual-only clone at origin, auto-destroy
    // 3. SFX: transient scene-level AudioSource (NOT under puppet — see Pitfall 4)
    // 4. fake projectile: pooled visual-only GameObject, straight-line Update motion (D-14)
    // 5. unknown kind never reaches here — core filters fail-closed (D-15)
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `SKILL` 7 / `SYNC_ABILITY` 10 live ability sync (reliable, `fromId` stamped in body) | Dead paths: senders have zero callers, receivers validate-and-discard | Phase-2 cutover (2026-10-05) | D-08 "reuse" = clone framing conventions only; do not resurrect these IDs for the pilot |
| `POSITION` 1 + `ANIM` 2 fragmented streams | `PLAYER_STATE` 18 unified (pos+state+flags+hash+speeds+nick, marker-led) | Anim rework (D-09/D-12 fase 1) | Packet 19 follows the 18 shape (marker-led, header identity, byte-identical relay) |
| Per-packet `playerId`/`fromId` in body | Identity in envelope `clientId`; server reemits bytes verbatim | Phase 2 (D-02/D-15) | Packet 19 body carries NO sender id (D-11); note `SKILL`/`SYNC_ABILITY` server relays still stamp `fromId` — 19 must NOT copy that (it would break byte-identical relay) |
| Cloning full Sein for puppets | Lightweight visual-subtree puppets + whitelist strip | 2026-10-05 (bug #2 fixes) | Fake projectile + VFX must be visual-only from birth (Pitfalls 4–5) |
| Spirit Flame target unknown ("a confirmar") | Candidates confirmed via assembly strings probe (this session): `SeinSpiritFlameAbility`, `SeinStandardSpiritFlameAbility`, `OnShoot`, `get_SpiritFlame`, `ShootDirection`, `GenerateSpiritFlameProjectileOffset`, `StandardSpiritFlameProjectile` | 2026-10-06 (this research) | Wave 0 narrows to exact declaring class + signature via dnSpy/ILSpy instead of blind search |

**Deprecated/outdated:**
- `OriCoopDedicatedServer.Core` + old `Game/` path: deleted 2026-10-05 — never reference, never re-create `Server.cs`/`Client.cs`/`Packet.cs` legados.
- `SeinSpiritMP`/`SeinStompMP` MP classes mentioned in `docs/game-and-mod.md` vocabulary: no such files exist under `src/` (verified by grep this session — zero `SeinSpirit`/`SeinAbility` hits in repo source). Treat as aspirational vocabulary, not existing code.
- `SendSkill`/`SendSyncAbility` client senders: exist but uncalled — reference material, not live flow.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `OnShoot` is the exact method that spawns the Spirit Flame projectile (vs. animation callback or inherited stub) | Architecture Patterns / Pitfall 1 | Patch never fires → pilot dead; mitigated by Wave-0 dnSpy confirmation + verbose detect log |
| A2 | The owning `SeinCharacter` is reachable from the ability `__instance` (field/property) for the D-07 filter | Architecture Patterns (Pattern 1) | Filter can't be implemented as sketched; fallback: patch at `SeinCharacter` level or compare via `Game.Characters.Sein` ability reference — Wave 0 confirms |
| A3 | Aim direction is readable at fire time (via `ShootDirection`-like member or facing fallback) | Code Examples / D-11 payload | Payload dir degrades to facing-based ±X; still shippable for pilot, refined later |
| A4 | `StandardSpiritFlameProjectile` prefab (or its visual parts) is clonable inactive for the fake without side effects | Pitfall 5 | Fake must be built from generic sprite/particle parts instead; visual fidelity slightly lower |
| A5 | New ID 19 is free on both ends (no hidden legacy use) | Standard Stack | Verified in shared enum + protocol tables (1,2,3,5,negatives banned; 4,6,7,10–18 taken; 100–106 system) — collision risk minimal, but server `DispatchAsync` default branch silently drops unknowns, so a clash would be silent |
| A6 | Per-packet seq-domain separation is required (single shared counter would starve snapshots) | Pitfall 3 | If both streams share `NextSeq` but receivers key drop-old per (sender,packetId), sharing is harmless — planner to verify against actual `NextSeq`/`IsNewerThanLast` wiring; worst case adds a small refactor |

## Open Questions

1. **Exact Harmony target (class + method signature)**
   - What we know: classes `SeinSpiritFlameAbility` / `SeinStandardSpiritFlameAbility` / `SeinChargeFlameAbility` exist; method name `OnShoot` exists; `SeinCharacter` exposes `get_SpiritFlame`; aim helpers `ShootDirection`, `GenerateSpiritFlameProjectileOffset` exist [VERIFIED: Assembly-CSharp.dll strings probe, this session].
   - What's unclear: which class declares the projectile-spawning method, its parameters, and how to reach the owning Sein from `__instance`.
   - Recommendation: Wave 0 task — dnSpy/ILSpy on local `Assembly-CSharp.dll` (present at `C:\Program Files (x86)\Steam\...\oriDE_Data\Managed\`); fallback: runtime dump patch logging `new StackTrace` / слот names at shot time. Gate all patch code on this answer.

2. **Which `AudioClip` / particle prefab identifies the Spirit Flame shot**
   - What we know: whitelist strips `AudioSource`s from puppets (so SFX needs scene-level playback); `AnimationRegistry.CollectClips` reflection pattern can enumerate referenced assets.
   - What's unclear: exact prefab/clip names for the muzzle + projectile + shot sound.
   - Recommendation: Wave 0 — same dnSpy pass on `SeinStandardSpiritFlameAbility` fields + `ShootingSound`/`ShootEffect`-like members (names seen in assembly strings); confirm in-game with verbose log.

3. **Throttle follow-up threshold (D-12)**
   - What we know: no throttle in pilot by decision; spam is free.
   - What's unclear: at what fire rate 2-client UDP shows flooding.
   - Recommendation: instrument `ReplicationObservability` with event counters now; calibrate only if the manual 2-client test shows flooding. Not a planning blocker.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK (`dotnet build` server) | Server build + SmokeProbe | ✓ | 8.0.425 (verified this session) | Historical Roslyn-toolset fallback in `docs/operations.md` (not needed) |
| `csc.exe` (.NET Framework, client build) | `build.ps1` client compile | ✓ (path `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` per build.ps1) | — | `dotnet build` of `OriCoopBepInEx.csproj` if compatible SDK present |
| Ori DE install + `oriDE_Data\Managed` (`Assembly-CSharp.dll`, `UnityEngine.dll`, `0Harmony.dll`) | Client refs + Wave-0 dnSpy target | ✓ | `C:\Program Files (x86)\Steam\steamapps\common\Ori DE` present (verified); `D:\SteamLibrary\...` absent | — |
| BepInEx 5.x (`BepInEx.dll`, config + `Preloader.Entrypoint`) | Plugin load | ✓ (per docs; entrypoint `Assembly-CSharp/LoadingBootstrap.Awake`) | — | — |
| UDP port 7777 (default) + LAN IPv4 | 2-client manual test | ✓ (assumed free; SmokeProbe uses 7779/7790 for automated) | — | `--port` override |
| dnSpy / ILSpy (operator workstation) | Wave-0 target confirmation | [ASSUMED] present or installable | — | Runtime dump-patch fallback (log stack/field names at shot time) |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** dnSpy/ILSpy — fallback is a temporary in-game dump patch (acceptable, slightly slower).

## Validation Architecture

No formal `workflow.nyquist_validation` key exists (no `.planning/config.json` in repo — verified this session), so per the researcher contract this section is included with the repo's actual validation means.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None (repo has zero automated client tests) + SmokeProbe (server, `dotnet run --project SmokeProbe -- --test all` → `SMOKE_OK`) |
| Config file | none — see Wave 0 |
| Quick run command | `dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7779 --test relay` (add a packet-19 relay case) |
| Full suite command | `dotnet run --project .\src\OriCoopDedicatedServer\SmokeProbe\SmokeProbe.csproj -- --port 7779 --test all` + manual 2-client checklist |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-06/D-07 | Local shot fires event; remote/puppet never publishes | manual (2-client + log) | `LogOutput.log` shows `[EVENT] SpiritFlame detected` only on shooter | ❌ Wave 0 (add verbose detect log) |
| D-09 | Shot travels unreliable, no retry; loss heals next shot | automated (new SmokeProbe case) + manual | `--test relay` extended: 19 relayed byte-identical, no `SysAck` generated | ❌ Wave 0 (new case) |
| D-11 | Body field order frozen; clientId header-only | automated parse test | `TryParsePlayerEvent` round-trip (server) + client branch | ❌ Wave 0 |
| D-13 | Puppet shows clip+VFX+SFX+fake, zero gameplay effect | manual (2-client) | Checklist: remote sees shot; local game state unchanged (no damage/world change) | ❌ Wave 0 (checklist entry in `docs/operations.md`) |
| D-15 | Unknown kind keeps last anim + logs reason | manual + verbose log | Send crafted unknown kind (SmokeProbe or temp patch); puppet pose unchanged | ❌ Wave 0 |
| D-10 | Same-build pair; protocol doc updated | process (build+deploy both, update `docs/protocol.md` + `docs/README.md` index) | `build.ps1` + `dotnet build` both green; binaries deployed per AGENTS.md | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `build.ps1` (client) and/or `dotnet build` (server) must stay green — the change's side only.
- **Per wave merge:** full `dotnet build OriCoopPlus.sln` + SmokeProbe `--test all` green.
- **Phase gate:** 2-client manual run (shots visible both directions, no echo, disconnect/reconnect clean) + `docs/protocol.md` + `docs/operations.md` updated before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] dnSpy/ILSpy confirmation of exact Harmony target (class + signature + owner accessor + aim source) — blocks `SpiritFlamePatch.cs`
- [ ] Verbose `[EVENT]` detect log + `ReplicationObservability` event counters — blocks testability of D-06/D-07
- [ ] SmokeProbe packet-19 relay case (byte-identical, no-echo, drop-old, no-SysAck) — blocks D-09 automation
- [ ] `docs/operations.md` pilot checklist entry (2-client shot validation + disconnect/reconnect) — blocks phase gate
- [ ] Framework install: none needed (no new test framework; manual + SmokeProbe only)

## Security Domain

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | Partial | Session token + fixed-endpoint check per datagram (existing); new packet inherits automatically — no new auth surface |
| V3 Session Management | Partial | `IsReady` gate + sweeper + `Reject` on mismatch (existing); 19 must only relay between `IsReady` sessions (same as 18) |
| V4 Access Control | No | No privileged operation in pilot (visual-only); no gating config (unlike `SYNC_*` + `ShareAbilities`) — intentional per D-12/D-13 |
| V5 Input Validation | **Yes** | Length-check before every read (`ExpectRemaining`/`TryParse` → drop + `Warning`), marker check, fail-closed unknown kind; fuzz-shaped packets must never throw out of the handler (existing handlers throw `InvalidDataException` caught by receive loop — keep same discipline) |
| V6 Cryptography | No | No secrets in pilot payload (positions/directions are public game state); no new crypto |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed 19-body crash / over-read | Tampering / DoS | `payload.Length` validation before each field (protocol rule 4); `nick`-style length caps if any string is ever added (none in pilot) |
| Seq-spoof / replay freezing puppet | Spoofing | Wrap-safe drop-old per (sender, packet-domain) + token/endpoint session check; no state-changing action on event receipt |
| Shot-flood UDP amplification | DoS | D-12 accepts spam for pilot; server does no per-packet work beyond validate+relay (no retry/ACK state for 19); throttle follow-up if manual test shows flooding |
| Fake-event injection (shots from nobody) | Spoofing | Events only accepted from `IsReady` sessions; renderer shows them as the sender's puppet — no authority claim beyond "this puppet shot" (visual only, no damage) |

## Sources

### Primary (HIGH confidence)
- Repo source read this session: `PacketType.cs:1-33`, `NetProtocol.cs:14-49`, `NetworkService.cs` (send paths 195-221/269-337, receive 689-929, seq 948-964), `SeinCharacterPatch.cs:7-17`, `PlayerStateReader.cs`, `OriCoopPlugin.cs:288-297` + main-thread queue, `RemotePlayerManager.cs`, `RemotePlayerPuppet.cs:94-202`, `RemotePuppetFactory.cs:182-264`, `AnimationRegistry.cs:537-552`, `INetworkService.cs:6-10`, `PlayerState.cs`, `GameHandlers.cs:191-227/291-354/717-737`, `PlayerStateRelay.cs:20-31`, `NetServerHost.cs:554-566`, `DummyBot.cs`, `build.ps1`, `ReplicationObservability.cs`
- `Assembly-CSharp.dll` raw-strings probe (this session, PowerShell regex over installed game DLL): confirmed `SeinAbility`, `SeinChargeFlameAbility`, `SeinSpiritFlameAbility`, `SeinStandardSpiritFlameAbility`, `OnShoot`, `get_SpiritFlame`/`set_SpiritFlame`, `CurrentSpiritFlame`, `ShootDirection`, `GenerateSpiritFlameProjectileOffset`, `StandardSpiritFlameProjectile`, `RegularSpiritFlame`, `SeinSpiritFlameTargetting`, `AllowSpiritFlameTargetting`, `ShootingSound`, `ShootEffect`
- Grep verification this session: zero `SeinSpirit|SeinAbility|SpiritFlame` hits in `src/` (no existing mod-side ability code); zero callers of `SendSyncAbility`/`SendSkill` (dead send paths)

### Secondary (MEDIUM confidence)
- [CITED: docs/protocol.md] — envelope 24B, ID tables + never-reuse list, reliability classes, body-marker + order-freeze rules
- [CITED: docs/architecture.md] — BepInEx/server split, same-build rule, init cycle
- [CITED: docs/bepinex-architecture.md] — plugin layers, C# 5 restrictions, entrypoint config
- [CITED: docs/operations.md] — builds, deploy paths, 2-client checklists, known-bug classes (NRE cascades, whitelist rationale, C# 5 limits)
- [CITED: docs/anim-sync-CONTEXT.md] — D-05..D-17 (enum authority, fail-closed, on-change+heartbeat, blind relay, no-legacy)
- [CITED: docs/server-rewrite-CONTEXT.md] — D-02/D-09..D-15 (envelope, unreliable vs ACK+retry, Transport/Session/Game layers, one-way ID redesign)
- [CITED: docs/game-and-mod.md] — vocabulary (ability/pickup/world-event), MPSettings, limits
- [CITED: docs/code-map.md] — class locations

### Tertiary (LOW confidence)
- None relied upon. All game-internals claims above come from the strings probe (primary), not training memory; dnSpy-level specifics (signatures, field layouts) are explicitly deferred to Wave 0 rather than asserted.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every component read in-repo this session with line-verified quotes; no registry packages involved.
- Architecture: HIGH — patterns are clones of live, hardened flows (18-send/relay/receive, whitelist puppets, fail-closed resolve); only the detector target needs Wave-0 confirmation.
- Pitfalls: HIGH — each pitfall traces to a verified code site or a documented incident class in `docs/operations.md`.
- Patch target + VFX/SFX specifics: MEDIUM — assembly-strings evidence is strong but signatures/asset names await dnSpy/ILSpy (tracked as Open Questions + Wave 0 gaps, never asserted as fact).

**Research date:** 2026-10-06
**Valid until:** 2026-11-05 (stable domain; game DLLs don't change under the mod — re-probe only if Ori DE updates)

## RESEARCH COMPLETE

**Phase:** 03 - player-event-core
**Confidence:** HIGH (MEDIUM on exact Harmony signature + VFX/SFX asset names — gated as Wave 0)

### Key Findings
1. **Patch targets confirmed in the real game DLL** (strings probe this session): `SeinSpiritFlameAbility`, `SeinStandardSpiritFlameAbility`, `SeinChargeFlameAbility`, candidate fire method `OnShoot`, `SeinCharacter.SpiritFlame` property, aim helpers (`ShootDirection`, `GenerateSpiritFlameProjectileOffset`), projectile `StandardSpiritFlameProjectile`. Wave 0 confirms exact signature via dnSpy/ILSpy.
2. **D-08 "reuse" is reference-only**: `SendSkill`/`SendSyncAbility` have zero callers and their receivers validate-and-discard; no `SeinSpirit*` code exists in `src/`. Clone their framing, don't wire into them.
3. **Recommended contract**: `PLAYER_EVENT = 19` (next free ID), body `int 19 + byte kind + dir(3f) + origin(3f) + timestamp(long)`, order frozen; unreliable-sequenced end to end; separate seq domain for events (Pitfall 3).
4. **Two hard integration constraints discovered**: puppet whitelist destroys `AudioSource`s (SFX must be scene-level transient) and all non-visual components (fake projectile must be visual-only from birth, instantiated inactive).
5. **No new dependencies, no test framework to install**: C# 5 client (`build.ps1` lint discipline) + .NET 8 server; validation = SmokeProbe extension (packet-19 relay case) + manual 2-client checklist + `docs/protocol.md` update in the same change (D-10 one-way rule).

### File Created
`C:/Users/raiso/Documents/GitHub/Ori-Coop-Plus/.planning/phases/03-player-event-core/03-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | All components read + line-quoted this session; no external packages |
| Architecture | HIGH | Clones of live hardened flows; responsibility map prevents tier misassignment |
| Pitfalls | HIGH | Each grounded in verified code sites or documented incidents |
| Harmony target + assets | MEDIUM | Strong strings-probe evidence; signatures/names need Wave-0 dnSpy pass |

### Open Questions
Wave-0 dnSpy/ILSpy pass (exact fire-method signature + owner accessor + aim source + clip/prefab/sound names); throttle calibration only if 2-client test floods (D-12 follow-up).

### Ready for Planning
Research complete. Planner can now create PLAN.md files.
