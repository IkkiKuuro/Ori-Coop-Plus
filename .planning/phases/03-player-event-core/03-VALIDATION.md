---
phase: 03
slug: player-event-core
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-06
---

# Phase 03 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | none — manual 2-client validation (repo has no automated tests) |
| **Config file** | none — Wave 0 installs nothing |
| **Quick run command** | `dotnet build src/OriCoopPlus/OriCoopServer` (server) |
| **Full suite command** | client `build.ps1` + `dotnet build` server + 2-client checklist (`docs/operations.md`) |
| **Estimated runtime** | ~120 seconds (builds only; manual checklist extra) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build` (server) / `build.ps1` (client) for touched side
- **After every plan wave:** Run full suite command
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 300 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | D-10 (new ID 19) | — | N/A | build | `dotnet build` + `build.ps1` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Full per-task map to be completed by planner from RESEARCH.md Validation Architecture.*

---

## Wave 0 Requirements

- [ ] Existing infrastructure covers all phase requirements (no framework to install; SmokeProbe packet-19 relay case added in Wave 1 per RESEARCH.md)

*If none: "Existing infrastructure covers all phase requirements."*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Spirit Flame visual on puppet (clip+particle+sound, no damage) | D-13/D-14 | No automated tests in repo; needs running game | 2 clients, fire Spirit Flame on client A, observe puppet on client B per `docs/operations.md` + `docs/anim-test-battery.md` |
| Puppet never publishes (no echo) | D-07 | Needs 2 running clients | Fire on puppet side, confirm no relay storm |
| Unknown event fail-closed | D-15 | Needs crafted packet | Send unknown event kind, confirm last anim held |
| No UDP flood under spam | D-12 | Needs live traffic | Spam fire, watch ReplicationObservability counters |

*If none: "All phase behaviors have automated verification."*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 300s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
