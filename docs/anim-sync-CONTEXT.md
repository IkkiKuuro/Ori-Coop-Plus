# Fase: sincronização de animações de jogadores — Context

**Gathered:** 2026-10-05
**Status:** Ready for planning

> Nota: este repositório ainda não possui `.planning/ROADMAP.md` — não há
> número de fase formal. Este arquivo em `docs/` é o registro canônico da
> discussão até que o roadmap seja criado.

## Phase Boundary

Retrabalhar a sincronia de animações de movimentação do Ori remoto para que o
puppet reproduza fielmente o jogador local: sem loop de animação quando parado
e sem cair em sprite aleatório. Escopo: inventário completo das anims de
movimentação, nova estratégia de sincronia (enum como autoridade), novo
transporte de rede sem fragmentação, aplicação com histerese no puppet e
diagnóstico capaz de explicar cada transição. Não inclui novas capacidades
(ex.: sync de inimigos/entidades, descoberta LAN, internet) — essas pertencem a
outras fases.

## Implementation Decisions

### Inventário de anims

- **D-01:** Levantar a lista real com dump duplo: dump em runtime de todos os
  `TextureAnimationWithTransitions` + `CurrentAnimation` do Sein, confirmado por
  inspeção manual nos assemblies do jogo (dnSpy/ILSpy).
- **D-02:** Escopo fechado nos 12 estados do enum `ActionVisualState` já
  existente (Idle, Running, Jump, DoubleJump, Falling, WallSlide, WallJump,
  Bash, Glide, ChargeJump, Stomp, Dash). Hoje o fallback por substring só cobre
  7 — Glide/Dash/Stomp/ChargeJump/WallJump ficam sem fallback e precisam entrar
  agora. *(Implementado: `s_nameToState` explícito + `docs/anim-catalog.md`;
  aliases SEED a calibrar via dump F8.)*
- **D-03:** Catalogar como enum + hash: `ActionVisualState` (1 byte) é a chave
  de sincronia; nome/hash FNV1a fica como debug/fallback de resolve exato.
  Isso elimina a causa do "sprite aleatório" (substring `Contains("idle")`
  escolhendo o primeiro clipe com o termo).
- **D-04:** Registrar em dois lugares: tabela em `docs/` (nova página ou
  extensão do protocolo) com nome real → hash → `ActionVisualState`, mais
  dicionário explícito no código. Downstream lê os dois.

### Estratégia de sincronia

- **D-05:** Enum manda. Quando nome/hash e estado divergirem, o puppet toca o
  clipe do `ActionVisualState` e ignora o nome divergente.
- **D-06:** Sender lê velocidade real (`Sein.Speed`) + `grounded` real +
  `FacingLeft` real no `Sein` local. Acaba a inferência por delta de posição no
  remoto (delta/dt com clamp e Lerp é ruidoso e sustenta o "loop parado").
  `PlayerStateReader` já lê `Speed`, mas `NetworkService` hoje descarta — passar
  a enviar.
- **D-07:** Envio on-change + heartbeat: só envia anim quando estado/nome muda,
  mais heartbeat 2–5 Hz para corrigir perda UDP. Substitui o spam atual
  (POSITION + ANIM a cada FixedUpdate).
- **D-08:** Sob perda/atraso, o remoto deriva o estado localmente a partir de
  vel + grounded reais (parado vira Idle na hora) em vez de congelar a última
  anim.

### Transporte de rede

- **D-09:** Pacote novo com ID novo (não estender POSITION/ANIM no fim). —
  **Reversibility:** one-way — quebra o contrato de pacotes; exige cliente e
  servidor do mesmo build.
- **D-10:** Payload = struct `AnimationSyncData` existente
  (`State:byte` + `Flags` com FacingLeft/IsGrounded + `AnimNameHash:uint` +
  `SpeedX/Y:float`). Hoje o struct existe mas ninguém o trafega — só
  `PlayerSnapshot` com `Name` string.
- **D-11:** Servidor só repassa o pacote novo como recebeu, sem fundir e sem
  inferir. A fusão por jogador + heurística `looksLikeAnimPacket` morre junto
  com a fragmentação.
- **D-12 (sem legado):** Sem compatibilidade retroativa com os fluxos
  POSITION/ANIM fragmentados. Remover handlers antigos e heurísticas de fusão;
  não manter fallback temporário. Decisão do usuário: "não usamos nenhum código
  legado/antigo, o que precisar fazer será feito". — **Reversibility:** one-way
  — builds antigos deixam de interoperar.

### Aplicação no puppet

- **D-13:** Troca de clipe com histerese: só troca quando o estado muda, com
  debounce 100–200 ms e histerese de velocidade (ex.: entra em Run acima de
  limiar alto, sai abaixo de limiar baixo). Mata a oscilação Run↔Idle que parece
  "loop quando parado".
- **D-14:** Clipe desconhecido (hash/nome fora do catálogo) mantém a última
  anim válida — nunca chuta para Idle genérico. Fail-closed em vez de
  fail-open.
- **D-15:** Quem dirige a anim continua sendo só o
  `SpriteAnimatorWithTransitions.SetAnimation`, com `CharacterAnimationSystem`
  preservado porém desligado (comportamento validado pós-bug #2 — destruí-lo
  congelava o puppet). Não religar/dirigir via CAS nesta fase.
- **D-16:** Espelhamento sempre via `CharacterSpriteMirror.FaceLeft` quando
  disponível; restart do clipe do zero somente em troca de estado (nunca em
  loop de reaplicação do mesmo clipe).

### Diagnóstico

- **D-17:** Log de transição full: estado enviado → recebido → aplicado +
  motivo (trocou / manteve última / desconhecido / histerese segurou). Hoje o
  `ReplicationObservability` só loga quando `animName` muda + resumo a cada 3 s,
  sem dizer por que caiu em fallback.
- **D-18:** Verbosity atrás de flag verbose/config (padrão desligado, liga via
  cfg ou comando) para não inundar `LogOutput.log`.
- **D-19:** Botão in-game com 2 abas: aba 1 = logs do mod, aba 2 = logs do
  BepInEx. (Pedido literal do usuário; estende o HUD atual de
  nick/coords/ping.)
- **D-20:** Comando de dump do catálogo (nome → hash → estado) no log + HUD,
  para validar a cobertura dos 12 estados do inventário.

### Agent's Discretion

- Thresholds exatos da histerese (ex.: 0.3/0.6), duração do debounce
  (100–200 ms) e frequência do heartbeat (2–5 Hz): researcher/planner calibram
  com o dump real e teste com 2 clientes.
- Layout exato do botão/abas de logs: seguir padrões da UI nativa existente
  (`docs/native-ui-architecture.md`).

## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Protocolo e contrato

- `docs/protocol.md` §"Fragmentacao POSITION/ANIM (causa raiz do bug #2)" —
  por que não aplicar pacotes isolados; regra de fusão atual a ser removida.
- `docs/protocol.md` §"Regras para mudancas" — IDs, ordem de campos, validação
  de length (vale mesmo com ID novo: mudar cliente+servidor juntos).
- `src/OriCoopPlus/OriCoopShared/PacketType.cs` — IDs atuais (1 POSITION,
  2 ANIM); o pacote novo precisa de ID inédito.
- `src/OriCoopPlus/OriCoopShared/AnimationSyncData.cs` — struct a reutilizar
  como payload (State/Flags/Hash/SpeedX/Y).

### Arquitetura e código atual

- `docs/architecture.md` — separação cliente BepInEx / servidor dedicado,
  ciclo de inicialização, autoridade de cada lado.
- `docs/code-map.md` — onde fica cada classe (Client, Patches, Networking…).
- `docs/bepinex-architecture.md` — camadas do plugin, restrições de C# 5
  (sem `$""`, sem `?.`) e dependências de `Assembly-CSharp`.
- `src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs` — leitura atual
  (`Sein.Speed`, `FaceLeft`, `CurrentAnimation.name` via `TextureAnimation`).
- `src/OriCoopPlus/OriCoopBepInEx/Patches/SeinCharacterPatch.cs` — publica
  snapshot a cada FixedUpdate do `Sein` local.
- `src/OriCoopPlus/OriCoopBepInEx/Networking/NetworkService.cs`
  (`SendPlayerSnapshot`, `ReadServerPacket`) — fragmentação atual
  POSITION sem vel/anim + ANIM só com nome; handshake e `WriteLegacyString`.
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerManager.cs` — fusão por
  jogador + velocidade inferida por delta/dt (a remover com o pacote novo).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePlayerPuppet.cs`
  (`ApplySnapshot`, `ApplyAnimation`) — resolve atual e guarda
  `SetAnimation(target, true)`; interpolação com snap > 15u.
- `src/OriCoopPlus/OriCoopBepInEx/Client/AnimationRegistry.cs` (`Prewarm`,
  `Resolve`, `InferStateFromMovement`) — catálogo por substring e thresholds
  atuais (`|vx|>0.4`, `vy±1.0`).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemotePuppetFactory.cs`
  (`EnsureTemplate`, `CleanPuppetComponents`) — clonagem inativa do Sein,
  whitelist de componentes, CAS preservado-desligado (bug #2).
- `src/OriCoopPlus/OriCoopBepInEx/Client/RemoteVisualController.cs` — material
  próprio por puppet, `StripExtraLights`, watchdog de visibilidade (não tocar
  em `sharedMaterial`/alpha).
- `src/OriCoopPlus/OriCoopBepInEx/Patches/AnimationPrewarmPatch.cs` — prewarm
  no `CharacterAnimationSystem.Start`.
- `src/OriCoopPlus/OriCoopBepInEx/Diagnostics/ReplicationObservability.cs` —
  `TrackPacket`/`LogVisibilityEvent` atuais a estender.

### Operação e validação

- `docs/operations.md` §"Protocolo de Validação de Renderização e Animação" —
  sequência Idle→Run→Pulo→PuloDuplo→WallSlide→Bash→Queda e métrica
  `[OBSERVABILITY][NET-METRICS] Dropped: 0`.
- `docs/operations.md` §"Diagnostico rapido" (linhas de anim invisível e de
  build/C# 5) — sintomas conhecidos e restrição de linguagem do plugin.
- `docs/native-ui-architecture.md` — padrões para o botão in-game de 2 abas.

## Existing Code Insights

### Reusable Assets

- `AnimationSyncData` (struct pronto, com `ComputeFnv1aHash`): virar payload do
  pacote novo — hoje parado.
- `AnimationRegistry`: base do catálogo (prewarm + caches por hash/nome); trocar
  heurística de substring por dicionário explícito nome→estado.
- `ReplicationObservability`: base do diagnóstico (contadores + eventos de
  visibilidade); estender para transição full + verbose flag.
- HUD existente (nick/coords/ping no canto superior esquerdo): estender com
  estado de anim + idade do último pacote.

### Established Patterns

- C# 5 no plugin BepInEx (`.NET Framework 3.5` via `build.ps1`/`csc.exe`): sem
  interpolação `$""`, sem `?.` — ver `docs/operations.md`.
- Strings de rede em formato legado: `int32 length` + ASCII (`WriteLegacyString`),
  nunca `BinaryWriter.Write(string)` puro.
- Clonagem inativa do Sein (`SetActive(false)` antes de `Instantiate`) +
  `try/finally` restaurando `Game.Characters.Sein/Current` + `DontDestroyOnLoad`.
- Cliente e servidor sempre compilados e testados como par (2 clientes +
  desconexão/reconexão).

### Integration Points

- Sender: `SeinCharacterPatch.FixedUpdate` → `PlayerStateReader.Read` →
  `OriCoopPlugin.Publish` → `NetworkService.SendPlayerSnapshot` → UDP →
  servidor dedicado → rebroadcast → `NetworkService.ReadServerPacket` →
  `RemotePlayerManager.HandleSnapshot` → `RemotePuppetFactory.CreatePuppet` →
  `RemotePlayerPuppet.ApplySnapshot/ApplyAnimation` →
  `SpriteAnimatorWithTransitions` + `CharacterSpriteMirror`.
- Diagnóstico: `ReplicationObservability` + `LogOutput.log` + HUD + futuro
  botão de 2 abas.

## Specific Ideas

- "Não usamos nenhum código legado/antigo, então o que precisar fazer será
  feito e não será usado código legado que não representa a arquitetura atual."
  → sem compat retroativa; remover, não adaptar.
- "Botão in-game mostrando duas abas, os logs do mod e outra pro BepIn" →
  visualizador de logs em jogo, não só arquivo.
- Sintomas-guia: loop de animação quando parado; sprite aleatório em vez da
  pose correta.

## Deferred Ideas

None — discussão permaneceu dentro do escopo da fase. Fora de escopo conhecido
(para outras fases): sync de inimigos/entidades, descoberta automática de
servidores na LAN, jogo pela internet, submenu "Ori Coop" do pause.

---

*Phase: sincronização de animações de jogadores*
*Context gathered: 2026-10-05*
