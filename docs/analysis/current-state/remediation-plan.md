# Plan de remediacion y madurez

## 1. Objetivo

Este plan prioriza las correcciones derivadas de la reevaluacion profunda de auditoria actualizada al **29 de marzo de 2026**.

La logica de prioridad usada es:

- `P0`: bloquea aprobacion para produccion
- `P1`: alta prioridad del primer sprint posterior
- `P2`: deuda importante, pero ya no bloqueo inmediato

## 2. Prioridades

| Prioridad | Iniciativa | Motivo | Estado |
|---|---|---|---|
| P0 | Separar acceso global de plataforma del auth core | Se elimino `PlatformAdminEmails`, se introdujo `PlatformOperator` persistido y el backoffice ahora usa tokens `platform` separados | Implementado |
| P0 | Redisenar auditoria para no persistir PII sensible de RRHH | La auditoria conserva payloads completos y el detalle expone `Before/After/Diff` | Pendiente |
| P0 | Introducir rate limiting y controles anti abuso en auth | No hay proteccion visible para `register`, `login`, `external` ni `refresh` | Pendiente |
| P0 | Cerrar confianza de `X-Forwarded-*` por entorno | La app limpia proxies/redes conocidas y usa esa IP para logging y auditoria | Pendiente |
| P1 | Volver tenant filter global a `fail-closed` | El filtro EF actual permite lecturas abiertas cuando falta tenant context | Pendiente |
| P1 | Revisar y gobernar todos los `IgnoreQueryFilters()` | El patron esta ampliamente distribuido y necesita matriz de legitimidad y pruebas | Pendiente |
| P1 | Unificar la gobernanza entre planes, add-ons, `effectiveModules` y permisos | El backend ya deja `FREE` y `MASTER` con la misma cobertura modular base, mantiene `MASTER` como plan interno con resincronizacion automatica y conserva la union de entitlements de plan + add-on, pero la semantica comercial sigue distribuida y el marketplace aun no evalua redundancia o compatibilidad por modulo | En progreso |
| P1 | Agregar auditoria persistente para writes globales de `CommercialPlan` | El backoffice ya persiste `PlatformAuditLog` para writes globales y reemplazo de suscripciones | Implementado |
| P1 | Automatizar OpenAPI y pruebas de contrato | El contrato versionado ya fue ampliado para core y backoffice clave, pero sigue manual y sin contract tests automatizados | En progreso |
| P1 | Estandarizar `publicId` y `normalizedCode` en contratos publicos | La API necesitaba ocultar `id` interno, estabilizar identificadores publicos y unificar `code` en `UPPERCASE` | Implementado |
| P1 | Sacar exportes pesados del request path | Hay exportes y analytics que materializan datasets completos y generan archivos en controllers | Pendiente |
| P2 | Endurecer politica de password con blocklist de credenciales comprometidas | La politica actual valida complejidad y datos personales, pero no credenciales comprometidas | Pendiente |
| P2 | Agregar observabilidad por modulo y endpoint | Hace falta medir latencia, tamano y errores de rutas pesadas y de seguridad alta | Pendiente |
| P2 | Mantener analisis vivos y checklist alineados con el codigo real | La reevaluacion detecto drift documental en arquitectura, testing y contrato API | En progreso |

## 3. Orden recomendado

1. auditoria y PII de RRHH
2. rate limiting y anti abuso de auth
3. forwarded headers y confianza de proxy
4. tenant filter `fail-closed`
5. matriz de `IgnoreQueryFilters()`
6. matriz canonica de planes, add-ons, `effectiveModules` y permisos
7. OpenAPI versionado y contract tests
8. exportes y analytics fuera del request path
9. blocklist de passwords, observabilidad y disciplina documental

Iniciativas ya cerradas en la actualizacion del 29 de marzo:

- separacion del acceso global de plataforma respecto al auth core
- auditoria persistente para writes globales de plataforma

## 4. Auditoria puntual de suscripciones, add-ons y accesos

Hallazgos permanentes que deben guiar la remediacion:

- los add-ons no estan fuera de `effectiveModules` en backend; `PlanEntitlementService` ya calcula la union de modulos del plan activo y de los add-ons activos.
- el problema real es de gobernanza y trazabilidad comercial: la plataforma separa correctamente producto comercial y modulo efectivo, pero todavia no ofrece una matriz canonica unica para responder que producto habilita que capacidad y que permisos quedan condicionados por ese modulo.
- `FREE` y `MASTER` hoy comparten el catalogo base completo para evitar bloqueos operativos por plan, pero todavia falta una matriz canonica unica que alinee catalogo, marketplace, `effectiveModules` y permisos.
- la elegibilidad de add-ons hoy no usa redundancia ni compatibilidad por modulo como criterio primario; eso puede mostrar add-ons "comprables" aunque no agreguen capacidad neta frente al plan actual.
- el acceso a `USERS` queda protegido tecnicamente por `RBAC_USERS` y `PermissionMatrixCatalog`, pero el diseño sigue siendo indirecto y facil de malinterpretar en futuras extensiones.

Salida esperada de esta remediacion:

- matriz canonica `producto comercial -> modulos efectivos -> recursos RBAC -> permisos`
- reglas explicitas de redundancia, incompatibilidad y dependencia para add-ons
- catalogo base comercial alineado entre `FREE` y `MASTER`
- pruebas de integracion que validen que marketplace, preview y enforcement de acceso responden a la misma verdad

## 5. Criterio de cierre

Cada iniciativa deberia cerrarse solo cuando:

- el codigo este implementado
- la documentacion viva quede actualizada
- existan pruebas nuevas en la zona de riesgo correspondiente
- el cambio deje evidencia verificable de la mejora

## 6. Resultado esperado

La solucion no deberia considerarse aprobada para produccion hasta cerrar todos los puntos `P0` pendientes y, como minimo, dejar encaminados con implementacion real los `P1` de tenant isolation y contrato API.
