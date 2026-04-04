---
name: unit-test-dotnet-cqrs-user-story
description: Usa esta skill cuando una historia de usuario o cambio backend en .NET ya tiene lógica implementada o en progreso y necesitas crear o actualizar unit tests para Domain, Application, Validators, autorización pura, mapping no trivial o pipeline behaviors. No usar para integration tests, API tests, DB tests reales ni para cierre documental de la HU.
---

# Unit Test Dotnet CQRS User Story

## 1. Propósito

Esta skill existe para crear o actualizar **unit tests** de forma consistente en CLARIHR Backend para historias de usuario o cambios backend implementados con:

- .NET 10
- Clean Architecture
- CQRS
- PostgreSQL
- JWT + RBAC
- multi-tenant isolation

Su objetivo es asegurar que cada HU relevante deje pruebas unitarias:

- enfocadas en comportamiento,
- estables y determinísticas,
- alineadas con la arquitectura,
- conscientes de tenant, permisos y errores,
- y útiles para validar reglas críticas sin depender de infraestructura real.

---

## 2. Cuándo usar esta skill

Usar esta skill cuando la tarea principal sea crear, ajustar o completar **unit tests** para una HU o requerimiento backend.

### Casos típicos
- “Crea los unit tests para este CommandHandler”
- “Agrega tests para este QueryHandler”
- “Necesito pruebas para este validator”
- “Agrega tests para estas reglas de dominio”
- “Valida con tests tenant scope y permisos de este caso de uso”
- “Completa las pruebas unitarias de esta HU”

---

## 3. Cuándo NO usar esta skill

No usar esta skill para:

- integration tests con base de datos real,
- pruebas de API / controllers / middleware,
- pruebas E2E,
- pruebas de carga o performance,
- testing de servicios externos reales,
- cierre documental de la HU,
- implementación del feature en sí.

Si la tarea principal es implementar backend, usa primero:

- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`

Si la tarea principal es documentar el cierre de la HU, usa:

- `.agents/skills/close-user-story-docs/SKILL.md`

---

## 4. Fuentes de verdad obligatorias

Antes de escribir tests, revisar siempre en este orden:

1. `docs/technical/overview/project-foundation.md`
2. `/AGENTS.md`
3. `docs/AGENTS.md`
4. `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`
5. la HU o requerimiento fuente
6. el código real implementado
7. convenciones actuales del proyecto de tests

Si el código implementado contradice el diseño correcto del proyecto, los tests no deben reforzar un error de arquitectura o seguridad sin advertirlo.

---

## 5. Alcance unitario permitido

Esta skill sí cubre pruebas para:

- handlers de Application (Commands / Queries),
- reglas de Domain,
- invariantes de entidades y value objects,
- validators,
- lógica pura de autorización cuando esté en servicios testeables,
- mapping no trivial,
- Result / ErrorCodes,
- pipeline behaviors de Application cuando sean unit-testable.

Esta skill no cubre:

- base de datos real,
- HTTP pipeline,
- middleware,
- controllers,
- integración real con servicios externos,
- load/performance tests.

---

## 6. Principios no negociables

### 6.1 Probar comportamiento, no detalles internos
Los tests deben validar el resultado observable y las reglas relevantes, no la implementación interna accidental.

### 6.2 Determinismo
No depender de:
- tiempo real,
- red real,
- base de datos real,
- aleatoriedad no controlada.

### 6.3 Independencia de infraestructura
Los unit tests deben correr sin infraestructura real.

### 6.4 Conciencia multi-tenant
Cuando el caso de uso lo requiera, probar explícitamente:
- tenant scope,
- acceso permitido,
- acceso denegado,
- aislamiento de datos.

### 6.5 Validar errores correctamente
No basta con probar que “falla”; también debe probarse:
- tipo de fallo esperado,
- ErrorCode correcto,
- mensajes clave si aplica,
- comportamiento consistente del Result.

---

## 7. Stack y convenciones esperadas

Usar como base:

- **xUnit**
- **FluentAssertions**
- librería de mocks estandarizada por el proyecto

### Convenciones recomendadas
- Arrange / Act / Assert
- nombres claros y legibles
- una intención principal por test
- fixtures o builders solo cuando realmente simplifiquen

### Evitar
- tests demasiado acoplados al orden interno de llamadas,
- setups gigantes difíciles de leer,
- duplicación excesiva de inicialización si puede abstraerse razonablemente.

---

## 8. Dependencias abstractas esperadas

Cuando el caso de uso lo requiera, los tests deben apoyarse en abstracciones como:

- `IClock`
- `ICurrentUser`
- `ITenantContext`

o equivalentes reales del proyecto.

### Regla
Si el caso de uso depende del usuario actual, tenant actual o tiempo actual, no usar fuentes reales dentro del unit test.

---

## 9. Entradas mínimas esperadas

Para usar esta skill, identifica o infiere:

- HU o requerimiento relacionado,
- caso de uso o componente a probar,
- capa afectada,
- reglas de negocio clave,
- escenarios felices,
- escenarios de error,
- validaciones esperadas,
- permisos esperados,
- comportamiento tenant-scoped,
- dependencias que deben mockearse.

---

## 10. Flujo de trabajo

## Paso 1. Leer el requerimiento y el código
Entender:

- qué hace el caso de uso,
- qué reglas valida,
- qué dependencias externas usa,
- qué salidas correctas e incorrectas puede producir,
- qué riesgos de seguridad, permisos y tenant existen.

## Paso 2. Determinar qué tipo de prueba corresponde
Clasificar si debes probar:

- Domain
- CommandHandler
- QueryHandler
- Validator
- autorización pura
- mapping
- pipeline behavior

## Paso 3. Diseñar escenarios mínimos obligatorios
Definir qué escenarios sí o sí deben existir.

Como mínimo considerar:
- happy path,
- validación,
- error esperado,
- permiso / autorización,
- tenant scope,
- regla crítica del negocio.

## Paso 4. Preparar dependencias mockeadas
Mockear solo dependencias externas o colaboraciones necesarias.

No mockear el comportamiento que realmente quieres validar dentro de la unidad bajo prueba.

## Paso 5. Implementar tests claros
Escribir tests pequeños, legibles y con una intención clara.

## Paso 6. Verificar cobertura útil
Confirmar que los tests realmente validan el riesgo funcional o técnico del caso y no solo “aumentan número”.

---

## 11. Qué probar según el tipo de componente

## 11.1 Domain
Probar:

- invariantes,
- reglas de negocio puras,
- creación válida e inválida,
- cambios de estado válidos e inválidos,
- comportamiento de value objects,
- errores esperados si el dominio usa Result o excepciones controladas.

### No probar aquí
- persistencia,
- EF Core,
- HTTP,
- detalles de infraestructura.

---

## 11.2 CommandHandler
Probar:

- éxito del caso de uso,
- validaciones o precondiciones relevantes,
- errores esperados,
- permisos,
- tenant scope,
- auditoría o side effects observables del caso si están dentro del alcance unitario,
- Result success/failure y ErrorCode.

### Casos mínimos sugeridos
- crea o actualiza correctamente cuando todo es válido,
- falla cuando la entrada es inválida,
- falla cuando el recurso no existe,
- falla cuando el tenant no corresponde,
- falla cuando el usuario no tiene permiso,
- falla cuando una regla crítica de negocio se incumple.

---

## 11.3 QueryHandler
Probar:

- retorno correcto del DTO,
- comportamiento cuando el recurso existe,
- comportamiento cuando no existe,
- filtros o paginación si la lógica de aplicación participa,
- tenant scoping,
- autorizaciones si aplican,
- errores esperados si aplica Result.

### Regla
No intentes convertir unit tests de QueryHandler en pruebas de EF real.

---

## 11.4 Validators
Probar:

- required fields,
- longitudes,
- formatos,
- rangos,
- reglas declarativas relevantes,
- combinaciones importantes de campos.

### Casos mínimos sugeridos
- request válido pasa,
- request inválido falla por cada regla crítica,
- mensajes o códigos esperados cuando el proyecto los estandariza.

---

## 11.5 Authorization logic pura
Si existe lógica de permisos desacoplada y testeable, probar:

- permiso concedido,
- permiso denegado,
- diferencias por rol,
- diferencias por acción,
- diferencias por ownership o tenant si aplica.

---

## 11.6 Mapping no trivial
Si hay mapping importante y no trivial, probar:

- que los campos relevantes se transforman correctamente,
- que no se pierden datos importantes,
- que reglas condicionales del mapping se respetan.

---

## 11.7 Pipeline behaviors
Si existen behaviors unit-testable en Application, probar:

- validación,
- autorización,
- timing / performance behavior solo como comportamiento lógico, no como benchmark real,
- short-circuit cuando corresponda.

---

## 12. Reglas para mocks

Mockear solo:
- repositorios,
- servicios externos,
- contexto de usuario,
- tenant context,
- clock,
- colaboradores técnicos externos a la unidad.

### Evitar
- mockear excesivamente entidades o lógica que puedes construir de forma real,
- mockear tanto que el test ya no valide nada importante,
- verificar demasiadas interacciones internas salvo cuando sea necesario.

### Preferir
- asserts sobre resultados,
- asserts sobre efectos importantes,
- setups mínimos.

---

## 13. Reglas para Result y errores

Cuando el proyecto use `Result<T>` o equivalente, los tests deben validar explícitamente:

- éxito o fracaso,
- ErrorCode,
- estado esperado del resultado,
- datos principales retornados en éxito,
- campos clave del error cuando aplique.

### No hacer
- solo revisar que “no sea null”,
- solo revisar que “lanzó algo” si el flujo usa Result,
- ignorar códigos de error del estándar del proyecto.

---

## 14. Reglas para tenant y permisos

Si la HU toca seguridad, usuarios, permisos, salarios, aprobaciones o datos sensibles, incluir pruebas explícitas de:

- tenant correcto permite acceso,
- tenant incorrecto deniega o no encuentra,
- usuario autorizado puede operar,
- usuario no autorizado falla correctamente,
- recursos no visibles fuera del tenant.

### Regla
No dejes tenant isolation implícito cuando sea parte del riesgo principal del caso de uso.

---

## 15. Selección mínima de escenarios por HU

Como base, toda HU con lógica relevante debería dejar pruebas para:

- [ ] happy path
- [ ] validaciones
- [ ] error esperado principal
- [ ] permisos
- [ ] tenant scope
- [ ] regla crítica de negocio

Si alguno no aplica, documentarlo mentalmente y no forzarlo artificialmente.

---

## 16. Estructura sugerida de nombres

Usar nombres de tests claros y orientados a comportamiento.

### Ejemplos
- `Handle_ShouldCreateOrganization_WhenRequestIsValid`
- `Handle_ShouldReturnNotFound_WhenOrganizationDoesNotExist`
- `Handle_ShouldReturnForbidden_WhenUserLacksPermission`
- `Validate_ShouldFail_WhenNameIsEmpty`

### Regla
El nombre debe explicar:
- qué se está probando,
- en qué condición,
- cuál es el resultado esperado.

---

## 17. Qué evitar siempre

Evitar siempre:

- tests que dependen de DB real,
- tests que validan detalles internos irrelevantes,
- tests frágiles por exceso de mocks,
- tests con Arrange gigante e ilegible,
- tests sin assertions significativas,
- tests que ignoran ErrorCodes,
- tests que no contemplan tenant o permisos cuando sí aplican,
- duplicar escenarios casi idénticos sin necesidad.

---

## 18. Verificación mínima

Antes de cerrar el trabajo de testing, verificar:

```bash
dotnet test