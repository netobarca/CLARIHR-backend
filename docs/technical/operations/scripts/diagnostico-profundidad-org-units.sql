-- Diagnóstico previo al despliegue del guard de profundidad de `PATCH /organization-units/{id}/move`.
--
-- Por qué existe: el guard medía la profundidad del nodo movido y no la altura de su subárbol, así que
-- un subárbol podía empujar a sus descendientes por encima de MaxDepth (15). Con el guard corregido,
-- una unidad que YA está por encima del límite no se podrá reorganizar sin bajarla antes — el
-- movimiento parte de un estado inválido. Esto lista a quién afecta ANTES de desplegar.
--
--   psql -h localhost -p 5433 -U clarihr -d clarihr_dev -f diagnostico-profundidad-org-units.sql
--
-- No modifica nada. Solo lee.

\set MAX_DEPTH 15

WITH RECURSIVE jerarquia AS (
    -- raíces: sin padre
    SELECT
        ou.id,
        ou.tenant_id,
        ou.code,
        ou.name,
        ou.parent_id,
        ou.is_active,
        1                        AS profundidad,
        ou.code::text            AS ruta
    FROM org_units ou
    WHERE ou.parent_id IS NULL

    UNION ALL

    SELECT
        h.id,
        h.tenant_id,
        h.code,
        h.name,
        h.parent_id,
        h.is_active,
        p.profundidad + 1,
        p.ruta || ' > ' || h.code
    FROM org_units h
    JOIN jerarquia p ON h.parent_id = p.id
    -- corta por si hay datos cíclicos: sin esto la consulta no termina
    WHERE p.profundidad < 100
)
SELECT
    tenant_id                                        AS empresa,
    COUNT(*)                                         AS unidades_sobre_el_limite,
    COUNT(*) FILTER (WHERE is_active)                AS de_ellas_activas,
    MAX(profundidad)                                 AS profundidad_maxima,
    MIN(profundidad)                                 AS primera_profundidad_invalida
FROM jerarquia
WHERE profundidad > :MAX_DEPTH
GROUP BY tenant_id
ORDER BY profundidad_maxima DESC;

-- Detalle: qué unidades concretas hay que bajar, con su ruta completa desde la raíz.
WITH RECURSIVE jerarquia AS (
    SELECT ou.id, ou.tenant_id, ou.code, ou.name, ou.parent_id, ou.is_active,
           1 AS profundidad, ou.code::text AS ruta
    FROM org_units ou
    WHERE ou.parent_id IS NULL
    UNION ALL
    SELECT h.id, h.tenant_id, h.code, h.name, h.parent_id, h.is_active,
           p.profundidad + 1, p.ruta || ' > ' || h.code
    FROM org_units h
    JOIN jerarquia p ON h.parent_id = p.id
    WHERE p.profundidad < 100
)
SELECT
    tenant_id      AS empresa,
    profundidad,
    code,
    name,
    is_active      AS activa,
    ruta
FROM jerarquia
WHERE profundidad > :MAX_DEPTH
ORDER BY tenant_id, profundidad, code;

-- Cómo leer el resultado:
--
--   0 filas  → nada que hacer, el guard se puede desplegar sin más.
--
--   Con filas → cada una es una unidad que hoy vive por encima del límite. Con el guard puesto,
--   ninguna de ellas ni sus ancestros podrán reorganizarse hasta que la rama baje de 15 niveles.
--   La columna `ruta` dice exactamente por dónde cortar; `primera_profundidad_invalida` dice cuánto
--   hay que subir la rama para que vuelva a ser válida.
--
-- Nota: el límite vive en `OrgUnitValidationRules.MaxDepth`. Si cambia allí, cambiar `MAX_DEPTH` aquí.
