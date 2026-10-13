# Animações, inimigos, dano e dados (save) — Ori DE em D:

Investigação estática de 2026-10-13 sobre `D:\SteamLibrary\steamapps\common\Ori DE`,
cruzada com `src/` e `docs/` existentes. Sem execução do jogo nesta rodada;
efeitos visuais seguem **a confirmar** em run com 2 clientes.

> "Mecânicas de dados" foi lido de dois jeitos: **dano** (`DamageType`,
> `DamageDealer/Reciever`, vida/energia) e **dados de save/progresso**
> (`SaveGameController`, `playerData.txt`, inventário). Ambos estão abaixo.

## 1. Como rodam as animações do Ori

Cadeia vanilla (nomes confirmados via strings do `Assembly-CSharp.dll` em D:):

```
SeinCharacter (.Animation)
  -> CharacterAnimationSystem (.Animator, dicionário Animations,
      AddAnimation / TryGetAnimation — ver scripts/target_types_dump.txt:7-24)
    -> SpriteAnimatorWithTransitions
         (.DefaultAnimation / .CurrentTextureAnimationTransitions /
          .PreviousTextureAnimationTransitions / .CurrentAnimation,
          SetAnimation(clip, restart))
      -> TextureAnimationWithTransitions (wrapper ScriptableObject, .name)
        -> TextureAnimation (interna, .name, .Animation)
```

Drivers lógicos (um MonoBehaviour por estado, todos com string presente na DLL):

`SeinIdle`, `SeinRun`, `SeinJump`, `SeinDoubleJump`, `SeinWallJump`,
`SeinBash`, `SeinGlide`, `SeinStomp`, `SeinDash`, `SeinSwim`,
mais containers `CarryAnimations` e `SwimmingAnimations`.
Espelhamento via `CharacterSpriteMirror.FaceLeft`.
`SeinController` expõe as flags que decidem o estado real:
`IsBashing`, `IsStomping`, `IsDashing`, `IsGliding`, `IsChargingJump`,
`IsGrabbingWall/Block/Lever`, `IsSwimming`, `IsAimingGrenade`, `IsCarrying`,
`IsCrouching`, `IsPushPulling`, `IsStandingOnEdge` (+ `Sein.IsOnGround`,
`Sein.Speed`, `Sein.FaceLeft`).

Como o mod usa isso hoje:

- Sender: `Patches/PlayerStateReader.cs:12-47` lê `Sein.Speed`, `FaceLeft`,
  `IsOnGround` e o nome do **wrapper**
  (`CurrentTextureAnimationTransitions.name`, fallback para
  `CurrentAnimation.name`); deriva `ActionVisualState` com prioridade
  controller → nome do clipe → velocidade (`DeriveFullState`,
  `Patches/PlayerStateReader.cs:87-158`).
- Catálogo: `Client/AnimationRegistry.cs` — `Prewarm()` coleta por reflection
  na hierarquia do Sein (`CollectClips`, `CollectFromObject` até profundidade 5,
  arrays + `List<T>`, + drivers `Default/Current/Previous`), indexa nome do
  wrapper **e** nome interno (`TextureAnimation.name`) para o mesmo asset, e
  faz merge global só para resolve exato. Fallback por estado (`s_stateClips`)
  usa **só** clipes com proveniência do Sein — nunca de inimigo (correção do
  bug "idle de inimigo virou Idle do Ori"). Ordem: exato → estado-Sein →
  manter última (fail-closed). Dump via `F8` → `[ANIM-DUMP]` (`DumpCatalog`).
- Receptor: `Client/RemotePlayerPuppet.cs:124-232`
  (`ApplySnapshotDirect` + `ApplyConfirmedAnimation` com histerese 0.15 s,
  `IsPlayingClip` compara o **wrapper** atual, `SetAnimation(target, true)` só
  em troca de estado) + `CharacterAnimationSystem` preservado porém desligado
  (destruí-lo congelava o puppet — bug #2). Espelho via
  `CharacterSpriteMirror.FaceLeft`, fallback para rotação Y 180°.
- Fábrica: `Client/RemotePuppetFactory.cs` instancia só a subárvore visual do
  Sein; `Client/RemoteVisualController.cs` dá material próprio por puppet e
  watchdog de visibilidade; `Patches/AnimationPrewarmPatch.cs` dá prewarm no
  `CharacterAnimationSystem.Start`; `Patches/FrustumCullingBypassPatch.cs`
  impede culling do puppet fora do frustum.
- Contrato: `OriCoopShared/AnimationSyncData.cs` (`ActionVisualState` 1 byte +
  `Flags` + `AnimNameHash` FNV1a + `SpeedX/Y`); catálogo nominal em
  `docs/anim-catalog.md`.

## 2. Inimigos: animações próprias e dependências

Inimigos **não** usam o rig do Sein. Cada família tem seu par
`<Nome>Enemy` + `<Nome>EnemyAnimations` (+ `...Settings/Placeholder`),
confirmado via strings da DLL em D::

`AcidSlugEnemy`, `RammingEnemy`/`ArmouredRammingEnemy`, `DashOwlEnemy`,
`DropSlugEnemy`, `FishEnemy`, `FloatingRockTurretEnemy`/
`FloatingRockLaserEnemy`, `JumperEnemy` (estados
`Charging/Fall/Idle/Stomped/Stunned/Thrown`), `SpitterEnemy` (`Charging/Idle/
RunBack/Shooting/Stomped/Stunned/Thrown/Walk`), `SwarmEnemy`
(+ `SwarmEnemyManager`), `WormEnemy`/`MortarWormEnemy`,
`KamikazeSootEnemy`/`ChargingSootEnemy`, `SlugEnemy`, `StarSlugEnemy`,
`OwlEnemy`/`DropOwlEnemy`, `GroundEnemy`, `DeadBatEnemy`, `EnemyZone`,
`EnemyStopper`, `DeadEnemyRagdoll`.

Sem classe `Boss` (grep `*boss*` = zero tipos — já registrado em
`docs/game-entry-map.md`): "chefes" são sequências scriptadas
(`MistyWoodsKuroController`, `ValleyOfTheWindKuroGameplayController`,
`CatAndMouseKuroKillController`) + chaves de progressão
(`GinsoTreeKey`/`ForlornRuinsKey`/`MounthoruKey` em `InventoryManager`) +
`SeinWorldState (WaterCleansed, WindReleased)`.
Contratos de combate vanilla (strings presentes): `IAttackable`
(`CanBeChargeFlamed/Dash/Grenade/Stomp/Bash/SpiritFlamed`,
`IsStompBouncable`, `CanBeLevelUpBlasted`, `IsDead`) +
`IBashAttackable` (`OnEnterBash`, `BashPriority`),
`IChargeDashAttackable`, `IChargeFlameAttackable`, `IStompAttackable`,
registro global `Game.Targets.Attackables`.
`CleverAnimationSet` e `SeinVisuals` **não existem** na DLL (nomes de build
MP antiga); `EntityController` **existe** e é a base do fluxo legado
`EntitySync` (posição + nome da anim + direção — sem vida/morte/fases).

Status no mod: inimigos/entidades **não sincronizados** — `EntitySync` é só
contrato + relay posicional; vida, morte, fases, padrões de ataque, drops e
arena seguem **a confirmar/não implementados**.

## 3. Mecânicas de dano

Tipos vanilla confirmados por strings na DLL em D: (todos `True`):

`DamageType`, `DamageDealer`, `DamageReciever` (+ `CarryableDamageReciever`,
`CreepDamageReceiver`), `DamageContext`, `DamageAmount`,
`DamageReactionSettings`, `DamagePrefabSpawner`, `DamageDisplayText`,
`SeinDamageReciever`, `SeinMortality`, `SeinHealthController`, `SeinEnergy`.
Amostras de valores/comportamento: `AcidDamage`, `BashBounceDamage`,
`StompDamage`/`StompBlashRadius` (via `SeinStomp`), `DamageOnTouch`,
`DamageOverTime`, `DamageDirection*`, `DamageMultiplierPerOriStrength`.

Dependências: dano passa por `DamageDealer` → `DamageReciever` →
`SeinMortality`/`SeinHealthController` (vida) e `SeinEnergy` (energia);
morte/respawn envolve `Checkpoint`/`SaveGameController`.
Grep em `src/` por `SeinHealth|SeinEnergy|SeinMortality|DamageReciev|SaveGame|
DamageType` retorna **zero ocorrências** — o mod hoje só toca dano de forma
visual (Spirit Flame remoto = beam + som, `RemotePlayerPuppet.
PlaySpiritFlameVisual`, zero dano/colisão/estado) e via contratos
`CanBeStomped()/CanBeSpiritFlamed()` para escolher alvos. Sincronizar
vida/dano/morte de jogadores ou inimigos é lacuna aberta.

## 4. Mecânicas de dados (save / progresso)

Tipos vanilla confirmados por strings na DLL em D: (todos `True`):

`SaveGameController` (`PerformSave/PerformLoad`, `CurrentSaveSlot`,
`CurrentSaveFilePath`, `OnPerformSave`, `GameSaveSequence`, `GameSaved`),
`SaveSlotsManager` (`AnySaveSlotsExist`, `CopySaveSlots`, `FindOrCreateSaveSlot`),
`SavePedestal` (`TeleportOnPedestal`, `SaveOnPedestal`, `MarkAsUsed`,
`CanTeleport`), `SeinSoulFlame`/`SoulFlame` (`CastSoulFlame`,
`SpawnSoulFlame`), `Checkpoint`, `SeinInventory` (`Keystones`, `MapStones`,
`SkillPointsCollected`), `SeinLevel` (XP/level/skill point),
`SeinPickupProcessor` (8 `OnCollect*`), `PickupBase` (`IsCollected`,
`Collected()`, `Radius`), `MoonGuid (A,B,C,D)` (ID estável de
portas/alavancas/eventos), `WorldEvents`/`WorldEventsRuntime`/
`WorldEventsManager`, `InventoryManager`.

Arquivos em D: que guardam dados:

- `oriDE_Data/playerData.txt` (209 bytes: `vSyncCount`, `fixedDeltaTime`,
  `displayResolutionDialog`, `useDirect3D11`, `Build Number = 5963`) —
  só settings de player, não é save.
- Saves reais ficam fora da pasta do jogo (perfil do usuário), via
  `SaveSlotsManager` — **a confirmar** o caminho exato nesta máquina.
- `MPSettings.json` e `Server.backup.json` na raiz são do mod/servidor,
  não do vanilla.
- `StreamingAssets/` em D: contém só vídeos `.avi` (`E3Trailer`,
  `EnemyConcepts`, `Sequences`, `Story`…); `Resources/` só tem
  `unity default resources` / `unity_builtin_extra`.

Status no mod: pickups têm varredura legada (`SeinPickupMPProcessor` sobre
`PickupBase`, raio 2,5u) e mundo tem `SYNC_ABILITY/SYNC_LEVER/SYNC_DOOR/
SYNC_WORLDEVENT` como **relay sem aplicação** (`NetworkService.cs:990-1036`
valida e descarta); save/slots/checkpoint/inventário/XP seguem locais por
cliente, **não sincronizados**.

## Arquivos de origem (onde mora cada ponto)

| Ponto | Arquivo de origem em D: | Papel |
| --- | --- | --- |
| Lógica de animação, inimigos, dano, save | `oriDE_Data\Managed\Assembly-CSharp.dll` (2.042.880 bytes, SHA-256 `18e70a4c96f093b6dc0ece83ad2842fd45a130196f17a13008d202e7b69c1d88`, `oriDE.exe` 16.990.208 bytes) | tipos `Sein*`, `CharacterAnimationSystem`, `SpriteAnimator/TextureAnimation*`, `*Enemy*`, `Damage*`, `Save*`, `MoonGuid`, `WorldEvents` |
| Runtime Unity | `oriDE_Data\Managed\UnityEngine.dll` (+ `UnityEngine.UI/Networking.dll`, `Mono/`) | `Animator`, `ScriptableObject`, `Resources.FindObjectsOfTypeAll` |
| Clipes/materiais/skinning | `oriDE_Data\level*` + `sharedassets*.assets(.resS/.resource)` + `resources.assets`, `globalgamemanagers(.assets)` | assets `TextureAnimationWithTransitions` referenciados pelos drivers do Sein e de cada inimigo — **a confirmar** mapeamento asset↔clipe por dump Unity |
| Vídeos, sem lógica | `oriDE_Data\StreamingAssets\*.avi` | só cinemáticas/conceitos |
| Settings locais | `oriDE_Data\playerData.txt`, `output_log.txt`, `guids.txt`, `svnRevision.txt` | build/settings/logs, não saves |
| Leitura do mod | `src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs`, `Client/AnimationRegistry.cs`, `Client/RemotePlayerPuppet.cs`, `Client/RemotePuppetFactory.cs`, `OriCoopShared/AnimationSyncData.cs` | sender, catálogo, puppet, contrato |
| Dumps de referência | `scripts/target_types_dump.txt`, `scripts/disasm_out.txt`, `scripts/strings_found.txt` | `CharacterAnimationSystem.Animator`, `SpriteAnimatorWithTransitions.SetAnimation/get_CurrentAnimation`, `AddAnimation/TryGetAnimation` |

## Dependências entre os pontos

- Estado de animação do Ori depende de `SeinController` (habilidade ativa) +
  física (`Speed`, `IsOnGround`) + nome do clipe atual; inimigos não entram
  nessa cadeia.
- Dano depende de colisão/ataque (`DamageDealer` + `CanBe*`) e escreve em
  vida/energia (`SeinMortality`/`SeinHealthController`/`SeinEnergy`); morte
  aciona `Checkpoint`/`SaveGameController` (respawn), não o sistema de animação.
- Save/progresso depende de `MoonGuid` (portas/alavancas/eventos) +
  `PickupBase`/`SeinInventory`/`SeinLevel` + `SavePedestal`/`SoulFlame`;
  animação só reflete o resultado (ex. estado `Lever`).
- No mod, os três sistemas são independentes: animação tem fluxo funcional
  (sender→puppet visual), dano e save de inimigos/mundo seguem sem aplicador
  local.

## Verificação

- REA `inspect_managed_artifact` em D: (`ev_1e693972f70a7394a24194d4c206b9fb23d26c61afc4e0f4c1e31d9cbe6ed86d`):
  mesmo SHA/tamanho da auditoria anterior; `inspect_managed_members`
  (`ev_c875d7c352b550377ed733f0ccd66bb2c7b16c50412dc2f6f2de9cd6d0f796a1`)
  indisponível por teto de representação (67 MB) nesta versão — listas de
  tipos acima vêm de strings da DLL + `docs/game-entry-map.md` (REA anterior
  `ev_b8a19444…`), não de catálogo REA novo.
- Grep `src/` confirma zero ocorrências de `SeinHealth|SeinEnergy|
  SeinMortality|DamageReciev|SaveGame|DamageType` (dano/save sem entrada no mod).
- Sem execução do jogo: nomes exatos de clipes (`docs/anim-catalog.md` = SEED
  best-effort), mapeamento asset↔clipe nos `sharedassets/level*`, caminho dos
  slots de save e comportamento de chefe em coop seguem **a confirmar** (dump
  `F8` + run 2 clientes, ver `docs/operations.md` e `docs/anim-test-battery.md`).
