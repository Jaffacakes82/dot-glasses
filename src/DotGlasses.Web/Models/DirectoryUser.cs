namespace DotGlasses.Web.Models;

public record DirectoryUser(Guid Id, string Name, string Role, IReadOnlyList<string> Scope, string Status, string LastLogin, string Sales, bool CanManage);
