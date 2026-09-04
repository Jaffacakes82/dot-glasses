using DotGlasses.Domain.Common;

namespace DotGlasses.Application.Tests.Hierarchy;

public class HierarchyPathTests
{
    [Theory]
    [InlineData("/1/")]
    [InlineData("/1/4/12/")]
    [InlineData("/1/40/123/4567/")]
    public void APathOfSlashBracketedIntegerSegmentsIsAccepted(string value)
    {
        Assert.True(HierarchyPath.IsValid(value));
    }

    [Theory]
    [InlineData("/1/4", "no trailing slash")]
    [InlineData("1/4/", "no leading slash")]
    [InlineData("", "empty")]
    [InlineData("/", "no segments")]
    [InlineData("//", "an empty segment")]
    [InlineData("/1//4/", "an empty segment mid-path")]
    [InlineData("/1/a/", "a non-digit segment")]
    [InlineData("/1/-4/", "a signed segment")]
    [InlineData("/1/4 /", "whitespace in a segment")]
    public void APathThatBreaksTheInvariantIsRejected(string value, string why)
    {
        Assert.False(HierarchyPath.IsValid(value), why);
        Assert.False(HierarchyPath.TryParse(value, out _), why);
        Assert.Throws<ArgumentException>(() => HierarchyPath.Parse(value));
    }

    [Fact]
    public void ANullPathIsRejectedRatherThanTreatedAsTheRoot()
    {
        Assert.False(HierarchyPath.IsValid(null));
        Assert.False(HierarchyPath.TryParse(null, out _));
        Assert.Throws<ArgumentException>(() => HierarchyPath.Parse(null));
    }

    [Fact]
    public void MissingTheTrailingSlashIsRejectedRatherThanQuietlyRepaired()
    {
        // The trailing slash is the whole invariant: "/1/4" as a prefix would match "/1/40/".
        // Accepting it and appending one would hide the caller's bug, so it is a rejection.
        Assert.Throws<ArgumentException>(() => HierarchyPath.Parse("/1/4"));
    }

    [Fact]
    public void APathRoundTripsThroughTheStringTheDatabaseStores()
    {
        const string stored = "/1/4/12/";

        var path = HierarchyPath.Parse(stored);

        Assert.Equal(stored, path.Value);
        Assert.Equal(stored, path.ToString());
        Assert.Equal(path, HierarchyPath.Parse(path.ToString()));
    }

    [Fact]
    public void TwoPathsWithTheSameValueAreEqual()
    {
        Assert.Equal(HierarchyPath.Parse("/1/4/"), HierarchyPath.Parse("/1/4/"));
        Assert.NotEqual(HierarchyPath.Parse("/1/4/"), HierarchyPath.Parse("/1/40/"));
    }

    [Theory]
    [InlineData("/1/", 1)]
    [InlineData("/1/4/", 2)]
    [InlineData("/1/40/123/", 3)]
    public void DepthCountsSegmentsNotCharacters(string value, int expectedDepth)
    {
        Assert.Equal(expectedDepth, HierarchyPath.Parse(value).Depth);
    }

    [Fact]
    public void ASiblingSharingLeadingDigitsIsNotInsideTheOtherSubtree()
    {
        // The reason this type exists: "/1/40/" shares every character of the prefix "/1/4" but
        // is an unrelated sibling of "/1/4/".
        var sibling = HierarchyPath.Parse("/1/40/");
        var subtreeRoot = HierarchyPath.Parse("/1/4/");

        Assert.False(sibling.IsSelfOrDescendantOf(subtreeRoot));
        Assert.False(subtreeRoot.IsSelfOrAncestorOf(sibling));
    }

    [Fact]
    public void ARowBeneathANodeIsInsideThatNodesSubtree()
    {
        var row = HierarchyPath.Parse("/1/4/12/");
        var node = HierarchyPath.Parse("/1/4/");

        Assert.True(row.IsSelfOrDescendantOf(node));
        Assert.True(node.IsSelfOrAncestorOf(row));
    }

    [Fact]
    public void TheTwoDirectionsAnswerOppositeQuestionsAboutTheSamePair()
    {
        // Asking the wrong one of these is the bug class the naming exists to prevent, so the
        // answers must differ for an ancestor/descendant pair rather than both being true.
        var ancestor = HierarchyPath.Parse("/1/4/");
        var descendant = HierarchyPath.Parse("/1/4/12/");

        Assert.True(descendant.IsSelfOrDescendantOf(ancestor));
        Assert.False(descendant.IsSelfOrAncestorOf(ancestor));

        Assert.True(ancestor.IsSelfOrAncestorOf(descendant));
        Assert.False(ancestor.IsSelfOrDescendantOf(descendant));
    }

    [Fact]
    public void ANodeIsWithinItsOwnScopeInBothDirections()
    {
        // Scoping includes the caller's own node, so both questions answer true for self.
        var path = HierarchyPath.Parse("/1/4/");

        Assert.True(path.IsSelfOrDescendantOf(path));
        Assert.True(path.IsSelfOrAncestorOf(path));
    }

    [Fact]
    public void AnUnrelatedBranchIsNeitherAboveNorBelow()
    {
        var left = HierarchyPath.Parse("/1/4/12/");
        var right = HierarchyPath.Parse("/1/5/13/");

        Assert.False(left.IsSelfOrDescendantOf(right));
        Assert.False(left.IsSelfOrAncestorOf(right));
    }
}
