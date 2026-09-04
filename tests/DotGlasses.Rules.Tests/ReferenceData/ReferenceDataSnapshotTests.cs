using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests.ReferenceData;

/// <summary>
/// Every case here builds its snapshot as a plain literal — no database, no HTTP, no fake to
/// configure. That is the point of the type, and tickets 09–11 test their rules the same way.
/// </summary>
public class ReferenceDataSnapshotTests
{
    private static readonly Guid ActiveReason = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RetiredReason = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherReason = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid NeverExisted = Guid.Parse("99999999-9999-9999-9999-999999999999");

    /// <summary>The server's filling: every item, retired ones carrying IsActive = false.</summary>
    private static ReferenceDataSnapshot ServerSnapshot() => new(
        [
            new ReferenceItemSnapshot(ActiveReason, ReferenceDataCategory.ReasonNotPurchased, "Too expensive", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(RetiredReason, ReferenceDataCategory.ReasonNotPurchased, "Wants to think about it", IsActive: false, IsOtherOption: false),
            new ReferenceItemSnapshot(OtherReason, ReferenceDataCategory.ReasonNotPurchased, "Other", IsActive: true, IsOtherOption: true),
        ],
        [],
        [],
        []);

    /// <summary>The Field App's filling of the same library, through the adapter the App calls:
    /// the API returns active items only, so the retired one is simply absent.</summary>
    private static ReferenceDataSnapshot ClientSnapshot() => ReferenceDataSnapshot.FromCachedReferenceData(
        [
            new ReferenceDataItemDto { Id = ActiveReason, Category = ReferenceDataCategory.ReasonNotPurchased, Label = "Too expensive" },
            new ReferenceDataItemDto { Id = OtherReason, Category = ReferenceDataCategory.ReasonNotPurchased, Label = "Other", IsOtherOption = true },
        ],
        [],
        [],
        []);

    [Fact]
    public void ResolveLabel_KnownItem_ReturnsItsLabel()
    {
        Assert.Equal("Too expensive", ServerSnapshot().ResolveLabel(ActiveReason));
    }

    [Fact]
    public void ResolveLabel_RetiredItem_StillRendersItsLabel()
    {
        // The whole reason the server's adapter loads inactive items: Event History and the
        // conversion form show historical records, and an option retired since must not turn
        // into an em-dash retrospectively.
        Assert.Equal("Wants to think about it", ServerSnapshot().ResolveLabel(RetiredReason));
    }

    [Fact]
    public void ResolveLabel_OtherOptionWithFreeText_PrefersTheFreeText()
    {
        Assert.Equal("Saving for school fees", ServerSnapshot().ResolveLabel(OtherReason, "Saving for school fees"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveLabel_OtherOptionWithoutFreeText_FallsBackToTheStoredLabel(string? otherText)
    {
        Assert.Equal("Other", ServerSnapshot().ResolveLabel(OtherReason, otherText));
    }

    [Fact]
    public void ResolveLabel_NonOtherItemWithStrayFreeText_KeepsTheStoredLabel()
    {
        // Only an "Other" item's free text may win — a stray value on a normal item is ignored
        // rather than shown, matching what every call site did before the collapse.
        Assert.Equal("Too expensive", ServerSnapshot().ResolveLabel(ActiveReason, "ignore me"));
    }

    [Fact]
    public void ResolveLabel_UnknownId_FallsBackToTheEmDash()
    {
        Assert.Equal("—", ServerSnapshot().ResolveLabel(NeverExisted));
        Assert.Equal(ReferenceDataSnapshot.MissingLabel, ServerSnapshot().ResolveLabel(NeverExisted));
    }

    [Fact]
    public void ResolveLabel_NullId_FallsBackToTheEmDash()
    {
        Assert.Equal("—", ServerSnapshot().ResolveLabel(null));
        Assert.Equal("—", ServerSnapshot().ResolveLabel(null, "free text with no item behind it"));
    }

    [Fact]
    public void IsActiveItem_BothFillings_AgreeOnPresentAndActive()
    {
        var server = ServerSnapshot();
        var client = ClientSnapshot();

        // Active: present in both, active in both.
        Assert.True(server.IsActiveItem(ActiveReason, ReferenceDataCategory.ReasonNotPurchased));
        Assert.True(client.IsActiveItem(ActiveReason, ReferenceDataCategory.ReasonNotPurchased));

        // Retired: present-but-inactive on the server, absent on the client — the two adapters
        // disagree about *why*, and must still agree on the answer.
        Assert.False(server.IsActiveItem(RetiredReason, ReferenceDataCategory.ReasonNotPurchased));
        Assert.False(client.IsActiveItem(RetiredReason, ReferenceDataCategory.ReasonNotPurchased));

        // Never existed: absent from both.
        Assert.False(server.IsActiveItem(NeverExisted, ReferenceDataCategory.ReasonNotPurchased));
        Assert.False(client.IsActiveItem(NeverExisted, ReferenceDataCategory.ReasonNotPurchased));
    }

    [Fact]
    public void IsActiveItem_RightIdWrongCategory_IsRejected()
    {
        Assert.False(ServerSnapshot().IsActiveItem(ActiveReason, ReferenceDataCategory.Occupation));
    }

    [Fact]
    public void IsActiveItem_NullId_IsRejected()
    {
        Assert.False(ServerSnapshot().IsActiveItem(null, ReferenceDataCategory.ReasonNotPurchased));
    }

    [Fact]
    public void FindItem_WrongCategory_ReturnsNullEvenThoughTheIdExists()
    {
        var snapshot = ServerSnapshot();

        Assert.NotNull(snapshot.FindItem(ActiveReason));
        Assert.Null(snapshot.FindItem(ActiveReason, ReferenceDataCategory.FrameColour));
    }

    [Fact]
    public void Empty_ResolvesNothingAndRejectsEverything()
    {
        Assert.Equal("—", ReferenceDataSnapshot.Empty.ResolveLabel(ActiveReason));
        Assert.False(ReferenceDataSnapshot.Empty.IsActiveItem(ActiveReason, ReferenceDataCategory.ReasonNotPurchased));
    }

    [Fact]
    public void Constructor_DuplicateIdInALiteral_DoesNotThrow()
    {
        // A hand-written test literal that repeats an id must not blow up before the rule under
        // test ever runs — last one in wins, deliberately.
        var snapshot = new ReferenceDataSnapshot(
            [
                new ReferenceItemSnapshot(ActiveReason, ReferenceDataCategory.ReasonNotPurchased, "First", IsActive: true, IsOtherOption: false),
                new ReferenceItemSnapshot(ActiveReason, ReferenceDataCategory.ReasonNotPurchased, "Second", IsActive: true, IsOtherOption: false),
            ],
            [],
            [],
            []);

        Assert.Equal("Second", snapshot.ResolveLabel(ActiveReason));
    }
}
