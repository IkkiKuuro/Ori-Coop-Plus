---
last_mapped_commit: 9816477729158db1007480b98cb16f4d49eb5cfb
last_mapped_at: 2026-10-09
---
# Codebase Concerns

**Analysis Date:** 2026-10-09

Scope audited: full repository. Primary source of truth: `src/OriCoopPlus/`
(BepInEx client, ~4.6k lines) and `src/OriCoopDedicatedServer/`
(.NET 8 server, ~3.6k lines), plus `src/OriCoopPlus/OriCoopShared/`
(build-time shared contract). The repo is a fork of a Unity multiplayer
launcher ("WW Launcher"); the Ori Coop Plus code is a small island inside it
(50 tracked `.cs` files vs. 28 tracked binaries).

---

## Tech Debt

### Repo is a launcher fork carrying 28 tracked binaries and 6 unrelated games

- Issue: `Modules/`, `API/`, `Launcher.zip`, `dump.exe`, `dump2.exe` and the
  root catalogs (`AllGames.txt`, `AllMods.txt`, `WWGames.txt`, `CHANGELOGS/`,
  `ICONS/`) belong to the parent launcher project. The Ori-specific code is only
  under `src/`. `Modules/` alone ships prebuilt client/server DLLs for COTL, DD,
  FARLS, HK, ORIWOTW and WW, including a `BACKUP_ORIGINAL/` folder of ORIDE DLLs
  (`Modules/ORIDEModules/BACKUP_ORIGINAL/`).
- Files: `Modules/**`, `API/**`, `Launcher.zip`, `dump.exe`, `dump2.exe`,
  `AllGames.txt`, `AllMods.txt`, `WWGames.txt`, `apiVersion.txt`,
  `LatesVersion.txt`
- Impact: clone size and noise; a contributor cannot tell which binaries are
  authoritative; a stale `Modules/ORIDEModules/*.dll` can be mistaken for the
  BepInEx plugin described in `README.md` and deployed by mistake.
- Fix approach: move launcher-only assets to a separate repo or decouple them
  into a git submodule / separate release artifact; if they must stay, add a
  root `README` section and a `.gitattributes` marking them as
  generated/release payloads and never build inputs.

### Two divergent build paths for the same client sources

- Issue: the client compiles two different ways. `dotnet build` uses
  `src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj` (`net35`,
  `<LangVersion>7.3</LangVersion>`), while `build.ps1` shells out to
  `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` with
  `/langversion:default` (C# 5) and `/nostdlib+`. `build.ps1` also omits the
  `UnityEngine.UI.dll` reference the csproj declares and does not reference
  `Microsoft.NETFramework.ReferenceAssemblies`.
- Files: `src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj`,
  `src/OriCoopPlus/OriCoopBepInEx/build.ps1`
- Impact: any C# 6/7 syntax (`$""`, `?.`, expression-bodied members) compiles
  in Visual Studio/MSBuild but breaks `build.ps1`, which is the documented
  deploy path in `docs/operations.md` and `scripts/build.rsp`. A green solution
  build proves nothing about the deployed DLL.
- Fix approach: make MSBuild the single build path (it already emits the exact
  `bin/Release/OriCoopBepInEx.dll`), demote `build.ps1` to a thin
  `dotnet build` wrapper, and add a CI check that runs both.

### README and docs reference a server assembly that no longer exists

- Issue: `README.md` instructs copying `OriCoopDedicatedServer.Core.dll` from
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer.Core/bin/Release/net8.0`
  into `<ORI_DIR>\Server\`. That project was deleted in the 02-04 cutover
  (see `src/OriCoopDedicatedServer/OriCoopDedicatedServer/OriCoopDedicatedServer.csproj`
  comment, `docs/code-map.md:28-30`, `docs/operations.md:58-59`). The real output
  is `OriCoopDedicatedServer.exe` + `.dll` next to it.
- Files: `README.md:84-99`, `README.md:246-249`, `docs/operations.md:58-59`
- Impact: installation per the README is impossible; missing DLL at runtime.
- Fix approach: delete the `Core.dll` paragraphs from `README.md` and keep the
  authoritative list in `docs/operations.md` (which is already correct).

### `Scripts/` is 40+ ad-hoc reverse-engineering scratch files

- Issue: `scripts/` holds one-off PowerShell inspectors
  (`inspect_sein.ps1`, `inspect_sein2.ps1`, `inspect_ta.ps1`,
  `dump_complete.ps1`, `dump_preload.ps1`, `dump_resolved.ps1`, …) plus committed
  output dumps (`all_members_dump.txt`, `strings_found.txt`,
  `all_types_resolved.txt`, `disasm_out.txt`). Several are near-duplicates of
  each other.
- Files: `scripts/*.ps1`, `scripts/*.txt`, `scripts/build.rsp`
- Impact: no way to tell which inspector produced the assumptions the code
  relies on (the `SeinSpiritFlameAbility.ThrowSpiritFlames` patch target, the
  frustum-optimizer type names); the dumps are a snapshot of one specific
  Assembly-CSharp build and will silently rot.
- Fix approach: keep one canonical `scripts/inspect_game.py/ps1` with a header
  stating the Assembly-CSharp build it was run against; move the static dumps to
  `docs/` as dated reference or delete them.

### Verbatim-duplicated protocol codecs across client and server

- Issue: the wire codecs are written twice with no shared implementation. The
  client builds/parses the 24-byte envelope with `BinaryWriter`/`BinaryReader`
  (`NetworkService.BuildEnvelope`, `NetworkService.ReadServerPacket`), while the
  server uses `BinaryPrimitives` + a separate `EnvelopeCodec`. `GameHandlers`
  also reimplements `BuildChatPayload`/`StripBrackets`/`ReadFloat`/`WriteFloat`
  that `NetServerHost` already has. Chat payload construction is literally
  copy-pasted three times (`NetServerHost.BuildChatPayload`,
  `GameHandlers.BuildChatPayload`, and the inline loop in
  `NetServerHost.HandleChatAsync`).
- Files: `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/EnvelopeCodec.cs`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:822-833`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:425-436`
- Impact: every protocol change (a new field in `PLAYER_STATE`, a chat layout
  tweak) must be edited in 2-4 places; the byte offsets are already duplicated
  as constants in `NetProtocol` + `PlayerEventProtocol` + hardcoded literals in
  `GameHandlers.BuildPlayerStatePayload` (`4`, `16`, `17`, `18`, `30`, `34`).
  This is exactly the class of bug that produced the earlier "i18n" drift
  mentioned in `README.md:244-249`.
- Fix approach: extract a single `OriCoop.Codec` project compiled into both
  (source-linked like `OriCoopShared` already is) that owns header framing and
  the frozen bodies; leave only the domain-specific dispatch on each side.

### Botched-to-illegal enum values left in the shared contract

- Issue: `PacketType` retains `POSITION = 1` and `ANIM = 2` marked
  `// LEGACY_REMOVED`, and `CoopSkillType` is a dead enum with no readers.
  `CoopConfig` (`src/OriCoopPlus/OriCoopShared/CoopConfig.cs`) is likewise
  orphaned — the server uses `ConfigStore` instead.
- Files: `src/OriCoopPlus/OriCoopShared/PacketType.cs:5-8`,
  `src/OriCoopPlus/OriCoopShared/PacketType.cs:39-44`,
  `src/OriCoopPlus/OriCoopShared/CoopConfig.cs`
- Impact: the "never reuse these IDs" policy only works if the values stay
  visible; a reader cannot tell whether `POSITION = 1` is still legal. Dead types
  invite a future contributor to "reuse" `CoopConfig`.
- Fix approach: delete `CoopConfig` and `CoopSkillType`; keep the reserved-ID
  note as a comment block (no enum members), with the reserved list documented in
  `docs/protocol.md`.

### Duplicated "help" command list

- Issue: `NetServerHost.BuildHelpText()` hardcodes
  `/coop /tp /dummy /clientcolors /entitysync /help /stop`, and
  `OriCommands.HelpCommand.PrimaryNames` hardcodes the same seven names again.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:449-474`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/OriCommands.cs:521-524`
- Impact: adding a console command silently desynchronises the `/help` chat reply
  from the registry.
- Fix approach: have the host ask the `CommandRegistry` for its primary names
  (pass the registry, or expose `IServerContext.Commands`) and delete both
  hardcoded arrays.

## Known Bugs

### G-03-1 / G-03-2 — remote Spirit Flame produces no projectile, no sound, no clip (open, major)

- Symptoms: shooter fires; the remote puppet shows no muzzle particle, no shot
  sound and no fake projectile, and the animation stays on whatever it was
  playing. Both UAT directions failed (A shoots → B sees nothing; B shoots → A
  sees nothing).
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` (effect
  path `PlaySpiritFlameVisual`), `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs`
- Trigger: any received `PLAYER_EVENT` (19) of kind `SpiritFlame` while a remote
  puppet exists.
- Root cause (from `.planning/phases/03-player-event-core/03-UAT.md` G-03-1):
  the `AimThrow` clip gate returns before spawning anything —
  `AnimationRegistry` fills `s_stateClips` by **exact** alias match against an
  uncalibrated SEED table, while the sender classifies with **substring**
  `Contains`, so `TryResolveState(ActionVisualState.AimThrow)` deterministically
  misses. The 03-04 reorder ("effects first, always") shipped but the clip
  fallback still misses the real attack-clip name, and nothing renders at all.
- Workaround: none. `F8` dumps the catalog (`AnimationRegistry.DumpCatalog`) so
  the real clip name can be pinned, but that is a debugging step, not a fix.
- Fix approach: pin the exact attack-clip name via an F8 dump during live play,
  or populate `s_stateClips` with the same substring matching
  `PlayerStateReader.TryDeriveFromName` uses (see
  `src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs:160-271`).

### G-03-4 — attack clip never resolves: `aplicado=manteve-atual motivo=clip-desconhecido`

- Symptoms: every received event logs
  `[EVENT] P<n> kind=spiritflame aplicado=manteve-atual motivo=clip-desconhecido`
  even after the substring fallback added in 03-04.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs`
  (`s_nameToState` SEED at lines 29-115, `RegisterClips` at 418-512,
  `MergeGlobalForExact` at 178-191)
- Trigger: receiving any Spirit Flame event.
- Root cause: undiagnosed. The substring `aim`/`throw` fallback still misses, so
  either the clip is named something else entirely, or the lookup runs against
  the wrong collection (the puppet is visually stripped and re-collecting from it
  yields nothing — see the `RefreshFromSein` fallback chain).
- Workaround: puppet keeps the last valid animation (fail-closed by design), so
  the bug is visible as "the attack never plays" rather than a crash.
- Fix approach: `F8` dump on a live client, then add the exact name (or broaden
  matching) to `s_nameToState`.

### G-03-5 — effects do not render even though the code is clip-independent (blocker, open)

- Symptoms: nothing appears for anyone despite 03-04 making the particle, sound
  and fake projectile independent of clip resolution; `EvApplied` increments
  without any visible output.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs`
- Trigger: any received Spirit Flame event.
- Root cause: undiagnosed. Candidates: null particle prefab / sound clip on the
  puppet path, spawn at wrong position or layer, an exception swallowed by the
  per-effect `try/catch`, or `StripToVisualOnly`'s whitelist removing the spawned
  object. `EvApplied` counting non-rendered events means the telemetry is also
  wrong.
- Workaround: none.
- Fix approach: trace the spawn path with null checks and a temporary
  non-gated log per effect (the existing `VerboseEvent` helper does exactly this
  but is `IsAnimVerbose`-gated); fix `EvApplied` to count rendered effects, not
  received events.

### G-03-3 — no proof the shooter never echoes its own event (open, major)

- Symptoms: both clients' logs show only the local
  `[EVENT] SpiritFlame detected` line; delivery of `fase=recebido` was confirmed
  only on the receiver after 03-04 ungated the telemetry.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` (legacy
  floating HUD, `SetVerboseLogging`),
  `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs`
- Trigger: two clients, one shooting.
- Root cause: verbose gating. Every `fase=enviado/recebido/aplicado` line and all
  `EvRecv/EvApplied/EvDropped` counters sat behind `if (IsAnimVerbose())`, which
  reads the `Diagnostics/AnimVerbose` config entry (default `false`), while the
  in-game toggle wrote `ShowNetworkLogs` — a flag with **zero readers**
  (`src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:91-92`).
- Workaround: set `AnimVerbose = true` in
  `BepInEx/config/com.ikkikuuro.oricoop.cfg` on both clients.
- Fix approach: 03-04 already ungated the EVENT lines; the remaining step is the
  shooter-side log proving `enviado`-only / never `recebido`-aplicado on own
  shots. Also delete the write-only `ShowNetworkLogs` and `ShowPartnerHp`
  properties (`OriCoopPlugin.cs:91-92`) or give them readers.

### `SendNicknameUpdate` sends a body the server ignores

- Symptoms: changing nickname in the connection dialog updates the local HUD and
  the name tag, but the server-side `Nickname` never changes (the `/tp` command
  and server chat keep the old name until reconnect).
- Files:
  `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:480-498`
  (`SendNicknameUpdate` writes a legacy string into a `MsgConfirm` body),
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:182-205`
  (`HandleConfirmAsync` reads only the header, never the payload)
- Trigger: "Salvar Nome" in `ServerConnectionDialog` while connected.
- Fix approach: give the server a real nickname-update packet (new ID, or reuse
  `MsgConfirm` with a length-prefixed body plus a `Hello`-style parse) and apply
  it to `session.Nickname`.

### `DummyBot` echo buffer can enqueue unboundedly under jitter

- Symptoms: dummy bot drifts/lags behind the player; `_echoBuffer` grows past the
  50-frame trim only on dequeue, and `TryDequeueEchoLocked` enqueues one frame
  per tick while the primary player keeps sending.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/DummyBot.cs:507-560`
- Fix approach: bound the enqueue side (skip if `Count >= cap`) rather than only
  trimming on dequeue.

### `ConfigStore.Load` corrupt-file path resets defaults but still reports success

- Symptoms: an unreadable `serverconfig.json` leaves the operator believing saved
  options loaded; the log line says defaults were used, but the file is never
  repaired or backed up.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ConfigStore.cs:118-150,152-182`
- Fix approach: rename the corrupt file to `serverconfig.json.bak` on parse
  failure and save a clean default, so the next boot is unambiguous.

## Security Considerations

### No authentication, no encryption, session token is trivially guessable

- Risk: the entire protocol is plaintext UDP. The session token is a 32-bit
  random (`Session.NewToken`) that is transmitted in every `Welcome`; an attacker
  who observes one handshake (ARP spoofing, hub/mirrored port, plain LAN) can
  read the token and inject arbitrary packets — including `TELEPORT_REQUEST`,
  `SYNC_*`, `DUMMY_ACTION` and `DISCONNECT` — as that player. `ChatPacketId = -5`
  broadcast lets anyone spam all clients, and `TELEPORT_REQUEST` lets anyone move
  anyone else. `SYNC_ABILITY` with `ShareAbilities` on grants abilities.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/Session.cs:43-51`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs:61-83`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs`
- Current mitigation: endpoint pinning — `ValidatePacket` requires the same
  `IPEndPoint` as the `Hello`, and `IsReady` gating drops pre-`Confirm` packets.
- Recommendations: treat this as LAN-party trust only and document it in
  `README.md`/`docs/protocol.md`; add a per-packet HMAC over header+body keyed by
  the token; rate-limit `Hello` per endpoint; never expose the port beyond the
  LAN. Given "no TLS on UDP" is inherent, at minimum stop trusting `clientId`
  from the header for authority decisions (it is currently the only identity).

### `Hello` flood occupies player slots

- Risk: `SessionManager.HandleHello` allocates a session before any proof of
  liveness and only removes it via the 10 s sweeper; a spoofed-source UDP flood
  can pin all `maxPlayers` slots and deny service.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs:128-176`,
  `.../Net/Session/SessionManager.cs:99-120`, `.../Net/NetServerHost.cs:508-547`
- Current mitigation: `_sessions.Count >= _maxPlayers` reject, sweeper.
- Recommendations: require a client-generated nonce echoed in `Confirm` before
  the slot is reserved; rate-limit per source IP; shorten the timeout for
  sessions that never reach `IsReady`.

### Unauthenticated server controls reachable by any client

- Risk: `DUMMY_ACTION` (spawn/despawn the bot, trigger abilities),
  `TELEPORT_REQUEST`, `COLOR`, `CONFIG_SYNC` (the server correctly ignores
  client `CONFIG_SYNC`, but only because of a single `default:` branch) are all
  accepted from any ready session. There is no operator/host role in the new
  core — the old "host" concept from `README.md` ("O host pode teleportar") has
  no server-side enforcement at all; `/tp` is console-only and
  `TELEPORT_REQUEST` is client-triggered.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:212-242`,
  `.../Net/Game/GameHandlers.cs:503-533`
- Recommendations: introduce an explicit privileged session id (the first
  joiner, or a console-approved id) and gate `TELEPORT_REQUEST`/`DUMMY_ACTION`
  on it.

### Server binds all interfaces and advertises LAN addresses

- Risk: `UdpTransport.Start` binds `IPAddress.Any` and `LogBindAddresses` prints
  every IPv4 — convenient, but it means the port is open on every network the
  host joins (public Wi-Fi included) with no auth.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs:50-67`,
  `.../Net/NetServerHost.cs:850-866`
- Recommendations: add a `--bind <ip>` flag defaulting to loopback, and print the
  bind notice; document the firewall rule already mentioned in `README.md:157`.

### Nickname and chat are only bracket-stripped, not validated

- Risk: `StripBrackets` removes `<`/`>` from nicknames and chat (rich-tag
  injection mitigation), but there is no length cap on the nickname beyond the
  64-byte `Hello` parse, no charset check, and no per-sender chat rate limit. A
  client can spam `SendChatMessage` at high frequency to flood every peer.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:438-447`,
  `.../Net/NetServerHost.cs:327-376`,
  `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:263-281`
- Recommendations: cap nickname length (e.g. 24), enforce a chat cooldown per
  session, and reject non-printable ASCII.

### Decode-then-reject amplifies logging

- Risk: `EnvelopeCodec.TryDecode` failure triggers `SendRejectAsync` to the
  sender, and `DispatchAsync` logs every drop at Warning; a hostile or
  mis-configured peer generates unbounded reject traffic and log growth.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:111-121`,
  `.../Net/Transport/EnvelopeCodec.cs:69-119`
- Recommendations: rate-limit rejects per remote endpoint and downgrade repeated
  identical rejects to Debug.

## Performance Bottlenecks

### `Resources.FindObjectsOfTypeAll` global scan on the animation registry

- Problem: `AnimationRegistry.MergeGlobalForExact()` calls
  `Resources.FindObjectsOfTypeAll<TextureAnimationWithTransitions>()`, which
  returns **every loaded object of that type in the whole process**, then
  `RegisterClips` indexes all of them into `s_animNameCache`/`s_animHashCache`.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs:178-191`,
  `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs:418-512`
- Cause: defensive "never miss a clip" merge, executed on top of the Sein-scoped
  collection and never bounded or invalidated.
- Improvement path: only run this on explicit operator request (F8 dump path) or
  once per scene load with a size cap; otherwise rely on the Sein-hierarchy
  reflection which is the authoritative source.

### Reflection walk over every MonoBehaviour field, depth 5, with `visited` list identity scan

- Problem: `AnimationRegistry.CollectClips` calls
  `root.GetComponentsInChildren<MonoBehaviour>(true)` and then
  `CollectFromObject` for each, reflecting **all instance fields**
  (`BindingFlags.NonPublic | Instance`) and recursing into lists, arrays and
  nested references up to depth 5.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs:218-242`,
  `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs:272-397`
- Cause: generic clip discovery by reflection instead of known component types.
- Improvement path: target the handful of known drivers (`SeinIdle`,
  `SeinRun`, `SeinJump`, `SeinDoubleJump`, `SeinWallJump`, `SeinBash`,
  `CharacterAnimationSystem.m_states`, `SpriteAnimatorWithTransitions`) and read
  only their documented fields. At minimum, add an early bail when
  `result.Count` exceeds a threshold, and cache per-object-type field arrays
  instead of calling `GetFields` per instance.

### Three re-enumerations of the whole puppet hierarchy per puppet spawn

- Problem: `RemotePuppetFactory.CleanPuppetComponents` runs, per puppets:
  `GetComponentsInChildren<Behaviour>`, `GetComponentsInChildren<Transform>`
  (destroying children), `GetComponentsInChildren<Collider>`,
  `GetComponentsInChildren<Rigidbody>`, `GetComponentsInChildren<AudioSource>`,
  `GetComponentsInChildren<AudioListener>`,
  `GetComponentsInChildren<MonoBehaviour>`, then a **3-pass**
  `GetComponentsInChildren<Component>` destroy loop — 8 full subtree walks plus
  3 more.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs:171-336`
- Cause: "keep-list is the guarantee" philosophy, applied by repeated
  whole-subtree sweeps.
- Improvement path: one `GetComponentsInChildren<Component>(true)` pass into a
  list, then classify and `DestroyImmediate` in that single pass; keep the
  multi-pass only as a verification assertion. `DestroyImmediate` on a live
  object is also far more expensive than `Destroy`.

### `Puppet.Update` allocates via `Vector3.Lerp` + `Vector3.Distance` every frame

- Problem: every remote puppet runs a `Vector3.Distance` and (for the common
  path) a `Vector3.Lerp` each frame; at 4-8 puppets this is minor, but it also
  runs `Time.deltaTime`-scaled smoothing on top of server-side extrapolation
  that already advanced the goal, compounding the offset described in
  `RemotePlayerPuppet.cs:366-390`.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:366-390`
- Improvement path: compare squared distance against a squared threshold
  (`MaxExtrapolationSec`**2 * speed**2 **) and skip the Lerp when within
  epsilon.

### Puppets are never culled, so off-screen players cost full Update/LateUpdate

- Problem: `RemoteVisualController.StripFrustumOptimizers` removes every
  frustum-culling component and `FrustumCullingBypassPatch` force-skips
  `CameraFrustumOptimizer` for anything under a `RemotePlayerPuppet`. Combined
  with `EnforceVisibility` running twice per second from `LateUpdate`, every
  remote puppet stays fully simulated and visibility-forced regardless of
  distance.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs:175-209`,
  `src/OriCoopPlus/OriCoopBepInEx/Patches/FrustumCullingBypassPatch.cs`,
  `src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs:66-136`
- Improvement path: keep the culling bypass (it was a real bug fix) but add a
  distance-based LOD: skip `Update` smoothing for puppets farther than N units,
  and throttle `EnforceVisibility` further (1 Hz) for distant puppets.

### Server broadcast is O(n²) per datagram with per-send allocation

- Problem: every reliable relay iterates all ready sessions and, per target,
  allocates a fresh envelope and a string key inside
  `AckTracker.Track` (`remote.Address + ":" + remote.Port + "#" + seq`) and calls
  `File.AppendAllText` per log line under a global lock.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:574-588`,
  `.../Net/Reliability/AckTracker.cs:32-35,41-60`,
  `.../Net/Diagnostics/ServerLogger.cs:43-67`
- Cause: no key caching and synchronous file I/O per log call.
- Improvement path: cache the `IPEndPoint` string key per `Session` (computed
  once at `Hello`), and make the file logger buffered/async with a background
  flush. Fine at 4-10 players, but the log sink blocks every send path today.

### `DummyBot.OnTick` blocks a timer thread on a sync-over-async send

- Problem: `OnTick` builds the payload under `lock (_sync)` and then calls
  `_transport.BroadcastStateAsync(...).GetAwaiter().GetResult()`, blocking a
  `Timer` callback thread. At 100 ms cadence this is survivable, but a slow
  socket would stall the timer pool and delay the retry/sweeper timers.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/DummyBot.cs:408-505`
- Improvement path: make `OnTick` async-void/async-Timer and `await` the send;
  move the payload build out of the lock where possible.

### Client `NetworkService.ReceiveLoop` re-allocates a thread per connect

- Problem: each `Connect()` constructs a fresh `NetworkService` (new `UdpClient`,
  new `Thread`); `Disconnect()` joins with a 1000 ms timeout and closes the
  socket. Repeated F6 reconnect cycles leak sockets briefly and always pay
  thread-start latency, and the receive loop uses a 20 ms `ReceiveTimeout`
  busy-spin.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:101-161,500-550,1142-1171`,
  `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:114-152,154-192`
- Improvement path: reuse one socket/service and re-point the endpoint; or use
  `Socket.Select`/`ReceiveAsync` instead of a 20 ms polling timeout.

## Fragile Areas

### String-typed reflection against game internals will break on any game patch

- Problem: `RemoteVisualController.StripFrustumOptimizers` matches component
  **type names as strings** (`"CameraFrustumOptimizer"`,
  `"MeshRendererFrustrumOptimiser"`, `"DisableRendererWhenOutOfFrustrum"`,
  `"DisableGameObjectWhenOutOfFrustrum"`, `"SuspendWhenOutOfFrustrum"`) and
  `StripExtraLights` matches `"Halo" | "LensFlare" | "FlareLayer" | "Projector" |
  "TrailRenderer"`. These live in `Assembly-CSharp`, not in a stable API; an Ori
  update that renames or removes one silently disables the fix each addresses
  (the original bugs were extreme bloom from cloned Point Lights, and frozen
  puppets from destroying `CharacterAnimationSystem`).
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs:138-209`,
  `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs:171-336`
- Safe modification: never shorten the keep/destroy lists to "clean up"; every
  entry corresponds to a documented bug. When a string stops matching, log a
  warning (currently silent) so the regression is visible in
  `BepInEx/LogOutput.log`.
- Test coverage: none — only the manual 2-client checklist in
  `docs/operations.md`.

### The puppet "keep-list is the guarantee" policy is one whitelist away from visually broken puppets

- Problem: `CleanPuppetComponents` disables every `Behaviour` except four types
  and destroys every `MonoBehaviour` except the same four plus
  `CharacterAnimationSystem` (kept but disabled), and the 3-pass sweep destroys
  every remaining `Component` except `Transform`, `Renderer`, `MeshFilter` and
  those same types. Anything added to the visual subtree by the game (a new
  mirror script, a new animator driver) is deleted on spawn.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs:181-207,266-336`
- Safe modification: add to the whitelist rather than removing from it; log a
  count of destroyed component types at spawn so a game update shows up as an
  unexplained delta.
- Test coverage: none.

### `RemoteVisualController` material isolation depends on the shader having `_Color`

- Problem: the "extreme brightness" fix (`BUG #1`) clones
  `sharedMaterial` per renderer and later forces `alpha = 1` when `a < 0.01` via
  `Shader.PropertyToID("_Color")`. If the game's sprite shader lacks `_Color`,
  `HasProperty` returns false and the invisible-puppet guard silently does
  nothing; conversely any legit fade (dash/bash) is now frozen at alpha 1 for the
  puppet because the material is a private instance that the game can no longer
  fade.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs:40-64,119-136`
- Safe modification: track the source alpha and clamp relative to it rather than
  forcing an absolute 1.0.
- Test coverage: none.

### Client shared-state caches are never reset on disconnect

- Problem: `RemotePlayerManager` holds `_puppets` and is cleared on disconnect,
  but `AnimationRegistry`'s static caches (`s_animHashCache`,
  `s_animNameCache`, `s_stateClips`, `s_seinClipRefs`) and
  `RemotePlayerPuppet`'s statics (`s_fakePool`, `s_fakeQuadMesh`,
  `s_fakeBoltMaterial`, `s_cachedShotClip`) survive reconnects and scene changes.
  `s_cachedShotClip` and `s_fakeBoltMaterial` come from the local Sein, so a
  scene change leaves dangling Unity references.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs:13-24`,
  `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs:102-105`,
  `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:154-192`
- Improvement path: clear/soak the registry on `Disconnect()` and null the Unity
  object caches in `OnDestroy`.

### `PlayerEventCore` has a static transport binding with no ownership

- Problem: `PlayerEventCore.s_transport` is set by
  `OriCoopPlugin.Awake`/`Connect` and by `Disconnect` (set to null), and
  `s_handlers` is a static dictionary that `RemotePlayerManager`'s constructor
  registers into. Two plugin instances (or a manager that outlives a
  disconnect) produce duplicate/unknown handlers. `PublishSpiritFlame` guards on
  `transport.IsConnected`, so a reconnect can silently send events on a stale
  transport between `BindTransport(new)` and the next `Disconnect()`.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Events/PlayerEventCore.cs:19-36`,
  `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs:14-17`,
  `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:281-292`
- Safe modification: only ever call `BindTransport` from the single plugin
  lifecycle path; never construct a second `RemotePlayerManager`.

### Lock-ordering hazard between `DummyBot._sync` and `GameHandlers._sync`

- Problem: `DummyBot.Spawn()` and `OnTick()` hold `DummyBot._sync` and then call
  `GetPrimaryPlayer()` → `GameHandlers.GetPrimaryPlayerView()` which takes
  `GameHandlers._sync`. The inverse order exists in
  `GameHandlers.OnTeleportRequestAsync`, which holds `GameHandlers._sync` (via
  `TryGetPosition`/`GetPrimaryPlayerView`) and then calls `_dummy.IsActive` and
  `_dummy.GetPosition` (taking `DummyBot._sync`). Classic ABBA deadlock.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/DummyBot.cs:169-196,408-505`,
  `.../Net/Game/GameHandlers.cs:312-372`
- Fix approach: snapshot the dummy position/active flag into locals **before**
  taking `GameHandlers._sync`, or route both through a single lock. Not yet
  observed in the field (timing-dependent), so it will surface as a rare hang
  under load.

### `GameHandlers.RelayReliableAsync` reuses the sender's `seq` as the ACK key

- Problem: the reliable relay calls `_acks.Track(target.EndPoint, seq, ...)` with
  the **sender's** sequence number. Sequence numbers are per-sender counters, so
  two different senders can produce the same `seq`; the key is
  `endpoint#seq`, which disambiguates by *destination*, but the sender's seq
  space and the server's `NextServerSeq()` space are unrelated and can collide
  for the same destination (server-originated vs relayed). A misfired ACK
  cancels the wrong pending send.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/NetServerHost.cs:574-588`,
  `.../Net/Reliability/AckTracker.cs:32-35`
- Fix approach: issue a fresh server-side seq per relayed datagram per
  destination and rebuild the envelope (or add a distinct relay key namespace).

### `SessionManager.SweepExpired` mutates the dictionary it is enumerating

- Problem: `SweepExpired` iterates `_sessions` (a `ConcurrentDictionary`) with
  `foreach` and calls `TryRemove` inside the loop.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs:99-120`
- Safe modification: materialise the expiry candidates into a list first, then
  remove. `ConcurrentDictionary` tolerates this today; it is still fragile and
  will break if the collection type ever changes.

## Scaling Limits

**Player count (`maxPlayers`):**
- Current capacity: hard-capped at 10 by `SessionManager`'s constructor
  (`maxPlayers < 1 ? 1 : maxPlayers > 10 ? 10 : maxPlayers`) and mirrored by
  `Program.ClampMaxPlayers`; README documents 1-10, default 4.
- Limit: `README.md:128-131`, `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Program.cs:139-142`
- Scaling path: the cap is arbitrary, not architectural — the O(n²) reliable
  relay and per-target `AckTracker` entries are the real ceiling. At 10 players a
  `SYNC_LEVER` broadcast creates 10 pending entries with 250 ms retries; raise
  the cap only after switching to aggregated per-tick sends (which the code
  deliberately avoids, `D-11`).

**Entity sync:**
- Current capacity: zero. `SYNC_BREAKABLE` (13) is validated and dropped by
  design (`GameHandlers.OnSyncBreakable`), and `EntitySync` is a bare config bool
  with no producer, no consumer and no protocol body — it is only logged on
  receipt (`OnEntitySyncChanged` logs "enabled/disabled").
- Limit: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:487-501`,
  `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:454-457`
- Scaling path: the `PacketType` enum reserves no room beyond 19 and the protocol
  doc must be updated in the same change (`docs/protocol.md`); adding real entity
  sync needs an ownership/interest model that does not exist yet.

**UDP receive depth:**
- Current capacity: bounded `Channel` of 1024 datagrams with
  `BoundedChannelFullMode.DropOldest`.
- Limit: src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Transport/UdpTransport.cs:37-43,113-119
- Impact: a burst >1024 in flight silently discards snapshots (acceptable — they
  are unreliable by design) but would also drop critical `Hello`/`Confirm`
  datagrams, turning an overload into a handshake failure loop.

**Server log file:**
- Current capacity: unbounded `Logs/server.log`, appended per line under a global
  lock. At Debug level with chat and ACK retries this grows quickly on a long
  host session.
- Limit: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Diagnostics/ServerLogger.cs:43-67`
- Scaling path: add rolling file target or a size cap; today a 24/7 host
  eventually fills the disk.

**Nickname / chat strings:**
- Current capacity: nick limited to 64 bytes at `Hello`
  (`TryParseHello`), chat to `NetProtocol.ChatMaxChars = 350`.
- Limit: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Session/SessionManager.cs:245-276`,
  `src/OriCoopPlus/OriCoopShared/NetProtocol.cs:48`
- Impact: fine, but the nickname cap is only enforced on the first `Hello`;
  `SendNicknameUpdate` has no cap on the client side.

## Dependencies at Risk

**Ori DE `Assembly-CSharp.dll` (implicit, unversioned):**
- Risk: the client compiles directly against the installed game assembly and
  patches by **method name**: `SeinCharacter.FixedUpdate`, `SeinInput.Update`,
  `CharacterAnimationSystem.Start`,
  `CameraFrustumOptimizer.ProcessFrustumOptimizable`,
  `SeinSpiritFlameAbility.ThrowSpiritFlames`, `InventoryManager.Awake/Show/ShowImmediate`.
  Any Ori update that renames, re-signatures or removes one of these makes
  `Harmony.PatchAll()` throw at `Awake` (or silently skip the patch), and the
  type-name strings in `StripFrustumOptimizers`/`StripExtraLights` stop matching.
- Impact: total mod failure on the client, or silent loss of a specific fix
  (the historical bugs in this file are exactly that: brightness, frozen
  puppets, culling).
- Migration plan: catch and log per-patch failures at
  `Harmony.PatchAll()` (`src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:294-296`)
  instead of letting one failure abort `Awake`; record the game build the patch
  targets (there is no version check anywhere in the client).

**`BepInEx` / `0Harmony` DLLs referenced by path with no version pin:**
- Risk: `OriCoopBepInEx.csproj` resolves `BepInEx.dll` and `0Harmony.dll` from
  `<ORI_DIR>\BepInEx\core` or `oriDE_Data\Managed`, falling back to
  `API/Client/` — which contains **four different** Harmony/launcher builds
  (`0Harmony.dll`, `OLD0Harmony.dll`, `WWClient.dll`, `OLDWWClient.dll`,
  `WWClient2cpp.dll`) with no indication which is current.
- Files: `src/OriCoopPlus/OriCoopBepInEx/OriCoopBepInEx.csproj:19-29`,
  `src/OriCoopPlus/OriCoopBepInEx/build.ps1:50-67`, `API/Client/*`
- Impact: a build can silently bind against a stale Harmony and fail only at
  runtime inside the game.
- Migration plan: pin exact versions, delete the `OLD*` duplicates, and record
  the resolved DLL hash in the build output.

**Hand-rolled UDP reliability layer (`AckTracker`) vs. a known-good library:**
- Risk: 250 ms × 3 retries, no RTT estimate, no congestion control, no
  fragmentation (a datagram larger than the MTU is dropped by the network and
  retried verbatim three times — the same bytes, same failure). Sequences are
  `uint` with a wrap-safe comparison, which is sound, but there is no SACK and
  no reordering buffer on the client's snapshot path (a late-arriving newer
  snapshot is dropped by `IsNewerThanLast` and the older one that arrives later
  is also dropped — a snapshot can be lost permanently if it arrives after a
  newer one).
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Reliability/AckTracker.cs`,
  `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:1057-1095`
- Migration plan: at minimum document the no-fragmentation constraint; the
  protocol has no path for payloads > ~1400 bytes.

**`.NET 8` server runtime requirement:**
- Risk: the server is a console app targeting `net8.0`; `start_server.bat`
  already contains a ".NET 8.0 not found" workaround message and
  `scripts/download_dotnet8.ps1` exists as a fallback installer. A host without
  the runtime cannot run the server at all.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/OriCoopDedicatedServer.csproj:1-9`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/start_server.bat:12-21`
- Migration plan: publish a self-contained single-file build (documented as the
  historical fallback in `docs/operations.md:94-108`) so the runtime is not a
  prerequisite.

## Missing Critical Features

**Operator/host authority is not implemented in the new core:**
- Problem: `README.md:200-222` documents "O host pode teleportar um jogador ate
  outro" via `/tp`, but `/tp` is **console-only** (typed into the server
  window). Over the wire, `TELEPORT_REQUEST` lets *any* client teleport itself to
  *any* player with no privilege check; `DUMMY_ACTION` lets any client
  spawn/despawn the bot and trigger abilities on it. There is no host role, no
  operator list and no per-command authorization in `GameHandlers` or
  `CommandRegistry`.
- Blocks: safe public/LAN hosting; any "host controls the session" gameplay.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:312-372,503-533`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/Commands/OriCommands.cs:143-250`

**Remote player HP / damage:**
- Problem: `ShowPartnerHp` is a plugin property and a menu toggle
  (`OriCoopMenuScreen.OnToggleHpClicked`) with **no reader anywhere**; health is
  not in `PlayerSnapshot` (`Domain/PlayerState.cs:48-58`) and not in
  `PLAYER_STATE` (18). The UI advertises "Vida do Parceiro: LIGADO" and does
  nothing.
- Blocks: any cooperative gameplay that depends on seeing a partner's health.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs:91`,
  `src/OriCoopPlus/OriCoopBepInEx/UI/OriCoopMenuScreen.cs:478-489`

**Abilities, doors/levers and world events are relayed but never consumed by the client:**
- Problem: `NetworkService.ReadServerPacket` validates and **discards**
  `SKILL`, `COLOR`, `SYNC_ABILITY`, `SYNC_LEVER`, `SYNC_DOOR`,
  `SYNC_WORLDEVENT` and `SYNC_BREAKABLE` (marker checks, `ReadBytes`,
  no handler). The server faithfully relays them, so `ShareAbilities`,
  `ShareDoorsAndLevers` and `ShareWorldEvents` produce wire traffic and server
  log lines with zero client-side effect.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:990-1036`
- Blocks: the entire "shared progression / shared world" feature set that
  `ConfigStore` and `/coop` expose.

**Chat composition from the client:**
- Problem: `NetworkService.SendChatMessage` exists and is wired to the server,
  but no UI calls it — no chat box, no keybind, no on-screen history. The only
  chat surface is the server-initiated broadcast (join/leave/help/dummy
  announcements) and `Logger.LogMessage`.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:263-281`
- Blocks: player-to-player communication, which is what the chat protocol
  (`-5`) was built for.

**`SeinInputPatch` is an empty postfix:**
- Problem: `Patches/SeinInputPatch.cs` patches `SeinInput.Update` with a body
  that does nothing and carries only a comment ("the input adapter will be added
  once the game's concrete fields are verified"). `PlayerInputState` in
  `Domain/PlayerState.cs` and the `Input` field on `PlayerSnapshot` are
  therefore always default. Input is not replicated, which is why animation is
  inferred from velocity and clip names instead of driven.
- Files: `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinInputPatch.cs`,
  `src/OriCoopPlus/OriCoopBepInEx/Domain/PlayerState.cs:30-37,54`

**`DummyBot` cannot be given lever/door actions over the wire:**
- Problem: `DummyBot.TriggerLever`/`TriggerDoor` exist but are console-only by
  design (`DUMMY_ACTION` accepts only 0/1 from clients), and the client
  `NetworkService` has no `SendDummyAction` caller at all. The `/dummy lever` and
  `/dummy door` subcommands are unreachable from the game.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/DummyBot.cs:329-365`,
  `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:415-448`

**Server-side player position history / reconnect state:**
- Problem: `_lastKnown` positions and `_unlockedAbilities` are in-memory only and
  are cleared on `OnSessionLeft`; a player who drops and rejoins (new ID, since
  IDs are never reused) loses their ability history replay and their position
  anchor. `serverconfig.json` persists only the 8 config bools — no colors
  (`_colors` is in-memory) and no ability state.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/GameHandlers.cs:200-208`,
  `src/OriCoopDedicatedServer/OriCoopDedicatedServer/Net/Game/ConfigStore.cs:321-347`

## Test Coverage Gaps

**Everything on the client side — the highest-risk code — has zero automated tests.**
- What's not tested: `AnimationRegistry` (clip collection, hash resolution,
  state-clip population), `RemotePuppetFactory` (the whole
  component-stripping whitelist), `RemoteVisualController` (material isolation,
  frustum stripping, visibility watchdog), `RemotePlayerPuppet` (interpolation,
  extrapolation, animation hysteresis), `NetworkService` (envelope framing,
  drop-old sequencing, retry/ack, all packet decoders), and all Harmony patches.
- Files: all of `src/OriCoopPlus/OriCoopBepInEx/**`
- Risk: every one of the five open UAT gaps in this document is a client-side
  failure that shipped undetected; the registry/whitelist code is exactly the
  kind that breaks silently on a game update.
- Priority: High. The F8 `DumpCatalog` path and `AnimationRegistry` resolution
  rules are the cheapest first targets (pure functions, no Unity engine
  dependency beyond the clip type).

**Server protocol is only smoke-tested, and only by hand.**
- What's not tested: `GameHandlers` gating logic, `ConfigStore` persistence
  round-trip and corrupt-file handling, `SessionManager` handshake edge cases
  (wrong token, endpoint change, full server, protocol-version mismatch),
  `AckTracker` retry/expiry, and the `CommandRegistry` parsers.
- Files: `src/OriCoopDedicatedServer/OriCoopDedicatedServer/**`
- Existing coverage: `src/OriCoopDedicatedServer/SmokeProbe/Program.cs` — a
  BCL-only, hand-rolled harness that spawns the server and checks
  invalid-magic → handshake → relay → player-event → ping (`SMOKE_OK`), plus a
  byte-identical round-trip for packet 19. It is a standalone console project
  with no assertion library, no CI hook, and it is **not part of
  `src/OriCoopPlus/OriCoopPlus.sln`** (verified: the solution contains only
  `OriCoopBepInEx` and `OriCoopDedicatedServer`).
- Risk: the smoke probe is the only automated safety net in the repo and it is
  easy to skip; `.planning/phases/03-player-event-core/03-VALIDATION.md` itself
  records `Framework: none — manual 2-client validation`.
- Priority: High — add `SmokeProbe` to the solution and run it in CI.

**No CI at all.**
- What's not tested: the build. `README.md` and `docs/operations.md` describe
  two separate manual build commands (`dotnet build` on the solution and the
  server csproj; `build.ps1` for the client) with no check that either still
  works, and no check that they agree.
- Risk: the divergent-build problem in Tech Debt is unguarded; a commit can
  break `build.ps1` (the deploy path) while the solution builds clean.
- Priority: High — a GitHub Actions job running both builds plus SmokeProbe.

**No verification that the deployed DLL matches the source.**
- What's not tested: `AGENTS.md` mandates an immediate manual copy of every
  freshly built DLL into `<ORI_DIR>\BepInEx\plugins\` (two candidate Steam paths
  are hardcoded) and treats the task as incomplete otherwise. Nothing verifies
  the file was actually replaced, or that it corresponds to the current commit.
- Risk: the recurring "is the deploy stale?" question — raised again in
  `03-UAT.md:55-57` ("Deployed build verified current ... failures below are
  real code issues, not a stale deploy") — is answered by hand every time.
- Priority: Medium — a small script that hashes the built DLL and the deployed
  DLL and prints both would close this permanently.

---

*Concerns audit: 2026-10-09*
