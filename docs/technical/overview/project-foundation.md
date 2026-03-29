# Project Foundation — CLARIHR Backend

## 1. Propósito del documento

Este documento define la **base canónica del proyecto CLARIHR Backend** y establece las reglas generales que deben guiar:

- la implementación funcional y técnica,
- la organización arquitectónica,
- la seguridad,
- el rendimiento,
- la estrategia de pruebas,
- y la documentación que debe mantenerse viva a medida que evolucionan las historias de usuario.

Este archivo es la **fuente oficial de referencia del proyecto** a nivel de visión, lineamientos y gobernanza técnica.  
Los archivos operativos para Codex, skills, plantillas y documentación específica deben alinearse con este documento.

---

## 2. Contexto del sistema

**CLARIHR** es un sistema SaaS de recursos humanos multiempresa / multi-tenant, orientado a administrar procesos organizacionales y operativos relacionados con personas, estructura organizativa, permisos, compensaciones, auditoría y módulos posteriores de RRHH.

### Características generales del sistema
- Producto SaaS multi-tenant
- Backend API para consumo de sitio web y aplicación móvil
- Arquitectura preparada para crecimiento por tenant, usuarios, empleados y transacciones
- Manejo de información sensible:
  - datos personales,
  - información salarial,
  - permisos y autorizaciones,
  - auditoría y trazabilidad
- Módulos funcionales con dependencia entre sí
- Necesidad de seguridad, mantenibilidad, trazabilidad y rendimiento desde el diseño

---

## 3. Stack tecnológico oficial

### Backend
- **.NET 10 Web API**

### Arquitectura
- **Clean Architecture**
- **CQRS**
- **FluentValidation**
- **Result Pattern + ProblemDetails**
- **Autorización RBAC nivel 3**
- **Diseño tenant-scoped**

### Persistencia
- **PostgreSQL**
- **EF Core**

### Autenticación y autorización
- **JWT Access Token**
- **Refresh Tokens**
- **RBAC por roles, módulos, acciones y permisos de campo**

### Integraciones y ejecución
- Consumo desde:
  - sitio web
  - aplicación móvil
- Infraestructura cloud-ready
- Diseño agnóstico del proveedor cuando sea posible

---

## 4. Objetivos principales del backend

El backend debe garantizar:

1. separación estricta de responsabilidades por capas,
2. implementación consistente de CQRS,
3. aislamiento real entre tenants,
4. seguridad aplicada desde el diseño,
5. rendimiento predecible en consultas y operaciones transaccionales,
6. trazabilidad funcional y técnica,
7. facilidad para pruebas unitarias y evolución futura,
8. documentación viva y ordenada,
9. reducción del caos documental,
10. consistencia al implementar nuevas historias de usuario.

---

## 5. Principios no negociables del proyecto

### 5.1 Arquitectura limpia
- Domain no depende de Application, Infrastructure ni API.
- Application depende solo de Domain.
- Infrastructure implementa contratos definidos por Application.
- API depende de Application.

### 5.2 Separación de responsabilidades
- Los controladores no contienen lógica de negocio.
- Domain no contiene lógica de persistencia.
- No se exponen entidades EF directamente al cliente.
- Los DTOs no contaminan la capa Domain.

### 5.3 Disciplina CQRS
- Los **Commands** modifican estado.
- Los **Queries** nunca modifican estado.
- Las lecturas deben proyectar a DTOs.
- Las escrituras deben respetar reglas de dominio, validación y autorización.

### 5.4 Seguridad por diseño
- Toda operación debe respetar tenant, usuario y permisos.
- No debe existir acceso cross-tenant.
- Los datos sensibles no deben exponerse innecesariamente.
- Los secretos nunca se almacenan en código fuente.

### 5.5 Rendimiento por diseño
- No se permiten consultas sin paginación en endpoints de listado.
- Las lecturas deben ser optimizadas con proyecciones y `AsNoTracking`.
- Los trabajos pesados deben salir del request path cuando aplique.
- Los índices deben responder a patrones reales de consulta.

### 5.6 Documentación viva
- El proyecto debe evitar documentos duplicados para un mismo propósito.
- Cada tipo de información debe tener una **fuente canónica única**.
- Las historias de usuario deben actualizar documentos existentes antes de crear nuevos.

---

## 6. Arquitectura oficial del proyecto

La arquitectura base del backend es:

- **Clean Architecture**
- **CQRS**
- **Tenant-scoped by default**
- **Result + ErrorCodes + ProblemDetails**
- **Seguridad y performance integrados como reglas de implementación**

### Capas oficiales
- **Domain**
- **Application**
- **Infrastructure**
- **API**

---

## 7. Responsabilidades por capa

## 7.1 Domain

Contiene:
- entidades,
- value objects,
- reglas e invariantes de negocio,
- eventos de dominio,
- aggregates cuando aplique.

Reglas:
- no usa EF Core,
- no usa conceptos HTTP,
- no usa DTOs,
- no depende de infraestructura,
- no contiene acceso a base de datos.

---

## 7.2 Application

Contiene:
- Commands y CommandHandlers,
- Queries y QueryHandlers,
- DTOs de request/response,
- validadores,
- contratos/interfaces,
- políticas de autorización y requerimientos funcionales,
- behaviors de pipeline,
- mapeos no triviales,
- lógica de casos de uso.

Reglas:
- no accede directamente a la base de datos,
- no depende de infraestructura concreta,
- aquí vive el flujo de aplicación,
- debe ser testeable sin infraestructura real.

---

## 7.3 Infrastructure

Contiene:
- `DbContext`,
- configuraciones EF Core,
- repositorios,
- servicios de cifrado,
- caché,
- integraciones externas,
- background jobs,
- logging/telemetry wiring.

Reglas:
- implementa contratos definidos en Application,
- no define reglas de negocio,
- no expone detalles de infraestructura al resto del sistema.

---

## 7.4 API

Contiene:
- controllers,
- middleware,
- configuración del pipeline,
- autenticación y autorización a nivel de entrada,
- mapeo de errores a `ProblemDetails`,
- wiring general del backend.

Reglas:
- no contiene lógica de negocio,
- no accede directamente a EF,
- no contiene reglas de dominio.

---

## 8. Estándares de implementación CQRS

## 8.1 Commands
Los Commands deben:
- representar una intención explícita de cambio,
- validar entrada,
- ejecutar reglas de negocio,
- aplicar autorización requerida,
- persistir cambios de forma segura,
- generar auditoría cuando aplique.

## 8.2 Queries
Los Queries deben:
- usar `AsNoTracking()` por defecto,
- proyectar a DTOs,
- soportar paginación, filtros y ordenamiento cuando aplique,
- evitar cargar agregados completos innecesariamente,
- respetar tenant y permisos de lectura.

## 8.3 Validación
- Toda entrada debe validarse mediante validadores explícitos.
- Las reglas de validación deben vivir fuera del controlador.
- Los errores deben regresar en un formato consistente.

## 8.4 Errores
- Se usa `Result` como patrón de respuesta interna.
- La salida HTTP debe mapearse a `ProblemDetails`.
- No deben exponerse stack traces ni detalles sensibles en producción.

---

## 9. Reglas multi-tenant

El sistema es **tenant-scoped by default**.

### Reglas obligatorias
- El `TenantId` proviene del claim `tid` del JWT.
- Toda lectura y escritura debe estar acotada al tenant activo.
- Deben usarse filtros globales donde tenga sentido.
- Las validaciones de pertenencia al tenant deben existir también a nivel de aplicación.
- No debe ser posible acceder a información de otro tenant.
- El diseño de tablas, índices y consultas debe contemplar siempre el tenant.

### Regla de exposición
Cuando aplique por seguridad, las respuestas deben evitar revelar si un recurso existe fuera del tenant.

---

## 10. Convenciones de API

## 10.1 Principios generales
- Los endpoints deben ser consistentes y estables.
- Los listados deben ser paginados.
- Las respuestas deben ser claras, mínimas y específicas para el caso de uso.
- El contrato de API debe priorizar claridad, estabilidad y seguridad.

## 10.2 Manejo de errores HTTP
Mapeo base:
- `200 / 201` éxito
- `400` validación o entrada inválida
- `401` no autenticado
- `403` no autorizado
- `404` no encontrado o no visible por política de tenant
- `409` conflicto
- `429` rate limit
- `500` error inesperado

## 10.3 IDs
Estándar obligatorio del proyecto:
- Toda entidad persistida debe conservar `Id` interno (`BIGINT`) para PK, FK, joins, EF Core e índices.
- Toda entidad persistida debe exponer también `PublicId` persistido (`public_id`) para identificación externa.
- Ningún request, response, exportación o contrato público puede exponer `id` ni `internalId`.
- Hacia afuera el nombre oficial es siempre `publicId` o `<Entidad>PublicId`.
- Los módulos deben resolver recursos públicos por `PublicId` y traducir a `Id` interno solo dentro de Application/Infrastructure.
- Los seeds y catálogos globales deben usar `public_id` determinístico cuando la estabilidad entre ambientes importe.

## 10.4 Convenciones de salida
- No exponer entidades EF directamente
- No exponer información sensible innecesaria
- Mantener DTOs explícitos por endpoint o caso de uso cuando el dominio lo requiera
- Si un recurso tiene código de negocio, el contrato público debe incluir `code` y `normalizedCode`
- `code` y `normalizedCode` deben publicarse en `UPPERCASE`

---

## 11. Línea base de seguridad

La seguridad del backend no es opcional; es parte del diseño del sistema.

## 11.1 Transporte
- HTTPS obligatorio en producción
- Configuración correcta detrás de reverse proxy

## 11.2 Tokens
- Access tokens JWT de vida corta
- Refresh tokens con rotación y revocación
- Los refresh tokens deben almacenarse de forma segura

## 11.3 Autorización
- RBAC nivel 3
- Validación por rol, módulo, acción y permisos requeridos
- Validación de ownership / tenant / alcance funcional cuando aplique

## 11.4 Protección contra abuso
- Rate limiting en endpoints sensibles
- Lockout o protección ante intentos fallidos de autenticación
- Respuestas genéricas para evitar enumeración de usuarios

## 11.5 Protección de datos sensibles
- Cifrado cuando aplique
- No registrar secretos, contraseñas ni tokens
- Minimización de exposición de PII

## 11.6 Auditoría
Debe existir trazabilidad mínima para eventos como:
- login / logout,
- cambios de permisos,
- aprobaciones,
- cambios salariales,
- ejecuciones críticas,
- exportaciones,
- acciones administrativas relevantes.

---

## 12. Línea base de rendimiento

El rendimiento debe ser una propiedad del diseño, no una corrección tardía.

## 12.1 Consultas
- Todas las lecturas deben evitar cargas innecesarias
- `AsNoTracking()` por defecto en queries
- Proyección directa a DTOs
- No se permiten listados sin paginación

## 12.2 Base de datos
- Índices orientados a los patrones reales de uso
- Índices compuestos tenant-first donde corresponda
- Evitar full scans sobre tablas calientes
- Definir estrategia de particionamiento cuando el volumen lo justifique

## 12.3 Escrituras
- Transacciones cortas
- Evitar request path pesado
- Evaluar operaciones masivas y asincronía en procesos grandes

## 12.4 Reportes y exportaciones
- Los procesos pesados deben prepararse para ejecución asíncrona
- No se deben construir reportes pesados leyendo tablas transaccionales sin estrategia

## 12.5 Caché
- Solo para datos de baja volatilidad y alta repetición
- Siempre tenant-scoped
- Con política clara de invalidación

## 12.6 Observabilidad
El sistema debe prepararse para medir:
- latencia,
- errores,
- consultas lentas,
- throughput,
- rendimiento por endpoint,
- comportamiento de base de datos bajo carga.

---

## 13. Estrategia oficial de pruebas unitarias

El proyecto debe mantener una estrategia de pruebas consistente, especialmente en Application y Domain.

## 13.1 Alcance unitario
Se deben probar:
- reglas de dominio,
- invariantes,
- handlers CQRS,
- validadores,
- servicios puros de autorización,
- mapeos no triviales,
- Result / ErrorCodes / transformaciones puras.

No se cubre con unit tests:
- base de datos real,
- middleware HTTP,
- controllers,
- integraciones externas reales,
- pruebas de carga.

## 13.2 Principios
- probar comportamiento, no implementación interna,
- tests determinísticos,
- sin dependencia real de reloj, red o base de datos,
- uso de abstracciones para tiempo, usuario y tenant cuando aplique.

## 13.3 Estándares
- framework base: **xUnit**
- assertions: **FluentAssertions**
- mocking: una sola librería estandarizada por proyecto
- tests con convención legible y consistente

## 13.4 Enfoque mínimo por HU
Toda historia implementada debe dejar pruebas al menos para:
- happy path,
- validaciones,
- permisos,
- tenant scope,
- errores esperados,
- reglas críticas del caso de uso.

---

## 14. Estructura documental del proyecto

La documentación del proyecto se divide en dos grandes grupos:

### 14.1 Documentación viva
Representa el **estado actual** del sistema.  
Estos archivos se **actualizan**, no se duplican por historia.

Ejemplos:
- flujos actuales de negocio,
- arquitectura actual,
- análisis actual de seguridad,
- análisis actual de performance,
- testing analysis,
- referencia técnica vigente.

### 14.2 Registro de cambio
Representa el **impacto de una historia de usuario específica**.  
Aquí sí puede existir un archivo por HU, pero controlado y resumido.

Ejemplos:
- qué se implementó,
- qué documentos se actualizaron,
- impactos técnicos,
- riesgos,
- validaciones ejecutadas,
- cambios pendientes.

---

## 15. Regla de fuente canónica única

Para cada tipo de información debe existir una sola fuente oficial.

### Reglas
- No se deben crear documentos paralelos con la misma finalidad.
- Si ya existe un documento vivo, se actualiza.
- Si el cambio corresponde a una HU, se registra además en su archivo de cambio.
- La documentación técnica manual no debe duplicarse con referencias generadas automáticamente.
- Swagger/OpenAPI no sustituye la documentación técnica funcional del proyecto.

---

## 16. Política documental por historia de usuario

Cada historia de usuario completada debe dejar una salida ordenada.

## 16.1 Salida obligatoria por HU
Toda HU completada debe producir:

1. implementación del código requerido,
2. actualización de documentación viva impactada,
3. registro de cambio por HU,
4. pruebas correspondientes,
5. resumen de verificación.

## 16.2 Salida condicional por HU
Solo si aplica, la HU también debe actualizar:
- flujo de negocio,
- arquitectura,
- seguridad,
- performance,
- referencia de API,
- scripts SQL,
- ADRs,
- documentación de operación.

## 16.3 Regla de no duplicación
Una HU no debe crear nuevas carpetas o nuevos árboles documentales si ya existe una fuente canónica para ese contenido.

---

## 17. Estructura documental objetivo

A nivel conceptual, la documentación debe organizarse así:

```text
docs/
  business/
    current-system-business-flows.md

  analysis/
    current-state/
      architecture-analysis.md
      security-analysis.md
      performance-analysis.md
      testing-analysis.md
      remediation-plan.md
      validation-checklist.md
    changes/
      hu-index.md
      HU-XXXX.md

  technical/
    overview/
      project-foundation.md
    api/
      endpoint-reference.md
      openapi.yaml
    security/
    performance/
    operations/
    data/

  decisions/
    ADR-XXXX.md

  templates/
    hu-closeout-template.md
    adr-template.md
