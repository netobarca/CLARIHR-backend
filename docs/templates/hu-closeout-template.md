# HU-XXXX — Cierre de implementación

## 1. Información general

- **Código HU:** HU-XXXX
- **Título:** [Nombre corto de la historia]
- **Módulo:** [Módulo funcional / técnico]
- **Fecha de cierre:** [YYYY-MM-DD]
- **Estado:** [Completada / Parcial / Pendiente de validación]
- **Responsable:** [Nombre o agente]
- **Referencia funcional:** [link, archivo o referencia interna]
- **Referencia técnica:** [link, archivo o referencia interna]

---

## 2. Objetivo de la HU

Describir de forma breve:

- qué problema resuelve,
- qué capacidad nueva agrega,
- o qué comportamiento corrige.

### Resumen
[Explicación breve y concreta del propósito de la historia.]

---

## 3. Alcance implementado

Describir únicamente lo que sí fue implementado.

### Incluye
- [Punto 1]
- [Punto 2]
- [Punto 3]

### No incluye
- [Punto fuera de alcance 1]
- [Punto fuera de alcance 2]

---

## 4. Impacto funcional

Explicar cómo cambia el sistema desde el punto de vista del negocio o del usuario.

### Cambios funcionales
- [Cambio funcional 1]
- [Cambio funcional 2]

### Flujo afectado
- [Nombre del flujo o proceso]
- [Pantalla / endpoint / módulo afectado]

### ¿Requiere actualización de flujo de negocio?
- [Sí / No]

### Documento vivo afectado
- `docs/business/current-system-business-flows.md`
- [Otro documento, si aplica]

---

## 5. Impacto técnico

Describir las capas, componentes o artefactos modificados.

### Capas afectadas
- [ ] Domain
- [ ] Application
- [ ] Infrastructure
- [ ] API
- [ ] Tests
- [ ] Documentation
- [ ] SQL / Data

### Componentes modificados
- [Entidad / Aggregate / VO]
- [Command / Query / Handler]
- [Validator]
- [Repository / Service]
- [Controller / Endpoint]
- [Script SQL / índice / constraint]

### Resumen técnico
[Descripción breve y concreta de la solución implementada.]

---

## 6. Cambios en API

Completar solo si aplica.

### Endpoints nuevos
- `METHOD /ruta`
- `METHOD /ruta`

### Endpoints modificados
- `METHOD /ruta` — [qué cambió]
- `METHOD /ruta` — [qué cambió]

### Contratos afectados
- Request: [Sí / No]
- Response: [Sí / No]
- Códigos de error: [Sí / No]
- Paginación / filtros / sorting: [Sí / No]
- Autenticación / autorización: [Sí / No]

### Documentación actualizada
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`
- [Otro, si aplica]

---

## 7. Cambios en datos y persistencia

Completar solo si aplica.

### Cambios realizados
- [Tabla nueva]
- [Columna nueva]
- [Índice nuevo o ajustado]
- [Constraint]
- [Seed / backfill]
- [Cambio de relación]
- [Cambio tenant-scoped]

### Scripts o migraciones relacionados
- `docs/technical/sql/[archivo].sql`
- `[ruta de migración]`

### Consideraciones
[Explicación breve del impacto en persistencia.]

---

## 8. Seguridad

Indicar si la HU impacta seguridad, permisos o datos sensibles.

### Validaciones de seguridad aplicadas
- [ ] Tenant isolation
- [ ] Autenticación
- [ ] Autorización / RBAC
- [ ] Permisos por acción
- [ ] Permisos por campo
- [ ] Protección de datos sensibles
- [ ] Auditoría
- [ ] No aplica

### Resumen
[Describir cómo se protegió el caso de uso.]

### Documento vivo afectado
- `docs/analysis/current-state/security-analysis.md`
- [Otro, si aplica]

---

## 9. Rendimiento

Indicar si la HU tiene impacto en consultas, carga o procesamiento.

### Consideraciones de rendimiento
- [ ] Paginación
- [ ] Proyección a DTO
- [ ] `AsNoTracking()`
- [ ] Índices revisados
- [ ] Evitar N+1
- [ ] Proceso pesado fuera del request path
- [ ] No aplica

### Resumen
[Describir decisiones relevantes de rendimiento.]

### Documento vivo afectado
- `docs/analysis/current-state/performance-analysis.md`
- [Otro, si aplica]

---

## 10. Pruebas realizadas

Describir qué pruebas se agregaron o ajustaron.

### Unit tests agregados o modificados
- [Nombre del test o clase de test]
- [Qué valida]
- [Resultado esperado]

### Cobertura mínima validada
- [ ] Happy path
- [ ] Validaciones
- [ ] Errores esperados
- [ ] Permisos
- [ ] Tenant scope
- [ ] Reglas críticas
- [ ] No aplica

### Ejecución
- `dotnet test`
- [Comando específico, si aplica]

### Documento vivo afectado
- `docs/analysis/current-state/testing-analysis.md`
- [Otro, si aplica]

---

## 11. Documentación actualizada

Listar toda la documentación viva o técnica que fue actualizada como parte de esta HU.

### Documentos actualizados
- `docs/technical/overview/project-foundation.md`
- `docs/business/current-system-business-flows.md`
- `docs/analysis/current-state/architecture-analysis.md`
- `docs/analysis/current-state/security-analysis.md`
- `docs/analysis/current-state/performance-analysis.md`
- `docs/analysis/current-state/testing-analysis.md`
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

### Documentos no requeridos
- [Documento 1]
- [Documento 2]

---

## 12. Riesgos, limitaciones y pendientes

Documentar cualquier punto importante que no deba perderse.

### Riesgos identificados
- [Riesgo 1]
- [Riesgo 2]

### Limitaciones actuales
- [Limitación 1]
- [Limitación 2]

### Pendientes
- [Pendiente 1]
- [Pendiente 2]

---

## 13. Verificación funcional y técnica

Describir cómo validar que la HU quedó correctamente implementada.

### Pasos de validación
1. [Paso 1]
2. [Paso 2]
3. [Paso 3]

### Resultado esperado
[Describir el comportamiento esperado.]

### Comandos de validación
```bash
dotnet restore
dotnet build
dotnet test