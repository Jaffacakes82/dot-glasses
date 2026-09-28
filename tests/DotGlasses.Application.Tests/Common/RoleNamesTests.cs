using DotGlasses.Application.Common;

namespace DotGlasses.Application.Tests.Common;

/// <summary>
/// RoleNames.Primary is the one deterministic tie-break UserAccessLoader (computing access) and
/// UserAdminService.ListAsync (rendering User Directory) are both meant to share, so the
/// directory can never show a different role from the one access was computed with — see
/// CLAUDE.md's RBAC section. Pinned here, dependency-free, rather than only through the two real
/// call sites, so the rule itself — alphabetical, ordinal, order of input irrelevant — is
/// checked directly.
/// </summary>
public class RoleNamesTests
{
    [Fact]
    public void Primary_PicksTheAlphabeticallyFirstRole_RegardlessOfInputOrder()
    {
        Assert.Equal(RoleNames.Admin, RoleNames.Primary([RoleNames.User, RoleNames.Admin]));
        Assert.Equal(RoleNames.Admin, RoleNames.Primary([RoleNames.Admin, RoleNames.User]));
    }

    [Fact]
    public void Primary_OfASingleRole_ReturnsThatRole()
    {
        Assert.Equal(RoleNames.User, RoleNames.Primary([RoleNames.User]));
    }

    [Fact]
    public void Primary_OfNoRoles_ReturnsNull()
    {
        Assert.Null(RoleNames.Primary([]));
    }
}
