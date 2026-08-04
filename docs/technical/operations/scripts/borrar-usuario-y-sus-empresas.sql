-- =============================================================================
-- Borra por completo un usuario, las empresas de las que es dueño y TODOS los
-- datos de esos tenants, para poder registrarlo de cero con el mismo correo.
--
-- SOLO PARA ENTORNOS DE DESARROLLO / PRUEBA. Es irreversible.
--
-- NO requiere privilegios de superusuario.
--
-- Por qué no basta con `DELETE FROM auth_users`:
--   · Las tablas de negocio se aíslan por una columna `tenant_id uuid` que NO es
--     llave foránea hacia `companies` (147 tablas la tienen; solo 3 tienen FK, y
--     esas 3 son RESTRICT). Borrar la empresa ni arrastra sus datos ni está
--     permitido mientras existan: deja huérfanos o falla a media ejecución.
--   · `iam_users` referencia al usuario por `linked_user_public_id` (Guid), no
--     por FK, así que tampoco cascadea.
--
-- Cómo resuelve el orden de borrado sin ser superusuario:
--   Recorre las tablas por pasadas. En cada pasada intenta borrar de todas; las
--   que fallan por llave foránea (aún tienen hijos) se saltan y se reintentan en
--   la siguiente, cuando esos hijos ya se fueron. Termina cuando una pasada
--   completa no borra nada. Es un punto fijo: no hay que conocer el grafo de
--   dependencias de 147 tablas ni mantenerlo cuando el esquema cambie.
--
-- Regla de seguridad: una empresa con OTROS miembros NO se borra — solo se quita
-- la membresía del usuario. Evita arrasar el tenant de un compañero en un
-- servidor compartido.
--
-- Ensayo sin aplicar cambios (recomendado la primera vez):
--   ( echo "BEGIN;"; cat este_archivo.sql; echo "ROLLBACK;" ) | psql "<cadena>"
-- =============================================================================

DO $$
DECLARE
    -- ⇩⇩⇩ ÚNICO VALOR A CAMBIAR ⇩⇩⇩
    v_email            text := 'usuario@ejemplo.com';

    v_user_id          bigint;
    v_user_public_id   uuid;
    v_tenants          uuid[];
    v_shared           uuid[];
    v_tabla            record;
    v_borradas         bigint := 0;
    v_pasada           bigint := 0;
    v_total            bigint := 0;
    v_iteracion        int    := 0;
    v_max_iteraciones  int    := 25;
    v_pendientes       text;
BEGIN
    SELECT id, public_id
      INTO v_user_id, v_user_public_id
      FROM auth_users
     WHERE normalized_email = lower(trim(v_email));

    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'No existe ningún usuario con el correo "%"', v_email;
    END IF;

    RAISE NOTICE 'Usuario: % (id=%, publicId=%)', v_email, v_user_id, v_user_public_id;

    -- Empresas donde este usuario es el único miembro: se eliminan por completo.
    SELECT coalesce(array_agg(c.public_id), '{}')
      INTO v_tenants
      FROM companies c
      JOIN user_companies uc ON uc.company_id = c.id
     WHERE uc.user_id = v_user_id
       AND (SELECT count(*) FROM user_companies x WHERE x.company_id = c.id) = 1;

    -- Empresas compartidas con otros usuarios: se conservan.
    SELECT coalesce(array_agg(c.public_id), '{}')
      INTO v_shared
      FROM companies c
      JOIN user_companies uc ON uc.company_id = c.id
     WHERE uc.user_id = v_user_id
       AND (SELECT count(*) FROM user_companies x WHERE x.company_id = c.id) > 1;

    RAISE NOTICE 'Empresas a ELIMINAR por completo: %', coalesce(array_length(v_tenants, 1), 0);
    RAISE NOTICE 'Empresas CONSERVADAS (tienen otros miembros): %', coalesce(array_length(v_shared, 1), 0);

    -- Si el usuario conserva empresas compartidas, se le retira su acceso IAM en
    -- ellas antes de borrarlo (iam_users no cuelga del usuario por llave foránea).
    IF coalesce(array_length(v_shared, 1), 0) > 0 THEN
        DELETE FROM iam_users
         WHERE linked_user_public_id = v_user_public_id
           AND tenant_id = ANY(v_shared);
    END IF;

    -- El usuario se borra ANTES de limpiar el tenant, no después. Motivo:
    -- `user_companies.role_id` referencia `iam_roles` con RESTRICT y esa tabla no
    -- tiene columna `tenant_id`, así que el bucle de abajo nunca la alcanza y
    -- `iam_roles` quedaría bloqueada para siempre. Borrar el usuario arrastra sus
    -- `user_companies` por CASCADE y libera el bloqueo.
    -- CASCADE se lleva además: user_preferences, refresh tokens, password-reset
    -- tokens, email-verification tokens e invitaciones.
    DELETE FROM auth_users WHERE id = v_user_id;
    RAISE NOTICE 'Usuario eliminado (y sus membresías, tokens y preferencias).';

    IF coalesce(array_length(v_tenants, 1), 0) > 0 THEN
        LOOP
            v_iteracion := v_iteracion + 1;
            v_pasada := 0;

            FOR v_tabla IN
                SELECT c.table_name
                  FROM information_schema.columns c
                  JOIN information_schema.tables t
                    ON t.table_schema = c.table_schema AND t.table_name = c.table_name
                 WHERE c.table_schema = 'public'
                   AND c.column_name  = 'tenant_id'
                   AND t.table_type   = 'BASE TABLE'
                 ORDER BY c.table_name
            LOOP
                BEGIN
                    EXECUTE format(
                        'DELETE FROM public.%I WHERE tenant_id = ANY($1)', v_tabla.table_name)
                      USING v_tenants;
                    GET DIAGNOSTICS v_borradas = ROW_COUNT;

                    IF v_borradas > 0 THEN
                        v_pasada := v_pasada + v_borradas;
                        RAISE NOTICE '  [pasada %] % → % fila(s)', v_iteracion, v_tabla.table_name, v_borradas;
                    END IF;
                EXCEPTION
                    WHEN foreign_key_violation THEN
                        -- Todavía tiene hijos; se reintenta en la próxima pasada.
                        NULL;
                END;
            END LOOP;

            v_total := v_total + v_pasada;
            EXIT WHEN v_pasada = 0;

            IF v_iteracion >= v_max_iteraciones THEN
                RAISE EXCEPTION
                    'No se pudo vaciar el tenant en % pasadas: hay un ciclo de llaves foráneas sin resolver.',
                    v_max_iteraciones;
            END IF;
        END LOOP;

        -- Comprobación: ninguna tabla debe conservar filas de estos tenants.
        SELECT string_agg(t.table_name, ', ')
          INTO v_pendientes
          FROM information_schema.columns c
          JOIN information_schema.tables t
            ON t.table_schema = c.table_schema AND t.table_name = c.table_name
         WHERE c.table_schema = 'public'
           AND c.column_name  = 'tenant_id'
           AND t.table_type   = 'BASE TABLE'
           AND (SELECT count(*) FROM pg_class WHERE relname = t.table_name) > 0
           AND EXISTS (
                SELECT 1 FROM pg_class pc WHERE pc.relname = t.table_name
           )
           AND (xpath('/row/c/text()',
                query_to_xml(format('SELECT count(*) AS c FROM public.%I WHERE tenant_id = ANY(%L)',
                                    t.table_name, v_tenants), false, true, '')))[1]::text::bigint > 0;

        IF v_pendientes IS NOT NULL THEN
            RAISE EXCEPTION 'Quedaron filas de tenant sin borrar en: %', v_pendientes;
        END IF;

        -- Cualquier fila residual de iam_users del usuario (sin tenant en la lista).
        DELETE FROM iam_users WHERE linked_user_public_id = v_user_public_id;

        DELETE FROM companies WHERE public_id = ANY(v_tenants);
        RAISE NOTICE 'Empresas eliminadas: %', coalesce(array_length(v_tenants, 1), 0);
    END IF;

    RAISE NOTICE '----------------------------------------------------';
    RAISE NOTICE 'Listo en % pasada(s). Filas de tenant borradas: %', v_iteracion, v_total;
    RAISE NOTICE 'El correo "%" queda libre para registrarse de nuevo.', v_email;
END $$;
