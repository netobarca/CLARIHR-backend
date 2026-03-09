# HU-PF-EMP-001 - Modulo Expediente de Empleados (Administrativo Integral y Ciclo de Vida Laboral)

## 0) Validacion del estado actual
Resultado del analisis funcional y tecnico sobre el backend actual (`PersonnelFiles`, HU-023 a HU-027):

- Cobertura existente confirmada:
  - `recordType` soporta `Candidate|Employee`.
  - Secciones personales/curriculares ya operativas (`personal-info`, `educations`, `languages`, etc.).
  - Soporte de documentos, observaciones, impresion, export, consulta dinamica y analitica.
  - Campo configurable existente via `custom_data` + `personnel_custom_field_definitions`.
  - Cuentas bancarias existentes en `bank-accounts`.
- Cobertura faltante o parcial para este nuevo modulo:

| Opcion solicitada | Estado actual | Diagnostico |
|---|---|---|
| Personal administrativo del empleado | No cubierto | No existe perfil laboral-administrativo con codigo de empleado, estado laboral, tipo de contrato, jornada, planilla, plaza, puesto, unidad, centro de trabajo, centro de costo, antiguedad y configuracion de vacaciones. |
| Contrataciones (historial) | No cubierto | No existe historial estructurado de contratacion/recontratacion con vigencias. |
| Posicion jerarquica (jefe/subalternos) | Parcial | Existe `orgUnitId`, pero no una vista de jerarquia laboral efectiva por puesto/plaza con jefe inmediato y personal a cargo. |
| Salario (estructura) | No cubierto | No existe estructura salarial por items, vigencias, moneda y periodicidad de pago por empleado. |
| Beneficios adicionales | No cubierto | No existe seccion de beneficios asignados por catalogo (aguinaldo, vacaciones, seguro, etc.). |
| Formas de pago | Parcial | Existen cuentas bancarias, pero no configuracion de forma de pago ni seleccion de cuenta vigente para el pago. |
| Sustitucion para autorizaciones | No cubierto | No existe entidad/endpoint para sustituciones temporales de autorizacion. |
| Informacion configurable | Parcial alto | Existe base tecnica de custom fields, falta estandarizar uso en contexto laboral del empleado. |
| Historico de acciones de personal | Parcial bajo | Solo hay observaciones/auditoria general; no hay bitacora funcional de acciones de personal con filtros y export. |
| Historico de transacciones en planilla | No cubierto | No existe seccion para ingresos/descuentos de planilla por empleado. |
| Equipo o acceso | No cubierto | No existe modelado de activos, licencias y accesos asignados por empleado. |
| Seguros y beneficiarios | No cubierto | No existe modelado de afiliaciones/polizas y beneficiarios por empleado. |
| Reclamos de seguro medico | No cubierto | No existe consulta estructurada de reclamos medicos asociados al empleado/beneficiarios. |
| Evaluaciones | No cubierto (integracion pendiente) | No hay modulo de desempeno integrado al expediente. |
| Competencias del puesto | Parcial (infraestructura disponible) | Existe `CompetencyFramework` por perfil de puesto, falta aterrizarlo al empleado y su brecha real evaluada. |
| Concurso de seleccion | No cubierto (integracion pendiente) | No existe modulo de Reclutamiento/Seleccion integrado al expediente. |
| Competencias curriculares | No cubierto | Curriculum actual no contempla esta seccion orientada a requisitos curriculares del puesto. |

Conclusion:
- El requerimiento representa una expansion mayor de `PersonnelFiles` hacia el ciclo de vida laboral del empleado.
- Se recomienda implementacion por fases para evitar alto riesgo de entrega.

---

## 1) Historia de usuario
Como **Administrador de RRHH / Analista de Gestion de Personal**,
quiero **administrar el expediente laboral completo del empleado una vez contratado**,
para **controlar su ciclo administrativo, salarial, contractual, beneficios, seguros, evaluaciones y trazabilidad de acciones de personal desde una sola vista**.

---

## 2) Objetivo funcional
- Convertir y enriquecer el expediente cuando un aspirante pasa a empleado.
- Incorporar las 17 opciones administrativas solicitadas con datos estructurados y auditables.
- Garantizar integraciones con estructura organizativa, competencias, planilla, reclutamiento y desarrollo (cuando aplique).
- Habilitar consulta historica con filtros y export para acciones de personal y transacciones de planilla.

---

## 3) Alcance
### Incluye
- Bloque laboral-administrativo del expediente de empleado.
- Secciones nuevas para contratos, posicion, salario, beneficios, formas de pago, sustituciones, equipo/accesos, seguros, reclamos y competencias curriculares.
- Historicos consultables/exportables:
  - acciones de personal
  - transacciones de planilla.
- Integraciones de lectura con modulos relacionados (competencias, evaluaciones, concursos), con fallback cuando no existan datos.
- Seguridad tenant-scoped, concurrencia y auditoria por cada operacion.

### Fuera de alcance (v1)
- Motor de planilla (calculo de pagos, deducciones y cierre de nomina).
- Resolucion de reclamos medicos con flujos de aseguradora.
- Motor BPM de aprobaciones complejas (se incluye solo sustitucion configurada).
- BI externo o dashboards avanzados.

---

## 4) Desglose funcional requerido (To-Be)
### 4.1 Personal administrativo del empleado
Registrar:
- codigo de empleado
- estado laboral / activo
- tipo de contrato
- fecha de ingreso
- categoria y motivo de retiro
- observaciones y fecha de retiro
- jornada
- planilla
- plaza, puesto, unidad
- centro de trabajo y centro de costo
- inicio y fin de contrato
- antiguedad (derivable)
- configuracion para vacaciones.

### 4.2 Contrataciones
- Historial de contrataciones/recontrataciones.
- Campos: tipo de contratacion, fecha de contrato, fecha de fin de contrato, plaza.

### 4.3 Posicion
- Vista de posicion jerarquica actual:
  - jefe inmediato
  - personal a cargo (subalternos).

### 4.4 Salario
- Estructura salarial por item.
- Campos por item: tipo de ingreso, rubro salarial, moneda, periodo de pago, valor, fecha inicio, fecha fin, estado.

### 4.5 Beneficios adicionales
- Asignacion de beneficios desde catalogo (aguinaldo, vacaciones, seguro, etc.).

### 4.6 Formas de pago
- Configuracion de forma de pago.
- Seleccion de cuenta bancaria vigente registrada en `bank-accounts`.

### 4.7 Sustitucion para autorizaciones
- Definir sustituto temporal del empleado para autorizaciones.
- Campos: empleado sustituto, puesto, fecha inicio, fecha fin.

### 4.8 Informacion configurable
- Reusar `custom fields` existentes para datos personalizados del expediente laboral.

### 4.9 Historico de Acciones de Personal
- Consulta de eventos de personal:
  - incrementos salariales
  - cambios de contrato
  - cambios de centro de trabajo
  - ascensos
  - cambios de puesto/unidad
  - ausencias e incapacidades
  - tiempo compensatorio
  - sustituciones temporales
  - reconocimientos, amonestaciones, retiro, etc.
- Debe incluir filtros y export a Excel.

### 4.10 Historico de transacciones en planilla
- Consulta de ingresos/descuentos aplicados en planilla.
- Debe incluir filtros y export a Excel.

### 4.11 Equipo o acceso
- Asignacion de equipo, licencias y accesos.
- Campos: equipo/acceso, fecha alta, fecha baja, observacion, fecha entrega, estado de entrega.

### 4.12 Seguros y beneficiarios
- Registro de seguros afiliados y beneficiarios.
- Seguro: nombre, cuota empleado, cuota patronal, rango, poliza, valor asegurado, activo.
- Beneficiario: nombre, documento, fecha nacimiento, parentesco.

### 4.13 Reclamos de seguro medico
- Consulta de reclamos por empleado/beneficiarios.
- Campos: seguro, numero de cuenta, tipo de reclamo, diagnostico, monto reclamo, moneda, monto pagado, tiempo respuesta, observaciones.

### 4.14 Evaluaciones
- Registro/consulta de evaluaciones de desempeno:
  - evaluador, fecha, nota obtenida, nota cualitativa, comentario.
- Integracion para navegar al detalle de evaluacion del modulo de Desarrollo.

### 4.15 Competencias del puesto
- Consulta de competencias organizacionales y tecnicas del puesto del empleado:
  - competencia
  - conductas deseadas
  - nota esperada
  - nota alcanzada
  - brecha
  - fecha de evaluacion.

### 4.16 Concurso de seleccion
- Historial de concursos de seleccion en que participo el empleado, con fechas y resultados.

### 4.17 Competencias curriculares
- Registro de competencias curriculares asociadas a requisitos:
  - tipo de requisito
  - nombre del requisito
  - dominio de competencia
  - tiempo de experiencia
  - metrica.

---

## 5) Reglas de negocio
### RN-01 Conversion de aspirante a empleado
- Cuando `recordType` pase de `Candidate` a `Employee`, debe existir perfil laboral minimo obligatorio.

### RN-02 Multiples plazas y recontrataciones
- Se permiten multiples plazas por empleado, con reglas de no solapamiento por plaza/periodo.
- Debe existir una asignacion principal activa.

### RN-03 Integridad de fechas
- Toda vigencia debe cumplir `fechaFin >= fechaInicio`.
- Fechas de retiro deben ser consistentes con estado laboral.

### RN-04 Codigo de empleado
- Debe ser unico por tenant.

### RN-05 Estructura salarial
- Items salariales no deben tener vigencias inconsistentes para mismo rubro y periodo.
- Montos no negativos donde aplique.

### RN-06 Forma de pago
- Si el metodo requiere cuenta bancaria, la cuenta debe existir y estar activa para el expediente.

### RN-07 Sustituciones
- No se permite auto-sustitucion.
- No se permiten sustituciones activas solapadas para el mismo tipo de autorizacion.

### RN-08 Seguros y beneficiarios
- Catalogos de seguro, rango y parentesco deben existir y estar activos.
- Beneficiario debe tener identificacion valida.

### RN-09 Historicos
- Acciones de personal y transacciones de planilla deben ser filtrables por rango de fechas, tipo y estado.
- Export debe respetar exactamente filtros y orden.

### RN-10 Seguridad y tenant
- `401` no autenticado.
- `403` sin permisos o tenant cruzado.
- `404` recurso inexistente dentro del tenant.

### RN-11 Concurrencia
- Toda operacion de escritura por seccion usa `concurrencyToken`.
- Conflicto devuelve `409 CONCURRENCY_CONFLICT`.

### RN-12 Auditoria
- Es obligatoria en toda operacion de alta/actualizacion/inactivacion/export.

---

## 6) Requerimientos tecnicos
### RT-01 Arquitectura
- Mantener patron actual:
  - Clean Architecture
  - CQRS
  - FluentValidation
  - tenant-scoped authorization
  - ProblemDetails
  - auditoria centralizada.

### RT-02 Persistencia sugerida (nuevas tablas)
- `personnel_file_employee_profiles`
- `personnel_file_employment_assignments`
- `personnel_file_contract_histories`
- `personnel_file_salary_items`
- `personnel_file_additional_benefits`
- `personnel_file_payment_methods`
- `personnel_file_authorization_substitutions`
- `personnel_file_personnel_actions`
- `personnel_file_payroll_transactions`
- `personnel_file_assets_accesses`
- `personnel_file_insurances`
- `personnel_file_insurance_beneficiaries`
- `personnel_file_medical_claims`
- `personnel_file_performance_evaluations`
- `personnel_file_position_competency_results`
- `personnel_file_selection_contests`
- `personnel_file_curricular_competencies`.

### RT-03 Integraciones
- Estructura organizativa:
  - unidad, plaza, puesto, centro de costo, centro de trabajo.
- Competencias:
  - `CompetencyFramework` y/o matriz por perfil de puesto.
- Evaluaciones:
  - modulo Desarrollo (si existe; en v1 puede ser integracion de solo lectura con `null-safe`).
- Reclutamiento/Seleccion:
  - historial de concursos (si no existe modulo, habilitar tabla interna de staging).
- Planilla:
  - transacciones de ingresos/descuentos por empleado.

### RT-04 Hardening y performance
- Indices por `tenant_id`, `personnel_file_id`, fechas de vigencia, estado y campos de filtro frecuente.
- Paginacion obligatoria en listados historicos.
- Export streaming o lotes para evitar alto consumo de memoria.

### RT-05 Observabilidad
- Audit trail y logs con:
  - actor
  - tenant
  - endpoint
  - filtros
  - rowCount (export/listado)
  - correlation id.

---

## 7) Catalogos funcionales requeridos
Categorias sugeridas:
- `EmployeeStatus`
- `EmploymentContractType`
- `EmploymentRetirementCategory`
- `EmploymentRetirementReason`
- `EmploymentWorkday`
- `EmploymentPayrollType`
- `EmploymentIncomeType`
- `EmploymentSalaryRubric`
- `EmploymentPayPeriod`
- `EmploymentBenefitType`
- `EmploymentPaymentMethod`
- `EmploymentAuthorizationSubstitutionType`
- `EmploymentAssetType`
- `EmploymentAccessLevel`
- `EmploymentInsurance`
- `EmploymentInsuranceRange`
- `EmploymentKinship`
- `EmploymentMedicalClaimType`
- `EmploymentEvaluationQualitativeScale`
- `EmploymentCurricularRequirementType`
- `EmploymentCurricularMetricType`.

---

## 8) API y contratos (propuesta v1)
### Endpoints nuevos por seccion
- `PUT /api/v1/personnel-files/{id}/employee-profile`
- `PUT /api/v1/personnel-files/{id}/employment-assignments`
- `PUT /api/v1/personnel-files/{id}/contract-history`
- `GET /api/v1/personnel-files/{id}/position-hierarchy`
- `PUT /api/v1/personnel-files/{id}/salary-items`
- `PUT /api/v1/personnel-files/{id}/additional-benefits`
- `PUT /api/v1/personnel-files/{id}/payment-methods`
- `PUT /api/v1/personnel-files/{id}/authorization-substitutions`
- `GET /api/v1/personnel-files/{id}/personnel-actions`
- `GET /api/v1/personnel-files/{id}/personnel-actions/export?format=xlsx|csv`
- `GET /api/v1/personnel-files/{id}/payroll-transactions`
- `GET /api/v1/personnel-files/{id}/payroll-transactions/export?format=xlsx|csv`
- `PUT /api/v1/personnel-files/{id}/assets-accesses`
- `PUT /api/v1/personnel-files/{id}/insurances`
- `GET /api/v1/personnel-files/{id}/medical-claims`
- `GET /api/v1/personnel-files/{id}/evaluations`
- `GET /api/v1/personnel-files/{id}/position-competencies`
- `GET /api/v1/personnel-files/{id}/selection-contests`
- `PUT /api/v1/personnel-files/{id}/curricular-competencies`.

### Contratos base por endpoints de escritura
- Request:
  - `items[]` (o payload simple por seccion)
  - `concurrencyToken`.
- Response:
  - `PersonnelFileResponse` expandido con nuevas secciones.

---

## 9) Criterios de aceptacion (Gherkin)
### CA-01 Conversion a empleado con datos minimos
Given un expediente en `recordType=Candidate`  
When actualizo a `recordType=Employee` sin `employee-profile` minimo  
Then recibo `422` por campos laborales requeridos.

### CA-02 Registro de perfil administrativo
Given un expediente de empleado valido  
When envio `PUT /employee-profile` con datos validos  
Then recibo `200` y el perfil laboral queda persistido.

### CA-03 Historial de contrataciones
Given un expediente de empleado  
When envio `PUT /contract-history` con periodos validos no solapados  
Then recibo `200` y la historia de contrataciones queda actualizada.

### CA-04 Validacion de solapamiento contractual
Given periodos de contratacion solapados para la misma plaza  
When envio `PUT /contract-history`  
Then recibo `422` con detalle de conflicto de vigencias.

### CA-05 Estructura salarial
Given un expediente de empleado  
When envio `PUT /salary-items` con rubros y vigencias validas  
Then recibo `200` y los items salariales quedan registrados.

### CA-06 Forma de pago con cuenta bancaria
Given un expediente con cuentas bancarias registradas  
When envio `PUT /payment-methods` con una cuenta inexistente o inactiva  
Then recibo `422`.

### CA-07 Sustitucion para autorizaciones
Given un expediente de empleado  
When envio `PUT /authorization-substitutions` con sustituto igual al titular  
Then recibo `422`.

### CA-08 Historico de acciones de personal filtrable/exportable
Given acciones de personal registradas para el empleado  
When consulto `GET /personnel-actions` con filtros por fecha/tipo/estado  
Then recibo resultados paginados y consistentes con los filtros.

Given los mismos filtros aplicados  
When exporto `GET /personnel-actions/export?format=xlsx`  
Then el archivo refleja exactamente el mismo conjunto filtrado.

### CA-09 Historico de transacciones de planilla
Given transacciones de planilla asociadas al empleado  
When consulto y exporto con filtros  
Then obtengo datos consistentes y auditados.

### CA-10 Seguros y beneficiarios
Given un expediente de empleado  
When envio `PUT /insurances` con catalogos activos y datos validos  
Then recibo `200` y seguros/beneficiarios quedan persistidos.

### CA-11 Integracion de evaluaciones
Given evaluaciones existentes en modulo Desarrollo  
When consulto `GET /evaluations`  
Then recibo listado con evaluador, fecha, notas y acceso al detalle.

### CA-12 Competencias del puesto
Given un empleado asignado a puesto con matriz de competencias  
When consulto `GET /position-competencies`  
Then recibo competencia, nota esperada, nota alcanzada y brecha.

### CA-13 Concurso de seleccion
Given historial de concursos del empleado en Reclutamiento  
When consulto `GET /selection-contests`  
Then recibo fechas, resultados y referencia al concurso.

### CA-14 Competencias curriculares
Given un expediente de empleado  
When envio `PUT /curricular-competencies` con tipos de requisito validos  
Then recibo `200` y la seccion queda persistida.

### CA-15 Seguridad y concurrencia
Given usuario sin autenticacion  
When invoca cualquier endpoint nuevo  
Then recibe `401`.

Given usuario autenticado sin permisos o tenant cruzado  
When invoca endpoints nuevos  
Then recibe `403`.

Given `concurrencyToken` desactualizado  
When ejecuta `PUT/PATCH` de cualquier seccion  
Then recibe `409`.

---

## 10) Plan de pruebas
### Unit tests
- Validaciones de fechas, vigencias y solapamientos.
- Validaciones de monto, estado, catalogos y reglas de negocio por seccion.

### Application tests
- Seguridad por permisos.
- Tenant mismatch.
- Concurrencia stale token.
- Errores semanticos `422`.

### Integration tests
- Flujo de conversion `Candidate -> Employee`.
- CRUD por seccion nueva.
- Filtros y export de historicos.
- Integraciones con competencias/evaluaciones/concursos (si disponibles).

### Contract tests
- Backward compatibility de `PersonnelFileResponse`.
- Endpoints existentes no deben romperse.

### Performance tests
- Historicos con alto volumen (paginacion/export).
- Consultas con filtros compuestos por fecha + tipo + estado.

---

## 11) Entrega recomendada por fases
### Fase 1 (nucleo laboral)
- `employee-profile`
- `employment-assignments`
- `contract-history`
- `salary-items`
- `additional-benefits`
- `payment-methods`
- `authorization-substitutions`
- `custom-data` laboral.

### Fase 2 (historiales y cobertura operativa)
- `personnel-actions` (+ export)
- `payroll-transactions` (+ export)
- `assets-accesses`
- `insurances` + `beneficiaries`
- `medical-claims`.

### Fase 3 (integraciones avanzadas)
- `evaluations`
- `position-competencies`
- `selection-contests`
- `curricular-competencies`.

---

## 12) Riesgos y decisiones abiertas
- Dependencias inter-modulo:
  - Si no existe backend fuente para planilla/reclutamiento/desarrollo, se requiere estrategia temporal de staging interno.
- Definicion de fuente de verdad:
  - Determinar si ciertas secciones son editables en `PersonnelFiles` o solo espejo de otro modulo.
- Dimensionamiento:
  - Es un requerimiento grande; sin fases aumenta riesgo de atraso y deuda tecnica.
- Gobernanza de catalogos:
  - Debe definirse ownership funcional de cada categoria para evitar inconsistencias.

---

## 13) Definicion de terminado (DoD)
- Todas las secciones de la fase comprometida implementadas con validaciones y auditoria.
- Documentacion API actualizada.
- Pruebas unitarias, de aplicacion e integracion en verde.
- Scripts SQL y `001_apply_clarihr_schema.sql` actualizados.
- Criterios Gherkin de la fase ejecutados y aprobados por QA/Negocio.

