# Competency Framework — Guía de consumo (frontend) · Fase 11

> **Prerequisitos:** [onboarding 1–6](../README.md), [Job Profiles](../job-profiles/README.md)
> (Fase 10 — la matriz de competencias cuelga de un job profile, y las conductas usan los catálogos
> `Competency`/`CompetencyType`/`BehaviorLevel`/`Behavior` de Job Catalogs).
>
> Convenciones globales en el [índice maestro](../README.md). Cada doc de recurso documenta lo
> específico.

---

## Overview

El **Competency Framework** modela las competencias esperadas de la organización de forma
estructurada (la versión avanzada de las "competencias legacy" simples del job profile, Fase 10).
Son **3 recursos**:

| Recurso (tag "Competency Framework") | Doc | Rol |
|--------------------------------------|-----|-----|
| Occupational Pyramid Levels | [occupational-pyramid-levels.md](./occupational-pyramid-levels.md) | catálogo de niveles de la pirámide ocupacional (Operativo, Táctico, Estratégico…) |
| Competency Conducts | [competency-conducts.md](./competency-conducts.md) | conductas observables (competencia × tipo × nivel) con behaviors anidados |
| Job Profile Competency Matrix | [competency-matrix.md](./competency-matrix.md) | la matriz de un job profile: qué se espera por nivel y competencia |

## El modelo de datos (leer antes de integrar)

```
Job Catalogs (Fase 10)                Competency Framework
┌──────────────────────┐
│ Competency           │◄──┐
│ CompetencyType       │◄──┤   Competency Conduct  =  competency × competencyType ×
│ BehaviorLevel        │◄──┤        (conducta)         behaviorLevel + description
│ Behavior             │◄──┼───────────────────────────┐ + behaviors[] (anidados)
└──────────────────────┘   │                           │
                           │   Occupational Pyramid     │
                           │   Levels (este módulo)     │
                           │        (catálogo)          │
                           ▼                            ▼
              ┌──────────────────────────────────────────────────┐
              │  Job Profile Competency Matrix  (por job profile) │
              │  item = nivel-pirámide × competencia × tipo ×     │
              │         behaviorLevel + conducts[] + evidencia    │
              └──────────────────────────────────────────────────┘
```

- **Occupational Pyramid Levels**: catálogo de niveles jerárquicos (la dimensión vertical de la
  matriz). CRUD + activate/inactivate.
- **Competency Conducts**: conductas observables reutilizables. Cada una combina una **competencia**,
  un **tipo de competencia** y un **nivel de comportamiento** (las 3 FKs salen de los Job Catalogs de
  la Fase 10, categorías `Competency`/`CompetencyType`/`BehaviorLevel`), más una descripción y una
  colección de **behaviors** anidados (categoría `Behavior`).
- **Competency Matrix**: por cada job profile, ensambla la expectativa — para cada
  (nivel de pirámide × competencia × tipo × nivel de comportamiento) qué conductas y qué evidencia se
  esperan. La terna competencia/tipo/nivel **se deriva de las conductas** del item (no se envía): cada
  item lleva nivel de pirámide + conductas (≥1, todas de la misma terna) + evidencia. Es un **replace
  completo** (un solo `PUT`).

## Convenciones de la familia

- **Auth/permisos**: Bearer JWT. `GET` → `CompetencyFramework.Read`; escrituras →
  `CompetencyFramework.Admin` (o `iam.administration.manage`). Sin permiso → `403
  COMPETENCY_FRAMEWORK_FORBIDDEN`.
- **Compañía activa**: los listados/creación son scoped por `companyPublicId` (= tenant activo del
  JWT); cross-tenant → `403 TENANT_MISMATCH` / `404`.
- **Concurrencia**: token **fuerte** GUID. `concurrencyToken` en body + header `ETag`; las
  mutaciones exigen `If-Match: "<token>"` (faltante → `400`, stale → `409 CONCURRENCY_CONFLICT`).
- **Paginación**: `page`/`pageSize` (máx 100), `q` (búsqueda), `includeAllowedActions`.
- **Rate limits**: búsqueda 120/min, export 10/min (por usuario+tenant).
- **JSON Patch**: array desnudo RFC 6902 (`application/json-patch+json`).

## Orden de integración

1. **Job Catalogs** (Fase 10): poblá las categorías `Competency`, `CompetencyType`, `BehaviorLevel`,
   `Behavior`.
2. **Occupational Pyramid Levels**: definí los niveles de la pirámide.
3. **Competency Conducts**: creá las conductas (con sus behaviors).
4. **Competency Matrix**: armá la matriz de cada job profile usando todo lo anterior.

## Próximas fases

Módulos vecinos aún sin documentar: **Position Slots**, **Position Description Catalogs**,
**Salary Tabulator** y el resto de los módulos de negocio.
