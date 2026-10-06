# Catálogo de animações de movimentação do Ori

Mapeamento explícito nome → `ActionVisualState`, usado por
`AnimationRegistry.s_nameToState`. **Nomes exatos a confirmar** via dump em
jogo (`F8` → linhas `[ANIM-DUMP]` no `LogOutput.log`, agora com coluna
`src=sein/global`); aliases abaixo são SEED best-effort. Miss nunca vira
sprite aleatório: resolve desconhecido retorna null e o puppet mantém a
última anim.

> Correção 2026-10-05 (DLL 76.800 bytes): o fallback por estado (`s_stateClips`)
> só aceita clipes coletados via reflection na hierarquia do Sein
> (`CollectClips` — `SeinIdle/Run/Jump/...`, arrays de `Jump/DoubleJump/WallJump`,
> `DirectionalAnimationSets` do Bash, containers `Carry/Swimming`). O scan global
> (`Resources.FindObjectsOfTypeAll`) continua existindo como fallback, mas só
> alimenta o cache de resolve exato (hash/nome apontam para o mesmo asset
> compartilhado, sempre seguro) e nunca o fallback — foi assim que um `idle`
> de inimigo (Kuro/slug/owl/...) virou o Idle do Ori parado. Ordem de resolve:
> exato → estado-Sein → manter última. Sender agora deriva o estado com
> prioridade real: `Controller.IsBashing/IsStomping/IsDashing/IsGliding/
> IsChargingJump/IsGrabbingWall` + nome do clipe (doublejump/backflip, walljump,
> wallslide/grabwall, bash, glide/feather/parachute, chargejump/superjump,
> stomp/groundpound, dash) antes da velocidade; `IsOnGround` real substitui a
> heurística `|vy|<1`.

> Correção 2026-10-06 (DLL 88.064 bytes): cobertura total do Sein. Novos
> estados append-only no enum (`Swim=12 … StandingOnEdge=23`, byte opaco no
> protocolo, sem quebra): `CollectFromObject` agora lê `List<T>` (antes só
> arrays — `FastThrowAnimations`, `m_states` eram perdidos), profundidade 5 e
> clipes dos drivers (`SpriteAnimatorWithTransitions.Default/Current/Previous`);
> `Prewarm` sempre faz merge global para resolve exato; novo
> `RefreshFromSein()` idempotente (só adiciona) chamado pelo factory a cada
> puppet e pelo puppet quando o resolve falha (antes re-coletava do próprio
> puppet despojado — inútil); `RegisterClips` indexa nome do wrapper E nome
> interno (`TextureAnimation.name`); sender envia nome do wrapper; comparação
> de "já tocando" usa `CurrentTextureAnimationTransitions` (comparar
> `CurrentAnimation` interna com wrapper dava sempre diferente e re-setava a
> cada pacote).

| Estado | Aliases SEED (case-insensitive) | Hash FNV1a |
|--------|---------------------------------|------------|
| Idle | idle, oriidle, seinidle, stand, oristand | a confirmar |
| Running | run, running, orirun, seinrun | a confirmar |
| Jump | jump, orijump, seinjump, jumpup, rise | a confirmar |
| DoubleJump | doublejump, oridoublejump, flip | a confirmar |
| Falling | fall, falling, orifall, seinfall, drop | a confirmar |
| WallSlide | wallslide, oriwallslide, slide | a confirmar |
| WallJump | walljump, oriwalljump | a confirmar |
| Bash | bash, oribash, seinbash | a confirmar |
| Glide | glide, origlide, seinglide, feather, slowfall | a confirmar |
| ChargeJump | chargejump, orichargejump, charge | a confirmar |
| Stomp | stomp, oristomp, seinstomp, groundpound | a confirmar |
| Dash | dash, oridash, seindash, airdash, chargedash | a confirmar |
| Swim | swim, oriswim, seinswim, swimsurface, swimidle, jumpoutofwater | a confirmar |
| Carry | carry, oricarry, seincarry, pickup | a confirmar |
| GrabWall | grabwall, origrabwall, climb, climbup, climbdown, edgeclimb | a confirmar |
| GrabBlock | grabblock, push, pull | a confirmar |
| Hurt | hurt, orihurt, seinhurt | a confirmar |
| Crouch | crouch, oricrouch, seicrouch | a confirmar |
| LookUp | lookup, orilookup | a confirmar |
| AimThrow | aim, oriaim, throw, orithrow | a confirmar |
| PushAgainstWall | pushagainstwall | a confirmar |
| EdgeClamber | (derivado por nome edgeclamber; fallback exato cobre) | a confirmar |
| Lever | lever | a confirmar |
| StandingOnEdge | standingonedge, facingedge | a confirmar |

Notas:
- O resolve exato por hash/nome (`ComputeFnv1aHash` do `CurrentAnimation.name`
  do sender) tem prioridade sobre aliases — na prática o nome do sender é o
  nome real do clipe, pois ambos usam os mesmos assets do jogo.
- Histerese do sender: entra em Running acima de `RunEnterSpeed` (0.6), sai
  abaixo de `RunExitSpeed` (0.3); ápice do pulo preserva último estado aéreo.
- Confirmação no receptor: 0.15 s (`ConfirmDelaySec`).
