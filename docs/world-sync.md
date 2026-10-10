# Sincronização do mundo (investigação 2026-10-10)

Como cada objeto do mundo funciona no vanilla e o que falta para
sincronizá-lo para todos. Evidências: REA `inspect_managed_members` do
`Assembly-CSharp.dll` (`ev_b8a19444...`, SHA-256 `18e70a4c...`) e do
`ORIDEClientModule.dll` legado (`ev_7bebd160...`, 36 tipos / 130 métodos).

## Chave de identidade: `MoonGuid(A,B,C,D)`

Todo objeto sincronizável é identificado por 4 `int`s. `MoonGuid.Serialize`
grava `A,B,C,D`; `WorldEventsManager.Serialize` persiste cada entrada como
`MoonGuid + Value`. O fio do mod carrega o GUID desmontado em ints —
é assim que remetente e receptor falam do **mesmo** objeto.

## Alavanca (`Lever` + `SeinLever`)

- Vanilla: `OnPushLeverLeft/Right/Middle` gravam `Direction`
  (`LeverDirections`: `Left/Middle/Right`) e disparam eventos/sons;
  `SetLeverDirection` despacha para o mesmo caminho; `Serialize` persiste
  `Direction`; `LeverMode`: `LeftRightToggle`, `LeftRightGrab`,
  `LeftMiddleRightSpring`, `LeftMiddleRightStay`. Lado do jogador:
  `SeinLever` (`EnterLever`, `GrabLever`, `PushLeverLeft/Right/Middle`,
  `UpdateLeverDirection`, `ReleaseLever`).
- Fio: `SYNC_LEVER` 11 = marcador + `MoonGuid(4)` + `direction(1)` = 24 B;
  relay = marcador + `fromId` + 5 ints = 28 B (`GameHandlers.cs:435-449`,
  `BuildStampedPayload`). Confiável (ACK + retry 250 ms x3).
- Aplicação legada (`ReceiveLeverSync`): guarda `IsSyncing`, varre os
  `Lever` da cena, compara os 4 ints do GUID e chama
  `SetLeverDirection(direction)` no igual, depois solta a guarda.
  Sem eco: quem aplica não republica.
- Falta no `src/` atual: patch detector (`LeverLeft/Right/MiddlePatch`
  existiam no legado) + aplicador. `SendSyncLever` já existe
  (`NetworkService.cs:353-372`).

## Porta de keystones (`DoorWithSlots`) e `EnergyDoor`

- Vanilla: `NumberOfOrbsRequired/Used`, `m_slotsPending/m_slotsFilled`,
  `CurrentState` (máquina de estados com `switch` em `FixedUpdate`, 173
  instruções), `OnOpenedAction`/`OnFailAction`, `RestoreOrbs`,
  `Serialize` (orbes + estado). `EnergyDoor` varia por energia
  (`AmountOfEnergyRequired/Used`). `TimedDoor` (`OpenTheDoor`,
  `CloseTheDoor`, `PuzzleSolved`) é puzzle com timer — fora do escopo.
  `Door` (`OnTriggerStay` → `SeinDoorHandler`) é transição de cena,
  não porta sincronizável.
- Fio: `SYNC_DOOR` 12 = marcador + `MoonGuid(4)` = 20 B; relay 24 B
  (`GameHandlers.cs:451-465`). Abrir **é** o evento — sem campo de estado.
- Aplicação legada (`ReceiveDoorSync` + lista `_openedDoors` com
  `Contains/Add`): guarda `IsSyncing`, acha o `DoorWithSlots` pelo GUID,
  ignora se já está aberto, senão grava `CurrentState` = aberto e dispara
  `OnOpenedAction.Perform()` (efeitos/som), uma única vez por porta.
- Falta no `src/` atual: patch detector (`DoorOpenedPatch` existia no
  legado) + aplicador com `_openedDoors` e guarda `IsSyncing`.
  `SendSyncDoor` já existe (`NetworkService.cs:374-392`).

## Eventos globais (`SetWorldEventAction` + `WorldEventsRuntime`)

- Vanilla: `SetWorldEventAction(WorldEvents, State).Perform` resolve via
  `WorldEventsManager.Find` e grava `WorldEventsRuntime.Value = State`
  (23 instruções de IL). A tabela é `Dictionary<MoonGuid,
  WorldEventsRuntime>` + `WorldEventsManager.Instance`; `Serialize`
  persiste GUID + valor. Flags derivadas de progressão:
  `Sein.World.Keys` (`GinsoTree`, `ForlornRuins`, `MountHoru`) e
  `Sein.World.Events` (`GinsoTreeEntered`, `MistLifted`, `WaterPurified`,
  `WindRestored`, `GumoFree`, `SpiritTreeReached`, `WarmthReturned`,
  `DarknessLifted`) + `SeinWorldState(WaterCleansed, WindReleased)` —
  seguem por consequência quando o evento aplica; sincronizá-las em
  separado é follow-up, não requisito.
- Fio: `SYNC_WORLDEVENT` 14 = marcador + `MoonGuid(4)` + `state(1)` = 24 B;
  relay 28 B (`GameHandlers.cs:467-481`). Confiável.
- Aplicação legada (`ReceiveWorldEventSync`): guarda `IsSyncing`, busca a
  entrada pelo GUID em `WorldEventsManager.Instance`, grava `Value`, ou
  cria `new WorldEventsRuntime(state)` se o receptor ainda não tem a
  entrada (receptor atrás do emissor não quebra). Nomes textuais via
  `GetIDFromName/GetNameFromID` são só diagnóstico.
- Falta no `src/` atual: patch detector (`WorldEventPatch` existia no
  legado) + aplicador. `SendSyncWorldEvent` já existe
  (`NetworkService.cs:394-413`).

## Gating e reentrância (obrigatório nos aplicadores)

O legado prova 3 regras que o novo aplicador precisa repetir:

1. `IsSyncing=true` durante a aplicação — sem isso, aplicar
   `SetLeverDirection`/abrir porta dispara o próprio detector e gera eco.
2. Deduplicação: `_openedDoors` (`Contains/Add`) para portas; para
   alavancas/eventos, comparar estado atual antes de aplicar.
3. Direção do fluxo: só o jogador local publica; receptor nunca republica
   (mesma regra D-07 do `PlayerEventCore`).

## O que NÃO sincronizar

- `SavePedestal` (`TeleportOnPedestal`, `SaveOnPedestal`, `MarkAsUsed`,
  `m_used`): save/teleporte são por jogador. Transmitir marcaria o save
  alheio como usado e teleportaria outros — fora do escopo.
- `Sein.World.Keys/Events`, `SeinWorldState`: derivados dos eventos;
  follow-up, não pacote próprio nesta etapa.
- `TimedDoor`: puzzle com timer local; **a confirmar** se faz sentido.
- `SYNC_BREAKABLE` 13: sem classe `Breakable` no vanilla e sem relay no
  servidor (`GameHandlers.cs:483-497` só loga). Manter fora até existir alvo.
- `Door` (troca de cena): cada cliente atravessa por conta própria.

## Ordem de implementação sugerida

1. Aplicadores cliente (`IsSyncing` + busca por `MoonGuid` + `SetLeverDirection`
   / `CurrentState` + `OnOpenedAction` / `Value`) — receptor hoje descarta
   tudo (`NetworkService.cs:990-1036`).
2. Detectores Harmony (`LeverLeft/Right/MiddlePatch`, `DoorOpenedPatch`,
   `WorldEventPatch` no padrão do legado) chamando `SendSync*` existente.
3. Teste com 2 clientes + `DummyBot` (`/dummy` → `TriggerLever`/`TriggerDoor`
   emitem o formato relay de 28/24 B e servem de gerador de eventos).
4. Depois: `Sein.World.*` como follow-up observável, se algum fim de fase
   não destrava no receptor.

Valores int de `LeverDirections` e do estado "aberto" de `CurrentState`
estão **a confirmar** em run (não constam nos metadados inspecionados).
