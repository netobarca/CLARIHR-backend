# Job Profiles — Guía de consumo (frontend) · Fase 10

> **Prerequisitos:** [onboarding 1–6](../README.md), [Organización](../organization/README.md)
> (Fase 8 — los perfiles cuelgan de Org Units) y [General Catalogs](../general-catalogs/general-catalogs.md).
>
> Empezá por las [Convenciones](./_conventions.md) — reglas transversales (auth, compañía activa,
> estados Draft/Published/Archived, `If-Match`, JSON Patch, el patrón de sub‑recurso). Cada doc de
> recurso solo documenta lo específico.
>
> 🧪 **¿Querés probar el flujo de punta a punta?** Seguí la
> [Guía de prueba E2E paso a paso](./e2e-testing-walkthrough.md): crea cada prerrequisito en orden
> (org unit, categoría, catálogos…), el perfil, sus sub‑recursos, publica, edita publicado y archiva,
> con los bodies exactos y las pruebas negativas.

---

## Overview

Un **Job Profile** (perfil de puesto / descriptor de cargo) es el documento que define un puesto:
su objetivo, funciones, requisitos, competencias, capacitaciones, beneficios, condiciones de
trabajo, relaciones, posiciones dependientes y compensación. Es un **aggregate root con ciclo de
vida** (Draft → Published → Archived) con 9 sub‑recursos, alimentado por 2 catálogos.

| Recurso | Doc | Rol |
|---------|-----|-----|
| Job Profiles (shell) | [job-profiles.md](./job-profiles.md) | crear/buscar/editar el perfil + `catalog-manifest` + publicar/archivar |
| Job Catalogs | [job-catalogs.md](./job-catalogs.md) | catálogos editables por categoría + diccionario global de valores |
| Functions | [functions.md](./functions.md) | funciones del puesto (general/específica) |
| Requirements | [requirements.md](./requirements.md) | requisitos (educación/experiencia/conocimiento/certificación) |
| Competencies | [competencies.md](./competencies.md) | competencias esperadas |
| Trainings & Benefits | [trainings-and-benefits.md](./trainings-and-benefits.md) | capacitaciones y beneficios (estructura idéntica) |
| Working Conditions | [working-conditions.md](./working-conditions.md) | condiciones de trabajo |
| Relations | [relations.md](./relations.md) | relaciones internas/externas del puesto |
| Dependent Positions | [dependent-positions.md](./dependent-positions.md) | puestos que dependen de este |
| Compensations | [compensations.md](./compensations.md) | compensación (1 por perfil, ligada a tabulador salarial) |

---

## El modelo de datos (leer antes de integrar)

```
                          ┌──────────────────────────────────┐
   Organization Unit ────►│  JOB PROFILE  (Draft/Published/   │
   (Fase 8, obligatorio)  │               Archived)          │
   reportsTo (self) ─────►│  code · title · objective ...    │
   Position Category ────►│  + concurrencyToken               │
   Catalog Items ────────►└──────────────┬───────────────────┘
                                         │ 9 sub‑recursos (cuelgan del perfil)
        ┌────────────────────────────────┼─────────────────────────────────┐
   functions  requirements  competencies trainings  benefits  working-conditions
   relations  dependent-positions  compensations(→ Salary Tabulator Line, 1×)
                                         │ casi todos referencian ▼
                          ┌──────────────────────────────────┐
                          │  JOB CATALOGS (por categoría)     │  + Internal Catalogs
                          │  EducationLevel, Training,        │  (diccionario global
                          │  BenefitType, RelationType, ...   │   free‑text para requisitos)
                          └──────────────────────────────────┘
```

- **El perfil cuelga de una Organization Unit** (`orgUnitPublicId`, obligatorio — Fase 8) y puede
  reportar a otro perfil (`reportsToJobProfilePublicId`, con detección de ciclos), referenciar una
  Position Category y varios catálogos.
- **Los 9 sub‑recursos** son las secciones del descriptor; casi todos referencian un **Job Catalog**
  de cierta categoría (`catalogItemPublicId`). `compensations` es especial: 1 por perfil, ligada a
  una línea de **tabulador salarial**.
- **Catálogos**: `job-catalogs` son catálogos **editables por compañía** (por categoría); los
  `internal-catalogs` son un diccionario **global** de valores free‑text (autocomplete) para
  requisitos.

## Orden de integración recomendado

1. **Job Catalogs** — poblá las categorías que vas a usar (o usá las de sistema) antes de los forms.
2. **Job Profile (shell)** — `POST` crea en `Draft`; cargá el `catalog-manifest` para saber qué
   catálogo alimenta cada campo.
3. **Sub‑recursos** — agregá funciones, requisitos, competencias, etc. sobre el perfil en `Draft`.
4. **Publicar** — `PATCH /status → Published` cuando estén los prerrequisitos (objective,
   responsibilities, ≥1 requirement, ≥1 function).
5. **Compensación** — ligá la línea de tabulador salarial (1 por perfil).

## Conexión con otros módulos

- **Organización** (Fase 8): `orgUnitPublicId` obligatorio; Position Category.
- **Salary Tabulator**: `compensations` referencia una línea de tabulador (módulo de compensación).
- **Competency Framework** (módulo vecino, no documentado acá): el job profile tiene competencias
  "legacy" simples (referencian Job Catalog); la matriz de competencias avanzada
  (`competency-matrix`, occupational pyramid) es del Competency Framework.
- **Position Slots** (módulo vecino): un job profile se instancia como posiciones ocupables.

## Próximas fases

Módulos vecinos aún no documentados: **Competency Framework** (conducts, matrix, occupational
pyramid), **Position Slots**, **Position Description Catalogs**, y los demás módulos de negocio.
