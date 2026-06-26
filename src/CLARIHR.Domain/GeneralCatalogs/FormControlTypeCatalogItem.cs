using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.GeneralCatalogs;

/// <summary>
/// The kind of value a form control captures, used by the exit-interview form builder to validate field
/// definitions and answers.
/// </summary>
public enum FormControlValueKind
{
    Text = 0,
    Number = 1,
    Date = 2,
    Boolean = 3,
    Options = 4,
}

/// <summary>
/// Closed, system-governed catalog of form control types for the exit-interview builder
/// (general-catalogs key <c>form-control-types</c>). Each type declares the metadata the backend uses to
/// validate field coherence and the frontend uses to render: value kind, whether it supports options
/// (lists/radio/multi), a numeric range, and multiple selection. Seeded via HasData; not tenant-editable.
/// </summary>
public sealed class FormControlTypeCatalogItem : GeneralCatalogItem
{
    private FormControlTypeCatalogItem()
    {
    }

    private FormControlTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        FormControlValueKind valueKind,
        bool supportsOptions,
        bool supportsRange,
        bool supportsMultiple)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        ValueKind = valueKind;
        SupportsOptions = supportsOptions;
        SupportsRange = supportsRange;
        SupportsMultiple = supportsMultiple;
    }

    public FormControlValueKind ValueKind { get; private set; }

    public bool SupportsOptions { get; private set; }

    public bool SupportsRange { get; private set; }

    public bool SupportsMultiple { get; private set; }

    public static FormControlTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        FormControlValueKind valueKind,
        bool supportsOptions,
        bool supportsRange,
        bool supportsMultiple) =>
        new(
            Guid.NewGuid(),
            countryCatalogItemId,
            countryCode,
            code,
            name,
            isActive,
            sortOrder,
            valueKind,
            supportsOptions,
            supportsRange,
            supportsMultiple);
}
