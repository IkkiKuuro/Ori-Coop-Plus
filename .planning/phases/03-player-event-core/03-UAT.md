---
status: complete
phase: 03-player-event-core
source: [03-VERIFICATION.md]
started: 2026-10-06
updated: 2026-10-06
---

## Current Test

[testing complete]

## Tests

### 1. C1 - A atira, B ve (docs/operations.md checklist 03-03)
expected: Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao
result: issue
reported: "fail; nenhum projétil ou som aparece, e o projétil está sem o bloom padrão dele"
severity: major

### 2. C2 - B atira, A ve (simetria do relay)
expected: Mesmo que C1 na direcao oposta; nenhum cliente e 'host visual'
result: issue
reported: "fail, ninguém vê nada de tiro"
severity: major

### 3. C3 - sem eco (ambos os LogOutput.log)
expected: fase=enviado so no atirador, fase=recebido/aplicado so no remoto; atirador nunca toca o proprio visual
result: issue
reported: "prints de ambos os logs mostram apenas linhas [EVENT] SpiritFlame detected (deteccao local); nenhuma linha fase=recebido/aplicado visivel em nenhum dos lados"
severity: major

### 4. C4 - spam sob movimento + campos de observacao D-12
expected: Movimento suave sob rajadas; anotar tiros/s + EvRecv/EvApplied/EvDropped para calibrar throttle futuro
result: pass
note: "spam pros lados funciona bem; animacao nao e tao precisa para spam (observacao — imprecisao por tiro sob rajada e esperada em unreliable sem throttle, D-09/D-12)"

### 5. C5 - tipo desconhecido segura pose (exige injecao)
expected: Puppet mantem ultima anim, nunca Idle generico
result: pass

### 6. C6 - desconexao/reconexao limpa
expected: Puppet some e respawna no lugar certo; eventos voltam a replicar sem reiniciar o servidor
result: pass

## Summary

total: 6
passed: 3
issues: 3
pending: 0
skipped: 0
blocked: 0

## Gaps

- gap_id: G-03-1
  truth: "Puppet de A em B: clipe de ataque + particula + som + projetil falso em linha reta, sem dano/colisao"
  status: failed
  reason: "User reported: fail; nenhum projétil ou som aparece, e o projétil está sem o bloom padrão dele"
  severity: major
  test: 1
  artifacts: []
  missing: []
- gap_id: G-03-2
  truth: "Mesmo que C1 na direcao oposta; nenhum cliente e 'host visual'"
  status: failed
  reason: "User reported: fail, ninguém vê nada de tiro"
  severity: major
  test: 2
  artifacts: []
  missing: []
- gap_id: G-03-3
  truth: "fase=enviado so no atirador, fase=recebido/aplicado so no remoto; atirador nunca toca o proprio visual"
  status: failed
  reason: "User evidence: ambos os logs mostram apenas deteccao local [EVENT] SpiritFlame detected; nenhuma linha recebido/aplicado visivel — eventos podem nao estar chegando (envio ou relay)"
  severity: major
  test: 3
  artifacts: []
  missing: []
