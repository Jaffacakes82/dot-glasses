using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using DotGlasses.Web.Tests.LeadConversion;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;
using DomainCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Tests.ReferenceData;

/// <summary>The Admin Portal host with picture storage swapped for one held in memory, so a test
/// can see exactly what was stored and deleted.</summary>
public class FrameColourFactory : AdminPortalFactory
{
    public InMemoryPictureStore Pictures { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddScoped<IReferenceDataPictureStore>(_ => Pictures));
    }
}

public sealed class InMemoryPictureStore : IReferenceDataPictureStore
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();

    public IReadOnlyCollection<string> Names => _blobs.Keys.ToList();

    public byte[]? Bytes(string name) => _blobs.GetValueOrDefault(name);

    public async Task<string> SaveAsync(Stream content, ReferenceDataPictureType type, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var name = ReferenceDataPictures.NewName(type);
        _blobs[name] = buffer.ToArray();
        return name;
    }

    public Task<Stream?> OpenAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(_blobs.TryGetValue(name, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        _blobs.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Frame colours after Spec E: an adult list and a children's list, each with its own pictures;
/// a Sale's colour has to come from the list that matches its "children's frame" tick on both
/// write paths; and a picture is uploaded into private storage and served back by the Admin
/// Portal, never pasted as a web address.
/// </summary>
public class FrameColourTests(FrameColourFactory factory) : IClassFixture<FrameColourFactory>
{
    private const string CallerOutlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    // A real 1x1 PNG, and the leading bytes of a JPEG and a WebP.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    // --- Two lists --------------------------------------------------------------------------------

    [Fact]
    public async Task TheReferenceDataScreen_ListsAdultAndChildFrameColours_EachWithAPictureField()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/ReferenceData");

        Assert.Contains("Frame colours (adult)", html);
        Assert.Contains("Frame colours (child)", html);
        Assert.DoesNotContain("Frame colors", html);
        Assert.DoesNotContain("name=\"ImageUrl\"", html);

        // One upload field on each list's Add form.
        Assert.Equal(2, CountOf(html, "Picture (optional) — PNG, JPEG or WebP, up to 1 MB"));
    }

    [Fact]
    public async Task AnItemAddedToTheChildList_AppearsOnlyThere_AndTheApiReturnsBothLists()
    {
        var admin = factory.CreateAdminClient();
        var label = $"Sunshine Yellow {Guid.NewGuid():N}";

        await PostFormAsync(admin, "/ReferenceData/Create", [("Category", ((int)DomainCategory.FrameColourChild).ToString()), ("Label", label)]);

        var items = await factory.CreateTechnicianClient(CallerOutlet).GetFromJsonAsync<List<ReferenceDataItemDto>>("api/v1/reference-data");
        var added = Assert.Single(items!, i => i.Label == label);
        Assert.Equal(ReferenceDataCategory.FrameColourChild, added.Category);
        Assert.Contains(items!, i => i.Category == ReferenceDataCategory.FrameColour);
        Assert.Contains(items!, i => i.Category == ReferenceDataCategory.FrameColourChild && i.IsOtherOption);
    }

    [Fact]
    public async Task TheChildList_AllowsOnlyOneActiveOther()
    {
        var admin = factory.CreateAdminClient();
        var label = $"Another Other {Guid.NewGuid():N}";

        var response = await PostFormAsync(admin, "/ReferenceData/Create",
            [("Category", ((int)DomainCategory.FrameColourChild).ToString()), ("Label", label), ("IsOtherOption", "true")]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("already has an &quot;Other&quot; option", await response.Content.ReadAsStringAsync());
        Assert.False(Query(db => db.ReferenceDataItems.Any(i => i.Label == label)));
    }

    // --- The colour follows the "children's frame" tick --------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheSaleEndpoint_RefusesAColourFromTheOtherList_AndAcceptsOneFromTheMatchingList(bool childrensFrame)
    {
        var client = factory.CreateTechnicianClient(CallerOutlet);
        var adult = ColourIn(DomainCategory.FrameColour);
        var child = await AddChildColourAsync();

        var wrong = await client.PostAsJsonAsync("api/v1/sales", Sale(childrensFrame, childrensFrame ? adult : child));

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        using var body = JsonDocument.Parse(await wrong.Content.ReadAsStringAsync());
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal("FrameColourRefId", error.Name);
        Assert.Equal(
            childrensFrame ? "Choose a children's frame colour." : "Choose a frame colour.",
            error.Value.EnumerateArray().Single().GetString());

        var right = await client.PostAsJsonAsync("api/v1/sales", Sale(childrensFrame, childrensFrame ? child : adult));
        Assert.True(right.StatusCode == HttpStatusCode.Created, await right.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheConversionForm_OffersBothLists_ShowsTheOneForTheTick_AndRefusesAColourFromTheOther()
    {
        var leadId = SeedLead();
        var admin = factory.CreateAdminClient();
        var adult = ColourIn(DomainCategory.FrameColour);
        var child = await AddChildColourAsync();

        var html = await admin.GetStringAsync($"/Leads/Convert/{leadId}");
        Assert.Contains($"<option value=\"{adult}\" data-for-childrens-frame=\"false\">", html);
        Assert.Contains($"<option value=\"{child}\" data-for-childrens-frame=\"true\" hidden=\"hidden\" disabled=\"disabled\">", html);

        // Children's frame ticked, an adult colour posted: refused, and nothing recorded.
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(admin, $"/Leads/Convert/{leadId}");
        var response = await admin.PostAsync($"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token,
            ("Form.ConsentGiven", "true"),
            ("Form.LensRange", "custom"),
            ("Form.SphereLeft", "1.00"),
            ("Form.SphereRight", "1.00"),
            ("Form.PupilDistanceMm", "62"),
            ("Form.ChildrensFrame", "true"),
            ("Form.CoatingRefIds", ReferenceDataSeedConfiguration.CoatingClearId.ToString()),
            ("Form.FrameColourRefId", adult.ToString())));

        var refused = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choose a children&#x27;s frame colour.", refused);
        Assert.False(Query(db => db.Sales.IgnoreQueryFilters().Any(s => s.SourceLeadId == leadId)));

        // Re-rendered with the tick kept: the children's list is the one on offer now.
        Assert.Contains($"<option value=\"{child}\" data-for-childrens-frame=\"true\">", refused);
    }

    [Fact]
    public async Task ASaleRecordedBeforeTheSplit_StillShowsItsColour()
    {
        // An adult colour on a children's frame is what an older Sale may hold; it is never
        // rewritten, and its label still resolves by id.
        var adult = ColourIn(DomainCategory.FrameColour);
        var customerName = $"Older Sale {Guid.NewGuid():N}";
        factory.Seed(db =>
        {
            var customer = new Customer { Id = Guid.NewGuid(), FullName = customerName, PhoneNumber = "0700000000", HierarchyPath = CallerOutlet };
            db.Customers.Add(customer);
            db.Sales.Add(new Sale
            {
                Id = Guid.NewGuid(), HierarchyPath = CallerOutlet, TechnicianUserId = Guid.NewGuid(), CustomerId = customer.Id,
                ConsentGiven = true, LensRangeType = DotGlasses.Domain.Enums.LensRangeType.Custom, ChildrensFrame = true, FrameColourRefId = adult,
            });
        });

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.CustomerId == db.Customers.IgnoreQueryFilters().Single(c => c.FullName == customerName).Id));
        Assert.Equal(adult, sale.FrameColourRefId);

        var html = await factory.CreateAdminClient().GetStringAsync($"/EventHistory?tab=sales&search={Uri.EscapeDataString(customerName)}");
        Assert.Contains(customerName, html);
    }

    // --- Uploading a picture ----------------------------------------------------------------------

    [Fact]
    public async Task AnUploadedPicture_IsStoredUnderAGeneratedName_AndServedBackByteForByte()
    {
        var admin = factory.CreateAdminClient();
        var label = $"Uploaded {Guid.NewGuid():N}";

        var response = await PostFormAsync(admin, "/ReferenceData/Create",
            [("Category", ((int)DomainCategory.FrameColour).ToString()), ("Label", label)],
            ("Picture", "anything-at-all.png", "image/png", Png));
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var imageUrl = Query(db => db.ReferenceDataItems.Single(i => i.Label == label).ImageUrl)!;
        Assert.Matches("^/reference-data/pictures/[0-9a-f]{32}\\.png$", imageUrl);

        // Anonymous: the Field App shows these with no session.
        var served = await factory.CreateClient().GetAsync(imageUrl);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Contains("immutable", served.Headers.CacheControl?.ToString());
        // Even with no Origin on the request — see the controller for why.
        Assert.Contains("Origin", served.Headers.Vary);
        Assert.Equal(Png, await served.Content.ReadAsByteArrayAsync());
    }

    /// <summary>A photo straight off a phone is often bigger than the whole request is allowed to
    /// be. That used to fail inside the antiforgery check and come back as a blank 400 page; it is
    /// now refused with the same sentence a slightly-too-big picture gets, on the screen it came
    /// from, for a new item and for an edit alike.</summary>
    [Fact]
    public async Task APictureBiggerThanTheWholeRequestMayBe_IsRefusedWithTheSizeMessage_NotABlankPage()
    {
        var admin = factory.CreateAdminClient();
        var before = factory.Pictures.Names.Count;
        var label = $"Phone photo {Guid.NewGuid():N}";
        var phonePhoto = Png.Concat(new byte[9 * 1024 * 1024]).ToArray();

        var create = await PostFormAsync(admin, "/ReferenceData/Create",
            [("Category", ((int)DomainCategory.FrameColour).ToString()), ("Label", label)],
            ("Picture", "IMG_0001.png", "image/png", phonePhoto));

        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        Assert.Equal("/ReferenceData", create.Headers.Location?.ToString());
        var landing = System.Net.WebUtility.HtmlDecode(await admin.GetStringAsync("/ReferenceData"));
        Assert.Contains("Upload a picture of 1 MB or smaller.", landing);
        Assert.DoesNotContain(label, landing);

        var update = await PostFormAsync(admin, "/ReferenceData/Update",
            [("Id", (await AddChildColourAsync()).ToString()), ("Label", label)],
            ("Picture", "IMG_0002.png", "image/png", phonePhoto));

        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);
        Assert.Contains("Upload a picture of 1 MB or smaller.", System.Net.WebUtility.HtmlDecode(await admin.GetStringAsync("/ReferenceData")));
        Assert.Equal(before, factory.Pictures.Names.Count);
    }

    [Fact]
    public async Task AFileThatIsTooBig_OrNotAPicture_IsRefused_WithItsMessage_AndNothingIsStored()
    {
        var admin = factory.CreateAdminClient();
        var before = factory.Pictures.Names.Count;

        var tooBig = Png.Concat(new byte[1024 * 1024]).ToArray();
        var big = await PostFormAsync(admin, "/ReferenceData/Create",
            [("Category", ((int)DomainCategory.FrameColour).ToString()), ("Label", $"Too big {Guid.NewGuid():N}")],
            ("Picture", "big.png", "image/png", tooBig));
        Assert.Equal(HttpStatusCode.OK, big.StatusCode);
        Assert.Contains("Upload a picture of 1 MB or smaller.", await big.Content.ReadAsStringAsync());

        // A picture's name and content type on something that isn't one.
        var notAPicture = await PostFormAsync(admin, "/ReferenceData/Create",
            [("Category", ((int)DomainCategory.FrameColour).ToString()), ("Label", $"Not a picture {Guid.NewGuid():N}")],
            ("Picture", "innocent.png", "image/png", "<html><script>alert(1)</script></html>"u8.ToArray()));
        Assert.Equal(HttpStatusCode.OK, notAPicture.StatusCode);
        Assert.Contains("Upload a PNG, JPEG or WebP picture.", await notAPicture.Content.ReadAsStringAsync());

        // A list that has no pictures.
        var wrongList = await PostFormAsync(admin, "/ReferenceData/Create",
            [("Category", ((int)DomainCategory.Occupation).ToString()), ("Label", $"Occupation {Guid.NewGuid():N}")],
            ("Picture", "p.png", "image/png", Png));
        Assert.Contains("Only the frame colour lists have pictures.", await wrongList.Content.ReadAsStringAsync());

        Assert.Equal(before, factory.Pictures.Names.Count);
    }

    [Fact]
    public async Task ReplacingAPicture_DeletesTheOldOne_AndRemovingIt_ClearsTheItem()
    {
        var admin = factory.CreateAdminClient();
        var label = $"Replaced {Guid.NewGuid():N}";
        await PostFormAsync(admin, "/ReferenceData/Create",
            [("Category", ((int)DomainCategory.FrameColourChild).ToString()), ("Label", label)],
            ("Picture", "first.png", "image/png", Png));
        var item = Query(db => db.ReferenceDataItems.Single(i => i.Label == label));
        var first = ReferenceDataPictures.StoredNameOf(item.ImageUrl)!;

        // The edit form shows the current picture and offers to remove it.
        var html = await admin.GetStringAsync("/ReferenceData");
        Assert.Contains($"src=\"{item.ImageUrl}\" alt=\"Current picture of {label}\"", html);
        Assert.Contains("name=\"RemovePicture\"", html);

        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x03 };
        await PostFormAsync(admin, "/ReferenceData/Update", [("Id", item.Id.ToString()), ("Label", label)], ("Picture", "second.jpeg", "image/jpeg", jpeg));

        var second = ReferenceDataPictures.StoredNameOf(Query(db => db.ReferenceDataItems.Single(i => i.Id == item.Id).ImageUrl))!;
        Assert.EndsWith(".jpg", second);
        Assert.Null(factory.Pictures.Bytes(first));
        Assert.Equal(jpeg, factory.Pictures.Bytes(second));

        // An edit that touches neither leaves the picture alone.
        await PostFormAsync(admin, "/ReferenceData/Update", [("Id", item.Id.ToString()), ("Label", label + " renamed")]);
        Assert.Equal(ReferenceDataPictures.UrlFor(second), Query(db => db.ReferenceDataItems.Single(i => i.Id == item.Id).ImageUrl));

        await PostFormAsync(admin, "/ReferenceData/Update", [("Id", item.Id.ToString()), ("Label", label), ("RemovePicture", "true")]);
        Assert.Null(Query(db => db.ReferenceDataItems.Single(i => i.Id == item.Id).ImageUrl));
        Assert.Null(factory.Pictures.Bytes(second));
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync(ReferenceDataPictures.UrlFor(second))).StatusCode);
    }

    [Fact]
    public async Task AnOldPastedWebAddress_StillDisplays_AndRemovingItDeletesNothing()
    {
        var admin = factory.CreateAdminClient();
        var shop = Query(db => db.ReferenceDataItems.First(i => i.Category == DomainCategory.FrameColour && i.ImageUrl != null && i.ImageUrl.StartsWith("https://")));
        var storedBefore = factory.Pictures.Names.Count;

        Assert.Contains($"src=\"{shop.ImageUrl}\"", await admin.GetStringAsync("/ReferenceData"));
        Assert.Null(ReferenceDataPictures.StoredNameOf(shop.ImageUrl));
        Assert.Equal(storedBefore, factory.Pictures.Names.Count);
    }

    [Theory]
    [InlineData("/reference-data/pictures/not-a-generated-name.png")]
    [InlineData("/reference-data/pictures/..%2F..%2Fappsettings.json")]
    [InlineData("/reference-data/pictures/00000000000000000000000000000000.svg")]
    [InlineData("/reference-data/pictures/00000000000000000000000000000000.png")]
    public async Task TheServingEndpoint_AnswersNotFound_ForANameItDidNotGenerate_OrDoesNotHold(string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, "image/jpeg")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, null)] // a GIF
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, null)]             // "<svg"
    [InlineData(new byte[] { }, null)]
    public void APicturesType_IsReadFromItsSignature(byte[] header, string? contentType)
    {
        Assert.Equal(contentType, ReferenceDataPictures.Detect(header)?.ContentType);
    }

    // --- Helpers ----------------------------------------------------------------------------------

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, (string Key, string Value)[] fields, (string Field, string FileName, string ContentType, byte[] Bytes)? file = null)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/ReferenceData");
        var form = new MultipartFormDataContent { { new StringContent(token), "__RequestVerificationToken" } };
        foreach (var (key, value) in fields)
        {
            form.Add(new StringContent(value), key);
        }

        if (file is { } upload)
        {
            var content = new ByteArrayContent(upload.Bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(upload.ContentType);
            form.Add(content, upload.Field, upload.FileName);
        }

        return await client.PostAsync(path, form);
    }

    private async Task<Guid> AddChildColourAsync()
    {
        var label = $"Child colour {Guid.NewGuid():N}";
        await PostFormAsync(factory.CreateAdminClient(), "/ReferenceData/Create", [("Category", ((int)DomainCategory.FrameColourChild).ToString()), ("Label", label)]);
        return Query(db => db.ReferenceDataItems.Single(i => i.Label == label).Id);
    }

    private Guid ColourIn(DomainCategory category) =>
        Query(db => db.ReferenceDataItems.First(i => i.Category == category && i.IsActive && !i.IsOtherOption).Id);

    private static CreateSaleRequest Sale(bool childrensFrame, Guid frameColourRefId) => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        ConsentGiven = true,
        LensRangeType = LensRangeType.Custom,
        SphereLeft = 1.00m,
        SphereRight = 1.00m,
        PupilDistanceMm = 62m,
        ChildrensFrame = childrensFrame,
        FrameColourRefId = frameColourRefId,
        FrameCoverage = ContractFrameCoverage.FullFrame,
        CoatingRefIds = [ReferenceDataSeedConfiguration.CoatingClearId],
    };

    private Guid SeedLead()
    {
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.Customers.Add(new Customer { Id = customerId, FullName = "Wanjiru Kamau", PhoneNumber = "+254711000000", HierarchyPath = CallerOutlet });
            db.Leads.Add(new Lead { Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = CallerOutlet, ConsentGiven = true });
        });
        return leadId;
    }

    private T Query<T>(Func<DotGlassesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
