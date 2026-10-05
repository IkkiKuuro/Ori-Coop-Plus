# RESEARCH — sincronização de animações (anim-sync)

**Fase assumida:** 01 (sem ROADMAP.md no repo; renomear se o roadmap atribuir outro número)
**Contexto:** `docs/anim-sync-CONTEXT.md` (decisões D-01…D-20)
**Data:** 2026-10-05

> Pesquisa inline pelo orquestrador (sem runtime de subagentes nesta sessão).
> Fatos confirmados por leitura direta do código; itens abertos marcados
> **a confirmar** com caminho de investigação.

## 1. Fatos confirmados (com fonte)

### 1.1 Leitura do Sein local
- `PlayerStateReader.Read` (`src/OriCoopPlus/OriCoopBepInEx/Patches/PlayerStateReader.cs:9`):
  lê `sein.transform.position`, `sein.Speed` (Vector3) e
  `sein.Animation.Animator.CurrentAnimation.name` (tipo `TextureAnimation`).
  `FaceLeft` via `sein.FaceLeft`.
- Publicação a cada `FixedUpdate` via `SeinCharacterPatch`
  (`Patches/SeinCharacterPatch.cs:7`), só quando `__instance == Game.Characters.Sein`.
- `OriCoopPlugin.Publish` (`Plugin/OriCoopPlugin.cs:267`) preenche
  `PlayerId` e `Nick` antes de enviar.

### 1.2 Envio atual (a substituir)
- `NetworkService.SendPlayerSnapshot` (`Networking/NetworkService.cs:96`):
  pacote POSITION (`int 1` + 3×float + `bool facingLeft`) e, se nome não-vazio,
  pacote ANIM (`int 2` + string legado). Sem velocidade, sem estado, sem grounded.
- Strings: `WriteLegacyString` = `int32 length` + ASCII (`NetworkService.cs:384`).
  Servidor `Packet.Write(string)` = mesmo formato (`Core/Network/Packet.cs:119`).
  Leitura cliente valida length (`NetworkService.cs:396`).

### 1.3 Retransmissão do servidor
- `NetworkHandler.OnPacketRecived` (`Game/NetworkHandler.cs:46`):
  POSITION lê `Vector3+bool`, reescreve com `Color` + `nick` e `SendToAll`;
  ANIM lê string e reescreve com `pl.Id`. Repasse puro, sem fusão.
- IDs ocupados: 1–7, 10–17 (`OriCoopShared/PacketType.cs`). **ID 18 livre**
  para `PLAYER_STATE`. Pacotes negativos reservados: -1 handshake, -3 vars,
  -5 chat, -7 ping.
- `Packet` do servidor tem `Write(byte/int/float/bool/string/Vector3)` e
  `ReadByte/ReadInt/ReadFloat/ReadBool/ReadString/ReadVector3` — **sem uint**.
  Enviar `AnimNameHash` como `int` com cast (`unchecked((int)hash)`) e
  reconverter no cliente.

### 1.4 Puppet e animação
- Template = clone inativo do Sein (`RemotePuppetFactory.EnsureTemplate`,
  `SetActive(false)` antes de `Instantiate`); whitelist preserva
  `SpriteAnimatorWithTransitions`, `CharacterSpriteMirror`,
  `CharacterAnimationSystem` (desligado), `Renderer/MeshFilter/Transform`
  (`RemotePuppetFactory.cs:127`).
- Aplicação: `_animator.SetAnimation(targetClip, true)` só quando
  `CurrentAnimation != targetClip` (`RemotePlayerPuppet.cs:120`).
  Espelho: `_spriteMirror.FaceLeft`, senão rotação Y 180.
- Catálogo: `Resources.FindObjectsOfTypeAll<TextureAnimationWithTransitions>`
  no `CharacterAnimationSystem.Start` + retry preguiçoso
  (`AnimationRegistry.cs`, `AnimationPrewarmPatch.cs`).
- Diagnóstico atual: `ReplicationObservability.TrackPacket` (só quando
  `animName` muda) + resumo a cada 3 s; `LogVisibilityEvent`.

### 1.5 UI e chaves
- Precedente OnGUI: `ServerConnectionDialog` (toggle `F6`, fecha com `Escape`).
  `OriCoopPlugin.OnGUI` desenha HUD legado atrás de
  `EnableLegacyFloatingHud=false`. Chaves ocupadas: `F6`, `Escape`, `T`.
  **Livre sugerida: `F9`** para o visualizador de logs.
- Config: seção `[Network]` + `[UI]` via `Config.Bind` em `Awake`
  (`OriCoopPlugin.cs:240`). Nova flag `AnimVerbose` segue o mesmo padrão.

### 1.6 Build e restrições (obrigatório em todos os planos)
- Cliente: `.NET Framework 3.5`, **C# 5** (`build.ps1` via `csc.exe`):
  sem interpolação `$""`, sem `?.`, sem `Action` com 5+ parâmetros
  (ver `INetworkService.cs:4`, `ConfigSyncHandler` dedicado), cast explícito em
  `Instantiate`.
- Servidor: `.NET 8.0`, C# moderno, `dotnet build`.
- Deploy exige jogo/servidor fechados:
  `<ORI_DIR>\BepInEx\plugins\OriCoopBepInEx.dll` e `<ORI_DIR>\Server\`.
- Sem testes automatizados no repo — validação é manual com 2 clientes
  (`docs/operations.md`, checklist + protocolo de animação Idle→Run→Pulo→
  PuloDuplo→WallSlide→Bash→Queda).

## 2. Perguntas abertas (a confirmar em execução)

1. **Fonte de `grounded` no sender.** `Sein.Speed` existe; grounded real ainda
   não localizado no `SeinCharacter`. Caminho: inspecionar `Assembly-CSharp`
   (`SeinCharacter` — propriedades de colisão/chão) ou instrumentar dump.
   Fallback aprovado: heurística `|vy|<1` só como temporário do tracer.
2. **Nomes reais dos clipes.** `TextureAnimation.name` vs
   `TextureAnimationWithTransitions.name` podem divergir; a lista exata dos 12
   estados só sai do dump em runtime (comando de dump do plano 02).
3. **Papel do `CharacterAnimationSystem` desligado.** Preservado porque
   destruir congelava o `SpriteAnimator` (bug #2). Manter desligado (D-15);
   não investigar religamento nesta fase.
4. **Frequência real de `FixedUpdate` vs taxa UDP.** Heartbeat 2–5 Hz e
   on-change são calibragem do executor (discrição do agente).

## 3. Desenho aprovado do protocolo (ID 18 `PLAYER_STATE`)

Ordem dos campos (cliente→servidor e servidor→todos, idêntica):

| # | Campo | Tipo | Notas |
|---|-------|------|-------|
| 1 | `playerId` | int | servidor reescreve com `pl.Id` |
| 2 | `pos` | 3×float | `Vector3` |
| 3 | `state` | byte | `ActionVisualState` (enum manda, D-05) |
| 4 | `flags` | byte | bit0 FacingLeft, bit1 IsGrounded (`AnimationSyncData`) |
| 5 | `animHash` | int | `unchecked((int)Fnv1a(nome))`; 0 = desconhecido |
| 6 | `speedX` | float | `Sein.Speed.x` real (D-06) |
| 7 | `speedY` | float | `Sein.Speed.y` real |
| 8 | `nick` | string legado | `int32+ASCII`, no fim para leitura tolerante |

~31 bytes + nick por envio (contra 2 pacotes atuais). Apenas **acrescentar**;
nunca reutilizar IDs (regra `docs/protocol.md`). Sem compat retroativa (D-12):
remover POSITION/ANIM e fusão antiga no mesmo build cliente+servidor.

## 4. Desenho da histerese (D-13/D-14)

- Estado confirmado só após N ms estável (debounce 100–200 ms, `Time.time`).
- Limiares assimétricos por eixo (ex.: entra Run acima de V_alto, sai abaixo
  de V_baixo, V_baixo < V_alto) — valores calibrados no plano 02 com o dump.
- `animHash==0`/desconhecido ou `state` fora do catálogo → **mantém última**
  (nunca fallback para Idle). `SetAnimation` só em troca de estado confirmada.

## 5. Desenho do diagnóstico (D-17…D-20)

- `Config.Bind("Diagnostics", "AnimVerbose", false, ...)`; quando ligado,
  logar `enviado→recebido→aplicado + motivo` via `Logger.LogInfo`.
- Ring buffer `Queue<string>` (cap 200, com `lock`) alimentado nos mesmos
  pontos; janela OnGUI com 2 abas (mod | BepInEx — BepIn lê `LogOutput.log`
  do disco sob demanda), toggle `F9`.
- HUD: estender `FormatPlayerLine` com estado + idade do último pacote
  (`P1 Run 45ms`).

## 6. Riscos

- Nomes de clipe divergirem entre `TextureAnimation` e
  `TextureAnimationWithTransitions` → mitigado por enum-autoridade + hash
  opcional.
- `grounded` real indisponível → fallback heurístico temporário, sem travar
  o tracer.
- Quebra de protocolo exige atualizar os dois módulos juntos e avisar no
  `docs/protocol.md` (tabela de pacotes + §fragmentação removida).

## RESEARCH COMPLETE
