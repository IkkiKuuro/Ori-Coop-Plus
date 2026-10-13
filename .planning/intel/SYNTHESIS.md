# Synthesis Summary (ingest docs, merge mode)

- Docs synthesized: 18 (0 ADR, 0 PRD, 3 SPEC, 15 DOC, 0 UNKNOWN)
- Classifications consumed: all 18 `*.json` in `.planning/intel/classifications/`
- Manifest scope: `.planning/intel/ingest-manifest.yaml` (18 docs; 4 docs excluded to break navigational cross-ref cycles: docs/README.md, docs/graphify.md, docs/anim-test-battery.md, docs/game-assembly-audit.md)
- Decisions locked: 0 (no ADRs in scope; phase D-decisions are proposed, status Ready for planning)
- Requirements extracted: 0 (no PRDs in scope)
- Constraints: 3 (all type protocol — docs/protocol.md, docs/world-sync.md, docs/server-rewrite-CONTEXT.md)
- Context topics: 14 (covering all 15 DOCs; anim-sync/player-event-core/server-rewrite phase contexts recorded as proposed)
- Cycle detection: clean — no in-scope cross-ref cycles (out-of-scope refs treated as leaf nodes)
- Merge check: ingest sources consistent with existing `.planning/phases/02-server-rewrite/02-CONTEXT.md` and `.planning/phases/03-player-event-core/03-CONTEXT.md` (mirrors of docs/ copies); no locked contradictions; no PROJECT.md/REQUIREMENTS.md/ROADMAP.md/STATE.md exist yet
- Conflicts: 0 blockers, 0 competing-variants, 3 auto-resolved (informational)

## Pointers

- Detail: `.planning/INGEST-CONFLICTS.md`
- Decisions: `.planning/intel/decisions.md`
- Requirements: `.planning/intel/requirements.md`
- Constraints: `.planning/intel/constraints.md`
- Context: `.planning/intel/context.md`
