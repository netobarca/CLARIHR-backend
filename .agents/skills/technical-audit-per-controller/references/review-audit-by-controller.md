# Reference: Estándar de Auditoría Técnica por Controlador de API

## Objetivo del estándar

Este documento define el estándar de referencia para auditar técnicamente un controlador de API.

Debe usarse junto con la skill:

`auditoria-tecnica-por-controlador`

---

## Definición

Una **auditoría técnica por controlador** evalúa un controller específico y su vertical directa para determinar si cumple con buenas prácticas de:

- Arquitectura
- Seguridad
- Contrato API
- Rendimiento
- Concurrencia
- Observabilidad
- Pruebas
- Mantenibilidad

No representa una certificación completa de la API ni readiness productivo final.

---

## Clasificación del tipo de auditoría

| Nivel | Descripción | Certifica producción |
|---|---|---|
| Endpoint | Revisión detallada de un endpoint específico | No |
| Controller | Revisión del controller y su vertical directa | No |
| Módulo / Feature | Revisión de todos los controllers de un módulo | Parcialmente |
| API completa | Revisión integral de arquitectura, seguridad, performance y operación | Puede ser base para Go/No-Go |
| Productive Readiness | Evaluación final con pruebas, métricas, seguridad, observabilidad e infraestructura | Sí, si el alcance es completo |

Esta referencia aplica al nivel:

**Controller**

---

## Evidencia mínima esperada

| Evidencia | Obligatoria | Comentario |
|---|---:|---|
| Controller | Sí | Archivo principal auditado |
| Endpoints | Sí | Métodos y rutas expuestas |
| DTOs | Sí | Request/response |
| Validators | Sí | Validación de entrada |
| Handlers / Services | Sí | Lógica de aplicación |
| Repositories / Queries | Sí | Acceso a datos |
| Authorization Policies | Sí | Seguridad |
| Tests | Deseable | Unit/integration/contract |
| Swagger/OpenAPI | Deseable | Contrato público |
| Build/Test result | Deseable | Evidencia de calidad |
| Logs/Metrics | Opcional | Observabilidad |
| Pipeline CI/CD | Opcional | DevSecOps |

Si falta evidencia, marcar como:

**NO EVALUADO**

---

## Checklist base

### Arquitectura

| Control | Esperado |
|---|---|
| Controller sin lógica de negocio compleja | El controller solo orquesta request/response |
| Separación por capas | La lógica vive en Application/Domain/Services |
| Uso de DTOs | No exponer entidades directamente |
| Handlers o use cases claros | Cada operación debe tener responsabilidad clara |
| Validadores separados | Reglas de entrada fuera del controller |
| Bajo acoplamiento | No dependencias innecesarias |
| DI correcto | Sin instanciaciones innecesarias de servicios con dependencias |
| Async/await correcto | Sin bloqueos síncronos innecesarios |
| Transacciones claras | Operaciones críticas protegidas |
| Consistencia con patrones internos | Similar a controllers equivalentes |

---

### Seguridad

| Control | Esperado |
|---|---|
| Autenticación | Endpoints protegidos salvo excepciones justificadas |
| Autorización | Policies/roles/permissions por operación |
| BOLA / IDOR | No acceder recursos ajenos por ID manipulable |
| Function level authorization | No permitir funciones sin permiso correcto |
| Tenant isolation | Filtros y validaciones por tenant/company |
| Input validation | Validadores robustos |
| Mass assignment | DTOs limitados a campos permitidos |
| PII | No exponer datos sensibles innecesarios |
| Rate limiting | Aplicado en endpoints sensibles |
| CORS | Configuración restrictiva |
| Error handling | Sin stack traces ni detalles internos |
| Logging seguro | Sin tokens, passwords o PII innecesaria |
| Secrets | No hardcodeados |

---

### Contrato API

| Control | Esperado |
|---|---|
| Rutas RESTful | Recursos claros y consistentes |
| HTTP verbs | GET/POST/PUT/PATCH/DELETE usados correctamente |
| Versionamiento | Ruta o header versionado |
| Request DTO | Campos explícitos |
| Response DTO | Estructura estable |
| Status codes | Correctos y documentados |
| Error contract | ProblemDetails o estándar interno |
| Swagger/OpenAPI | Documentado |
| Paginación | Obligatoria en listados |
| Filtros | Validados |
| Sorting | Controlado |
| Search | Longitud mínima y límites |
| ETag / If-Match | En updates concurrentes si aplica |
| Idempotencia | En operaciones críticas si aplica |

---

### Rendimiento

| Control | Esperado |
|---|---|
| Queries eficientes | Sin consultas innecesarias |
| Índices esperados | Campos de búsqueda/filtro indexados |
| N+1 | Evitado |
| Paginación | En colecciones |
| Page size máximo | Definido |
| Proyecciones DTO | Preferible sobre cargar entidad completa |
| AsNoTracking | En lecturas si aplica |
| Includes | Solo los necesarios |
| Search sargable | Evitar LIKE '%x%' sin control |
| Operaciones pesadas | Evitar dentro del request |
| Llamadas externas | Controladas y resilientes |
| Rate limit | En endpoints costosos |

---

### Concurrencia y consistencia

| Control | Esperado |
|---|---|
| Transacciones | Para cambios críticos |
| Concurrencia optimista | ETag, RowVersion o equivalente |
| If-Match | Para PUT/PATCH sensibles |
| Conflictos | 409 o estándar interno |
| Validación de estado | No permitir transiciones inválidas |
| Rollback | Ante errores |
| Idempotencia | En operaciones repetibles |
| Cross-tenant validation | Obligatoria si aplica |

---

### Observabilidad

| Control | Esperado |
|---|---|
| Logs estructurados | Sí |
| Correlation ID | Sí |
| Audit logs | En acciones sensibles |
| Métricas | En endpoints críticos |
| Tracing | Deseable |
| Health checks | Deseable |
| Eventos de dominio | Si la arquitectura lo usa |
| Diagnóstico | Suficiente para QA/Producción |

---

### Pruebas

| Control | Esperado |
|---|---|
| Unit tests | Controller/handler/validator |
| Integration tests | Flujo real con DB o entorno equivalente |
| Contract tests | Request/response/status codes |
| Authorization tests | 401/403 |
| Tenant tests | Cross-tenant bloqueado |
| Validation tests | Inputs inválidos |
| Error tests | 400/404/409/422 |
| Concurrency tests | If-Match/ETag |
| Smoke tests | Endpoint responde en entorno |

---

## Estados del checklist

| Estado | Significado |
|---|---|
| PASS | Cumple |
| FAIL | No cumple |
| WARNING | Cumple parcialmente o con riesgo |
| NO EVALUADO | No hay evidencia suficiente |
| NO APLICA | El control no aplica al endpoint/controller |

---

## Severidades

### Crítica

Usar cuando el hallazgo puede causar:

- Acceso no autorizado a datos sensibles.
- Exposición grave de PII.
- Bypass de autenticación o autorización.
- Corrupción de datos críticos.
- Riesgo directo de incidente productivo severo.

### Alta

Usar cuando existe:

- Debilidad importante de seguridad.
- Falta de tenant isolation.
- Endpoint sensible sin control suficiente.
- Operación crítica sin validación adecuada.
- Riesgo alto de indisponibilidad o pérdida de datos.

### Media

Usar cuando existe:

- Deuda técnica relevante.
- Performance deficiente bajo ciertas condiciones.
- Contrato inconsistente.
- Falta de pruebas importantes.
- Observabilidad insuficiente en flujos relevantes.

### Baja

Usar cuando existe:

- Problema menor de mantenibilidad.
- Naming inconsistente.
- Duplicación pequeña.
- Mejora técnica no bloqueante.

### Observación

Usar para:

- Recomendaciones.
- Mejoras futuras.
- Buenas prácticas sugeridas.
- Riesgos no confirmados.

---

## Formato de hallazgo

```markdown
### H-001 — [Título del hallazgo]

**Severidad:** Crítica / Alta / Media / Baja / Observación  
**Categoría:** Seguridad / Arquitectura / Performance / Contrato / Pruebas / Observabilidad / Mantenibilidad  
**Ubicación:** Archivo, clase, método o endpoint  
**Condición encontrada:** Qué se encontró  
**Criterio esperado:** Qué debería cumplir  
**Impacto:** Qué riesgo genera  
**Evidencia:** Referencia concreta al código, prueba, configuración o resultado  
**Recomendación:** Qué se debe hacer  
**Prioridad:** Inmediata / Alta / Media / Baja  
**Esfuerzo estimado:** Bajo / Medio / Alto  
**Estado:** Abierto / Cerrado / Mitigado / Aceptado
```

---

## Matriz de priorización

```markdown
| ID | Severidad | Categoría | Hallazgo | Impacto | Esfuerzo | Prioridad | Acción recomendada |
|---|---|---|---|---|---|---|---|
| H-001 | Alta | Seguridad | Falta autorización en endpoint PATCH | Acceso indebido | Medio | Inmediata | Agregar policy y tests |
```

---

## Veredictos permitidos

### Aprobado

Usar cuando:

- No hay hallazgos críticos.
- No hay hallazgos altos.
- No hay hallazgos medios bloqueantes.
- La evidencia principal fue validada.
- El controller puede avanzar.

### Aprobado con observaciones

Usar cuando:

- Hay hallazgos bajos o medios no bloqueantes.
- Hay deuda técnica controlada.
- Hay limitaciones menores.
- El controller puede avanzar con seguimiento.

### No aprobado

Usar cuando:

- Hay hallazgos críticos.
- Hay hallazgos altos abiertos.
- Hay fallas de seguridad.
- Hay problemas graves de datos, contrato o concurrencia.
- Faltan controles esenciales.

### No evaluado completamente

Usar cuando:

- No hay evidencia suficiente.
- No se pudieron revisar archivos críticos.
- No se pudieron ejecutar pruebas esenciales.
- El entorno no permite validar comportamiento real.

---

## Tabla de veredicto

```markdown
| Nivel evaluado | Resultado |
|---|---|
| Controller auditado | Aprobado / Aprobado con observaciones / No aprobado |
| Endpoints internos del controller | Cubiertos / Parcialmente cubiertos / No cubiertos |
| Seguridad | Aprobado / Observaciones / No aprobado |
| Arquitectura | Aprobado / Observaciones / No aprobado |
| Performance | Aprobado / Observaciones / No aprobado |
| Pruebas | Aprobado / Observaciones / No aprobado |
| Readiness productivo completo | No certificado |
```

---

## Estructura final del reporte

```markdown
# Auditoría Técnica por Controlador — [NombreDelController]

## 1. Resumen ejecutivo

## 2. Alcance

## 3. Metodología

## 4. Inventario de endpoints

| Método | Ruta | Propósito | Handler/Use Case | Request | Response | Riesgo |
|---|---|---|---|---|---|---|

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia | Comentario |
|---|---|---|---|---|

## 6. Análisis técnico

### 6.1 Arquitectura

### 6.2 Seguridad

### 6.3 Contrato API

### 6.4 Rendimiento

### 6.5 Concurrencia y consistencia

### 6.6 Observabilidad

### 6.7 Pruebas

### 6.8 Build / DevSecOps

## 7. Hallazgos

## 8. Hallazgos fuera de alcance / trazabilidad

## 9. Matriz de priorización

## 10. Veredicto del controlador

## 11. Recomendaciones finales

## 12. Anexos / Evidencia revisada
```

---

## Límites de interpretación

Una auditoría por controller puede decir:

- “Este controller está aprobado.”
- “Este controller tiene observaciones.”
- “Este controller no debe avanzar.”
- “Este controller no fue evaluado completamente.”

No debe decir:

- “Toda la API está lista.”
- “El sistema está certificado para producción.”
- “La infraestructura está auditada.”
- “El módulo completo está aprobado”, salvo que todos sus controllers hayan sido auditados.

---

## Principio final

El auditor debe ser estricto con la evidencia.

Si no hay evidencia, no asumir.

Si algo no se puede comprobar, declarar:

**NO EVALUADO**
