# Plan de remediacion y madurez

## 1. Objetivo

Este plan prioriza las correcciones derivadas de la reevaluacion profunda de auditoria del **28 de marzo de 2026**.

La logica de prioridad usada es:

- `P0`: bloquea aprobacion para produccion
- `P1`: alta prioridad del primer sprint posterior
- `P2`: deuda importante, pero ya no bloqueo inmediato

## 2. Prioridades

| Prioridad | Iniciativa | Motivo | Estado |
|---|---|---|---|
| P0 | Bloquear autoelevacion a `platform_admin` desde registro local | Hoy el grant global depende de email allow-list y el registro local es anonimo con emision inmediata de tokens | Pendiente |
| P0 | Redisenar auditoria para no persistir PII sensible de RRHH | La auditoria conserva payloads completos y el detalle expone `Before/After/Diff` | Pendiente |
| P0 | Introducir rate limiting y controles anti abuso en auth | No hay proteccion visible para `register`, `login`, `external` ni `refresh` | Pendiente |
| P0 | Cerrar confianza de `X-Forwarded-*` por entorno | La app limpia proxies/redes conocidas y usa esa IP para logging y auditoria | Pendiente |
| P1 | Volver tenant filter global a `fail-closed` | El filtro EF actual permite lecturas abiertas cuando falta tenant context | Pendiente |
| P1 | Revisar y gobernar todos los `IgnoreQueryFilters()` | El patron esta ampliamente distribuido y necesita matriz de legitimidad y pruebas | Pendiente |
| P1 | Agregar auditoria persistente para writes globales de `CommercialPlan` | Hoy solo hay logging operativo, no `AuditLog` durable | Pendiente |
| P1 | Automatizar OpenAPI y pruebas de contrato | El contrato versionado esta muy por detras de la superficie real de la API | Pendiente |
| P1 | Estandarizar `publicId` y `normalizedCode` en contratos publicos | La API necesitaba ocultar `id` interno, estabilizar identificadores publicos y unificar `code` en `UPPERCASE` | Implementado |
| P1 | Sacar exportes pesados del request path | Hay exportes y analytics que materializan datasets completos y generan archivos en controllers | Pendiente |
| P2 | Endurecer politica de password con blocklist de credenciales comprometidas | La politica actual valida complejidad y datos personales, pero no credenciales comprometidas | Pendiente |
| P2 | Agregar observabilidad por modulo y endpoint | Hace falta medir latencia, tamano y errores de rutas pesadas y de seguridad alta | Pendiente |
| P2 | Mantener analisis vivos y checklist alineados con el codigo real | La reevaluacion detecto drift documental en arquitectura, testing y contrato API | En progreso |

## 3. Orden recomendado

1. autoelevacion a `platform_admin`
2. auditoria y PII de RRHH
3. rate limiting y anti abuso de auth
4. forwarded headers y confianza de proxy
5. tenant filter `fail-closed`
6. matriz de `IgnoreQueryFilters()`
7. auditoria persistente para plataforma
8. OpenAPI versionado y contract tests
9. exportes y analytics fuera del request path
10. blocklist de passwords, observabilidad y disciplina documental

## 4. Criterio de cierre

Cada iniciativa deberia cerrarse solo cuando:

- el codigo este implementado
- la documentacion viva quede actualizada
- existan pruebas nuevas en la zona de riesgo correspondiente
- el cambio deje evidencia verificable de la mejora

## 5. Resultado esperado

La solucion no deberia considerarse aprobada para produccion hasta cerrar todos los puntos `P0` y, como minimo, dejar encaminados con implementacion real los `P1` de tenant isolation, auditoria global y contrato API.
