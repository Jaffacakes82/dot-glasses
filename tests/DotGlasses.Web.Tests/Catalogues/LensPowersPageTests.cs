using System.Net;
using System.Text.RegularExpressions;
using DotGlasses.Domain.Enums;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>The read-only Lens powers page (ticket 08): it renders straight off
/// <c>DotGlasses.Rules.LensPowers.LensPowerValues</c> — nothing hard-coded here — and sits behind
/// the same policy as the rest of the Lens Sets screen (<c>PresetCatalogue.Manage</c>).</summary>
public class LensPowersPageTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    [Fact]
    public async Task ThePage_RendersTheAllowedValues_FromTheRulesDefinition()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/Catalogues/LensPowers");

        // Cylinder: 0.00 then -6.00 to -0.25 — the shop sells no positive cylinder. Scoped to the
        // cylinder section specifically, since +2.50 legitimately appears in the sphere/add lists
        // on the same page.
        var cylinderSection = Section(html, "lens-power-cylinder");
        Assert.Contains("-6.00", cylinderSection);
        Assert.DoesNotMatch(@"\+\d", cylinderSection);

        // Sphere: -10.00 to +10.00.
        var sphereSection = Section(html, "lens-power-sphere");
        Assert.Contains("+10.00", sphereSection);
        Assert.Contains("-10.00", sphereSection);

        // Add: 0.00 to +3.00.
        var addSection = Section(html, "lens-power-add");
        Assert.Contains("+3.00", addSection);
        Assert.DoesNotMatch(@"-\d", addSection);

        // Axis: 0 to 180 whole degrees.
        var axisSection = Section(html, "lens-power-axis");
        Assert.Contains("180", axisSection);

        // Pupil distance: 54 to 74 whole millimetres (LensPowerValues.PupilDistanceMm).
        var pupilDistanceSection = Section(html, "lens-power-pupil-distance");
        Assert.Contains(">54<", pupilDistanceSection);
        Assert.Contains(">74<", pupilDistanceSection);
        Assert.DoesNotContain(">75<", pupilDistanceSection);
    }

    [Fact]
    public async Task EachValueList_IsOneScrollableColumn_WithAOneLineRangeSummary()
    {
        var html = WebUtility.HtmlDecode(await factory.CreateAdminClient().GetStringAsync("/Catalogues/LensPowers"));

        // One value per row, in a list that scrolls inside its own box (dg-value-list).
        foreach (var id in new[] { "lens-power-sphere", "lens-power-cylinder", "lens-power-axis", "lens-power-add", "lens-power-pupil-distance" })
        {
            Assert.Matches($"<div id=\"{id}\" class=\"dg-value-list\"", html);
            Assert.Matches(@"^\s*<ul>(\s*<li>[^<]+</li>)+\s*</ul>\s*$", Section(html, id));
        }

        // The summaries are worked out from LensPowerValues' own lists: lowest, highest and step.
        Assert.Contains("-10.00 to +10.00 in steps of 0.25", html);
        Assert.Contains("-6.00 to 0.00 in steps of 0.25", html);
        Assert.Contains("0 to 180 in steps of 1", html);
        Assert.Contains("0.00 to +3.00 in steps of 0.25", html);
        Assert.Contains("54 to 74 in steps of 1", html);
    }

    [Fact]
    public async Task ThePage_StatesTheValidityRules()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/Catalogues/LensPowers");

        Assert.Contains("Axis", html);
        Assert.Contains("cylinder", html);
        Assert.Contains("Lens type", html);
        Assert.Contains("add", html);
    }

    [Fact]
    public async Task ThePage_IsReachable_OnlyByAnAdminAtCountryLevelOrAbove()
    {
        var dgiAdmin = factory.CreateAdminClient(OrganisationLevel.Dgi);
        (await dgiAdmin.GetAsync("/Catalogues/LensPowers")).EnsureSuccessStatusCode();

        // Below Country: an Admin at a reseller cannot reach it, exactly like the rest of the
        // Lens Sets screen (CLAUDE.md — the policy is real server-side, not decorative).
        var resellerAdmin = factory.CreateAdminClient(OrganisationLevel.Intermediate);
        Assert.Equal(HttpStatusCode.Forbidden, (await resellerAdmin.GetAsync("/Catalogues/LensPowers")).StatusCode);

        var outletAdmin = factory.CreateAdminClient(OrganisationLevel.RetailPoint);
        Assert.Equal(HttpStatusCode.Forbidden, (await outletAdmin.GetAsync("/Catalogues/LensPowers")).StatusCode);
    }

    /// <summary>Pulls the inner HTML of a single <c>id="..."</c> element out of the page, so an
    /// assertion about one value list (e.g. cylinder never positive) isn't confused by another
    /// list on the same page that legitimately shares a value (e.g. +2.50 in sphere and add).
    /// Non-greedy and single-level — fine here, since none of these sections nest another.</summary>
    private static string Section(string html, string elementId)
    {
        var match = Regex.Match(html, $"id=\"{elementId}\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No element with id=\"{elementId}\" found.");
        // Razor HTML-encodes "+" as "&#x2B;" — decode so the assertions read the same "+2.50"
        // LensPowerValues.FormatPower produced, not its escaped-for-HTML form.
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
