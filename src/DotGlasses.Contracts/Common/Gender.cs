namespace DotGlasses.Contracts.Common;

/// <summary>
/// Mirrors DotGlasses.Domain.Enums.Gender. Contracts has its own wire-shape enums rather than
/// referencing Domain directly — DotGlasses.App references only Contracts, and must never
/// transitively pull in Domain/Application (see CLAUDE.md's Architecture rules). Mapped to/from
/// the Domain enum in the Application layer (e.g. GenderMapping).
/// </summary>
public enum Gender
{
    Female = 0,
    Male = 1,
}
