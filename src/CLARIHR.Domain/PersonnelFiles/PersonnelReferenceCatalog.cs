using CLARIHR.Domain.Locations;

namespace CLARIHR.Domain.PersonnelFiles;

public static class PersonnelReferenceCatalog
{
    public static IReadOnlyList<PersonnelReferenceCatalogDefinition> Items { get; } = BuildItems();

    private static IReadOnlyList<PersonnelReferenceCatalogDefinition> BuildItems()
    {
        const string countryCode = ElSalvadorTerritorialCatalog.CountryCode;
        var items = new List<PersonnelReferenceCatalogDefinition>();
        var nextId = -9500L;

        AddSimpleCategory(
            items,
            ref nextId,
            countryCode,
            PersonnelReferenceCatalogCategories.MaritalStatus,
            [
                ("SOLTERO_A", "Soltero/a"),
                ("CASADO_A", "Casado/a"),
                ("UNION_NO_MATRIMONIAL", "Union no matrimonial"),
                ("DIVORCIADO_A", "Divorciado/a"),
                ("VIUDO_A", "Viudo/a"),
                ("SEPARADO_A", "Separado/a")
            ]);

        AddSimpleCategory(
            items,
            ref nextId,
            countryCode,
            PersonnelReferenceCatalogCategories.IdentificationType,
            [
                ("DUI", "DUI"),
                ("NIT", "NIT"),
                ("PASSPORT", "Pasaporte"),
                ("RESIDENT_CARD", "Carne de residente")
            ]);

        AddSimpleCategory(
            items,
            ref nextId,
            countryCode,
            PersonnelReferenceCatalogCategories.Profession,
            [
                ("ABOGADO_A", "Abogado/a"),
                ("ADMINISTRADOR_A_DE_EMPRESAS", "Administrador/a de empresas"),
                ("ANALISTA_DE_DATOS", "Analista de datos"),
                ("ARQUITECTO_A", "Arquitecto/a"),
                ("ASISTENTE_ADMINISTRATIVO_A", "Asistente administrativo/a"),
                ("AUDITOR_A", "Auditor/a"),
                ("AUXILIAR_CONTABLE", "Auxiliar contable"),
                ("BODEGUERO_A", "Bodeguero/a"),
                ("CAJERO_A", "Cajero/a"),
                ("COMERCIANTE", "Comerciante"),
                ("CONTADOR_A", "Contador/a"),
                ("DISENADOR_A_GRAFICO_A", "Disenador/a grafico/a"),
                ("DOCENTE", "Docente"),
                ("ECONOMISTA", "Economista"),
                ("ELECTRICISTA", "Electricista"),
                ("ENFERMERO_A", "Enfermero/a"),
                ("ESPECIALISTA_DE_RECURSOS_HUMANOS", "Especialista de recursos humanos"),
                ("INGENIERO_A_AGRONOMO_A", "Ingeniero/a agronomo/a"),
                ("INGENIERO_A_CIVIL", "Ingeniero/a civil"),
                ("INGENIERO_A_INDUSTRIAL", "Ingeniero/a industrial"),
                ("INGENIERO_A_EN_SISTEMAS", "Ingeniero/a en sistemas"),
                ("JEFE_A_DE_OPERACIONES", "Jefe/a de operaciones"),
                ("MEDICO_A", "Medico/a"),
                ("MERCADERISTA", "Mercaderista"),
                ("MOTORISTA", "Motorista"),
                ("ODONTOLOGO_A", "Odontologo/a"),
                ("OPERARIO_A_DE_PRODUCCION", "Operario/a de produccion"),
                ("PERIODISTA", "Periodista"),
                ("PSICOLOGO_A", "Psicologo/a"),
                ("RECEPCIONISTA", "Recepcionista"),
                ("SOLDADOR_A", "Soldador/a"),
                ("SUPERVISOR_A", "Supervisor/a"),
                ("TECNICO_A_DE_MANTENIMIENTO", "Tecnico/a de mantenimiento"),
                ("TECNICO_A_DE_SOPORTE", "Tecnico/a de soporte"),
                ("VENDEDOR_A", "Vendedor/a")
            ]);

        var departmentIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var sortOrder = 10;
        foreach (var department in ElSalvadorTerritorialCatalog.Departments)
        {
            var definition = new PersonnelReferenceCatalogDefinition(
                nextId--,
                countryCode,
                PersonnelReferenceCatalogCategories.Department,
                department.Code,
                department.Name,
                ParentId: null,
                sortOrder);
            items.Add(definition);
            departmentIds[department.Code] = definition.Id;
            sortOrder += 10;
        }

        sortOrder = 10;
        foreach (var municipality in ElSalvadorTerritorialCatalog.Municipalities)
        {
            items.Add(new PersonnelReferenceCatalogDefinition(
                nextId--,
                countryCode,
                PersonnelReferenceCatalogCategories.Municipality,
                municipality.Code,
                municipality.Name,
                departmentIds[municipality.DepartmentCode],
                sortOrder));
            sortOrder += 10;
        }

        AddSimpleCategory(
            items,
            ref nextId,
            countryCode,
            PersonnelReferenceCatalogCategories.Kinship,
            [
                ("CONYUGE", "Conyuge"),
                ("PAREJA", "Pareja"),
                ("PADRE", "Padre"),
                ("MADRE", "Madre"),
                ("HIJO_A", "Hijo/a"),
                ("HERMANO_A", "Hermano/a"),
                ("ABUELO_A", "Abuelo/a"),
                ("NIETO_A", "Nieto/a"),
                ("TIO_A", "Tio/a"),
                ("OTRO", "Otro")
            ]);

        return items;
    }

    private static void AddSimpleCategory(
        ICollection<PersonnelReferenceCatalogDefinition> target,
        ref long nextId,
        string countryCode,
        string category,
        IReadOnlyCollection<(string Code, string Name)> values)
    {
        var sortOrder = 10;
        foreach (var value in values)
        {
            target.Add(new PersonnelReferenceCatalogDefinition(
                nextId--,
                countryCode,
                category,
                value.Code,
                value.Name,
                ParentId: null,
                sortOrder));
            sortOrder += 10;
        }
    }
}

public sealed record PersonnelReferenceCatalogDefinition(
    long Id,
    string CountryCode,
    string Category,
    string Code,
    string Name,
    long? ParentId,
    int SortOrder);

public static class PersonnelReferenceCatalogCategories
{
    public const string Profession = "Profession";
    public const string MaritalStatus = "MaritalStatus";
    public const string IdentificationType = "IdentificationType";
    public const string Kinship = "Kinship";
    public const string Department = "Department";
    public const string Municipality = "Municipality";
    public const string InsuranceType = "InsuranceType";
    public const string InsuranceRange = "InsuranceRange";
}
