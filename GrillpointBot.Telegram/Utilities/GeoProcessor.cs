using System.Globalization;
using System.Text.Json;

namespace GrillpointBot.Telegram.Utilities;

public record ParsedAddress(
    string FullAddress,     // исходный display_name от Nominatim
    string DisplayAddress,  // красиво собранный адрес для UI
    string City,
    string Locality,        // town/village/hamlet
    string Suburb,
    string Road,
    string HouseNumber,
    string POI,             // детсад/магазин/школа и т.п.
    string Postcode
);

public class GeoProcessor
{
    private static readonly List<(double Lat, double Lon)> Polygon =
    [
        (59.743975, 30.309981),
        (59.734151, 30.299816),
        (59.729042, 30.319610),
        (59.725216, 30.321000),
        (59.724210, 30.327362),
        (59.726249, 30.330516),
        (59.719996, 30.354117),
        (59.728383, 30.355568),
        (59.730798, 30.366619),
        (59.735016, 30.362756),
        (59.735727, 30.350386),
        (59.744183, 30.344008),
        (59.743869, 30.333695),
        (59.738893, 30.326166),
    ];
    
    public static bool IsInPolygon((double Lat, double Lon) point)
    {
        var (x, y) = point;
        bool inside = false;

        for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
        {
            var (xi, yi) = Polygon[i];
            var (xj, yj) = Polygon[j];

            var intersect = ((yi > y) != (yj > y)) &&
                            (x < (xj - xi) * (y - yi) / (yj - yi + double.Epsilon) + xi);
            if (intersect) inside = !inside;
        }

        return inside;
    }
    
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(5);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GrillpointBot/1.0 (+https://t.me/gp_streetfood_bot)");
        return http;
    }

    public static async Task<JsonElement> ReverseRawAsync(double lat, double lon)
    {
        using var http = CreateHttpClient();

        var url =
            $"https://nominatim.openstreetmap.org/reverse?format=json" +
            $"&lat={lat.ToString(CultureInfo.InvariantCulture)}" +
            $"&lon={lon.ToString(CultureInfo.InvariantCulture)}" +
            "&zoom=18&addressdetails=1";
        
        var json = await http.GetStringAsync(url);
        return JsonDocument.Parse(json).RootElement;
    }
    
    private static async Task<JsonElement?> ForwardRawAsync(string text)
    {
        using var http = CreateHttpClient();

        var url =
            "https://nominatim.openstreetmap.org/search?format=json" +
            "&addressdetails=1&limit=1&q=" + Uri.EscapeDataString(text);

        var json = await http.GetStringAsync(url);
        var doc = JsonDocument.Parse(json).RootElement;

        if (doc.ValueKind != JsonValueKind.Array || doc.GetArrayLength() == 0)
            return null;

        return doc[0];
    }

    public static async Task<ParsedAddress> ReverseParseAsync(double lat, double lon)
    {
        var root = await ReverseRawAsync(lat, lon);
        return ParseAddress(root);
    }

    public static async Task<(double lat, double lon, ParsedAddress parsed)> ForwardParseAsync(string text)
    {
        var first = await ForwardRawAsync(text);

        if (first is null)
            return (0, 0, EmptyParsed());

        var lat = double.Parse(first.Value.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
        var lon = double.Parse(first.Value.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);

        var parsed = ParseAddress(first.Value);

        return (lat, lon, parsed);
    }
    
    public static ParsedAddress? ParseAddress(JsonElement root)
    {
        var fullDisplay = root.TryGetProperty("display_name", out var dn)
            ? dn.GetString() ?? ""
            : "";
        
        if (!root.TryGetProperty("address", out var addr))
            return null;
        
        string Get(string key) =>
            addr.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
        
        var city =
            Get("city") != "" ? Get("city") :
            Get("state") != "" ? Get("state") : "";
            
        var locality =             
            Get("town") != "" ? Get("town") :
            Get("village") != "" ? Get("village") :
            Get("hamlet");

        var suburb = Get("suburb");
        var neighbourhood = Get("neighbourhood");

        var road =
            Get("road") != "" ? Get("road") :
            Get("street") != "" ? Get("street") :
            Get("pedestrian") != "" ? Get("pedestrian") :
            Get("footway");

        
        var house = Get("house_number");

        var poi =
            Get("amenity") != "" ? Get("amenity") :
            Get("building") != "" ? Get("building") :
            Get("shop");

        var postcode = Get("postcode");

        var display = BuildDisplayAddress(city, locality, suburb, neighbourhood, road, house, poi, postcode);

        return new ParsedAddress(
            FullAddress: fullDisplay,
            DisplayAddress: display,
            City: city,
            Locality: locality,
            Suburb: suburb,
            Road: road,
            HouseNumber: house,
            POI: poi,
            Postcode: postcode
        );
    }
    
    private static string BuildDisplayAddress(
        string city,
        string locality,
        string suburb,
        string neighbourhood,
        string road,
        string house,
        string poi,
        string postcode)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(city))
            parts.Add(city);

        if (!string.IsNullOrWhiteSpace(locality))
            parts.Add(locality);

        if (!string.IsNullOrWhiteSpace(suburb))
            parts.Add(suburb);

        if (!string.IsNullOrWhiteSpace(neighbourhood))
            parts.Add(neighbourhood);

        if (!string.IsNullOrWhiteSpace(road))
            parts.Add(road);

        if (!string.IsNullOrWhiteSpace(house))
            parts.Add(house);

        if (string.IsNullOrWhiteSpace(house) && !string.IsNullOrWhiteSpace(poi))
            parts.Add(poi);

        var result = string.Join(", ", parts);

        if (!string.IsNullOrWhiteSpace(postcode))
            result += $" ({postcode})";

        return result;
    }
    
    private static ParsedAddress EmptyParsed(string full = "") => 
        new(full, "", "", "", "", "", "", "", "");
}