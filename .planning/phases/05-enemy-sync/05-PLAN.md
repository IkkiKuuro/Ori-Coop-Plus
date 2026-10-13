---
phase: 05
plan: "05"
type: tracer
wave: 1
depends_on: []
files_modified:
  - src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs
  - src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs
  - src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs
  - docs/animation-enemies-damage-data.md
  - docs/game-entry-map.md
autonomous: false
requirements: [REQ-06]
---
<objective>
Provar de ponta a ponta que o estado de cada inimigo no cliente remoto é idêntico ao que o host vê no mundo dele. Sender deriva ActionVisualState via EntitySync, receiver aplica animação no puppet com MoonGuid keyed dedup, fail-closed em dados desconhecidos. Ao fim, ambos os clientes mostram a mesma animação/posição do enemy idêntica à do host — sem sprite aleatório, desvio de anim ou oscilação Run↔Idle, em teste com 2 clientes.
</objective>

<tasks>
<task name="EntitySync: configurar event para inimigos">
<read_first>
- src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs (event EntitySyncChanged linha 94)
- src/OriCoopDedicatedServer/OriCoopDedicatedServer/ConfigStore.cs (EntitySync flag bit 7)
- docs/animation-enemies-damage-data.md (tipos de enemy: AcidSlugEnemy, RammingEnemy, DashOwlEnemy, etc.)
</read_first>
<action>
Garantir que o event `EntitySyncChanged` em NetworkService.cs esteja devidamente inscrito em OriCoopPlugin.cs (linha 137) e que o bit EntitySync no ConfigStore esteja acessível. Mapear o primeiro enemy type vanilla (AcidSlugEnemy) para receber MoonGuid(A,B,C,D) via field ou endereço do entity. O EntitySync payload conterá: posição (3 floats) + ActionVisualState (1 byte enum).
</action>
<verify>Build do cliente via build.ps1 sem erros; EntitySyncChanged event dispatch não causa exceção.</verify>
<acceptance_criteria>
- Source: NetworkService.cs contém event EntitySyncChanged { get; set; }
- Source: ConfigStore.EntitySync flag bit 7 é acessível
- Behavior: Pelo menos 1 tipo de enemy mapeado com MoonGuid
</acceptance_criteria>
</task>

<task name="EntitySync payload: posição + ActionVisualState">
<read_first>
- src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs (ReadServerPacket, WriteLegacyString padrões)
- docs/protocol.md (envelope 24B, IDs never reused)
- CONTEXT.md phase 05: EntitySync coverage = posição + animation state only
</read_first>
<action>
Em NetworkService.ReadServerPacket, adicionar leitura do pacote EntitySync: `pos: Vector3` (3 floats) + `animState: byte` (ActionVisualState ordinal). Em NetworkService.WriteServerPacket, adicionar escrita no mesmo ordem. Manter compatibilidade com o envelope 24B existente e a regra "IDs never reused" (novo ID EntitySync distinct de PLAYER_STATE 18).
</action>
<verify>Build do cliente sem erros; pacote EntitySync desmontado corretamente em LogOutput.log.</verify>
<acceptance_criteria>
- Source: ReadServerPacket lê pos (3 floats) + animState (1 byte) no payload EntitySync
- Source: WriteServerPacket escreve pos (3 floats) + animState (1 byte) no payload EntitySync
- Behavior: PACOTE 18 (PLAYER_STATE) permanece inalterado; EntitySync é um novo protocolo paralelo
</acceptance_criteria>
</task>

<task name="MoonGuid assignment for enemies">
<read_first>
- docs/game-entry-map.md § MoonGuid(A,B,C,D) — "é assim que remetente e receptor falam do mesmo objeto"
- CONTEXT.md phase 05: Identifier = MoonGuid existente da fase 04
- REA inspect_managed_members output (se disponível) sobre fields de entity
</read_first>
<action>
Para o primeiro type de enemy vanilla (AcidSlugEnemy), adicionar assignamento de MoonGuid(A,B,C,D) no Awake/Start do enemy MonoBehaviour. O GUID pode ser derivado do field `guid` existente no vanilla entity ou calculado como hash do endereço do objeto + índice do tipo. Se o GUID não puder ser determinado, usar fallback hash semente per-session. O MoonGuid será a chave no dicionário _openedDoors-style para dedup de EntitySync.
</action>
<verify>Build do cliente sem erros; enemy no jogo exibe MoonGuid no log verbose (opcional).</verify>
<acceptance_criteria>
- Source: AcidSlugEnemy (ou primeiro enemy type) tem field MoonGuid preenchido no Awake
- Behavior: MoonGuid usado como chave de dedup no EntitySync pipeline (sem duplicates na mesma sessão)
</acceptance_criteria>
</task>

<task name="RemotePlayerPuppet: aplicar AnimationState via EntitySync">
<read_first>
- src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs (já tem ApplySnapshot/anim authority)
- CONTEXT.md phase 05: EntitySync coverage = posição + animação; morte = vanilla
- FASE 01 DECISION: puppets visual-only, fail-closed, sem dano/colisão
</read_first>
<action>
Em RemotePlayerPuppet, adicionar campo `_confirmedAnimState:ActionVisualState`, `_pendingAnimState`, `_pendingSince:float`. Em `OnEntitySync`, ler `pos` + `animState` e preencher `_pendingAnimState = (ActionVisualState)animState`. `ApplySnapshot` passa a receber `animState` e s chama `ApplyAnimation` quando `animState != _confirmedAnimState` por mais de 0.15 s (`Time.time`). `Resolve` com hash-'nome'-estado, e se `targetClip==null` **mantém anim atual** (sem fallback para Idle). `SetAnimation(targetClip, true)` só na troca confirmada. Fail-closed: animHash desconhecido (0) não troca a anim visível.
</action>
<verify>Teste 2 clientes: enemy spawn no cliente A → animation state sincronizado no cliente B; idle estável; sem sprite aleatório em 60 s.</verify>
<acceptance_criteria>
- Behavior: enemy remoto em estado Idle → puppet mantém Idle estável (sem oscilar Run↔Idle)
- Behavior: enemy remoto em estado Run → puppet reproduz Run sem sprite aleatório
- Behavior: animHash desconhecido (0) nÃo troca a anim visÃvel (mantém atual)
</acceptance_criteria>
</task>

<task name="Teste 2 clientes + DummyBot: Enemy sync">
<read_first>
- docs/operations.md (checklist 2 clientes + DummyBot)
- CONTEXT.md phase 04: test strategy = 2 clients + DummyBot + SmokeProbe
- FASE 03 VERIFICATION.md patterns (Spirit Flame pilot test)
</read_first>
<action>
Cenário de teste: spawn de enemy idêntico nos 2 clientes. Cliente A é autoridade; EntitySync packets fluiem A → B via UDP. DummyBot fica próximo do enemy e valida animação. Verificar: (1) Enemy no cliente A e B inicia com mesma animState; (2) Mover enemy no cliente A → animState reflete no B; (3) Parar enemy → ambos os puppets retornam a Idle estável após 0.15 s; (4) Nenhum sprite aleatório em 60 s de teste automatizado.
</action>
<verify>Teste automatizado: 2 clientes + DummyBot, enemy spawn + movimentação + parada por 60 s. Logs: "EntitySync received", "Anim applied", "No random sprite". Falsos positivos: eco-gating verificado (mesmo estado → sem re-aplicar).</verify>
<acceptance_criteria>
- Behavior: enemy spawn sincronizado nos 2 clientes (mesmo animState inicial)
- Behavior: movimentação do enemy reflete no outro cliente
- Behavior: parada → Idle estável após debounce 0.15 s
- Behavior: 60 s teste sem sprite aleatório
</acceptance_criteria>
</task>

<task name="Log verboso de animação + docs">
<read_first>
- src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs (Config.Bind pattern)
- src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs
- docs/protocol.md
- CONTEXT.md phase 05: next phase (06) = damage/health/save sync
</read_first>
<action>
Adicionar `Config.Bind("Diagnostics", "AnimVerbose", false, ...)` e método `OriCoopPlugin.IsAnimVerbose()`. Em `RemotePlayerPuppet.OnEntitySync`, quando verbose, `LogInfo("[ENEMY-ANIM] P{playerId} recv={animState} applied={clipName} reason={trocar|manter|histerese}")` via `string.Format` (sem `$""`). Atualizar `docs/animation-enemies-damage-data.md`: nota de que EntitySync agora cobre animation state para enemies (antes só coverage era player). Manter `docs/protocol.md` atualizado com novo ID EntitySync e ordem de campos.
</action>
<verify>`docs/animation-enemies-damage-data.md` contém nota EntitySync; com `AnimVerbose=true`, `LogOutput.log` mostra linhas `[ENEMY-ANIM]`.</verify>
<acceptance_criteria>
- Source: `protocol.md` contém novo ID EntitySync + ordem de campos
- Docs: `animation-enemies-damage-data.md` nota a cobertura EntitySync
- Behavior: flag verbose controla logs [ENEMY-ANIM]; desligada por padrão
</acceptance_criteria>
</task>
</tasks>

<verification>
1. `build.ps1` (cliente) e `dotnet build` (servidor) sem erros.
2. Deploy nas pastas do jogo com processos fechados.
3. Servidor `--auto --max-players 2 --port 7777` + 2 clientes locais:
   enemy spawn → animState sincronizado, idle estável, sem sprite aleatório por 60 s.
3. [`OBSERVABILITY][NET-METRICS]` com `EntitySync dropped: 0` em rede local.
</verification>

<success_criteria>
- EntitySync packet trafega de ponta a ponta com posição + ActionVisualState para enemies
- Puppet remoto usa autoridade do enum com histerese de 0.15 s
- MoonGuid keyed dedup sem duplicates por sessão
- Clipe desconhecido mantém última anim (fail-closed)
- Protocolo documentado; build par cliente+servidor instalado
</success_criteria>

<must_haves>
truths:
  - "EntitySync payload: pos (3 floats) + animState (1 byte ActionVisualState ordinal)"
  - "MoonGuid(A,B,C,D) é a chave de dedup para EntitySync de entities/inimigos"
  - "Puppet: _confirmedAnimState/_pendingAnimState/_pendingHisDebounce 0.15s"
  - "Fail-closed: animHash desconhecido mantém animação atual, nunca chuta para Idle"
  - "Transição logada atrÃs de flag AnimVerbose desligada por padrÃo"
</must_haves>

<artifacts this phase produces>
- EntitySync payload writer/reader/relay (NetworkService + campo pos + animState)
- MoonGuid assignment para o primeiro type de enemy vanilla (Awake/Start)
- RemotePlayerPuppet._confirmedAnimState + _pendingAnimState + debounce 0.15s
- Histerese de troca de clipe baseada em confirmação (não em hash absoluto)
- Diagnostics/AnimVerbose estendido para [ENEMY-ANIM] logs
- docs/protocol.md �� novo ID EntitySync + ordem de campos
- docs/animation-enemies-damage-data.md �� nota de cobertura EntitySync
</artifacts this phase produces>