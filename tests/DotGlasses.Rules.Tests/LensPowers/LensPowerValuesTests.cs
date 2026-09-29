using DotGlasses.Rules.LensPowers;

namespace DotGlasses.Rules.Tests.LensPowers;

/// <summary>
/// The allowed lens power values, copied from the DOT Glasses online shop's prescription
/// configurator (ADR-0007; research branch <c>research/custom-lens-option-ranges</c>). Every screen
/// and validator reads these lists, so the lists themselves — values, order and display format —
/// are the behaviour under test.
/// </summary>
public class LensPowerValuesTests
{
    [Fact]
    public void Sphere_RunsFromMinusTenToPlusTenInQuarterSteps_WithZeroFirst()
    {
        var sphere = LensPowerValues.Sphere;

        // The shop lists 0.00 first, then -10.00 ascending to +10.00 without repeating 0.
        Assert.Equal(81, sphere.Count);
        Assert.Equal(0.00m, sphere[0]);
        Assert.Equal(-10.00m, sphere[1]);
        Assert.Equal(-9.75m, sphere[2]);
        Assert.Equal(10.00m, sphere[^1]);
        Assert.Single(sphere, v => v == 0m);
        Assert.Equal(sphere.Skip(1).Order(), sphere.Skip(1));
    }

    [Fact]
    public void Cylinder_IsZeroThenMinusSixToMinusAQuarter_NeverPositive()
    {
        var cylinder = LensPowerValues.Cylinder;

        Assert.Equal(25, cylinder.Count);
        Assert.Equal(0.00m, cylinder[0]);
        Assert.Equal(-6.00m, cylinder[1]);
        Assert.Equal(-0.25m, cylinder[^1]);
        Assert.DoesNotContain(cylinder, v => v > 0);
        Assert.Equal(cylinder.Skip(1).Order(), cylinder.Skip(1));
    }

    [Fact]
    public void Axis_IsEveryWholeDegreeFromZeroTo180()
    {
        Assert.Equal(Enumerable.Range(0, 181).Select(d => (decimal)d), LensPowerValues.Axis);
    }

    [Fact]
    public void Add_RunsFromZeroToThreeInQuarterSteps()
    {
        var add = LensPowerValues.Add;

        Assert.Equal(13, add.Count);
        Assert.Equal(0.00m, add[0]);
        Assert.Equal(0.25m, add[1]);
        Assert.Equal(3.00m, add[^1]);
    }

    [Fact]
    public void PupilDistance_IsUnchangedAtWholeMillimetresFrom54To74()
    {
        Assert.Equal(Enumerable.Range(54, 21).Select(mm => (decimal)mm), LensPowerValues.PupilDistanceMm);
    }

    [Theory]
    [InlineData(2.5, "+2.50")]
    [InlineData(0.25, "+0.25")]
    [InlineData(0, "0.00")]       // zero is not positive, so no sign
    [InlineData(-1.25, "-1.25")]
    [InlineData(-10, "-10.00")]
    public void FormatPower_SignsPositiveValuesAndShowsTwoDecimals(decimal value, string expected)
    {
        Assert.Equal(expected, LensPowerValues.FormatPower(value));
    }

    [Theory]
    [InlineData(-10, true)]
    [InlineData(10, true)]
    [InlineData(0, true)]
    [InlineData(-10.25, false)] // outside ±10
    [InlineData(10.25, false)]
    [InlineData(0.30, false)]   // off the quarter step
    public void SphereRange_AllowsExactlyTheListedValues(decimal value, bool allowed)
    {
        Assert.Equal(allowed, LensPowerValues.SphereRange.Allows(value));
        Assert.Equal(allowed, LensPowerValues.Sphere.Contains(value));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-6, true)]
    [InlineData(-0.25, true)]
    [InlineData(0.25, false)]   // positive cylinder
    [InlineData(-6.25, false)]  // below -6.00
    [InlineData(-1.1, false)]   // off-step
    public void CylinderRange_AllowsExactlyTheListedValues(decimal value, bool allowed)
    {
        Assert.Equal(allowed, LensPowerValues.CylinderRange.Allows(value));
        Assert.Equal(allowed, LensPowerValues.Cylinder.Contains(value));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(180, true)]
    [InlineData(181, false)]
    [InlineData(-1, false)]
    [InlineData(90.5, false)]
    public void AxisRange_AllowsExactlyTheListedValues(decimal value, bool allowed)
    {
        Assert.Equal(allowed, LensPowerValues.AxisRange.Allows(value));
        Assert.Equal(allowed, LensPowerValues.Axis.Contains(value));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(3.25, false)]
    [InlineData(-0.25, false)]
    [InlineData(0.1, false)]
    public void AddRange_AllowsExactlyTheListedValues(decimal value, bool allowed)
    {
        Assert.Equal(allowed, LensPowerValues.AddRange.Allows(value));
        Assert.Equal(allowed, LensPowerValues.Add.Contains(value));
    }

    [Theory]
    [InlineData(2.50, null, null, null, "SPH +2.50")]
    [InlineData(2.50, 0.00, null, 0.00, "SPH +2.50")]
    [InlineData(-1.25, -0.75, 90.0, null, "SPH -1.25 · CYL -0.75 × 90")]
    [InlineData(0.00, -0.50, null, 2.00, "SPH 0.00 · CYL -0.50 · ADD +2.00")]
    [InlineData(1.00, null, null, 1.50, "SPH +1.00 · ADD +1.50")]
    public void FormatLensPower_ShowsCylinderAndAddOnlyWhenTheLensHasThem(double sphere, double? cylinder, double? axis, double? add, string expected) =>
        Assert.Equal(expected, LensPowerValues.FormatLensPower((decimal)sphere, (decimal?)cylinder, (decimal?)axis, (decimal?)add));

    [Fact]
    public void PresetPupilDistanceBuckets_AreZeroToFour_OrZeroToTwoOnAChildrensFrame()
    {
        Assert.Equal([0, 1, 2, 3, 4], LensPowerValues.PresetPupilDistanceBuckets(childrensFrame: false));
        Assert.Equal([0, 1, 2], LensPowerValues.PresetPupilDistanceBuckets(childrensFrame: true));
        Assert.Equal(4, LensPowerValues.MaxPresetPupilDistanceBucket(childrensFrame: false));
        Assert.Equal(2, LensPowerValues.MaxPresetPupilDistanceBucket(childrensFrame: true));
    }
}
