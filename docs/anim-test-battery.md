# Bateria de testes — rework de sincronia de animações

Validar cada mudança do rework na ordem. Ambiente: 2 clientes locais
(`127.0.0.1:7777`, nicks distintos), servidor e DLL do **mesmo build**,
save controlável nos dois. Ligar `Diagnostics/AnimVerbose=true` em
`com.ikkikuuro.oricoop.cfg` só nos testes T1–T3 (desligar depois).

## T0 — Base (build + conexão)

1. `build.ps1` gera `OriCoopBepInEx.dll` sem erros; copiar para
   `<ORI_DIR>\BepInEx\plugins\` com o jogo fechado.
2. Servidor `--auto --max-players 2 --port 7777` mostra `Server started` e
   módulo carregado; 2 clientes conectam (toast `Conectado`, HUD `ORI COOP PLUS`
   com 2 linhas).
3. **Passa se:** sem exceção de Harmony no `LogOutput.log` dos dois clientes.

## T1 — Tracer pacote 18 (plano 01-01)

1. Parado 10 s: puppet remoto fica em Idle estável, sem oscilar.
2. Correr para os lados 10 s: puppet em Run; parar → assenta em Idle < 1 s.
3. Pular Cair: Jump/Falling sem congelar no ar.
4. Com verbose: `LogOutput.log` mostra `[ANIM] ... motivo=manteve/trocou/
   histerese-aguarda`, sem `motivo=desconhecido` em loop.
5. **Passa se:** sem teleporte para a origem, sem sprite aleatório em 60 s,
   `[NET-METRICS] Dropped: 0`.

## T2 — Dicionário + dump (plano 01-02)

1. Em jogo, `F8`: `LogOutput.log` recebe linhas `[ANIM-DUMP] name=... hash=...
   estado?=...` e `ANIM-DUMP concluído: N clipes`.
2. Conferir os nomes contra `docs/anim-catalog.md`; atualizar aliases SEED que
   estiverem `UNKNOWN` e rebuildar.
3. Sequência remota: Idle→Run→Pulo→PuloDuplo→WallSlide→Bash→Queda — cada etapa
   com pose compatível, sem T-pose.
4. **Passa se:** 12 estados com clipe resolvido (ou `UNKNOWN` documentado com
   fallback manter-última, sem sprite errado).

## T3 — Sem legado (plano 01-03)

1. Parado: tráfego cai para heartbeat (~2.5 pacotes/s por jogador — observar
   `[ANIM]` com verbose); movimento envia na hora (< 100 ms percebido).
2. `/dummy`: bot `Bot_Amigo` aparece parado em Idle; `/dummy` de novo remove e
   a câmera volta ao jogador.
3. `/dummy anim on`: bot performa o ciclo de 12 estados (2.5 s cada, log
   `[DUMMY-ANIM]` no console do servidor) — cada estado mostra a pose
   correspondente no puppet; `/dummy anim <estado>` trava um estado;
   `/dummy anim off` volta ao Idle parado; `/dummy status` mostra estado atual.
3. Desconectar/reconectar um cliente: puppet some e respawna no lugar certo.
4. **Passa se:** regressão OK — `T` teleporta, `/tp`, chat, cores, `CONFIG_SYNC`
   (`/coop`) continuam funcionando.

## T4 — Diagnóstico em jogo (plano 01-04)

1. `F9` abre `Ori Coop — Logs`; aba `Logs do mod` lista transições recentes;
   aba `BepInEx` mostra o `LogOutput.log`; `Esc`/Fechar fecha.
2. HUD (com `EnableLegacyFloatingHud=true`): linhas `nick | x,y,z | Run 45ms |
   ping` com idade crescendo de forma plausível.
3. **Passa se:** janela não trava gameplay; verbose desligado = sem spam.

## Registrar resultado

Anotar data, build (bytes da DLL) e itens `a confirmar` em
`docs/operations.md` § `Validacao do rework de anims`.
