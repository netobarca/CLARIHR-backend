# Checklist de validacion tecnica

## 1. Uso

Este checklist sirve para revisar cambios backend y documentales antes de cerrar una HU o requerimiento.

## 2. Arquitectura

- [ ] El cambio respeta Clean Architecture.
- [ ] No se movio logica de negocio al controller.
- [ ] Domain no depende de DTOs, HTTP ni EF.
- [ ] Los handlers representan casos de uso claros.

## 3. Multi-tenant

- [ ] La lectura o escritura respeta `TenantId`.
- [ ] No existe acceso cross-tenant.
- [ ] El endpoint y el handler validan correctamente el alcance del tenant.

## 4. Seguridad

- [ ] Se valido autenticacion cuando aplica.
- [ ] Se valido autorizacion por recurso y accion.
- [ ] Se considero si aplica permiso por campo.
- [ ] No se expone PII innecesaria.
- [ ] Las acciones sensibles dejan auditoria cuando corresponde.

## 5. Performance

- [ ] Los listados usan paginacion.
- [ ] Las lecturas proyectan solo lo necesario.
- [ ] Se usa `AsNoTracking()` cuando la lectura es no transaccional.
- [ ] No se introdujo un patron obvio de N+1.
- [ ] Se evaluo el costo de exportes o procesos pesados.

## 6. Testing

- [ ] Existe cobertura de happy path.
- [ ] Existen pruebas de validacion.
- [ ] Existen pruebas de permisos o tenant isolation si el caso lo requiere.
- [ ] Se agrego integration test cuando el cambio es observable por HTTP o wiring.

## 7. API y contratos

- [ ] La ruta, request y response coinciden con el comportamiento real.
- [ ] `ProblemDetails` y codigos de error quedaron consistentes.
- [ ] `endpoint-reference.md` fue actualizado si hubo cambio observable.
- [ ] `openapi.yaml` fue actualizado o se dejo trazado por que no aplica.

## 8. Documentacion viva

- [ ] Se actualizaron solo los documentos vivos realmente impactados.
- [ ] No se duplico contenido entre documentos.
- [ ] `project-foundation.md` sigue siendo la base canonicamente respetada.
- [ ] Si hubo HU, se actualizo `docs/analysis/changes/hu-index.md`.

## 9. Cierre

- [ ] La solucion compila.
- [ ] Las pruebas relevantes pasan.
- [ ] El resumen final identifica riesgos o pendientes reales.
