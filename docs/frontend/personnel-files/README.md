# Personnel Files — Guía de consumo de API (frontend) · Fase 9

Documentación de consumo de los endpoints de **Personnel Files** y sus sub‑recursos. Un documento por recurso, a nivel de integración (qué endpoints hay, qué enviar/recibir, ejemplos).

> **Empezá acá:** [`_conventions.md`](./_conventions.md) — reglas transversales (auth, `publicId`, concurrencia `If-Match`, JSON Patch, paginación, errores). Cada doc asume estas convenciones.

## Cómo encaja con el resto de la API

Personnel Files es el módulo de negocio que cuelga del onboarding y la estructura organizativa.
Prerequisitos y conexiones (ver el [índice maestro](../README.md)):

- **Sesión y tenant** — [Autenticación](../auth/authentication.md) (Fase 1) para el Bearer token y
  [Account Companies](../account-companies/account-companies.md) (Fase 2) para la **compañía
  activa**: el shell (`POST`/`GET` de búsqueda) es scoped por `companyPublicId`, que debe ser la
  compañía activa del JWT.
- **Permisos** — `PersonnelFiles.Read` / `PersonnelFiles.Manage`
  ([IAM](../iam-authorization/iam-authorization.md), Fase 5); el filtrado a nivel campo del módulo
  se gobierna por las field-policies del recurso `PERSONNEL_FILES` (ver
  [access-context / resource-policies](../account-companies/account-companies.md), Fase 2 §13–14).
- **Dropdowns de los formularios** — [General Catalogs](../general-catalogs/general-catalogs.md)
  (Fase 7): países, profesiones, bancos, idiomas, parentescos, departamentos/municipios, tipos de
  documento, etc. **Estos catálogos están gateados por `PersonnelFiles.Read`** justamente porque
  alimentan estos forms. Guardá el `code`, no el `publicId`.
- **FKs de la estructura organizativa** — [Organización](../organization/README.md) (Fase 8): el
  `employee-profile` y los `employment-assignments` referencian Org Units, Work Centers, Cost
  Centers y puestos. Esas entidades deben existir antes de finalizar el expediente como empleado.

## Recurso principal
- [Personnel Files (shell)](./personnel-files.md) — crear / buscar / obtener / actualizar / activar‑desactivar
- [Finalize](./finalize.md) — finalizar el archivo (Draft → Completed) + preview
- [Personal Info](./personal-info.md) — lectura consolidada de la info personal

## Identificación y datos personales
- [Identifications](./identifications.md)
- [Addresses](./addresses.md)
- [Emergency Contacts](./emergency-contacts.md)
- [Family Members](./family-members.md)

## Formación y trayectoria
- [Educations](./educations.md)
- [Languages](./languages.md)
- [Trainings](./trainings.md)
- [Previous Employments](./previous-employments.md)
- [References](./references.md)

## Intereses y relaciones
- [Hobbies](./hobbies.md)
- [Associations](./associations.md)
- [Employee Relations](./employee-relations.md)

## Talento y evaluación
- [Performance Evaluations](./evaluations.md)
- [Position Competency Results](./position-competency-results.md)
- [Selection Contests](./selection-contests.md)
- [Curricular Competencies](./curricular-competencies.md)

## Compensación
- [Salary Items](./salary-items.md)
- [Additional Benefits](./additional-benefits.md)
- [Bank Accounts](./bank-accounts.md)
- [Payment Methods](./payment-methods.md)
- [Insurances (y beneficiarios)](./insurances.md)
- [Medical Claims](./medical-claims.md)
- [Payroll Transactions](./payroll-transactions.md)

## Empleo
- [Employment Assignments](./employment-assignments.md)
- [Contract History](./contract-history.md)
- [Authorization Substitutions](./authorization-substitutions.md)
- [Assets & Accesses](./assets-accesses.md)
- [Personnel Actions](./personnel-actions.md)
- [Employee Profile](./employee-profile.md)
- [Position Hierarchy](./position-hierarchy.md)

## Documentos y reportería
- [Documents](./documents.md)
- [Observations](./observations.md)
- [Reporting (dynamic‑query / export / analytics)](./reporting.md)

---

> Los sub‑recursos de **Compensación**, **Talento** y **Empleo** solo admiten escrituras sobre un archivo **finalizado** (empleado). Ver [`_conventions.md` §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).
