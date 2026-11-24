using System.Text.Json.Serialization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MissionControl.Contracts;

namespace ProbeFactory.Tests;

public sealed class CreateProbeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly WebApplicationFactory<Program> _factory;

    public CreateProbeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string name, string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/probes")
        {
            Content = JsonContent.Create(new { Name = name, Kind = "Orbiter" }),
        };
        if (language is not null)
            request.Headers.AcceptLanguage.ParseAdd(language);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string field, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var messages = doc.RootElement.GetProperty("errors").EnumerateObject()
            .Single(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase))
            .Value.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains(message, messages);
    }

    [Fact]
    public async Task Creates_a_probe()
    {
        var client = _factory.CreateClient();
        var resp = await PostAsync(client, "Pioneer-11");
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<ProbeDto>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Pioneer-11", dto!.Name);
        Assert.Equal(ProbeKind.Orbiter, dto.Kind);
    }

    [Fact]
    public async Task Rejects_a_reserved_name_case_insensitively()
    {
        var resp = await PostAsync(_factory.CreateClient(), "voyager");
        await AssertErrorAsync(resp, "Name", "This probe name is already taken.");
    }

    [Fact]
    public async Task Rejects_a_duplicate_name()
    {
        var client = _factory.CreateClient();
        var first = await PostAsync(client, "Pioneer-12");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await PostAsync(client, "Pioneer-12");
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Validation_messages_are_in_french_with_accept_language_fr()
    {
        var resp = await PostAsync(_factory.CreateClient(), "", "fr");
        await AssertErrorAsync(resp, "Name", "Le champ Name est obligatoire.");
    }

    [Theory]
    [InlineData("fr", "Une ou plusieurs erreurs de validation se sont produites.")]
    [InlineData("en", "One or more validation errors occurred.")]
    public async Task Validation_problem_title_follows_accept_language(string language, string title)
    {
        var resp = await PostAsync(_factory.CreateClient(), "ab", language);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(title, doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unsupported_culture_falls_back_to_english()
    {
        var resp = await PostAsync(_factory.CreateClient(), "", "de");
        await AssertErrorAsync(resp, "Name", "The Name field is required.");
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_probe()
    {
        var resp = await _factory.CreateClient().GetAsync("/probes/Nobody", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
