# Skill: Auditoría Técnica por Controlador de API

## Propósito

Usa esta skill cuando el usuario solicite auditar un **controller específico** de una API backend.

La auditoría debe validar si el controlador y su vertical directa cumplen buenas prácticas de:

- Arquitectura
- Seguridad
- Contrato API
- Rendimiento
- Concurrencia
- Observabilidad
- Pruebas
- Calidad técnica
- Preparación para avanzar a QA o revisión productiva posterior

Esta skill **no certifica toda la API** ni reemplaza una auditoría integral de producción.

---

## Cuándo usar esta skill

Usar esta skill cuando el usuario solicite:

- Auditar un controller.
- Revisar un controlador específico.
- Validar si un controller está bien implementado.
- Revisar arquitectura, seguridad o performance de un controller.
- Generar un informe técnico por controller.
- Evaluar si un controller puede pasar a QA.
- Comparar un controller contra estándares internos del proyecto.

Ejemplos:

- “Audita este controller.”
- “Revisa si este controller está listo.”
- “Haz una auditoría técnica de `UsersController`.”
- “Valida seguridad, arquitectura y performance de este controlador.”
- “Quiero una auditoría similar al documento de PersonnelFilesController.”

---

## Alcance obligatorio

Auditar únicamente el controlador indicado y su vertical directa.

Incluir:

- Controller.
- Endpoints del controller.
- Commands / Queries / Use Cases relacionados.
- Handlers.
- Validators.
- DTOs de request y response.
- Services usados directamente.
- Repositories o queries relacionadas.
- Policies de autorización.
- Middleware, filtros o conventions que afecten al controller.
- Configuración OpenAPI/Swagger relacionada.
- Tests relacionados.
- Migraciones o cambios de base de datos si afectan directamente al controller.

Excluir:

- Auditoría completa de la API.
- Auditoría completa de infraestructura.
- Certificación productiva final.
- Controllers no relacionados.
- Pruebas profundas de carga, salvo que el usuario proporcione evidencia.

Si se detectan riesgos en controllers hermanos o componentes externos, documentarlos como:

## Hallazgos fuera de alcance / trazabilidad

No mezclar esos hallazgos con el veredicto principal del controller auditado.

---

## Reglas obligatorias

1. No inventar evidencia.
2. Todo hallazgo debe indicar archivo, clase, método, endpoint o componente relacionado.
3. Si algo no puede validarse, marcarlo como **NO EVALUADO**.
4. Diferenciar entre:
   - Hallazgo real.
   - Riesgo potencial.
   - Mejora recomendada.
   - Deuda técnica aceptable.
   - Elemento fuera de alcance.
5. No declarar que el controller está listo si existen hallazgos críticos o altos abiertos.
6. No declarar que la API completa está lista para producción.
7. Validar contra código real, no solo contra comentarios o nombres de archivos.
8. Indicar si las pruebas fueron ejecutadas o solo revisadas.
9. Si no hay pruebas de integración, indicarlo como limitación.
10. Usar severidades claras:
    - Crítica
    - Alta
    - Media
    - Baja
    - Observación

---

## Inputs recomendados

Solicitar o revisar, si están disponibles:

- Archivo del controller.
- Handlers / use cases.
- Validators.
- DTOs.
- Repositories.
- Services relacionados.
- Configuración de autorización.
- Configuración de rutas/versionamiento.
- Swagger/OpenAPI.
- Tests relacionados.
- Reglas internas del proyecto.
- Resultado de build.
- Resultado de pruebas.
- Migrations o scripts DB relacionados.

---

## Flujo de trabajo

### 1. Entender el alcance

Identificar:

- Nombre del controller.
- Módulo o feature.
- Tecnología.
- Arquitectura.
- Endpoints expuestos.
- Archivos relacionados.
- Limitaciones de evidencia.

### 2. Crear inventario de endpoints

Generar una tabla:

| Método | Ruta | Propósito | Handler/Use Case | Request | Response | Riesgo |
|---|---|---|---|---|---|---|

### 3. Revisar arquitectura

Validar:

- Separación de responsabilidades.
- Controller sin lógica de negocio compleja.
- Uso correcto de handlers, commands o services.
- DTOs adecuados.
- Dependency Injection.
- Async/await.
- Transacciones.
- Consistencia con patrones internos.
- Duplicación de lógica.
- Acoplamiento innecesario.

### 4. Revisar seguridad

Validar:

- Autenticación.
- Autorización.
- Roles, claims, scopes o permissions.
- BOLA / IDOR.
- Broken Function Level Authorization.
- Tenant isolation.
- Validaciones de entrada.
- Mass assignment.
- PII o datos sensibles.
- Rate limiting.
- CORS.
- Manejo seguro de errores.
- Logging seguro.
- OWASP API Security Top 10 aplicable.

### 5. Revisar contrato API

Validar:

- Rutas RESTful.
- Métodos HTTP correctos.
- Versionamiento.
- Request/response DTOs.
- Status codes.
- ProblemDetails o errores estándar.
- OpenAPI/Swagger.
- Paginación.
- Filtros.
- Ordenamiento.
- Búsqueda.
- ETag / If-Match.
- Idempotencia.
- Compatibilidad hacia atrás.

### 6. Revisar rendimiento

Validar:

- Queries.
- Índices esperados.
- Riesgo N+1.
- Paginación.
- Límites de page size.
- Búsquedas no sargables.
- Includes innecesarios.
- Proyecciones DTO.
- AsNoTracking o equivalente.
- Carga innecesaria de entidades.
- Llamadas externas repetitivas.
- Riesgo bajo concurrencia.
- Rate limit.

### 7. Revisar concurrencia y consistencia

Validar:

- Transacciones.
- Concurrencia optimista.
- ETag / If-Match.
- Validación de estados.
- Reglas de negocio críticas.
- Manejo de conflictos.
- Rollback.
- Idempotencia.
- Validación cross-tenant o cross-company.

### 8. Revisar observabilidad

Validar:

- Logs estructurados.
- Correlation ID.
- Audit logs.
- Eventos de dominio.
- Métricas.
- Health checks.
- Trazabilidad.
- Registro de acciones sensibles.

### 9. Revisar pruebas

Validar:

- Unit tests.
- Integration tests.
- Contract tests.
- Authorization tests.
- Tenant isolation tests.
- Validation tests.
- Error handling tests.
- Concurrency tests.
- Smoke tests.
- Evidencia de ejecución.

Indicar:

- Comando ejecutado.
- Resultado.
- Pruebas pasadas/fallidas.
- Pruebas no ejecutadas.
- Limitaciones del entorno.

### 10. Generar hallazgos

Cada hallazgo debe tener:

- ID.
- Título.
- Severidad.
- Categoría.
- Ubicación.
- Condición encontrada.
- Criterio esperado.
- Impacto.
- Evidencia.
- Recomendación.
- Prioridad.
- Esfuerzo.
- Estado.

### 11. Emitir veredicto

Usar uno de estos resultados:

- **Aprobado**
- **Aprobado con observaciones**
- **No aprobado**
- **No evaluado completamente**

No certificar readiness productivo completo.

---

## Formato de salida obligatorio

La respuesta debe generarse en Markdown con esta estructura:

```markdown
# Auditoría Técnica por Controlador — [NombreDelController]

## 1. Resumen ejecutivo
## 2. Alcance
## 3. Metodología
## 4. Inventario de endpoints
## 5. Checklist de auditoría
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

## Criterio de éxito

La auditoría es correcta si permite responder:

- Qué controller fue auditado.
- Qué endpoints contiene.
- Qué evidencia fue revisada.
- Qué cumple.
- Qué no cumple.
- Qué no pudo evaluarse.
- Qué riesgos existen.
- Qué debe corregirse.
- Si el controller puede avanzar o no.
- Qué queda fuera del alcance.
