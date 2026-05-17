using System.Globalization;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.Features.JobProfiles;

/// <summary>
/// Builds the format-agnostic <see cref="DocumentModel"/> for a job profile.
/// All formatting, ordering, placeholder and empty-state rules live here (moved
/// out of the QuestPDF renderer per technical-debt doc 01 §2.2) so every output
/// format renders identical content.
/// </summary>
public interface IJobProfileDocumentMapper
{
    DocumentModel Map(JobProfilePrintResponse payload);
}

public sealed class JobProfileDocumentMapper : IJobProfileDocumentMapper
{
    private const string EmptyValuePlaceholder = "—";
    private const string EmptyStateText = "Sin información registrada.";

    public DocumentModel Map(JobProfilePrintResponse payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var profile = payload.Profile;
        var sections = new List<DocumentSection>
        {
            BuildGeneralInformation(profile),
        };

        AddOptionalParagraph(sections, "Objetivo", profile.Objective);
        AddOptionalParagraph(sections, "Responsabilidades", profile.Responsibilities);
        AddOptionalParagraph(sections, "Recursos asignados", profile.AssignedResources);
        AddOptionalParagraph(sections, "Alcance de decisión", profile.DecisionScope);

        sections.Add(BuildFunctions(profile.Functions));
        sections.Add(BuildRequirements(profile.Requirements));
        sections.Add(BuildCompetencies(profile.Competencies));
        sections.Add(BuildTrainings(profile.Trainings));
        sections.Add(BuildItemsWithSummary("Beneficios", profile.Benefits, profile.BenefitsSummary,
            static b => b.SortOrder, static b => new BulletItem(b.Name, NullIfBlank(b.Notes))));
        sections.Add(BuildItemsWithSummary("Condiciones laborales", profile.WorkingConditions, profile.WorkingConditionSummary,
            static c => c.SortOrder, static c => new BulletItem(c.Name, NullIfBlank(c.Notes))));

        var compensation = BuildCompensation(profile.Compensation, profile.MarketSalaryReference);
        if (compensation is not null)
        {
            sections.Add(compensation);
        }

        sections.Add(BuildRelations(profile.Relations));
        sections.Add(BuildDependentPositions(profile.DependentPositions));

        var header = new[]
        {
            new DocumentField("Código", profile.Code),
            new DocumentField("Versión", profile.Version.ToString(CultureInfo.InvariantCulture)),
            new DocumentField("Estado", FormatStatus(profile.Status)),
        };

        return new DocumentModel(
            profile.Title,
            header,
            payload.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture),
            sections);
    }

    private static DocumentSection BuildGeneralInformation(JobProfileResponse profile) =>
        new("Información general",
        [
            new KeyValueBlock(
            [
                KeyValue("Unidad organizativa", profile.OrgUnitName),
                KeyValue("Reporta a", FormatReportsTo(profile)),
                KeyValue("Vigencia desde", FormatNullableDate(profile.EffectiveFromUtc)),
                KeyValue("Vigencia hasta", FormatNullableDate(profile.EffectiveToUtc)),
                KeyValue("Activo", profile.IsActive ? "Sí" : "No"),
            ]),
        ]);

    private static void AddOptionalParagraph(List<DocumentSection> sections, string title, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        sections.Add(new DocumentSection(title, [new ParagraphBlock(value)]));
    }

    private static DocumentSection BuildFunctions(IReadOnlyCollection<JobProfileFunctionResponse> functions)
    {
        if (functions.Count == 0)
        {
            return EmptySection("Funciones");
        }

        return new DocumentSection("Funciones",
        [
            new TableBlock(
                [DocumentTableColumn.Constant("Tipo", 80), DocumentTableColumn.Relative("Descripción")],
                functions.OrderBy(static f => f.SortOrder)
                    .Select(IReadOnlyList<string> (f) => [FormatFunctionType(f.FunctionType), f.Description])
                    .ToArray()),
        ]);
    }

    private static DocumentSection BuildRequirements(IReadOnlyCollection<JobProfileRequirementResponse> requirements)
    {
        if (requirements.Count == 0)
        {
            return EmptySection("Requisitos");
        }

        return new DocumentSection("Requisitos",
        [
            new TableBlock(
                [DocumentTableColumn.Constant("Tipo", 110), DocumentTableColumn.Relative("Descripción")],
                requirements.OrderBy(static r => r.SortOrder)
                    .Select(IReadOnlyList<string> (r) => [FormatRequirementType(r.RequirementType), r.Description])
                    .ToArray()),
        ]);
    }

    private static DocumentSection BuildCompetencies(IReadOnlyCollection<JobProfileCompetencyResponse> competencies)
    {
        if (competencies.Count == 0)
        {
            return EmptySection("Competencias");
        }

        return new DocumentSection("Competencias",
        [
            new TableBlock(
                [
                    DocumentTableColumn.Relative("Pirámide", 2),
                    DocumentTableColumn.Relative("Competencia", 3),
                    DocumentTableColumn.Relative("Nivel esperado", 2),
                    DocumentTableColumn.Relative("Conductas / Evidencia", 3),
                ],
                competencies.OrderBy(static c => c.SortOrder)
                    .Select(IReadOnlyList<string> (c) =>
                    [
                        c.OccupationalPyramidLevelName,
                        $"{c.CompetencyName} ({c.CompetencyTypeName})",
                        c.BehaviorLevelName,
                        BuildCompetencyEvidence(c),
                    ])
                    .ToArray()),
        ]);
    }

    private static DocumentSection BuildTrainings(IReadOnlyCollection<JobProfileTrainingResponse> trainings)
    {
        if (trainings.Count == 0)
        {
            return EmptySection("Entrenamientos sugeridos");
        }

        return new DocumentSection("Entrenamientos sugeridos",
        [
            new BulletListBlock(trainings.OrderBy(static t => t.SortOrder)
                .Select(static t => new BulletItem(t.Name, NullIfBlank(t.Notes)))
                .ToArray()),
        ]);
    }

    private static DocumentSection BuildItemsWithSummary<TItem>(
        string title,
        IReadOnlyCollection<TItem> items,
        string? summary,
        Func<TItem, int> sortKey,
        Func<TItem, BulletItem> toBullet)
    {
        var blocks = new List<DocumentBlock>();
        var hasSummary = !string.IsNullOrWhiteSpace(summary);

        if (hasSummary)
        {
            blocks.Add(new MutedTextBlock(summary!));
        }

        if (items.Count == 0)
        {
            if (!hasSummary)
            {
                blocks.Add(new MutedTextBlock(EmptyStateText));
            }

            return new DocumentSection(title, blocks);
        }

        blocks.Add(new BulletListBlock(items.OrderBy(sortKey).Select(toBullet).ToArray()));
        return new DocumentSection(title, blocks);
    }

    private static DocumentSection? BuildCompensation(
        JobProfileCompensationResponse? compensation,
        string? marketReference)
    {
        if (compensation is null && string.IsNullOrWhiteSpace(marketReference))
        {
            return null;
        }

        var blocks = new List<DocumentBlock>();

        if (compensation is not null)
        {
            blocks.Add(new KeyValueBlock(
            [
                KeyValue("Clase salarial", compensation.SalaryClassName),
                KeyValue("Escala", compensation.SalaryScaleCode),
                KeyValue("Moneda", compensation.CurrencyCode),
                KeyValue("Salario base", FormatNullableDecimal(compensation.BaseAmount)),
                KeyValue("Mínimo", FormatNullableDecimal(compensation.MinAmount)),
                KeyValue("Máximo", FormatNullableDecimal(compensation.MaxAmount)),
                KeyValue("Vigente desde", FormatNullableDate(compensation.ResolvedEffectiveFromUtc)),
                KeyValue("Vigente hasta", FormatNullableDate(compensation.ResolvedEffectiveToUtc)),
            ]));
        }

        if (!string.IsNullOrWhiteSpace(marketReference))
        {
            blocks.Add(new LabeledParagraphBlock("Referencia de mercado: ", marketReference));
        }

        return new DocumentSection("Compensación", blocks);
    }

    private static DocumentSection BuildRelations(IReadOnlyCollection<JobProfileRelationResponse> relations)
    {
        if (relations.Count == 0)
        {
            return EmptySection("Relaciones");
        }

        return new DocumentSection("Relaciones",
        [
            new TableBlock(
                [
                    DocumentTableColumn.Constant("Tipo", 70),
                    DocumentTableColumn.Relative("Contraparte", 3),
                    DocumentTableColumn.Relative("Notas", 4),
                ],
                relations.OrderBy(static r => r.SortOrder)
                    .Select(IReadOnlyList<string> (r) =>
                        [FormatRelationType(r.RelationType), r.Counterpart, r.Notes ?? EmptyValuePlaceholder])
                    .ToArray()),
        ]);
    }

    private static DocumentSection BuildDependentPositions(
        IReadOnlyCollection<JobProfileDependentPositionResponse> positions)
    {
        if (positions.Count == 0)
        {
            return EmptySection("Posiciones dependientes");
        }

        return new DocumentSection("Posiciones dependientes",
        [
            new TableBlock(
                [
                    DocumentTableColumn.Constant("Código", 90),
                    DocumentTableColumn.Relative("Título", 4),
                    DocumentTableColumn.Constant("Cantidad", 60),
                ],
                positions
                    .Select(IReadOnlyList<string> (p) =>
                    [
                        p.DependentJobProfileCode,
                        p.DependentJobProfileTitle,
                        p.Quantity.ToString(CultureInfo.InvariantCulture),
                    ])
                    .ToArray()),
        ]);
    }

    private static DocumentSection EmptySection(string title) =>
        new(title, [new MutedTextBlock(EmptyStateText)]);

    private static DocumentField KeyValue(string label, string? value) =>
        new(label, string.IsNullOrWhiteSpace(value) ? EmptyValuePlaceholder : value);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string FormatStatus(JobProfileStatus status) => status.ToString();

    private static string FormatFunctionType(JobFunctionType type) => type switch
    {
        JobFunctionType.General => "General",
        JobFunctionType.Specific => "Específica",
        _ => type.ToString()
    };

    private static string FormatRequirementType(JobRequirementType type) => type switch
    {
        JobRequirementType.Education => "Educación",
        JobRequirementType.Experience => "Experiencia",
        JobRequirementType.Knowledge => "Conocimiento",
        JobRequirementType.Certification => "Certificación",
        JobRequirementType.Other => "Otro",
        _ => type.ToString()
    };

    private static string FormatRelationType(JobRelationType type) => type switch
    {
        JobRelationType.Internal => "Interna",
        JobRelationType.External => "Externa",
        _ => type.ToString()
    };

    private static string FormatReportsTo(JobProfileResponse profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ReportsToJobProfileTitle) &&
            string.IsNullOrWhiteSpace(profile.ReportsToJobProfileCode))
        {
            return EmptyValuePlaceholder;
        }

        var title = profile.ReportsToJobProfileTitle ?? string.Empty;
        var code = profile.ReportsToJobProfileCode;
        return string.IsNullOrWhiteSpace(code) ? title : $"{title} ({code})";
    }

    private static string FormatNullableDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : EmptyValuePlaceholder;

    private static string FormatNullableDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString("N2", CultureInfo.InvariantCulture) : EmptyValuePlaceholder;

    private static string BuildCompetencyEvidence(JobProfileCompetencyResponse competency)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(competency.ExpectedEvidence))
        {
            parts.Add(competency.ExpectedEvidence!);
        }

        if (competency.Conducts.Count > 0)
        {
            var conducts = competency.Conducts
                .OrderBy(static c => c.SortOrder)
                .Select(static c => $"• {c.Description}");
            parts.Add(string.Join("\n", conducts));
        }

        return parts.Count == 0 ? EmptyValuePlaceholder : string.Join("\n", parts);
    }
}
