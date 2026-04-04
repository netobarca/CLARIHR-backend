# Guía de selección de escenarios de pruebas unitarias

## 1. Propósito

Esta guía ayuda a decidir **qué escenarios unitarios deben probarse** para una historia de usuario o requerimiento backend en CLARIHR.

Debe usarse junto con:

- `.agents/skills/unit-test-dotnet-cqrs-user-story/SKILL.md`
- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`
- `docs/technical/overview/project-foundation.md`
- `/AGENTS.md`

Su objetivo es evitar:

- tests superficiales,
- cobertura inútil,
- omisiones en reglas críticas,
- falta de pruebas sobre tenant, permisos o errores,
- y duplicación innecesaria de escenarios.

---

## 2. Regla principal

Antes de escribir tests, responder internamente:

1. ¿Cuál es la unidad bajo prueba?
2. ¿Qué comportamiento crítico debe garantizar?
3. ¿Qué puede salir bien?
4. ¿Qué puede salir mal?
5. ¿Qué reglas de negocio, seguridad, tenant o permisos están involucradas?
6. ¿Qué errores deben ser explícitamente validados?
7. ¿Qué escenarios son críticos y cuáles serían redundantes?

### Regla de decisión
Los tests deben priorizar:

- comportamiento crítico,
- reglas de negocio,
- seguridad,
- tenant isolation,
- permisos,
- errores esperados,
- y consistencia del resultado.

No escribir tests solo para aumentar cantidad.

---

## 3. Escenarios mínimos por tipo de unidad

## 3.1 Domain

Cuando pruebas una entidad, value object o regla de dominio, como mínimo evaluar:

### Happy path
- creación válida,
- transición válida de estado,
- operación válida sobre la entidad.

### Errores / reglas
- creación inválida,
- transición inválida,
- regla de negocio incumplida,
- combinación de valores no permitida.

### Qué validar
- invariantes,
- consistencia interna,
- errores o resultados esperados,
- comportamiento del dominio sin infraestructura.

### Ejemplos
- no permite estado inválido,
- no permite fechas incoherentes,
- no permite crear un objeto con datos esenciales ausentes,
- permite operación cuando se cumplen todas las reglas.

---

## 3.2 CommandHandler

Cuando pruebas un CommandHandler, como mínimo evaluar:

### Happy path
- ejecuta correctamente el caso de uso,
- persiste o produce el efecto esperado,
- retorna el resultado correcto.

### Errores esperados
- recurso no encontrado,
- entrada inválida,
- conflicto de negocio,
- estado inválido,
- operación no permitida.

### Seguridad y permisos
- usuario autorizado puede ejecutar,
- usuario no autorizado falla correctamente.

### Tenant scope
- tenant correcto permite operación,
- tenant incorrecto falla o no encuentra.

### Qué validar
- `Result` success/failure,
- `ErrorCode`,
- side effects relevantes,
- comportamiento observable del caso de uso.

### Ejemplos
- crea organización cuando request es válido,
- falla cuando ya existe un código duplicado,
- falla cuando el recurso pertenece a otro tenant,
- falla cuando el usuario no tiene permiso.

---

## 3.3 QueryHandler

Cuando pruebas un QueryHandler, como mínimo evaluar:

### Happy path
- retorna DTO correcto,
- retorna listado esperado,
- retorna detalle esperado.

### Casos alternos
- recurso no encontrado,
- resultado vacío,
- filtros aplicados correctamente,
- paginación lógica correcta cuando aplica en Application.

### Seguridad y tenant
- solo retorna datos del tenant correcto,
- acceso denegado si el caso lo requiere.

### Qué validar
- contenido del DTO,
- comportamiento cuando no hay datos,
- resultado consistente,
- alcance tenant-scoped.

### Ejemplos
- retorna organización por id dentro del tenant,
- no retorna recurso de otro tenant,
- devuelve lista paginada correctamente,
- devuelve vacío cuando no hay coincidencias.

---

## 3.4 Validator

Cuando pruebas un validator, como mínimo evaluar:

### Caso válido
- request válido pasa sin errores.

### Casos inválidos
- campo requerido vacío,
- longitud inválida,
- formato inválido,
- rango inválido,
- combinación inválida de datos.

### Qué validar
- que falle por la regla correcta,
- que falle en el campo correcto,
- que pase cuando todo es válido.

### Ejemplos
- nombre vacío falla,
- email inválido falla,
- longitud máxima excedida falla,
- request mínimo válido pasa.

---

## 3.5 Authorization logic pura

Cuando pruebas lógica de autorización desacoplada, como mínimo evaluar:

### Permiso concedido
- rol correcto permite acción,
- permiso correcto permite acción.

### Permiso denegado
- rol incorrecto deniega,
- permiso faltante deniega,
- ownership no válido deniega,
- tenant diferente deniega.

### Qué validar
- decisión final correcta,
- diferencias por rol/permiso,
- restricciones por tenant o alcance.

### Ejemplos
- admin puede crear,
- usuario sin permiso no puede editar,
- usuario de otro tenant no puede acceder,
- manager solo puede operar sobre recursos asignados.

---

## 3.6 Mapping no trivial

Cuando pruebas mapping importante, como mínimo evaluar:

### Caso normal
- mapea todos los campos relevantes,
- transforma correctamente los valores esperados.

### Casos especiales
- valores opcionales,
- valores nulos,
- transformaciones condicionales,
- defaults calculados.

### Qué validar
- integridad del mapping,
- no pérdida de datos importantes,
- reglas condicionales correctas.

---

## 3.7 Pipeline behaviors

Cuando pruebas un behavior unit-testable, como mínimo evaluar:

### Caso positivo
- deja pasar la ejecución cuando se cumplen las condiciones.

### Caso negativo
- corta ejecución cuando falla validación,
- corta ejecución cuando falta autorización,
- ejecuta la lógica transversal esperada.

### Qué validar
- comportamiento del behavior,
- short-circuit cuando aplica,
- que no altere el flujo incorrectamente.

---

## 4. Matriz mínima de selección de escenarios

Usar esta matriz como guía rápida:

| Tipo de unidad | Happy path | Validación | Error esperado | Permisos | Tenant scope | Regla crítica |
|---|---|---|---|---|---|---|
| Domain | Sí | Sí | Sí | No usualmente | No usualmente | Sí |
| CommandHandler | Sí | Sí | Sí | Sí si aplica | Sí si aplica | Sí |
| QueryHandler | Sí | A veces | Sí | Sí si aplica | Sí si aplica | Sí |
| Validator | Sí | Sí | Sí | No | No | A veces |
| Authorization | Sí | No | Sí | Sí | Sí si aplica | Sí |
| Mapping | Sí | A veces | A veces | No | No | Sí si aplica |
| Behavior | Sí | Sí | Sí | Sí si aplica | Sí si aplica | Sí |

---

## 5. Cómo decidir si un escenario vale la pena

Crear el escenario si cumple al menos uno de estos criterios:

- valida una regla de negocio importante,
- valida un error esperado del flujo,
- protege contra un bug probable,
- protege tenant isolation,
- protege permisos,
- protege datos sensibles,
- protege un caso crítico para negocio,
- valida una transformación importante.

### No priorizar escenarios que:
- repiten exactamente la misma idea,
- validan solo setters/getters triviales,
- están demasiado acoplados a implementación interna,
- no agregan información relevante si fallan.

---

## 6. Reglas para errores esperados

Cuando el flujo usa `Result`, validar explícitamente:

- éxito o fallo,
- `ErrorCode`,
- tipo de error esperado,
- estado final correcto del resultado.

### Ejemplos de errores a cubrir
- NotFound
- Forbidden
- Unauthorized
- Validation
- Conflict
- BusinessRuleViolation

### Regla
No basta con validar que “falló”; debe validarse **cómo** falló.

---

## 7. Reglas para tenant scope

Debes agregar pruebas de tenant cuando:

- el caso toca datos scoped por tenant,
- el recurso pertenece a una empresa u organización,
- el flujo puede exponer datos de otro tenant,
- el query o command depende del tenant actual,
- el caso es sensible por acceso interempresa.

### Escenarios mínimos sugeridos
- tenant correcto accede o modifica,
- tenant incorrecto no accede o no encuentra,
- filtro por tenant se aplica correctamente.

### Regla
Si el riesgo principal es cross-tenant leakage, esos tests son obligatorios.

---

## 8. Reglas para permisos

Debes agregar pruebas de permisos cuando:

- el caso requiere autorización,
- la acción depende de rol,
- la acción depende de permiso por módulo o acción,
- la acción es administrativa o sensible,
- la acción cambia estado importante.

### Escenarios mínimos sugeridos
- usuario con permiso correcto puede ejecutar,
- usuario sin permiso falla correctamente,
- usuario con otro rol no autorizado falla.

### Regla
No asumir que permisos ya están cubiertos por otra capa si el caso de uso también los valida.

---

## 9. Reglas para datos sensibles

Si la HU toca:

- salarios,
- datos personales,
- permisos,
- usuarios,
- aprobaciones,
- auditoría,
- información organizacional sensible,

agregar escenarios que validen:

- acceso permitido solo a quien corresponde,
- acceso denegado cuando no corresponde,
- no exposición indebida del recurso,
- comportamiento correcto ante falta de autorización.

---

## 10. Cuándo un solo test no es suficiente

No basta un solo test cuando:

- hay varias reglas independientes,
- hay ramas de error importantes,
- hay diferencias por tenant,
- hay diferencias por permiso,
- hay estados diferentes del flujo,
- hay combinaciones críticas de datos.

### Regla
Cada test debe cubrir una intención principal, pero el conjunto debe cubrir los riesgos importantes del caso.

---

## 11. Cuándo simplificar

Reducir escenarios cuando:

- varios tests validan exactamente la misma regla,
- los errores son equivalentes y no agregan valor adicional,
- la lógica es trivial,
- el costo de mantenimiento supera el beneficio,
- los escenarios extra solo repiten setup sin aumentar protección real.

---

## 12. Secuencia recomendada para elegir escenarios

1. Identificar la unidad bajo prueba.
2. Identificar el happy path.
3. Identificar la regla crítica principal.
4. Identificar el error esperado más importante.
5. Evaluar tenant.
6. Evaluar permisos.
7. Evaluar validaciones.
8. Evaluar si hay transformación o side effect importante.
9. Elegir el conjunto mínimo que cubra los riesgos principales.

---

## 13. Ejemplos rápidos

## Caso A: CreateOrganizationCommandHandler
Escenarios mínimos recomendados:
- crea correctamente cuando request es válido,
- falla cuando nombre es inválido,
- falla cuando existe duplicado relevante,
- falla cuando usuario no tiene permiso,
- falla cuando tenant no corresponde.

## Caso B: GetOrganizationByIdQueryHandler
Escenarios mínimos recomendados:
- retorna DTO cuando existe y pertenece al tenant,
- retorna not found cuando no existe,
- no retorna recurso de otro tenant,
- falla si el acceso requiere permiso y el usuario no lo tiene.

## Caso C: UpdateOrganizationValidator
Escenarios mínimos recomendados:
- request válido pasa,
- nombre vacío falla,
- longitud máxima inválida falla,
- formato inválido en campo relevante falla.

---

## 14. Checklist rápida

Antes de terminar los tests, validar:

- [ ] Definí el happy path
- [ ] Definí al menos un error esperado importante
- [ ] Validé reglas críticas del negocio
- [ ] Validé tenant si aplica
- [ ] Validé permisos si aplica
- [ ] Validé formato/entrada si aplica
- [ ] No agregué escenarios redundantes
- [ ] Los tests cubren riesgos reales del caso

---

## 15. Criterio rector

Selecciona escenarios que protejan el comportamiento crítico del caso de uso, especialmente reglas de negocio, errores, permisos y tenant isolation, evitando tests redundantes o superficiales.