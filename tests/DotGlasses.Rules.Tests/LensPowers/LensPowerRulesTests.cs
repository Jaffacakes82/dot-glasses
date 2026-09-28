using DotGlasses.Contracts.Common;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests.LensPowers;

/// <summary>
/// The lens power validity helper and the lens type rule, on their own — the pieces the Admin
/// Portal's add-lens dialog will reuse with its own field names. The consultation requests' use of
/// them, keyed on the request property names, is covered in ConsultationRulesTests.
/// </summary>
public class LensPowerRulesTests
{
    private static readonly LensPowerNames Names = new("Sphere", "Cylinder", "Axis", "Add");

    private static readonly Guid Bifocal = Guid.Parse("00000000-0000-0000-0000-00000000001a");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-00000000001c");

    private static ReferenceDataSnapshot Snapshot() => new(
        [
            new ReferenceItemSnapshot(Bifocal, ReferenceDataCategory.LensType, "Bifocal", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(Other, ReferenceDataCategory.LensType, "Other", IsActive: true, IsOtherOption: true),
        ],
        [], []);

    private static IReadOnlyList<RuleFailure> Check(decimal? sphere, decimal? cylinder = null, decimal? axis = null, decimal? add = null) =>
        LensPowerRules.Check(sphere, cylinder, axis, add, Names).ToList();

    // --- Allowed values -----------------------------------------------------------------------

    [Fact]
    public void ASphereAlone_IsAValidLensPower()
    {
        Assert.Empty(Check(sphere: 1.25m));
    }

    [Fact]
    public void SphereIsRequired()
    {
        var failure = Assert.Single(Check(sphere: null));

        Assert.Equal("Sphere", failure.Key);
        Assert.Equal("Sphere is required.", failure.Message);
    }

    [Theory]
    [InlineData(10.25)]
    [InlineData(-10.25)]
    [InlineData(1.10)]
    public void ASphereOutsideTheAllowedValues_IsRefused(decimal sphere)
    {
        var failure = Assert.Single(Check(sphere));

        Assert.Equal("Sphere", failure.Key);
        Assert.Equal("Sphere must be between -10 and 10 in 0.25 increments.", failure.Message);
    }

    [Theory]
    [InlineData(0.25)]  // positive cylinder
    [InlineData(2.00)]
    [InlineData(-6.25)] // below -6.00
    [InlineData(-8.00)]
    [InlineData(-1.10)] // off-step
    public void ACylinderOutsideTheAllowedValues_IsRefused(decimal cylinder)
    {
        // The axis is left blank on purpose: while the cylinder itself is wrong, "axis required"
        // would be a second message about a value the technician is about to change.
        var failure = Assert.Single(Check(sphere: 0m, cylinder: cylinder));

        Assert.Equal("Cylinder", failure.Key);
        Assert.Equal("Cylinder must be between -6 and 0 in 0.25 increments.", failure.Message);
    }

    [Theory]
    [InlineData(3.25)]
    [InlineData(-0.25)]
    [InlineData(0.10)]
    public void AnAddOutsideTheAllowedValues_IsRefused(decimal add)
    {
        var failure = Assert.Single(Check(sphere: 0m, add: add));

        Assert.Equal("Add", failure.Key);
        Assert.Equal("Add must be between 0 and 3 in 0.25 increments.", failure.Message);
    }

    // --- Axis ---------------------------------------------------------------------------------

    [Fact]
    public void AxisIsRequiredWithACylinder()
    {
        var failure = Assert.Single(Check(sphere: 0m, cylinder: -1.00m));

        Assert.Equal("Axis", failure.Key);
        Assert.Equal("Axis is required when Cylinder isn't 0.00 — choose an axis from 0 to 180.", failure.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    public void ACylinderWithAnAllowedAxis_IsValid(decimal axis)
    {
        Assert.Empty(Check(sphere: 0m, cylinder: -1.00m, axis: axis));
    }

    [Theory]
    [InlineData(null)] // a blank cylinder means 0.00
    [InlineData(0)]
    public void AxisIsRefusedWithoutACylinder(object? cylinder)
    {
        var failure = Assert.Single(Check(sphere: 0m, cylinder: cylinder is null ? null : Convert.ToDecimal(cylinder), axis: 90m));

        Assert.Equal("Axis", failure.Key);
        Assert.Equal("Axis must be empty when Cylinder is 0.00 — an axis only applies to a cylinder.", failure.Message);
    }

    [Theory]
    [InlineData(181)]
    [InlineData(-1)]
    [InlineData(90.5)]
    public void AnAxisOutsideTheAllowedValues_IsRefused(decimal axis)
    {
        var failure = Assert.Single(Check(sphere: 0m, cylinder: -1.00m, axis: axis));

        Assert.Equal("Axis", failure.Key);
        Assert.Equal("Axis must be a whole number of degrees between 0 and 180.", failure.Message);
    }

    // --- Add normalisation --------------------------------------------------------------------

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]      // an add of 0.00 is no add
    [InlineData(1.5, 1.5)]
    public void NormaliseAdd_TreatsZeroAsNoAdd(object? add, object? expected)
    {
        decimal? Dec(object? v) => v is null ? null : Convert.ToDecimal(v);

        Assert.Equal(Dec(expected), LensPowerRules.NormaliseAdd(Dec(add)));
        Assert.Equal(expected is not null, LensPowerRules.HasAdd(Dec(add)));
    }

    [Theory]
    [InlineData(null, false)] // blank means 0.00
    [InlineData(0, false)]
    [InlineData(-0.25, true)]
    public void HasCylinder_TreatsBlankAsZero(object? cylinder, bool expected)
    {
        Assert.Equal(expected, LensPowerRules.HasCylinder(cylinder is null ? null : Convert.ToDecimal(cylinder)));
    }

    // --- Lens type ----------------------------------------------------------------------------

    private static IReadOnlyList<RuleFailure> LensType(bool hasAdd, Guid? lensTypeRefId, string? otherText = null) =>
        LensPowerRules.LensType(hasAdd, lensTypeRefId, otherText, Snapshot(), "LensTypeRefId", "LensTypeOtherText").ToList();

    [Fact]
    public void LensType_IsRequiredWithAnAdd()
    {
        var failure = Assert.Single(LensType(hasAdd: true, lensTypeRefId: null));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("LensTypeRefId is required when an add power is set (two distinct powers on that eye).", failure.Message);
    }

    [Fact]
    public void LensType_IsRefusedWithoutAnAdd()
    {
        var failure = Assert.Single(LensType(hasAdd: false, lensTypeRefId: Bifocal));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("LensTypeRefId/LensTypeOtherText must be empty unless an add power is set.", failure.Message);
    }

    [Fact]
    public void LensType_EmptyWithoutAnAdd_IsSingleVision()
    {
        Assert.Empty(LensType(hasAdd: false, lensTypeRefId: null));
    }

    [Fact]
    public void LensType_OtherNeedsItsText()
    {
        var failure = Assert.Single(LensType(hasAdd: true, lensTypeRefId: Other));

        Assert.Equal("LensTypeOtherText", failure.Key);
        Assert.Equal("LensTypeOtherText is required when LensType is \"Other\".", failure.Message);

        Assert.Empty(LensType(hasAdd: true, lensTypeRefId: Other, otherText: "Trifocal"));
    }

    [Fact]
    public void LensType_MustBeAnActiveLensTypeItem()
    {
        var failure = Assert.Single(LensType(hasAdd: true, lensTypeRefId: Guid.NewGuid()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("LensTypeRefId must reference an existing, active LensType reference-data item.", failure.Message);
    }
}
