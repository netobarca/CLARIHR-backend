# Verificación — Catálogos de personnel-files listos en el servidor

| | |
| --- | --- |
| **Objetivo** | Confirmar que los catálogos que poblan los comboboxes del FE están **sembrados y disponibles** en un entorno (staging/prod) tras desplegar. |
| **Cuándo** | Después de cada despliegue que incluya migraciones de seed (`20260627212537`, `20260628234354`, `20260628235800`). |
| **Causa raíz del incidente** | Los catálogos llegan al servidor **solo** por migraciones `HasData` (las aplica el `MigrateAsync` del arranque). Si una migración no se desplegó, el catálogo sale **vacío**. |

Hay 3 ángulos de verificación, de más definitivo a más práctico. Con **uno** basta; el #1 es el más directo a la causa raíz.

---

## Ángulo 1 — ¿Están aplicadas las migraciones? (causa raíz)

Requiere acceso a la BD del servidor (o a los logs de arranque). Es la verificación más directa: si las migraciones están aplicadas, los `HasData` ya corrieron.

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '20260627212537%'   -- 3 catálogos reportados + payment/medical/off-payroll/contract/action…
   OR "MigrationId" LIKE '20260628234354%'   -- insurance / compensation / pay-periods / calculation-bases / education
   OR "MigrationId" LIKE '20260628235800%'   -- account-types
ORDER BY "MigrationId";
```

**Deben aparecer las 3 filas.** Si falta alguna → ese grupo de catálogos estará vacío → **falta desplegar** (o aplicar migraciones). En arranque normal el `MigrateAsync` las aplica solo; si no, `dotnet ef database update` en el entorno.

---

## Ángulo 2 — Conteo de filas por catálogo (definitivo)

Requiere acceso de lectura a la BD del servidor. `OK` = tiene filas; `VACIO ***` = no sembrado → revisar Ángulo 1.

```sql
SELECT catalogo, filas, CASE WHEN filas>0 THEN 'OK' ELSE 'VACIO ***' END estado FROM (
  SELECT 'account-types' catalogo, count(*) filas FROM bank_account_type_catalog_items
  UNION ALL SELECT 'asset-access-types', count(*) FROM asset_access_type_catalog_items
  UNION ALL SELECT 'delivery-statuses', count(*) FROM delivery_status_catalog_items
  UNION ALL SELECT 'substitution-types', count(*) FROM substitution_type_catalog_items
  UNION ALL SELECT 'payment-methods', count(*) FROM payment_method_catalog_items
  UNION ALL SELECT 'medical-claim-types', count(*) FROM medical_claim_type_catalog_items
  UNION ALL SELECT 'off-payroll-transaction-types', count(*) FROM off_payroll_transaction_type_catalog_items
  UNION ALL SELECT 'insurance-types', count(*) FROM insurance_type_catalog_items
  UNION ALL SELECT 'insurance-ranges', count(*) FROM insurance_range_catalog_items
  UNION ALL SELECT 'compensation-concept-types', count(*) FROM compensation_concept_type_catalog_items
  UNION ALL SELECT 'pay-periods', count(*) FROM pay_period_catalog_items
  UNION ALL SELECT 'calculation-bases', count(*) FROM calculation_base_catalog_items
  UNION ALL SELECT 'education-statuses', count(*) FROM education_status_catalog_items
  UNION ALL SELECT 'education-study-types', count(*) FROM education_study_type_catalog_items
  UNION ALL SELECT 'education-shifts', count(*) FROM education_shift_catalog_items
  UNION ALL SELECT 'education-modalities', count(*) FROM education_modality_catalog_items
  UNION ALL SELECT 'education-careers', count(*) FROM education_career_catalog_items
) s ORDER BY estado DESC, catalogo;
```

**Conteos esperados (SV)** — útil para comparar:

| catálogo | filas | catálogo | filas |
|---|---|---|---|
| account-types | 5 | education-careers | 6 |
| asset-access-types | 8 | education-modalities | 2 |
| delivery-statuses | 7 | education-shifts | 2 |
| substitution-types | 6 | education-statuses | 2 |
| payment-methods | 3 | education-study-types | 3 |
| medical-claim-types | 9 | insurance-types | 7 |
| off-payroll-transaction-types | 6 | insurance-ranges | 6 |
| compensation-concept-types | 17 | pay-periods | 4 |
| calculation-bases | 4 | | |

---

## Ángulo 3 — Vía API (lo que realmente ve el Frontend)

No requiere acceso a BD, solo un **token JWT válido** (el mismo que usa el FE; estos catálogos son de lectura autenticada, sin necesidad de empresa). Confirma que el endpoint **responde y trae datos**.

```bash
#!/usr/bin/env bash
# Uso:  BASE_URL=https://api.tu-servidor  TOKEN=<jwt>  bash verificar-catalogos-api.sh
set -uo pipefail
: "${BASE_URL:?define BASE_URL}"; : "${TOKEN:?define TOKEN (JWT)}"

check() { # $1=base (general-catalogs|reference-catalogs)  $2=key  $3=query-extra
  local n
  n=$(curl -s -H "Authorization: Bearer $TOKEN" "$BASE_URL/api/v1/$1/$2?countryCode=SV${3:-}" | jq 'length' 2>/dev/null)
  if [[ "$n" =~ ^[0-9]+$ && "$n" -gt 0 ]]; then printf "  %-32s OK (%s)\n" "$2" "$n"
  else printf "  %-32s VACIO/ERROR (%s)\n" "$2" "${n:-sin-respuesta}"; fi
}

echo "== general-catalogs =="
for k in account-types asset-access-types delivery-statuses substitution-types payment-methods \
         medical-claim-types off-payroll-transaction-types compensation-concept-types pay-periods \
         calculation-bases education-statuses education-study-types education-shifts \
         education-modalities education-careers; do check general-catalogs "$k"; done

echo "== reference-catalogs =="
check reference-catalogs insurance-types
check reference-catalogs insurance-ranges "&parentCode=VIDA"   # los rangos se filtran por tipo
```

> La respuesta de cada endpoint es un **array JSON** de items `{ id, code, name, isActive, sortOrder }`; el FE usa `code` como valor y `name` como label. `insurance-ranges` es jerárquico (filtra por `parentCode` = código del tipo de seguro).

---

## Si algún catálogo sale VACÍO

1. Revisar el **Ángulo 1**: ¿está aplicada la migración correspondiente? Si no → desplegar / `dotnet ef database update` en el entorno.
2. Confirmar que el arranque corre `MigrateAsync` (lo hace por defecto en todos los entornos vía `StartupInitializationExtensions`).
3. Recordatorio: **`DevSeedService` NO siembra estos catálogos en el servidor** (es solo-dev). La única vía al servidor es `HasData` en migración.
