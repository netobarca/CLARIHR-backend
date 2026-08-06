-- ============================================================================
-- Verificación del perfil legal patronal y los datos de la empresa
--
-- SOLO LECTURA: no modifica nada. Se puede correr en cualquier ambiente.
--
--   psql "<connection-string>" -f verificar-perfil-legal.sql
--
-- Recorre TODAS las empresas del tenant. Si tenés varias y querés una sola,
-- descomentá el filtro marcado como FILTRO OPCIONAL en la consulta 1.
--
-- Las reglas de formato replican las que valida la API desde 2026-08-03. Los
-- registros creados ANTES de ese cambio pudieron guardarse mal formados: esta
-- consulta es la forma de encontrarlos.
-- ============================================================================


-- ─────────────────────────────────────────────────────────────────────────────
-- 1. VEREDICTO — una fila por verificación
-- ─────────────────────────────────────────────────────────────────────────────
WITH empresas AS (
    SELECT c.public_id, c.name, c.slug, c.country_code, c.status
    FROM companies c
    -- FILTRO OPCIONAL: descomentá y ajustá para mirar una sola empresa
    -- WHERE c.slug = 'empresa-1'
),
datos AS (
    SELECT
        e.public_id      AS empresa_id,
        e.name           AS empresa,
        p.id             AS perfil_id,
        p.legal_name,
        p.employer_nit_number          AS nit,
        p.isss_employer_registration_number AS isss,
        p.fiscal_address,
        p.economic_activity_description AS actividad,
        p.legal_representative_public_id AS rep_id,
        p.created_utc                  AS perfil_creado,
        p.modified_utc                 AS perfil_modificado,
        pref.time_zone,
        pref.currency_code,
        pref.payroll_compliance_gates_enabled AS gates,
        lr.id            AS rep_row,
        lr.full_name     AS rep_nombre,
        lr.is_active     AS rep_activo,
        lr.tenant_id     AS rep_tenant
    FROM empresas e
    LEFT JOIN company_legal_profiles p  ON p.tenant_id    = e.public_id
    LEFT JOIN company_preferences   pref ON pref.tenant_id = e.public_id
    LEFT JOIN legal_representatives lr  ON lr.public_id   = p.legal_representative_public_id
),
checks AS (
    SELECT empresa, 1 AS orden, 'Perfil legal existe' AS verificacion,
           CASE WHEN perfil_id IS NULL THEN 'FALTA' ELSE 'OK' END AS estado,
           COALESCE(legal_name, 'la empresa no tiene perfil legal configurado') AS detalle
    FROM datos

    UNION ALL SELECT empresa, 2, 'NIT patronal con formato válido',
           CASE WHEN perfil_id IS NULL THEN 'n/a'
                WHEN nit ~ '^\d{4}-\d{6}-\d{3}-\d$' THEN 'OK'
                ELSE 'PROBLEMA' END,
           COALESCE(nit, '—') || '   (esperado ####-######-###-#)'
    FROM datos

    UNION ALL SELECT empresa, 3, 'Registro patronal ISSS con formato válido',
           CASE WHEN perfil_id IS NULL THEN 'n/a'
                WHEN isss ~ '^[0-9-]{6,20}$' THEN 'OK'
                ELSE 'PROBLEMA' END,
           COALESCE(isss, '—') || '   (solo dígitos y guiones, 6 a 20)'
    FROM datos

    UNION ALL SELECT empresa, 4, 'Campos sin espacios sobrantes',
           CASE WHEN perfil_id IS NULL THEN 'n/a'
                WHEN nit <> btrim(nit) OR isss <> btrim(isss)
                  OR legal_name <> btrim(legal_name) OR fiscal_address <> btrim(fiscal_address)
                THEN 'PROBLEMA' ELSE 'OK' END,
           'un valor con espacios al inicio o al final se imprime así en el F-14'
    FROM datos

    UNION ALL SELECT empresa, 5, 'Razón social parece real',
           CASE WHEN perfil_id IS NULL THEN 'n/a'
                WHEN lower(legal_name) ~ '(prueba|demo|test|ejemplo|empresa 1|acme|xxx)' THEN 'REVISAR'
                WHEN length(btrim(legal_name)) < 8 THEN 'REVISAR'
                ELSE 'OK' END,
           COALESCE(legal_name, '—')
    FROM datos

    UNION ALL SELECT empresa, 6, 'Dirección fiscal parece real',
           CASE WHEN perfil_id IS NULL THEN 'n/a'
                WHEN length(btrim(fiscal_address)) < 15 THEN 'REVISAR'
                WHEN lower(fiscal_address) ~ '(prueba|demo|test|ejemplo)' THEN 'REVISAR'
                ELSE 'OK' END,
           COALESCE(fiscal_address, '—')
    FROM datos

    UNION ALL SELECT empresa, 7, 'Actividad económica registrada',
           CASE WHEN perfil_id IS NULL THEN 'n/a'
                WHEN actividad IS NULL OR btrim(actividad) = '' THEN 'VACÍO'
                ELSE 'OK' END,
           COALESCE(actividad, 'opcional, pero sale en los reportes')
    FROM datos

    UNION ALL SELECT empresa, 8, 'Representante legal enlazado',
           CASE WHEN perfil_id IS NULL THEN 'n/a'
                WHEN rep_id IS NULL           THEN 'SIN ENLAZAR'
                WHEN rep_row IS NULL          THEN 'PROBLEMA'   -- GUID que no resuelve
                WHEN rep_tenant <> empresa_id THEN 'PROBLEMA'   -- pertenece a otra empresa
                WHEN NOT rep_activo           THEN 'PROBLEMA'   -- dado de baja
                ELSE 'OK' END,
           CASE WHEN rep_id IS NULL  THEN 'ningún representante enlazado; los reportes salen sin firmante'
                WHEN rep_row IS NULL THEN 'el id ' || rep_id || ' no corresponde a ningún representante'
                WHEN rep_tenant <> empresa_id THEN 'el representante pertenece a OTRA empresa'
                WHEN NOT rep_activo  THEN rep_nombre || ' está inactivo'
                ELSE rep_nombre END
    FROM datos

    UNION ALL SELECT empresa, 9, 'Zona horaria',
           CASE WHEN time_zone IS NULL THEN 'FALTA'
                WHEN country_code = 'SV' AND time_zone <> 'America/El_Salvador' THEN 'REVISAR'
                ELSE 'OK' END,
           COALESCE(time_zone, '—') || CASE WHEN country_code = 'SV'
                THEN '   (para El Salvador se espera America/El_Salvador)' ELSE '' END
    FROM datos JOIN empresas e2 ON e2.public_id = datos.empresa_id

    UNION ALL SELECT empresa, 10, 'Moneda',
           CASE WHEN currency_code IS NULL THEN 'FALTA'
                WHEN currency_code = 'USD' THEN 'OK' ELSE 'REVISAR' END,
           COALESCE(currency_code, '—')
    FROM datos

    UNION ALL SELECT empresa, 11, 'Gate de cumplimiento de planilla',
           'INFO',
           CASE WHEN gates IS TRUE THEN 'ENCENDIDO — sin perfil legal no se puede generar nómina'
                ELSE 'apagado — la falta de perfil legal todavía no bloquea nada' END
    FROM datos
)
SELECT empresa,
       verificacion,
       estado,
       detalle
FROM checks
ORDER BY empresa, orden;


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. CONTENIDO COMPLETO DEL PERFIL LEGAL — para revisar valor por valor
-- ─────────────────────────────────────────────────────────────────────────────
SELECT c.name                                AS empresa,
       p.public_id                           AS perfil_public_id,
       p.legal_name                          AS razon_social,
       p.employer_nit_number                 AS nit_patronal,
       p.isss_employer_registration_number    AS registro_isss,
       p.fiscal_address                      AS direccion_fiscal,
       p.economic_activity_description        AS actividad_economica,
       p.legal_representative_public_id       AS representante_id,
       p.concurrency_token,
       p.created_utc                         AS creado,
       p.modified_utc                        AS modificado
FROM company_legal_profiles p
JOIN companies c ON c.public_id = p.tenant_id
ORDER BY c.name;


-- ─────────────────────────────────────────────────────────────────────────────
-- 3. REPRESENTANTES LEGALES DE CADA EMPRESA
--    Invariante: como máximo UNO principal activo por empresa
--    (índice único parcial ux_legal_representatives__tenant_primary_active).
--    La columna `enlazado_al_perfil` marca cuál está referenciado.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT c.name                          AS empresa,
       lr.full_name                    AS representante,
       lr.document_type || ' ' || lr.document_number AS documento,
       lr.position_title               AS cargo,
       lr.is_primary                   AS es_principal,
       lr.is_active                    AS activo,
       lr.effective_from_utc           AS vigente_desde,
       lr.effective_to_utc             AS vigente_hasta,
       (p.legal_representative_public_id = lr.public_id) AS enlazado_al_perfil,
       CASE WHEN lower(lr.full_name) ~ '(prueba|demo|test|tempora|ejemplo)'
                 OR length(btrim(lr.full_name)) < 6
            THEN 'REVISAR: parece dato de relleno' ELSE '' END AS observacion
FROM legal_representatives lr
JOIN companies c ON c.public_id = lr.tenant_id
LEFT JOIN company_legal_profiles p ON p.tenant_id = c.public_id
ORDER BY c.name, lr.is_active DESC, lr.is_primary DESC NULLS LAST, lr.full_name;


-- ─────────────────────────────────────────────────────────────────────────────
-- 4. INTEGRIDAD — debe devolver CERO filas
--    Si alguna aparece, hay datos que la API nueva ya no permitiría crear.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT 'perfil legal huérfano (sin empresa)' AS anomalia,
       p.public_id::text AS referencia
FROM company_legal_profiles p
LEFT JOIN companies c ON c.public_id = p.tenant_id
WHERE c.public_id IS NULL

UNION ALL
SELECT 'representante enlazado que no existe',
       p.tenant_id::text || ' → ' || p.legal_representative_public_id::text
FROM company_legal_profiles p
LEFT JOIN legal_representatives lr ON lr.public_id = p.legal_representative_public_id
WHERE p.legal_representative_public_id IS NOT NULL
  AND lr.public_id IS NULL

UNION ALL
SELECT 'representante enlazado de OTRA empresa',
       p.tenant_id::text || ' → ' || lr.tenant_id::text
FROM company_legal_profiles p
JOIN legal_representatives lr ON lr.public_id = p.legal_representative_public_id
WHERE lr.tenant_id <> p.tenant_id

UNION ALL
SELECT 'representante enlazado inactivo',
       p.tenant_id::text || ' → ' || lr.full_name
FROM company_legal_profiles p
JOIN legal_representatives lr ON lr.public_id = p.legal_representative_public_id
WHERE NOT lr.is_active

UNION ALL
SELECT 'NIT con formato inválido',
       p.tenant_id::text || ' → ' || p.employer_nit_number
FROM company_legal_profiles p
WHERE p.employer_nit_number !~ '^\d{4}-\d{6}-\d{3}-\d$'

UNION ALL
SELECT 'registro ISSS con formato inválido',
       p.tenant_id::text || ' → ' || p.isss_employer_registration_number
FROM company_legal_profiles p
WHERE p.isss_employer_registration_number !~ '^[0-9-]{6,20}$'

UNION ALL
SELECT 'más de un representante principal activo',
       lr.tenant_id::text || ' → ' || count(*)::text
FROM legal_representatives lr
WHERE lr.is_primary AND lr.is_active
GROUP BY lr.tenant_id
HAVING count(*) > 1;
