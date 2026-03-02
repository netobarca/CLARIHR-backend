# Post-Implementation Validation Checklist Template

## How To Use

1. Copiar este archivo a `docs/implementation-analysis/history-and-iterations/`.
2. Renombrarlo con el identificador de la entrega, por ejemplo:
   - `HU-009-validation-checklist.md`
   - `iteration-05-validation-checklist.md`
3. Marcar un item con `[x]` solo cuando exista evidencia verificable.
4. Si un punto queda abierto, dejarlo en `[ ]` y documentar el riesgo o siguiente paso.
5. Enlazar este checklist desde el analisis narrativo de la entrega.

## Context

- Delivery:
- Date:
- Scope:
- Reviewed by:
- Related analysis document:
- Validation commands executed:
  - `dotnet build CLARIHR.slnx`
  - `dotnet test CLARIHR.slnx --no-build`

## Architecture

- [ ] La separacion de capas se mantiene: `Api -> Application -> Domain/Infrastructure`.
  - Evidence:
  - Notes:
- [ ] Los controladores siguen siendo delgados y no concentran logica de negocio.
  - Evidence:
  - Notes:
- [ ] Los casos de uso nuevos o modificados mantienen enfoque CQRS o una orquestacion consistente.
  - Evidence:
  - Notes:
- [ ] Los concerns transversales nuevos se resolvieron de forma reusable y no duplicada.
  - Evidence:
  - Notes:
- [ ] No se introdujeron dependencias circulares en DI.
  - Evidence:
  - Notes:
- [ ] No se agrego deuda estructural significativa sin documentar.
  - Evidence:
  - Notes:

## Security

- [ ] La API sigue enforzando auth y authorization en backend, sin depender de la UI.
  - Evidence:
  - Notes:
- [ ] El comportamiento por defecto para permisos faltantes sigue siendo deny-by-default.
  - Evidence:
  - Notes:
- [ ] El tenant isolation se mantiene tanto en lectura como en escritura.
  - Evidence:
  - Notes:
- [ ] Los updates sensibles validan permisos de campo cuando aplica.
  - Evidence:
  - Notes:
- [ ] Los errores de seguridad siguen el contrato estandar (`401/403` con code consistente).
  - Evidence:
  - Notes:
- [ ] No se persistieron ni expusieron secretos, tokens, hashes o datos sensibles indebidamente.
  - Evidence:
  - Notes:
- [ ] La auditoria cubre los cambios administrativos o sensibles de la entrega.
  - Evidence:
  - Notes:

## Performance

- [ ] Las lecturas principales usan queries eficientes y `AsNoTracking()` cuando corresponde.
  - Evidence:
  - Notes:
- [ ] Los listados o consultas de alto volumen estan paginados o acotados.
  - Evidence:
  - Notes:
- [ ] El modelo de datos soporta la entrega con indices adecuados.
  - Evidence:
  - Notes:
- [ ] La entrega no introduce N+1 queries o cargas de grafo innecesarias en hot paths.
  - Evidence:
  - Notes:
- [ ] Si se agrego cache, existe una estrategia clara de invalidacion.
  - Evidence:
  - Notes:
- [ ] El tamano de payload y serializacion sigue siendo controlado.
  - Evidence:
  - Notes:

## Testing

- [ ] La solucion compila limpia.
  - Evidence:
  - Notes:
- [ ] Las pruebas automaticas relevantes pasan.
  - Evidence:
  - Notes:
- [ ] Se agregaron o ajustaron pruebas para los cambios nuevos.
  - Evidence:
  - Notes:
- [ ] Existe cobertura de regresion para autorizacion, tenant isolation y errores principales si la entrega los toca.
  - Evidence:
  - Notes:
- [ ] Se ejecutaron validaciones manuales o smoke tests cuando aplica.
  - Evidence:
  - Notes:

## Operations And Release

- [ ] La configuracion requerida para local/QA esta documentada.
  - Evidence:
  - Notes:
- [ ] Los cambios de base de datos requeridos estan documentados y aplicados donde corresponde.
  - Evidence:
  - Notes:
- [ ] La documentacion funcional y tecnica afectada fue actualizada.
  - Evidence:
  - Notes:
- [ ] Los riesgos residuales estan listados con siguiente paso claro.
  - Evidence:
  - Notes:

## Delivery Gate

- [ ] Ready for local validation
- [ ] Ready for QA
- [ ] Ready for production

## Open Gaps

- Gap:
  - Impact:
  - Follow-up:
