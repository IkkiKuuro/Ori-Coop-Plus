---
status: testing
phase: 03-player-event-core
source: [03-VERIFICATION.md]
started: 2026-10-06
updated: 2026-10-06
---

## Current Test

number: 1
name: C1 - A atira, B ve (docs/operations.md checklist 03-03)
expected: |
  Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao
awaiting: user response

## Tests

### 1. C1 - A atira, B ve (docs/operations.md checklist 03-03)
expected: Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao
result: [pending]

### 2. C2 - B atira, A ve (simetria do relay)
expected: Mesmo que C1 na direcao oposta; nenhum cliente e 'host visual'
result: [pending]

### 3. C3 - sem eco (ambos os LogOutput.log)
expected: fase=enviado so no atirador, fase=recebido/aplicado so no remoto; atirador nunca toca o proprio visual
result: [pending]

### 4. C4 - spam sob movimento + campos de observacao D-12
expected: Movimento suave sob rajadas; anotar tiros/s + EvRecv/EvApplied/EvDropped para calibrar throttle futuro
result: [pending]

### 5. C5 - tipo desconhecido segura pose (exige injecao)
expected: Puppet mantem ultima anim, nunca Idle generico
result: [pending]

### 6. C6 - desconexao/reconexao limpa
expected: Puppet some e respawna no lugar certo; eventos voltam a replicar sem reiniciar o servidor
result: [pending]

## Summary

total: 6
passed: 0
issues: 0
pending: 6
skipped: 0
blocked: 0

## Gaps
