using System.Net;
using System.Net.Sockets;
using DropLink.Models;
using DropLink.Services;
using Xunit;

namespace DropLink.Tests;

public sealed class CoreTests
{

    [Theory]
    [InlineData("[ERROR]: unable to create share (unable to load environment; did you 'zrok2 enable'?)")]
    [InlineData("Error: environment is not enabled")]
    [InlineData("run zrok2 enable <account_token>")]
    public void ZrokEnvironmentSetupErrorsTriggerFirstRunAuthorization(string message)
    {
        Assert.True(ZrokTunnelService.LooksLikeEnvironmentSetupRequired(message));
    }


    [Theory]
    [InlineData("access your zrok share at the following endpoints:\ns3g2oh7ult2e.shares.zrok.io", "https://s3g2oh7ult2e.shares.zrok.io/")]
    [InlineData("{\"level\":\"INFO\",\"msg\":\"access your zrok share at the following endpoints:\\ns3g2oh7ult2e.shares.zrok.io\"}", "https://s3g2oh7ult2e.shares.zrok.io/")]
    [InlineData("Public URL: https://abc123.share.zrok.io/path", "https://abc123.share.zrok.io/")]
    public void ZrokPublicEndpointIsRecognizedFromUrlsAndBareHostnames(string output, string expected)
    {
        var endpoint = ZrokTunnelService.ExtractPublicEndpoint(output);
        Assert.NotNull(endpoint);
        Assert.Equal(expected, endpoint!.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://127.0.0.1:43821")]
    [InlineData("randomhost.example.com")]
    [InlineData("https://example.shares.zrok.io.evil.example")]
    public void ZrokEndpointParserRejectsNonPublicEndpoints(string output)
    {
        Assert.Null(ZrokTunnelService.ExtractPublicEndpoint(output));
    }

    [Fact]
    public void ZrokUnrelatedErrorsDoNotTriggerFirstRunAuthorization()
    {
        Assert.False(ZrokTunnelService.LooksLikeEnvironmentSetupRequired("connection refused by remote service"));
    }

    [Fact]
    public void ShareTokensAreShortRandomAndValidatedStrictly()
    {
        var manager = new ShareManager();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "test file");
            var first = manager.CreateShare(path, token => $"http://127.0.0.1:43821/s/{token}");
            var second = manager.CreateShare(path, token => $"http://127.0.0.1:43821/s/{token}");
            Assert.Equal(32, first.Token.Length);
            Assert.Matches("^[a-f0-9]{32}$", first.Token);
            Assert.NotEqual(first.Token, second.Token);
            Assert.False(ShareManager.IsValidToken("../../secret"));
            Assert.False(ShareManager.IsValidToken(new string('A', 32)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RevocationRemovesOnlyTheSelectedShare()
    {
        var manager = new ShareManager();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "test file");
            var share = manager.CreateShare(path, token => $"http://localhost/s/{token}");
            Assert.True(manager.TryGet(share.Token, out _));
            Assert.True(manager.Revoke(share.Token));
            Assert.False(manager.TryGet(share.Token, out _));
            Assert.False(manager.Revoke(share.Token));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("bytes=0-9", 100, 0, 9)]
    [InlineData("bytes=10-", 100, 10, 99)]
    [InlineData("bytes=-8", 100, 92, 99)]
    [InlineData("bytes=10-999", 100, 10, 99)]
    public void RangeParserSupportsSingleByteRanges(string value, long size, long start, long end)
    {
        Assert.True(RangeParser.TryParse(value, size, out var range));
        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
    }

    [Theory]
    [InlineData("bytes=100-120", 100)]
    [InlineData("bytes=0-1,3-5", 100)]
    [InlineData("items=1-2", 100)]
    [InlineData("bytes=-0", 100)]
    [InlineData("bytes=0-1", 0)]
    public void RangeParserRejectsInvalidOrMultipartRanges(string value, long size)
    {
        Assert.False(RangeParser.TryParse(value, size, out _));
    }

    [Theory]
    [InlineData("https://example.com", "HTTPS")]
    [InlineData("example.com/path", "slash")]
    [InlineData("user@example.com", "at")]
    public void HostnameValidationRejectsUrlParts(string value, string _)
    {
        Assert.Throws<ArgumentException>(() => SettingsValidator.NormalizeHostname(value));
    }

    [Fact]
    public async Task LocalServerServesFileAndHonorsRangeAndRevocation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var manager = new ShareManager();
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "0123456789abcdef");
        await using var server = new ShareServer(manager, port);
        try
        {
            var share = manager.CreateShare(path, token => $"http://127.0.0.1:{port}/s/{token}");
            await server.StartAsync();
            using var client = new HttpClient();
            var page = await client.GetStringAsync(share.Url);
            Assert.Contains("Download file", page);

            using var headResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, share.Url));
            Assert.Equal(HttpStatusCode.OK, headResponse.StatusCode);
            Assert.Empty(await headResponse.Content.ReadAsByteArrayAsync());

            using var fullDownload = await client.GetAsync($"http://127.0.0.1:{port}/s/{share.Token}/download");
            Assert.Equal("0123456789abcdef", await fullDownload.Content.ReadAsStringAsync());

            using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/s/{share.Token}/download");
            rangeRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(2, 5);
            using var rangeResponse = await client.SendAsync(rangeRequest);
            Assert.Equal(HttpStatusCode.PartialContent, rangeResponse.StatusCode);
            Assert.Equal("2345", await rangeResponse.Content.ReadAsStringAsync());

            Assert.True(manager.Revoke(share.Token));
            using var revokedResponse = await client.GetAsync(share.Url);
            Assert.Equal(HttpStatusCode.NotFound, revokedResponse.StatusCode);
        }
        finally
        {
            await server.StopAsync();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("192.0.2.1")]
    [InlineData("localhost")]
    [InlineData("server.local")]
    public void PublicHostnameMustBeAFullDnsName(string value)
    {
        Assert.Throws<ArgumentException>(() => SettingsValidator.NormalizeHostname(value));
    }

    [Fact]
    public void PublicLinksRequireStandardHttpsPort()
    {
        Assert.Equal(443, SettingsValidator.ParsePublicPort("443"));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ParsePublicPort("80"));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ParsePublicPort("8443"));
    }

    [Fact]
    public void UrlBuilderCanUseDomainAndKeepTokenOnlyAsPathSecret()
    {
        var settings = new AppSettings { PublicHost = "dl.example.com", PublicPort = 443 };
        var url = ShareUrlBuilder.Build(new string('a', 32), settings, 43821);
        Assert.Equal($"https://dl.example.com/s/{new string('a', 32)}", url);
    }
}
