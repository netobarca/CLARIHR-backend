# Plan técnico — Planilla: descuentos eventuales (REQ-009)

| | |
|---|---|
| **Fuente** | [`analisis-planilla-descuentos-y-endeudamiento.md`](../business/analisis-planilla-descuentos-y-endeudamiento.md) — **RATIFICADO 2026-07-12**. Este plan implementa el **Plan 2**: espejo 1:1 de REQ-006 (ingresos eventuales) cambiando la naturaleza a **Egreso** |
| **Fecha** | 2026-07-12 (anclas verificadas contra `feature/planilla-descuentos`, HEAD `fa7cf5d` — REQ-008 code-complete) |
| **Molde** | **REQ-006 está EN LA RAMA y es as-built** (gana sobre su plan): `PersonnelFileOneTimeIncome.cs`, `OneTimeIncomes.Rules.cs`, `OneTimeIncomes*.cs`, `OneTimeIncomeApplications*.cs`, `OneTimeIncomesBandeja*.cs`, `OneTimeIncomesController` + `OneTimeIncomeResolutionController` + `OneTimeIncomesReportingController` |
| **Rama** | `feature/planilla-descuentos` (acumulada con REQ-008) |
| **Migraciones** | **M1** (PR-1: catálogo de estados + concepto de liquidación) · **M2** (PR-2: 2 tablas de dominio) |
| **Seeds** | Bloque **`-9940…-9949`** — verificado libre con sufijo `L` (2026-07-12) |

---

## 0. Aclaraciones quirúrgicas (verificadas contra el código)

1. **Es un espejo, no un diseño nuevo.** Todo lo estructural ya existe en REQ-006 y se replica cambiando `Ingreso`→`Egreso` y `OneTimeIncome`→`OneTimeDeduction`. Las decisiones difíciles (métodos de cálculo, anti-self triple, re-imputación, índice único parcial de aplicación) **ya están tomadas y probadas**.
2. **⚠️ EL TOQUE AL MOTOR SE REPITE (§3.5).** `ResolveClass` (`SettlementCalculation.Rules.cs`) sigue siendo un **switch cerrado con `default → Ingreso`**. REQ-008 añadió `DESCUENTO_CICLICO_PENDIENTE` a su brazo `Descuento`, **pero eso NO cubre el concepto nuevo de REQ-009**. Hay que añadir `DescuentoEventualPendiente` a la misma lista, o el saldo se **pagaría** en vez de descontarse. Es el mismo error de una línea, en el mismo sitio.
3. **Conceptos**: se reutiliza el resolver **ya construido en REQ-008** `IPersonnelFileRepository.GetActiveDeductionConceptAsync` (Egreso **activo y NO estatutario** — ISSS/AFP/Renta → 422). **No hay que escribirlo de nuevo.**
4. **Sin centro de costo** (P-08) y **sin institución financiera**: un descuento eventual no es un crédito con acreedor, es un cobro puntual (multa, daño de equipo, anticipo). Solo plaza.
5. **Anti-self TRIPLE** (D-05/D-12 de REQ-006): ni el **empleado sujeto**, ni el **solicitante** (`RequestedByUserId`), ni el **registrador** (`RegisteredByUserId`) pueden decidir. Molde exacto: `OneTimeIncomes.Handlers.cs` (guards de resolución).
6. **APLICADO es reversible** (a diferencia del `FINALIZADO` de los cíclicos): la anulación de la aplicación devuelve el descuento a `AUTORIZADO`. 5 estados: `EN_REVISION` → `AUTORIZADO` → `APLICADO`, con ramas `RECHAZADO` / `ANULADO`.
7. **Un índice único parcial** garantiza **una sola aplicación activa** por descuento (RN-06 del molde).
8. **Sin `groupBy` de 8 dimensiones**: eso fue un net-new de REQ-006 para su búsqueda avanzada. Aquí la bandeja es la corporativa estándar (StatusCounts + totales por moneda), **igual que la de REQ-008 PR-5** — que es el molde más cercano y ya está construido.
9. **openapi**: se vuelca del swagger REAL de la app (receta de REQ-008 PR-6: test temporal que hace `GET /swagger/v1/swagger.json` + script de inyección). **No transcribir a mano.**

---

## 1. Modelo de datos (M2)

### `personnel_file_one_time_deductions` (`PersonnelFileOneTimeDeduction : TenantEntity`)

Espejo de `PersonnelFileOneTimeIncome`:

| Grupo | Columnas |
|---|---|
| Identidad | `id`, `public_id` (UQ), `tenant_id`, `personnel_file_id` (FK), `concurrency_token`, `created_utc`/`modified_utc`, `is_active` |
| Cabecera | `target_date` (fecha en que será aplicado), `reference` (200, NULL), `concept_type_code` (80) + `concept_name_snapshot` (200) — concepto país **Egreso NO estatutario**, `observations` (1000, NULL) |
| Plaza (P-08) | `assigned_position_public_id` (NOT NULL, default plaza principal) — **sin centro de costo** |
| Monto | `is_fixed_value` (bool), `amount numeric(18,2)` (>0), `currency_code` (3), `calculation_method_code` (40, NULL — `PORCENTAJE_SOBRE_BASE` / `CANTIDAD_POR_VALOR`), `base_amount`, `percent`, `quantity`, `unit_value`, `factor` (todos `numeric(18,4)` NULL — **componentes persistidos**) |
| Planilla | `payroll_type_code` (80), `payroll_period_id bigint NULL` (**FK real**) + `payroll_period_label` (80) — **re-imputable mientras AUTORIZADO** |
| Solicitante (trío) | `requester_file_public_id uuid NULL`, `requester_name_snapshot` (200, NULL), `requested_by_user_id uuid NULL` |
| Flujo | `status_code` (80), `registered_by_user_id`, `decided_by_user_id`/`decided_utc`/`decision_note` (500), `annulment_reason` (500)/`annulled_by_user_id`/`annulled_utc`, `closed_by_settlement_public_id uuid NULL` |

### `personnel_file_one_time_deduction_applications` (`…Application : TenantEntity`)

Espejo de la aplicación de REQ-006: `applied_date`, `amount` (snapshot), `currency_code`, `payroll_type_code`, `payroll_period_id` (FK) + label, `origin_code` (MANUAL/MOTOR), `status_code` (APLICADA/ANULADA), `applied_by_user_id`, `annulment_reason`/`annulled_by_user_id`/`annulled_utc`, `notes`.

- **Índice único parcial**: `(tenant_id, one_time_deduction_id) WHERE is_active` → **una sola aplicación activa** por descuento.

---

## 2. Orden de PRs

- **PR-1 — Configuración (M1)**: catálogo `one-time-deduction-statuses` (`-9940…-9944`: EN_REVISION/AUTORIZADO/RECHAZADO/APLICADO/ANULADO) por la **receta de 8 toques** + **concepto de liquidación `DESCUENTO_EVENTUAL_PENDIENTE = -9945`** (clase **Descuento**, `IsSystemCalculated=FALSE`, `Affects*=false`, sort ~136 — junto al `-9928`) + **3 permisos** `View/Manage/AuthorizeOneTimeDeductions` (receta completa; `Authorize*` **sin Admin**) + registro en `AuthorizationPolicyConventionGovernanceTests` + openapi temprano. *Verificar seeds libres con sufijo `L`.*
- **PR-2 — Dominio + reglas (M2)**: 2 entidades + mutadores custodiados + EF config/índices/CHECKs + **`OneTimeDeductionRules`** (molde `OneTimeIncomes.Rules.cs`: `Round2`, `ComputeAmount` con los 2 métodos, `ValidateComponents`, `CanTransition`) + repo + lock **`"ODED"`** (`0x4F_44_45_44`, molde `OTIN`). Golden de los 2 métodos de cálculo.
- **PR-3 — Flujo end-to-end**: `OneTimeDeductionsController` (CRUD + anulación) + `OneTimeDeductionResolutionController` (resolución + revocación, **anti-self TRIPLE**) + re-imputación del par planilla/periodo mientras `AUTORIZADO` + validaciones (concepto Egreso no estatutario vía el resolver de REQ-008, plaza, monto por factores recalculado en servidor).
- **PR-4 — Aplicación**: aplicación unitaria + **lote por periodo** (atómico, locks ordenados) + **reversión** (`APLICADO` → `AUTORIZADO`) + **test de carrera**.
- **PR-5 — Bandeja + exports + insumo + liquidación + cierre**: bandeja corporativa (`StatusCounts` + totales por moneda, molde REQ-008 PR-5) + 2 exports + insumo + **integración liquidación** (⚠️ **§0.2: añadir la constante al brazo `Descuento` de `ResolveClass`**; canal de sugerencias con los `AUTORIZADO` no aplicados; hooks Issue/Annul) + `openapi.yaml` (vía swagger dump) + **guía FE**.

---

## 3. Pruebas

- **Unitarias**: `OneTimeDeductionRulesTests` — los 2 métodos de cálculo (`PORCENTAJE_SOBRE_BASE`: base × % ; `CANTIDAD_POR_VALOR`: cantidad × valor × factor), `Round2`, componentes incoherentes → error, transiciones (incluida la **reversión** `APLICADO`→`AUTORIZADO`), paridad de localización de todos los `ONE_TIME_DEDUCTION_*`.
- **Integración** (`ApiIntegrationTests.OneTimeDeductions*.cs`): CRUD · concepto **estatutario → 422** · monto por factores **recalculado por el servidor** (mandar un `amount` mentiroso no cuela) · **anti-self TRIPLE** (sujeto/solicitante/registrador → 403; Admin sin grant → 403) · re-imputación solo en `AUTORIZADO` · aplicación única (segunda → 422) · **carrera** · reversión · bandeja/exports/insumo cuadrado · **liquidación e2e: la línea es `Descuento` y REDUCE EL NETO** · suite de liquidación existente verde **sin editarla**.

---

## 4. Checklist de despliegue

- [ ] Migraciones M1–M2.
- [ ] Asignar los 3 permisos (`AuthorizeOneTimeDeductions` a los autorizadores; **Admin no decide sin grant**).
- [ ] Sin storage nuevo, sin jobs, sin appsettings nuevos.
