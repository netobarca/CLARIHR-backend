-- Diagnóstico previo al despliegue de los dos guards de «padre inactivo» de la plaza:
--   · `POSITION_SLOT_ORG_UNIT_INACTIVE`     (la unidad organizativa, vía el perfil de puesto)
--   · `POSITION_SLOT_WORK_CENTER_INACTIVE`  (el centro de trabajo, referencia directa)
--
-- Por qué existe: hasta ahora se podía crear una plaza contra un perfil cuya unidad organizativa
-- estaba de baja. Con el guard puesto, crear Y ACTUALIZAR quedan bloqueados mientras la unidad no
-- se reactive — ambos verbos pasan por la misma resolución del perfil.
--
-- La plaza NO guarda la unidad: `position_slots` no tiene `org_unit_id`. La unidad se deriva del
-- perfil (`job_profiles.org_unit_id`), y de ahí que el guard viva en esa resolución y no en la plaza.
-- Consecuencia práctica: dar de baja una unidad congela las plazas de TODOS sus perfiles.
--
--   psql -h localhost -p 5433 -U clarihr -d clarihr_dev -f diagnostico-plazas-unidad-inactiva.sql
--
-- No modifica nada. Solo lee.

-- 1. Resumen por empresa: cuántas plazas quedarían sin poder actualizarse.
SELECT
    ps.tenant_id                                     AS empresa,
    COUNT(*)                                         AS plazas_afectadas,
    COUNT(*) FILTER (WHERE ps.is_active)             AS de_ellas_activas,
    COUNT(DISTINCT ou.id)                            AS unidades_de_baja_implicadas,
    COUNT(DISTINCT jp.id)                            AS perfiles_implicados
FROM position_slots ps
JOIN job_profiles jp ON jp.id = ps.job_profile_id
JOIN org_units    ou ON ou.id = jp.org_unit_id
WHERE ou.is_active = false
GROUP BY ps.tenant_id
ORDER BY plazas_afectadas DESC;

-- 2. Detalle: qué plazas concretas, y por qué unidad están congeladas.
SELECT
    ps.tenant_id        AS empresa,
    ou.code             AS unidad_de_baja,
    ou.name             AS unidad_nombre,
    jp.code             AS perfil,
    jp.status           AS perfil_estado,
    ps.code             AS plaza,
    ps.title            AS plaza_titulo,
    ps.is_active        AS plaza_activa
FROM position_slots ps
JOIN job_profiles jp ON jp.id = ps.job_profile_id
JOIN org_units    ou ON ou.id = jp.org_unit_id
WHERE ou.is_active = false
ORDER BY ps.tenant_id, ou.code, jp.code, ps.code;

-- 3. Puertas cerradas sin plazas todavía: unidades de baja con perfiles PUBLICADOS.
--    No hay plaza que se rompa, pero nadie podrá crear una ahí hasta reactivar la unidad.
--    Es el caso que el asistente encuentra primero, y el que más confunde: el perfil se ve
--    publicado y disponible, y la creación falla por algo que no está en la pantalla de plazas.
SELECT
    jp.tenant_id        AS empresa,
    ou.code             AS unidad_de_baja,
    ou.name             AS unidad_nombre,
    jp.code             AS perfil_publicado,
    jp.title            AS perfil_titulo
FROM job_profiles jp
JOIN org_units ou ON ou.id = jp.org_unit_id
WHERE ou.is_active = false
  AND jp.status = 'Published'
  AND NOT EXISTS (SELECT 1 FROM position_slots ps WHERE ps.job_profile_id = jp.id)
ORDER BY jp.tenant_id, ou.code, jp.code;

-- Cómo leer el resultado:
--
--   0 filas en las tres  → el guard se puede desplegar sin más.
--
--   Filas en 1 y 2  → esas plazas existen y hoy se pueden editar; tras el despliegue no, hasta que
--   la unidad de la columna `unidad_de_baja` se reactive. Conviene avisar antes: el mensaje de error
--   nombra la unidad, pero quien edita una plaza no espera que el bloqueo venga de la estructura.
--
--   Filas en 3  → no rompen nada, pero explican de antemano un «no me deja crear la plaza» que
--   llegará por soporte. La salida dice qué unidad reactivar.
--
-- Corrida del 2026-08-18 sobre `clarihr_dev`: 0 filas en las tres consultas. Con una salvedad que
-- conviene leer entera — en esa base NO hay ninguna unidad inactiva (27 unidades, todas activas), así
-- que el vacío dice «no hay a quién romper aquí», no «se revisó un universo poblado y salió limpio».
-- Volver a correrlo contra la base destino antes de desplegar allí.
--
-- Nota: el código de error es POSITION_SLOT_ORG_UNIT_INACTIVE (422). El guard vive en
-- `ResolveJobProfileLookupAsync`, en PositionSlotAdministration.cs.

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 4. El otro padre: plazas cuyo CENTRO DE TRABAJO está de baja.
--
-- A diferencia de la unidad organizativa —que la plaza hereda del perfil— el centro se referencia
-- directamente (`position_slots.work_center_id`). La plaza puede no tener centro: esas no aplican.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
SELECT
    ps.tenant_id                                     AS empresa,
    COUNT(*)                                         AS plazas_afectadas,
    COUNT(*) FILTER (WHERE ps.is_active)             AS de_ellas_activas,
    COUNT(DISTINCT wc.id)                            AS centros_de_baja_implicados
FROM position_slots ps
JOIN work_centers wc ON wc.id = ps.work_center_id
WHERE wc.is_active = false
GROUP BY ps.tenant_id
ORDER BY plazas_afectadas DESC;

-- Detalle: qué plazas y por qué centro quedan congeladas.
SELECT
    ps.tenant_id   AS empresa,
    wc.code        AS centro_de_baja,
    wc.name        AS centro_nombre,
    ps.code        AS plaza,
    ps.title       AS plaza_titulo,
    ps.is_active   AS plaza_activa
FROM position_slots ps
JOIN work_centers wc ON wc.id = ps.work_center_id
WHERE wc.is_active = false
ORDER BY ps.tenant_id, wc.code, ps.code;

-- Cómo leer estas dos: igual que las anteriores. 0 filas → el guard del centro se puede desplegar sin
-- más. Con filas, esas plazas existen y hoy se pueden editar; tras el despliegue no, hasta reactivar el
-- centro de la columna `centro_de_baja`.
