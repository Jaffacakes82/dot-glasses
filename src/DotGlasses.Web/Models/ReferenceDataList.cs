namespace DotGlasses.Web.Models;

public record ReferenceDataList(string Name, string ScopeNote, IReadOnlyList<string> Options);
