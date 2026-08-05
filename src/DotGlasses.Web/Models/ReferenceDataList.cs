using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public record ReferenceDataOption(Guid Id, string Label, string? ImageUrl);

public record ReferenceDataList(
    ReferenceDataCategory Category,
    string Name,
    string ScopeNote,
    bool ShowImageField,
    bool HasActiveOtherOption,
    IReadOnlyList<ReferenceDataOption> ActiveOptions,
    IReadOnlyList<ReferenceDataOption> RetiredOptions);
