# Auditoria do assembly do jogo (Ori DE)

Auditoria estática de 2026-10-10 sobre a instalação
`C:\Program Files (x86)\Steam\steamapps\common\Ori DE`, cruzada com o
`src/` atual. Complementa o [mapa de entradas](game-entry-map.md), que
descreve a intenção dos recursos; aqui vai o que foi confirmado no fio e
no código desta build.

## Fingerprint do `Assembly-CSharp.dll`

Via REA `inspect_managed_artifact` (evidência
`ev_037b0bf123304f6d532f6b7a6430f00f943f5f78ef0ddc251dc354a06ed41c9c`):

| Campo | Valor observado |
| --- | --- |
| Caminho | `oriDE_Data\Managed\Assembly-CSharp.dll` |
| SHA-256 | `18e70a4c96f093b6dc0ece83ad2842fd45a130196f17a13008d202e7b69c1d88` |
| Tamanho | 2.042.880 bytes |
| Runtime | `unity-mono`, CIL, AnyCPU |
| Tipos / métodos | 2539 `TypeDef` / 14123 `MethodDef` |
| Referências | `UnityEngine`, `mscorlib 2.0`, `System.Core 3.5`, `System 2.0`, `SteamworksManaged`, `J2i.Net.XInputWrapper`, `Assembly-CSharp-firstpass` |

`oriDE.exe` tem 16.990.208 bytes. Boot via `doorstop_config.ini` →
`BepInEx\core\BepInEx.Preloader.dll`. Deploy observado nesta máquina:
`BepInEx\plugins\OriCoopBepInEx.dll` (100.864 bytes) e `Server\`
com `OriCoopDedicatedServer.exe/.dll` — **a confirmar** que continuam do
mesmo build após a próxima compilação.

## Patches ativos nesta build

`src/OriCoopPlus/OriCoopBepInEx/Patches/` contém apenas:

* `SeinCharacterPatch.cs` (snapshot local via `Game.Characters.Sein`)
* `PlayerStateReader.cs` (posição, `Speed`, `FaceLeft`, `IsOnGround`, flags do `Controller`, nome do clipe)
* `SpiritFlamePatch.cs` (`SeinSpiritFlameAbility.ThrowSpiritFlames` → `PLAYER_EVENT 19`)
* `SeinInputPatch.cs`, `FrustumCullingBypassPatch.cs`, `AnimationPrewarmPatch.cs`

Não há patch de `Lever`, `DoorWithSlots`, `SetWorldEventAction`,
`PlayerAbilities.SetAbility`, `PickupBase` ou `EntityController` neste
diretório — **a confirmar** se foram removidos no cutover ou se vivem em
outra camada. O legado `scripts/all_members_dump.txt:77-108`
(`WorldSyncManager`, `LeverLeft/Right/MiddlePatch`, `DoorOpenedPatch`,
`WorldEventPatch`) não tem correspondente no `src/` atual.

## Recepção no cliente: relay sem aplicação

O servidor faz relay confiável de `SYNC_*`, mas o cliente valida o
framing e descarta, sem evento nem aplicação visual — verificado em
`src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs:990-1036`:

| Pacote | Recepção atual |
| --- | --- |
| `SKILL 7` | valida marcador + 8 bytes, descarta (`:990-998`) |
| `COLOR 6` | valida marcador, descarta (`:999-1008`) |
| `SYNC_ABILITY 10` | valida marcador + 8 bytes, descarta (`:1009-1014`) |
| `SYNC_LEVER 11` | valida marcador + 24 bytes, descarta (`:1015-1020`) |
| `SYNC_DOOR 12` | valida marcador + 20 bytes, descarta (`:1021-1026`) |
| `SYNC_WORLDEVENT 14` | valida marcador + 24 bytes, descarta (`:1027-1032`) |
| `SYNC_BREAKABLE 13` | valida só marcador (`:1033-1036`); servidor também não faz relay (`GameHandlers.cs:483-497`) |

Exceções funcionais: `PLAYER_STATE 18`, `PLAYER_EVENT 19`,
`TELEPORT_REQUEST 15`, `CONFIG_SYNC 16`, chat `-5` e `DISCONNECT 4`
têm consumidor. Todo o resto acima é contrato no fio sem efeito local.

## Divergências contra `game-entry-map.md` — atualização vanilla 2026-10-10

* Habilidades/desbloqueio, portas/alavancas, world events e pickups estão
  marcados como "Implementado" no mapa, mas o `src/` atual só tem sender
  (`SendSyncAbility/Lever/Door/WorldEvent`) + relay no servidor; falta o
  aplicador local. Tratar como **relay sem aplicação** até o handler voltar.
* `MoonGuid`, `PickupBase`, `SeinPickupMPProcessor`, `WorldSyncManager`,
  `SeinVisuals`, `CleverAnimationSet`, `IAttackable`,
  `ISpiritFlameAttackable`, `Targets.Attackables`, `SeinStompMP`,
  `SeinSpiritMP`, `EntityController`: zero ocorrências no `src/` atual
  (grep 2026-10-10). São tipos do jogo ou de build MP antiga —
  **confirmado em parte via `Assembly-CSharp.dll` em 2026-10-10**
  (REA `ev_b8a19444de607e2f2e70b01b728fa75e5f4d8751b8af89db15853e94dfbf4f3b**):
  `MoonGuid`, `PickupBase`, `IAttackable`, `ISpiritFlameAttackable`,
  `Game.Targets`, `SeinStomp`, `SeinSpiritFlameAbility` **existem no vanilla**;
  `WorldSyncManager`, `SeinPickupMPProcessor`, `SeinSpiritMP`, `SeinStompMP`,
  `SeinVisuals`, `CleverAnimationSet`, `EntityController` **não existem** nem
  no vanilla (nomes de build MP antiga ou **a confirmar** em outro assembly).
* Correções vanilla 2026-10-10: `AbilityType` tem 44 valores (não 15);
  `Boss`/`SpiritWell`/`Breakable` têm zero tipos (chefes = `Kuro*` +
  chaves/`SeinWorldState`; save/teleporte = `SavePedestal`+`SoulFlame`;
  `SYNC_BREAKABLE` sem alvo vanilla); `Door` (cena) ≠
  `DoorWithSlots`/`EnergyDoor` (orbes); `IAttackable` tem 9 métodos
  (não só Stomp/Flame). Ver `game-entry-map.md`.
* `CoopConfig.cs` tem 7 bools (`AllowCustomColors`, sem `EntitySync`);
  o fio `CONFIG_SYNC 16` e o `ConfigStore` do servidor têm 8
  (`ClientColors` + `EntitySync`) — **a confirmar** unificação.
* `SKILL 7` (`CoopSkillType Spirit/Stomp`) ainda tem relay no servidor,
  mas sem consumidor no cliente.

## Verificação

* REA `inspect_managed_artifact` no caminho acima + `grep` em `src/` pelos
  padrões da seção anterior + leitura de
  `NetworkService.cs:990-1036`. Sem execução do jogo nesta rodada; efeitos
  visuais seguem **a confirmar** em run com 2 clientes (ver
  [operações](operations.md)).
