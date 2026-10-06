# Phase 3: Core de eventos do personagem — Discussion Log

**Date:** 2026-10-06
**Mode:** default (interactive, single-question turns)

## Areas & selections

### 1. Escopo de eventos
- Q: "Escopo: o que entra no core nesta fase?" (Tudo junto / Combate primeiro / Visual primeiro)
  → User (Other): "O plano é uma coisa de cada vez, cada categoria é uma fase completa de implementação"
- Q: "Nesta fase 3, o que é entregue: só o core ou core + primeira categoria?" (Core+piloto / Core+1 categoria / Só infra)
  → "Core + piloto"
- Q: "Qual evento piloto valida o core?" (Spirit Flame / Habilidade de movimento / Morte-respawn)
  → "Spirit Flame"
- Q: "Outras categorias viram fases futuras fixas ou backlog?" (Lista fechada / Só Spirit resto futuro)
  → "Só Spirit, resto futuro"
- Gate: Next area.

### 2. Captura e escuta
- Q: "Como o core escuta: central com bus ou patches diretos?" (Core central / Patches diretos / Polling)
  → "Core central"
- Q: "Estratégia de detecção: Harmony, polling ou híbrido?" (Harmony eventos / Polling / Híbrido)
  → "Harmony eventos"
- Q: "Filtro local vs remoto: quem pode publicar?" (Só local / Todos)
  → "Só local publica"
- Q: "Relação com código MP atual: reusar ou reescrever?" (Reusar MP atual / Core novo puro / Referência e reescreve)
  → "Reusar MP atual"
- Gate: Next area.

### 3. Transporte por tipo
- Q: "Spirit Flame: unreliable rápido ou confiável com ACK?" (Unreliable / Confiável / Híbrido)
  → "Unreliable"
- Q: "Pacote do piloto: ID novo, reusar SKILL/SYNC ou estender PLAYER_STATE?" (ID novo / Reusar / Estender P18)
  → "ID novo"
- Q: "Payload mínimo ou completo?" (Mínimo / Completo)
  → "Mínimo"
- Q: "Throttle de spam: livre ou limitado?" (Sem throttle / Com throttle)
  → "Sem throttle"
- Gate: Next area.

### 4. Reprodução remota
- Q: "O que o puppet reproduz?" (Visual fiel / Só animação / Anim+VFX)
  → "Visual fiel"
- Q: "Projétil remoto: fake, flash local ou física real?" (Fake / Flash / Física real)
  → User (Other): "física real, pois os eventos e entidades do mundo serão sincronizadas também em próximas fases"
- Follow-up Q: "Física real já no piloto ou fake agora e real na fase de entidades?" (Real já / Fake agora real depois)
  → "Fake agora real depois"
- Q: "Evento desconhecido: mantém última ou cai para Idle?" (Fail closed / Fail open)
  → "Fail closed"
- Final gate unanswered → proceeded to write_context (all 4 areas covered).

## Deferred ideas
- Ver CONTEXT.md §Deferred Ideas (categorias futuras, entidades, LAN, internet, submenu pause).

## Agent's discretion items
- Nenhum "você decide" — usuário escolheu todas as opções (com 2 respostas em texto livre interpretadas acima).
