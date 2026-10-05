# Catálogo de animações de movimentação do Ori

Mapeamento explícito nome → `ActionVisualState`, usado por
`AnimationRegistry.s_nameToState`. **Nomes exatos a confirmar** via dump em
jogo (`F8` → linhas `[ANIM-DUMP]` no `LogOutput.log`); aliases abaixo são
SEED best-effort. Miss nunca vira sprite aleatório: resolve desconhecido
retorna null e o puppet mantém a última anim.

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

Notas:
- O resolve exato por hash/nome (`ComputeFnv1aHash` do `CurrentAnimation.name`
  do sender) tem prioridade sobre aliases — na prática o nome do sender é o
  nome real do clipe, pois ambos usam os mesmos assets do jogo.
- Histerese do sender: entra em Running acima de `RunEnterSpeed` (0.6), sai
  abaixo de `RunExitSpeed` (0.3); ápice do pulo preserva último estado aéreo.
- Confirmação no receptor: 0.15 s (`ConfirmDelaySec`).
