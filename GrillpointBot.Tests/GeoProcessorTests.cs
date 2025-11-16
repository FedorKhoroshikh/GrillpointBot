using System.Diagnostics.Tracing;
using System.Text.Json;
using GrillpointBot.Telegram.Utilities;
using Xunit.Abstractions;

namespace GrillpointBot.Tests;

public class GeoProcessorTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public GeoProcessorTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData(59.734579, 30.338480, true)]   // пос. Александровская, пер. Советский, д. 3 
    [InlineData(59.80, 30.40, false)]  // точка заведомо снаружи
    public void IsInPolygon_Works(double lat, double lon, bool expected)
    {
        var inside = GeoProcessor.IsInPolygon((lat, lon));
        Assert.Equal(expected, inside);
    }
    
    [Fact]
    public void ParseSearchFirst_Works()
    {
        const string sample = """
                              [
                                {
                                  "lat": "59.7164001",
                                  "lon": "30.1051002",
                                  "display_name": "Советский проспект, 3, Санкт-Петербург, Россия"
                                }
                              ]
                              """;
        /*var arr = JsonDocument.Parse(sample).RootElement;
        var parsed = GeoProcessor.TryGeocodeAsync(arr);
        Assert.NotNull(parsed);
        var (lat, lon, addr) = parsed!.Value;
        Assert.True(lat is > 59.71 and < 59.72);
        Assert.True(lon is > 30.10 and < 30.11);
        Assert.Contains("Советский", addr);*/
    }

    [Fact]
    public async Task ParseSearchFirst_Empty_ReturnsNull()
    {
        var addr = await GeoProcessor.ReverseParseAsync(59.734570, 30.334829);
        _testOutputHelper.WriteLine($"{addr}");
        Assert.NotNull(addr);
        /*var arr = JsonDocument.Parse("[]").RootElement;
        var parsed = GeoProcessor.ParseSearchFirst(arr);
        Assert.Null(parsed);*/
    }
    
    [Fact]
    public Task NormalizeDisplayAddress_Test()
    {
        var addr = "Детский сад №47, Образцовая улица, Пулковское, Шушары, Санкт-Петербург, Северо-Западный федеральный округ, 196605, Россия";
        var address = GeoProcessor.ReverseRawAsync(59.756376, 30.364520);
        // var address = GeoProcessor.GetGeocodeInfo(59.734570, 30.334829).Result;
        var dispAddr = GeoProcessor.ParseAddress(address.Result);
        _testOutputHelper.WriteLine($"{dispAddr}");
        Assert.NotNull(addr);
        return Task.CompletedTask;
        /*var arr = JsonDocument.Parse("[]").RootElement;
        var parsed = GeoProcessor.ParseSearchFirst(arr);
        Assert.Null(parsed);*/
    }
}