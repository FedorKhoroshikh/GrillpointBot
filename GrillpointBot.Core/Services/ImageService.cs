using GrillpointBot.Core.Models;
using Telegram.Bot.Types;

namespace GrillpointBot.Core.Services;

public interface IImageService
{
    Task<InputFile?> ResolveAsync(MenuItem item, CancellationToken ct);
}

public class ImageService : IImageService
{
    private readonly IHttpClientFactory _http;

    public ImageService(IHttpClientFactory http) => _http = http;

    public async Task<InputFile?> ResolveAsync(MenuItem item, CancellationToken ct)
    {
        // 1) Локальный файл по ключу
        if (!string.IsNullOrWhiteSpace(item.ImageKey))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", $"{item.ImageKey}.jpg");
            if (File.Exists(path))
                return InputFile.FromStream(File.OpenRead(path), Path.GetFileName(path));
        }

        // 2) Прямой URL с image/*
        if (!string.IsNullOrWhiteSpace(item.ImageUrl)
            && Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out var uri))
        {
            var client = _http.CreateClient();
            using var head = new HttpRequestMessage(HttpMethod.Head, uri);
            using var resp = await client.SendAsync(head, ct);
            var ctHeader = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (resp.IsSuccessStatusCode && ctHeader.StartsWith("image/"))
                return InputFile.FromUri(uri.ToString());
        }

        // 3) Ничего — вернём null (будет текстовая карточка)
        return null;
    }
}