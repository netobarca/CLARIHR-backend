# Personnel Files — Guía de consumo de API (frontend)

Documentación de consumo de los endpoints de **Personnel Files** y sus sub‑recursos. Un documento por recurso, a nivel de integración (qué endpoints hay, qué enviar/recibir, ejemplos).

> **Empezá acá:** [`_conventions.md`](./_conventions.md) — reglas transversales (auth, `publicId`, concurrencia `If-Match`, JSON Patch, paginación, errores). Cada doc asume estas convenciones.

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
