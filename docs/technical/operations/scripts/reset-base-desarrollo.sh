#!/usr/bin/env bash
#
# Reset de la base de desarrollo — DROP + CREATE, la deja completamente vacía. Sin confirmación.
#
# No siembra nada: los seeds del sistema los carga la API al arrancar, vía MigrateAsync()
# (catálogos globales con HasData) + los cuatro seeders de plataforma de StartupInitializationExtensions.
#
#   ./reset-base-desarrollo.sh              # reset y listo
#   ./reset-base-desarrollo.sh --verificar  # tras levantar la API: confirma que quedó como debe
#
# Conexión (sobrescribible por variable de entorno):
#   PGHOST=localhost  PGPORT=5433  PGUSER=clarihr  PGPASSWORD=clarihr  DB_NAME=clarihr_dev
#
# OJO: el contenedor clarihr-postgres publica el 5433. Si además tenés Postgres.app en 5432,
# son dos instancias distintas — con el puerto equivocado borrás la base equivocada.

set -euo pipefail

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5433}"
PGUSER="${PGUSER:-clarihr}"
PGPASSWORD="${PGPASSWORD:-clarihr}"
DB_NAME="${DB_NAME:-clarihr_dev}"
export PGPASSWORD

if command -v psql >/dev/null 2>&1; then
  psql_run() { psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$@"; }
elif docker ps --format '{{.Names}}' | grep -qx clarihr-postgres; then
  psql_run() { docker exec -e PGPASSWORD="$PGPASSWORD" -i clarihr-postgres psql -U "$PGUSER" "$@"; }
else
  echo "ERROR: no encontré psql ni el contenedor clarihr-postgres." >&2
  exit 1
fi

echo "Base: ${PGUSER}@${PGHOST}:${PGPORT}/${DB_NAME}"

if [ "${1:-}" = "--verificar" ]; then
  migraciones=$(psql_run -d "$DB_NAME" -tAc 'SELECT count(*) FROM "__EFMigrationsHistory"' 2>/dev/null || echo 0)
  tipos=$(psql_run -d "$DB_NAME" -tAc 'SELECT count(*) FROM org_unit_type_catalog_items' 2>/dev/null || echo 0)
  empresas=$(psql_run -d "$DB_NAME" -tAc 'SELECT count(*) FROM companies' 2>/dev/null || echo 0)
  echo "  migraciones aplicadas : ${migraciones}"
  echo "  empresas              : ${empresas}"
  echo "  tipos de unidad       : ${tipos}"
  if [ "$migraciones" -eq 0 ]; then
    echo "❌ Las migraciones no corrieron. ¿Arrancaste la API contra esta base?"; exit 1
  elif [ "$tipos" -ne 0 ]; then
    echo "❌ Hay ${tipos} tipo(s) de unidad sembrados: algo volvió a sembrar catálogos de empresa."; exit 1
  fi
  echo "✅ Migraciones aplicadas y catálogos de empresa vacíos, como debe ser."
  exit 0
fi

psql_run -d postgres -v ON_ERROR_STOP=1 -q -c "
  SELECT pg_terminate_backend(pid) FROM pg_stat_activity
  WHERE datname = '${DB_NAME}' AND pid <> pg_backend_pid();" >/dev/null
psql_run -d postgres -v ON_ERROR_STOP=1 -q -c "DROP DATABASE IF EXISTS ${DB_NAME};"
psql_run -d postgres -v ON_ERROR_STOP=1 -q -c "CREATE DATABASE ${DB_NAME} OWNER ${PGUSER};"

tablas=$(psql_run -d "$DB_NAME" -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'")
echo "✅ Base recreada vacía (tablas: ${tablas})."
echo
echo "Ahora levantá la API para que cargue los seeds del sistema:"
echo "    dotnet run --project src/CLARIHR.Api"
echo
echo "Carga: migraciones + catálogos globales · títulos y tipos de representación de representantes"
echo "legales · descriptores de tipo de catálogo · valores por defecto de los planes. Nada más."
echo "Luego: ./reset-base-desarrollo.sh --verificar   y a registrarte con POST /api/v1/auth/register"
