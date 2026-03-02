# Security Analysis

## Context

- Delivery baseline: current state up to HU-008
- Date: 2026-03-01
- Focus: auth, authorization, tenant isolation, sensitive data handling, auditability and exposure surface

## Security Compliance

### 1. Authentication and session handling

Estado: compliant for current scope

Evidencia:

- JWT bearer auth esta configurado en `src/CLARIHR.Api/Program.cs`.
- `SaveToken = false` y `ClockSkew = TimeSpan.Zero` reducen superficie innecesaria y laxitud en expiracion.
- `EnableSensitiveDataLogging(false)` esta activo en `src/CLARIHR.Infrastructure/DependencyInjection.cs`.

Observacion:

- En `Development` existe una signing key local comprometida intencionalmente en `appsettings.Development.json`. Esto es aceptable solo para entorno local y no debe migrar a QA o produccion.

### 2. Authorization enforced in backend

Estado: compliant

Evidencia:

- `src/CLARIHR.Api/Authorization/AuthorizeResourceFilter.cs` aplica RBAC L1/L2 a nivel endpoint.
- `src/CLARIHR.Infrastructure/IdentityAccess/RbacAuthorizationService.cs` aplica deny-by-default, valida tenant context y registra denegaciones.
- `AuthorizeFieldsAsync` niega updates con campos no editables y retorna `FIELD_EDIT_FORBIDDEN`.

Conclusion:

- El enforcement no depende de la UI. La API ya es el guard.

### 3. Multi-tenant isolation

Estado: compliant

Evidencia:

- `ApplicationDbContext` filtra entidades tenant-scoped automaticamente.
- Los writes tenant-scoped sin tenant context fallan con excepcion de infraestructura.
- Los servicios de negocio y autorizacion devuelven `TENANT_MISMATCH` cuando detectan ownership cruzado.

Conclusion:

- La aislacion por empresa esta reforzada en mas de una capa, que era un requisito clave desde el inicio.

### 4. Standardized authorization errors

Estado: compliant

Evidencia:

- `ProblemDetailsFactory` transforma errores de aplicacion a respuestas uniformes.
- Existen codigos dedicados como `UNAUTHENTICATED`, `RBAC_DENIED`, `TENANT_MISMATCH` y `FIELD_EDIT_FORBIDDEN`.

Conclusion:

- Los contratos de error estan estandarizados y alineados con HU-007.

### 5. Sensitive data handling and audit sanitization

Estado: compliant

Evidencia:

- `src/CLARIHR.Infrastructure/Auditing/AuditSanitizer.cs` remueve password, hashes, refresh tokens, secrets, api keys y private keys.
- La auditoria administrativa guarda before/after/diff sanitizado.
- `SecurityHeadersMiddleware` marca rutas `/api/auth` y `/api/iam` como `no-store`.

Conclusion:

- La solucion evita exponer o persistir secretos conocidos en auditoria.

### 6. Operational exposure

Estado: mostly compliant

Evidencia:

- Swagger se expone solo en `Development` en `Program.cs`.
- `UseHttpsRedirection()` se deja fuera de `Development` para evitar ruido local, pero se mantiene fuera de ese entorno.
- `UnhandledExceptionMiddleware` oculta detalle sensible fuera de `Development`.

Tradeoff:

- La seguridad de exposicion sigue dependiendo de que el entorno este correctamente configurado como `Production` fuera del entorno local.

## Residual Security Risks

### 1. HTTP integration coverage is still partial

Riesgo:

- Ya existe una suite HTTP end-to-end ampliada.
- La cobertura automatizada de seguridad en el pipeline real ya abarca company users, audit, IAM y endpoints RBAC sensibles de lectura y escritura, pero aun no cubre toda la superficie posible del API.

Impacto:

- Todavia puede haber regresiones fuera de los flujos criticos ya cubiertos.

### 2. Development secrets in repository

Riesgo:

- `appsettings.Development.json` contiene una signing key de desarrollo.

Impacto:

- No es un problema de produccion si se mantiene local, pero puede fomentar reutilizacion indebida o confusion operativa si no se documenta bien.

Mitigacion recomendada:

- Mantenerla solo para local y preferir `user-secrets` o variables de entorno cuando el entorno local requiera credenciales reales.

### 3. Limited response hardening headers

Riesgo:

- El middleware de seguridad actual agrega `nosniff`, `no-referrer` y `no-store` para rutas sensibles, pero no incluye otras capas operativas como HSTS o politicas de borde.

Impacto:

- Para una API detras de reverse proxy esto puede ser aceptable, pero debe quedar claro que parte del hardening queda delegado a infraestructura de despliegue.

### 4. Authorization freshness across nodes

Riesgo:

- La solucion ya soporta modo `Distributed`, pero el entorno debe registrar un `IDistributedCache` real para garantizar frescura cross-node.

Impacto:

- Si la aplicacion corre en varias instancias sin activar ese proveedor compartido, puede haber ventanas cortas de inconsistencia tras cambios de permisos.

## Final Assessment

Veredicto:

- La solucion cumple los requerimientos de seguridad funcional y de backend enforcement para el alcance actual.
- Los riesgos residuales son principalmente operativos y de madurez de pruebas, no fallas estructurales del modelo de seguridad.
- Antes de considerar produccion real, el sistema necesita endurecer validacion end-to-end y asegurar que la estrategia operativa de secretos y cache distribuida este activada segun la topologia real.
