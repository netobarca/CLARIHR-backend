# Auditoría Técnica — `PersonnelFilesController`

## 1. Alcance

Esta auditoría cubre el controlador [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs), sus handlers asociados en Application y las consultas principales en Infrastructure que soportan:

- creación de expedientes,
- búsqueda/listado,
- obtención del shell del expediente,
- activación,
- inactivación.

La revisión se hizo contra:

- `docs/technical/overview/project-foundation.md`
- `AGENTS.md`
- principios de Clean Architecture, CQRS, tenant isolation, seguridad, performance y trazabilidad.

## 2. Resumen Ejecutivo

El controlador está bien encaminado en separación de capas: permanece delgado, delega a CQRS, usa `ProblemDetails` y no accede directamente a infraestructura. Sin embargo, el flujo completo presenta varios riesgos relevantes de escalabilidad, seguridad de datos y cohesión contractual.

Veredicto general:

- Estado arquitectónico base: **aceptable**
- Estado de escalabilidad/performance: **mejorable con cambios concretos**
- Estado de seguridad/privacidad: **con hallazgos importantes**
- Estado de mantenibilidad del contrato: **con deuda de diseño visible**

## 3. Hallazgos

### Hallazgo 1 — Exposición innecesaria de `BirthDate` y `ConcurrencyToken` en el endpoint de listado

**Severidad:** Importante  
**Categoría:** Seguridad, privacidad, diseño de contrato

**Evidencia**

- El endpoint de búsqueda/listado está en [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs:70).
- El contrato `PersonnelFileListItemResponse` expone `BirthDate` y `ConcurrencyToken` en [PersonnelFileAdministration.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs:27).
- La consulta del listado efectivamente los proyecta y devuelve en [PersonnelFileRepository.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/PersonnelFiles/PersonnelFileRepository.cs:119).

**Riesgo**

- `BirthDate` es PII sensible y normalmente no debería exponerse en una tabla/listado general si basta con `Age`.
- `ConcurrencyToken` se está filtrando a consumidores de lectura, aunque conceptualmente es un token de control de escrituras.
- Esto aumenta superficie de exposición innecesaria y viola el principio de “no exponer datos sensibles innecesarios”.

**Solución recomendada**

- Crear una versión más mínima de `PersonnelFileListItemResponse`.
- Remover `BirthDate` del listado y conservar `Age` si el UX lo necesita.
- Remover `ConcurrencyToken` del listado; entregarlo solo en `GET /api/v1/personnel-files/{publicId}` o en los endpoints de detalle/edición donde realmente se usa para concurrencia.
- Si el frontend necesita `BirthDate` por un caso puntual, moverlo a una variante de detalle o a un endpoint especializado.

---

### Hallazgo 2 — `activate` e `inactivate` devuelven un agregado demasiado pesado para una mutación simple

**Severidad:** Importante  
**Categoría:** Performance, escalabilidad, seguridad de datos

**Evidencia**

- Los endpoints retornan `PersonnelFileResponse` completo en [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs:130) y [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs:146).
- Los handlers cargan el expediente completo antes y después de la mutación en [PersonnelFileAdministration.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs:4712) y [PersonnelFileAdministration.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs:4785).
- `GetResponseByIdAsync` incluye prácticamente todo el agregado y múltiples colecciones en [PersonnelFileRepository.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/PersonnelFiles/PersonnelFileRepository.cs:198).

**Riesgo**

- Activar/inactivar un expediente termina costando varias lecturas pesadas y serialización de gran volumen.
- El costo crece con el tamaño del expediente: educaciones, idiomas, documentos, observaciones, referencias, etc.
- Se expone más información de la necesaria para una operación cuyo resultado natural podría ser un shell liviano o una respuesta de estado.

**Solución recomendada**

- Cambiar la respuesta de `PATCH /activate` y `PATCH /inactivate` a `PersonnelFileShellResponse` o a un contrato específico de lifecycle.
- Reemplazar `GetResponseByIdAsync` por una lectura mínima para la respuesta.
- Para auditoría, registrar snapshots específicos del cambio (`isActive`, `lifecycleStatus`, `concurrencyToken`, timestamps) en lugar de reconstruir el agregado completo cuando no sea indispensable.

---

### Hallazgo 3 — El `POST` de creación hidrata el agregado completo aunque solo devuelve un shell

**Severidad:** Importante  
**Categoría:** Performance, escalabilidad

**Evidencia**

- El controlador devuelve solo `PersonnelFileShellResponse` en [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs:21).
- El handler de create, después de guardar, reconstruye `PersonnelFileResponse` completo y luego lo reduce manualmente a shell en [PersonnelFileAdministration.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Application/Features/PersonnelFiles/PersonnelFileAdministration.cs:3255).
- Esa reconstrucción usa la misma consulta pesada de [PersonnelFileRepository.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/PersonnelFiles/PersonnelFileRepository.cs:198).

**Riesgo**

- Se incurre en un costo de lectura y mapeo mucho mayor al necesario en el path de creación.
- A medida que el agregado crezca o gane más relaciones, la latencia del `POST` crecerá artificialmente.
- El write path queda más caro y más sensible a regresiones.

**Solución recomendada**

- Crear un `GetShellByIdAsync` reutilizable para create y devolver directamente ese contrato.
- Si auditoría necesita un snapshot “after”, construir uno específico para creación, no el `PersonnelFileResponse` completo.
- Evitar mapear de `PersonnelFileResponse` a `PersonnelFileShellResponse` cuando ya existe un query liviano para shell.

---

### Hallazgo 4 — No se observó rate limiting específico para endpoints sensibles de `PersonnelFiles`

**Severidad:** Recomendado  
**Categoría:** Seguridad operativa, resiliencia

**Evidencia**

- La configuración de rate limiter visible en [Program.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Program.cs:94) define una política específica para `auth-password-reset-request`.
- No se observan políticas explícitas aplicadas a create/search/activate/inactivate en [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs:21).

**Riesgo**

- El listado puede ser abusado para scraping o enumeración masiva dentro de un tenant.
- El create puede ser usado para spam de registros o consumo innecesario de recursos.
- Activate/Inactivate son mutaciones sensibles sobre datos de RRHH y carecen de una defensa operativa visible a nivel de endpoint.

**Solución recomendada**

- Definir políticas específicas de rate limiting para:
  - `POST /companies/{companyPublicId}/personnel-files`
  - `GET /companies/{companyPublicId}/personnel-files`
  - `PATCH /personnel-files/{publicId}/activate`
  - `PATCH /personnel-files/{publicId}/inactivate`
- Particionar por tenant y usuario autenticado, no solo por IP.
- Instrumentar métricas de rechazo `429` para detectar abuso o consumo anómalo.

---

### Hallazgo 5 — Cohesión contractual fragmentada entre `PersonnelFilesController` y `PersonnelFileProfileController`

**Severidad:** Recomendado  
**Categoría:** Arquitectura, mantenibilidad, DX

**Evidencia**

- `PersonnelFilesController` expone creación y shell del recurso raíz en [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs:21) y [PersonnelFilesController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFilesController.cs:119).
- La sección base editable (`personal-info`) vive en otro controller en [PersonnelFileProfileController.cs](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFileProfileController.cs:18).

**Riesgo**

- El consumidor percibe que “la base del expediente” está partida entre dos superficies distintas.
- Esto aumenta el costo de aprendizaje, la probabilidad de drift documental y la complejidad de versionar contratos.
- No es una violación de Clean Architecture, pero sí una deuda de cohesión del módulo.

**Solución recomendada**

- Definir una regla de diseño explícita y estable.
- Opción A: mantener `PersonnelFilesController` para recurso raíz y `Profile` para subsecciones, pero documentarlo como convención oficial del módulo.
- Opción B: mover `personal-info` al controller raíz del expediente para alinear create/read/update de la base del aggregate.
- Si no se hará refactor inmediato, al menos consolidar documentación viva y OpenAPI para que el flujo root/profile quede inequívoco.

## 4. Fortalezas Detectadas

- El controlador está delgado y no contiene lógica de negocio.
- La delegación a `ICommandDispatcher` e `IQueryDispatcher` respeta CQRS.
- La autorización sensible no está embebida en API sino en handlers/servicios de autorización.
- El listado usa paginación y validación de `pageSize`.
- `GetShellByIdAsync` ya existe como query liviano y demuestra una buena dirección para contratos bootstrap.
- El módulo ya usa `ProblemDetails`, `Result` y auditoría explícita.

## 5. Recomendaciones Prioritarias

1. Reducir el contrato del listado y eliminar `BirthDate` y `ConcurrencyToken` del search response.
2. Rediseñar `activate` e `inactivate` para retornar shell o respuesta mínima de lifecycle.
3. Eliminar la hidratación completa del agregado en el create path cuando solo se devuelve shell.
4. Agregar rate limiting específico por tenant/usuario para create, list y lifecycle mutations.
5. Decidir y documentar una convención estable para la frontera entre `PersonnelFilesController` y `PersonnelFileProfileController`.

## 6. Conclusión

`PersonnelFilesController` cumple razonablemente bien la disciplina de capa API, pero el módulo todavía tiene deuda relevante en diseño de contratos y costo de lectura asociado a mutaciones. El principal problema no es lógica de negocio en controller, sino el costo y sobreexposición que arrastran sus endpoints a través de las respuestas y consultas aguas abajo.

La prioridad técnica debería enfocarse primero en:

- minimizar payload y PII en el listado,
- adelgazar create/activate/inactivate,
- y endurecer controles operativos para un módulo con datos sensibles de RRHH.
