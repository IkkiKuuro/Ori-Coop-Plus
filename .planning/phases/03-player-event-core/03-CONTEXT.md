# Phase 3: Core de eventos do personagem (PlayerEventCore + piloto Spirit Flame) — Context

**Gathered:** 2026-10-06
**Status:** Ready for planning

> Nota: este repositório ainda não possui `.planning/ROADMAP.md` — não há
> número de fase formal. Este arquivo em `.planning/phases/03-player-event-core/`
> (espelhado em `docs/player-event-core-CONTEXT.md`) é o registro canônico da
> discussão até que o roadmap seja criado.

## Phase Boundary

Criar o core do personagem no cliente BepInEx que escuta e sincroniza para o
servidor tudo que o Ori faz — VFX, SFX, animações gerais, ataques etc. Nesta
fase o escopo foi fatiado por decisão do usuário ("uma coisa de cada vez, cada
categoria é uma fase completa"): entrega = **infra do core + 1 evento piloto
(Spirit Flame)**. As demais categorias (Stomp, Bash, ChargeJump, Dash, Glide,
VFX/SFX genéricos, anims gerais, morte/respawn, dano/vida, entidades do mundo)
são fases futuras, não entram aqui. Não inclui sync de inimigos/entidades além
do piloto, descoberta LAN, jogo pela internet, submenu de pause — essas
pertencem a outras fases.

## Implementation Decisions

### Escopo e fatiamento

- **D-01:** Uma categoria por fase. Cada categoria de evento (ataque, VFX, SFX,
  anim geral, morte etc.) é uma fase completa de implementação — sem "tudo
  junto" nesta fase.
- **D-02:** Fase 3 = core + piloto. Nem só infraestrutura sem evento real, nem
  core + categoria completa: entrega o `PlayerEventCore` funcionando ponta a
  ponta com 1 evento piloto para validar o caminho.
- **D-03:** Piloto = Spirit Flame (ataque de projétil do Ori). Motivo: o mais
  visível e simples de validar com 2 clientes.
- **D-04:** Só Spirit Flame nesta fase. Stomp/Bash/ChargeJump/Dash/Glide, VFX e
  SFX genéricos, anims gerais e morte/respawn ficam para fases futuras. O core
  deve nascer extensível (registro por tipo) para recebê-los sem reescrever.

### Captura e escuta (como o core ouve o Ori)

- **D-05:** Core central (`PlayerEventCore` + bus/registro). Patches Harmony só
  detectam e chamam o core; o core monta o evento e publica no
  `NetworkService`. Nada de lógica de rede espalhada nos patches.
- **D-06:** Detecção via Harmony (Prefix/Postfix nas classes de habilidade do
  Sein que disparam o Spirit Flame), não polling em FixedUpdate. Motivo:
  eventos discretos e rápidos se perdem no polling; Harmony pega o instante do
  disparo. (Polling continua existindo para `PLAYER_STATE` 18 — não é
  substituído.)
- **D-07:** Só o jogador local publica. Filtro estrito
  (`__instance == Game.Characters.Sein`, mesmo padrão do `SeinCharacterPatch`
  atual); puppet/remoto nunca publica nem republica — evita eco infinito.
- **D-08:** Reusar o código MP atual de habilidades como base/referência (fluxo
  de `SKILL` 7 / `SYNC_ABILITY` 10 e classes MP de Spirit/Stomp onde existirem
  no jogo ou no mod). Não reescrever do zero ignorando o que já funciona; o
  researcher deve mapear o que existe antes de propor o novo.

### Transporte por tipo (como viaja no fio)

- **D-09:** Piloto Spirit Flame = unreliable sequenciado (mesma classe de
  `PLAYER_STATE` 18, D-09 da fase 2): sem ACK, sem retry. Perda se resolve no
  próximo tiro. Motivo: tiro é frequente e visual; latência baixa importa mais
  que garantia por tiro.
- **D-10:** Pacote com ID novo (não reutilizar `SKILL` 7, `SYNC_ABILITY` 10 nem
  estender `PLAYER_STATE` 18) — **Reversibility:** one-way — redefine o contrato
  publicado; exige cliente e servidor do mesmo build e `docs/protocol.md`
  atualizado na mesma mudança (regra D-15 da fase 2).
- **D-11:** Payload mínimo no piloto: direção + posição de origem + timestamp
  (+ `clientId` no header do envelope, nunca no corpo). Suficiente para o
  puppet replicar o visual; sem cooldown/alvo/dano nesta fase. Ordem de campos
  congelada após o merge (regra "preserve a ordem" de `docs/protocol.md`).
- **D-12:** Sem throttle no piloto: cada disparo envia na hora, spam livre. Se
  o teste com 2 clientes mostrar inundação de UDP, throttle entra como
  follow-up (agent's discretion calibrar limite).

### Reprodução remota (como o puppet mostra)

- **D-13:** Fidelidade visual total sem efeito gameplay: o puppet toca o mesmo
  clipe + partícula + som do disparo, mas sem dano, sem colisão, sem alterar
  estado do jogo local. Servidor só repassa bytes (mesma regra D-11 da fase 1).
- **D-14:** Projétil fake agora, física real depois. No piloto o projétil
  remoto é visual falso (sem colisão/dano); a estrutura (payload, handler,
  ponto de spawn no puppet) já nasce preparada para a física real que virá na
  fase de sincronia de entidades/mundo. Decisão explícita do usuário: "física
  real, pois os eventos e entidades do mundo serão sincronizadas também em
  próximas fases" — caminho preparado agora, simulação real na fase própria.
- **D-15:** Fail-closed em evento desconhecido: pacote de evento fora do
  catálogo mantém a última anim válida, nunca chuta para Idle genérico.
  Coerente com D-14 da fase 1.

### Agent's Discretion

- Nome exato da classe (`PlayerEventCore` vs `PlayerEventBus`), assinatura do
  registro de listeners, valores de seq/throttle futuro, layout de logs e
  posição do handler no `NetworkService`/`RemotePlayerManager`: researcher e
  planner decidem a partir do código atual.
- Mapear via dnSpy/ILSpy qual classe/método real do `Assembly-CSharp` dispara o
  Spirit Flame (nome do método para o Harmony patch) — está **a confirmar**,
  não foi verificado nesta discussão.
- Detalhe do VFX/SFX do Spirit Flame no puppet (qual prefab/partícula clonar,
  qual `AudioClip` tocar, necessidade de `AudioSource` no puppet): descobrir no
  código/jogo durante research.

## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Protocolo e contrato

- `docs/protocol.md` — envelope 24B `0x4F43`/v2, regra "mudar cliente+servidor
  juntos", ordem de campos, `PLAYER_STATE` 18, tabela de IDs removidos (1, 2, 3,
  5, -1..-7 nunca reutilizar), confiabilidade por pacote (críticos com ACK vs
  snapshots unreliable).
- `docs/anim-sync-CONTEXT.md` — D-05/D-06/D-07 (enum manda, velocidade real,
  on-change + heartbeat), D-11/D-12/D-14 (servidor só repassa, sem legado,
  fail-closed) que o core de eventos precisa honrar.
- `docs/server-rewrite-CONTEXT.md` — D-09/D-10 (unreliable vs ACK+retry),
  D-13/D-15 (camadas Transport/Session/Game, redesenho de IDs autorizado com
  quebra one-way), D-16 (operação e persistência de config).
- `src/OriCoopPlus/OriCoopShared/PacketType.cs` — IDs atuais (base do ID novo
  autorizado em D-10).
- `src/OriCoopPlus/OriCoopShared/NetProtocol.cs` — `magic`, versão, flags
  `Reliable`/`AckPresent`, `AckRetryMs`/`MaxRetries`.
- `src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs` — padrão de payload
  compacto (referência para o payload mínimo do piloto).

### Cliente (onde o core mora)

- `src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs` — leitura atual
  do Sein local (`Speed`, `FaceLeft`, `CurrentAnimation`, `Controller.IsBashing
  /IsStomping/IsDashing/IsGliding/...`); padrão de filtro e derivação de estado.
- `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs` — patch
  `FixedUpdate` com filtro `__instance == Game.Characters.Sein` + `Publish`
  (modelo do filtro local de D-07).
- `src/OriCoopPlus/OriCoopBepInEx/Plugin/OriCoopPlugin.cs` — `Publish`,
  `FindLocalSein`, fila de main-thread, ciclo de vida da rede (onde o core se
  pluga).
- `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs` — transporte
  UDP, `SendPlayerSnapshot`, `ReadServerPacket`, confiabilidade espelhada no
  cliente, `WriteLegacyString` (`int32 length` + ASCII), restrições C# 5.
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs` — ciclo de
  vida dos puppets e roteamento de snapshots (onde o evento chega no remoto).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs` — aplicação de
  snapshot/anim no puppet (onde o visual do piloto será reproduzido).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs` — puppet leve
  (só subárvore visual, whitelist de componentes — determina o que o VFX/SFX do
  piloto pode usar sem trazer gameplay junto).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs` — watchdog
  de visibilidade (não quebrar ao adicionar VFX no puppet).
- `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` — catálogo e
  fallback (onde o fail-closed de D-15 se apoia).

### Arquitetura e operação

- `docs/architecture.md` — separação BepInEx/servidor, autoridade de cada lado,
  ciclo de inicialização, regra "mesmo build".
- `docs/bepinex-architecture.md` — camadas do plugin, restrições C# 5
  (sem `$""`, sem `?.`), entrypoint `LoadingBootstrap`, dependências de
  `Assembly-CSharp`.
- `docs/code-map.md` — onde fica cada classe.
- `docs/game-and-mod.md` — vocabulário (ataque, pickup, porta/alavanca, world
  event, entity sync) e limites conhecidos.
- `docs/operations.md` — build (`dotnet build` servidor, `build.ps1` cliente),
  instalação `<ORI_DIR>\Server\` + `BepInEx\plugins\`, checklist 2 clientes +
  desconexão/reconexão, diagnóstico rápido, restrição C# 5.

## Existing Code Insights

### Reusable Assets

- `Patches/PlayerStateReader.cs` (`Read`, `DeriveFullState`, prioridade do
  `Controller.*` + nome do clipe): padrão de leitura do Sein a estender para o
  detector do Spirit Flame.
- `Patches/SeinCharacterPatch.cs` (filtro local + `Publish`): molde do filtro
  de autoridade e do ponto de publicação do core.
- `Plugin/OriCoopPlugin.cs` (`Publish`, `EnqueueMainThread`, `FindLocalSein`):
  ponto de integração do core e marshaling para a main thread do Unity.
- `Networking/NetworkService.cs` (send/receive, unreliable vs ACK+retry,
  `WriteLegacyString`): transporte que o pacote novo do piloto vai usar.
- `Client/RemotePlayerManager.cs` + `Client/RemotePlayerPuppet.cs`: recepção e
  aplicação no puppet (onde o piloto fake será instanciado).
- Servidor `Net/Game/GameHandlers.cs` + `Net/Game/PlayerStateRelay.cs`: relay
  por pacote (modelo do repasse cego do evento novo).
- Servidor `Net/Reliability/AckTracker.cs`: referência do que o piloto
  explicitamente NÃO usa (unreliable), e do que eventos críticos futuros vão usar.

### Established Patterns

- Cliente BepInEx em C# 5 / `.NET Framework 3.5` via `build.ps1`/`csc.exe`:
  sem interpolação `$""`, sem `?.`, `Action` com no máximo 4 parâmetros — ver
  `docs/operations.md`. Qualquer payload/handler novo precisa respeitar isso.
- Servidor em `.NET 8` / C# moderno via `dotnet build`; deploy exige
  jogo/servidor fechados (`<ORI_DIR>\BepInEx\plugins\`, `<ORI_DIR>\Server\`).
- Sem testes automatizados no repo — validação manual com 2 clientes
  (`docs/operations.md` checklist + `docs/anim-test-battery.md` T0–T4).
- Cliente e servidor sempre compilados e testados como par, com quebra one-way:
  sem fallback para builds antigos.
- Corpo legado preservado: pacotes de jogo mantêm o corpo byte-idêntico
  (inclui `int` inicial com o próprio ID); `clientId` do header identifica o
  remetente; relay reemite bytes sem reconstrução.

### Integration Points

- Sender (piloto): detector Harmony do Spirit Flame no Sein local →
  `PlayerEventCore.Publish` → `NetworkService.Send` (ID novo, unreliable) →
  UDP → dedicado → rebroadcast → `NetworkService.ReadServerPacket` →
  `RemotePlayerManager` → `RemotePlayerPuppet` (VFX/SFX fake + clipe).
- Servidor: `UdpTransport` + `Channel` → `Session` (token/endpoint) →
  `GameHandlers`/`PlayerStateRelay` (repasse imediato, sem fusão) → broadcast
  aos `IsReady`.
- Diagnóstico: `Diagnostics/ReplicationObservability.cs` + `LogOutput.log` +
  HUD/botão de logs (estender motivo de transição para eventos, como D-17 da
  fase 1 fez para anims).

## Specific Ideas

- "Criar o core do personagem onde ele escuta e sincroniza pro servidor tudo
  que o Ori faz ou acontece: VFX, SFX, animações gerais, ataques e etc. Tudo
  que acontece com Ori será sincronizado pro servidor, e esse core do player
  deve ser responsável por isso."
- "Uma coisa de cada vez, cada categoria é uma fase completa de implementação."
- "Física real, pois os eventos e entidades do mundo serão sincronizadas também
  em próximas fases" → piloto com projétil fake, caminho preparado para física
  real na fase de entidades.

## Deferred Ideas

- Stomp, Bash, ChargeJump, Dash, Glide como eventos próprios — fases futuras de
  categoria (uma por fase ou agrupadas, a definir no roadmap).
- VFX genéricos e SFX genéricos como categorias próprias — fases futuras.
- Animações gerais além de movimentação — fase futura.
- Morte/respawn, dano/vida/energia — fase futura.
- Física real e dano do projétil remoto + sync de entidades/inimigos do mundo —
  fase futura de entidades (cobertura real ainda **a confirmar**).
- Descoberta LAN, jogo pela internet (NAT/port-forward), submenu "Ori Coop" do
  pause — futuras fases (registrados também nas fases 1–2).

---

*Phase: 3-player-event-core*
*Context gathered: 2026-10-06*
