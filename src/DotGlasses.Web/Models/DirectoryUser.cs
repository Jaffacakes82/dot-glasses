namespace DotGlasses.Web.Models;

public record DirectoryUser(string Name, string Role, IReadOnlyList<string> Scope, string Status, string LastLogin, string Sales);
