using System.Net;
using LazyTsql.Infrastructure;

namespace LazyTsql.Tests;

public sealed class UpdateCheckTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-update-").FullName;
    private readonly FakeNuGet _nuget = new();
    private DateTimeOffset _now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class FakeNuGet : HttpMessageHandler
    {
        public string Body { get; set; } = """{"versions":["0.1.3","0.1.12","0.1.15","0.2.0-preview.1"]}""";

        public bool Fail { get; set; }

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (Fail) throw new HttpRequestException("offline");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) });
        }
    }

    private sealed class Clock(Func<DateTimeOffset> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now();
    }

    private Task<string?> Check(string current = "0.1.12", string processPath = "/home/me/.dotnet/tools/lazytsql", string? optOut = null) =>
        new UpdateCheck(new HttpClient(_nuget), _directory, new Clock(() => _now), name => name == UpdateCheck.OptOutVariable ? optOut : null)
            .MessageAsync(Version.Parse(current), processPath, CancellationToken.None);

    [Fact]
    public async Task A_newer_release_on_NuGet_is_announced_with_the_command_that_installs_it()
    {
        Assert.Equal("Update available: 0.1.12 → 0.1.15 · dotnet tool update -g lazytsql", await Check());
        Assert.Equal("https://api.nuget.org/v3-flatcontainer/lazytsql/index.json", Assert.Single(_nuget.Requests).ToString());
    }

    [Fact]
    public async Task A_downloaded_binary_points_to_the_release_page()
    {
        Assert.Equal(
            "Update available: 0.1.12 → 0.1.15 · https://github.com/carandev/lazytsql/releases/latest",
            await Check(processPath: "/usr/local/bin/lazytsql"));
    }

    [Theory]
    [InlineData("0.1.15")]
    [InlineData("0.1.16")]
    public async Task The_latest_or_a_newer_version_has_nothing_to_announce(string current)
    {
        Assert.Null(await Check(current));
    }

    [Fact]
    public async Task NuGet_is_asked_at_most_once_a_day()
    {
        await Check();
        _nuget.Body = """{"versions":["0.1.20"]}""";
        _now = _now.AddHours(23);

        Assert.Equal("Update available: 0.1.12 → 0.1.15 · dotnet tool update -g lazytsql", await Check());
        Assert.Single(_nuget.Requests);

        _now = _now.AddHours(2);
        Assert.Equal("Update available: 0.1.12 → 0.1.20 · dotnet tool update -g lazytsql", await Check());
        Assert.Equal(2, _nuget.Requests.Count);
    }

    [Fact]
    public async Task Without_network_or_with_an_unexpected_answer_nothing_is_announced()
    {
        _nuget.Fail = true;
        Assert.Null(await Check());

        _nuget.Fail = false;
        _nuget.Body = "<html>";
        Assert.Null(await Check());
    }

    [Fact]
    public async Task LAZYTSQL_NO_UPDATE_CHECK_turns_the_check_off()
    {
        Assert.Null(await Check(optOut: "1"));
        Assert.Empty(_nuget.Requests);
    }
}
