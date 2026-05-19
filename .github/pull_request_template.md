<!-- AGENTS.md §16 — 1 finding = 1 PR. The issue is the atomic claim/lock. -->

## Finding
Closes #<!-- issue number; the issue must be status:claimed and assigned to you -->

## Qué cambió
<!-- resumen del cambio; diff atómico = solo el file set del issue -->

## Verificación
- [ ] `dotnet build CLARIHR.slnx` → 0/0
- [ ] Unit suite + guardrails verdes
- [ ] Integración dirigida del finding verde
- [ ] (si añade guardrail) sanity red→restore→green probada
- [ ] Diff atómico (solo el file set del issue; sin refactors no pedidos)
- [ ] doc `08` se actualiza tras el merge (§7 append-only, flip §5) — single-writer

Ver `AGENTS.md` §16 (estrategia de branching multi-sesión).
