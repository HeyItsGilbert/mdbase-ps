namespace Mdbase.Core.Links;

/// <summary>
/// The decomposed per-field shape of one <c>collection.links</c> entry (spec Ch.07 "Links"),
/// mirroring #37's <c>ReadDefaults</c> decomposition. <see cref="FieldPath"/> is the declaring
/// field-reference key (spec Ch.07 "Field References"), e.g. <c>assignee</c>, <c>blocks[]</c>,
/// or <c>/relations</c>.
/// </summary>
public sealed record LinkFieldRule
{
    public required string FieldPath { get; init; }

    /// <summary>Required matched type of the resolved target, when declared.</summary>
    public string? TargetType { get; init; }

    /// <summary>When true, an unresolved target on this field is a validation diagnostic.</summary>
    public bool ValidateExists { get; init; }
}
