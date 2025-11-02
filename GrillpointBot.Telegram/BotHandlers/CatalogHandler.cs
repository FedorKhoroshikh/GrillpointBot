using System.Text;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using GrillpointBot.Telegram.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using InputFile = Telegram.Bot.Types.InputFile;

namespace GrillpointBot.Telegram.BotHandlers;

public class CatalogHandler(
    ITelegramBotClient bot, 
    IMenuService menuService,
    ISessionStore sessions,
    MessagePipeline pipeline)
{
#region Functional implementation

    public async Task ShowCategoriesAsync(long chatId, CancellationToken ct)
    {
        var categories = await menuService.GetCategoriesAsync();

        if (!categories.Any())
        {
            await bot.SendMessage(chatId, "❌ Категории не найдены.", cancellationToken: ct);
            return;
        }
        
        var sent = await bot.SendMessage(
            chatId,
            "📋 Выберите категорию:",
            replyMarkup: Kb.Categories(categories),
            cancellationToken: ct);

        var s = await sessions.GetOrCreateAsync(chatId);
        s.CategoriesMessageId = sent.MessageId;
        await sessions.UpsertAsync(s);
    }

    public async Task ShowItemsAsync(long chatId, string category, CancellationToken ct)
    {
        // Удаляем сообщение с категориями и убираем клавиатуру
        var s = await sessions.GetOrCreateAsync(chatId);
        await pipeline.DeleteIfExistsAsync(chatId, s.CategoriesMessageId, ct);
        s.CategoriesMessageId = null;

        var items = await menuService.GetItemsByCategoryAsync(category);
        if (!items.Any())
        {
            await bot.SendMessage(chatId, "❌ В этой категории пока нет товаров.", cancellationToken: ct);
            return;
        }
        
        // отправляем карточки и накапливаем их messageId
        var ids = new List<int>();
        foreach (var item in items)
        {
            var id = await SendItemCardAsync(chatId, item, ct);
            ids.Add(id);
        }
        
        s.ItemMessageIds.AddRange(ids);
        await sessions.UpsertAsync(s);
    }
    
#endregion
    
# region Helpers

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly string AssetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
    private static readonly string NoPhotoPath = Path.Combine(AssetsDir, "no-photo.jpg");

    private static string? FindLocalAsset(string? imageKey)
    {
        if (string.IsNullOrWhiteSpace(imageKey)) return null;
        var jpg = Path.Combine(AssetsDir, $"{imageKey}.jpg");
        var png = Path.Combine(AssetsDir, $"{imageKey}.png");
        if (File.Exists(jpg)) return jpg;
        if (File.Exists(png)) return png;
        return null;
    }

    // Проверяем, что по URL реально лежит картинка
    private static async Task<bool> IsImageUrlAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, uri);
            using var resp = await Http.SendAsync(req, ct);
            var ok = resp.IsSuccessStatusCode;
            var ctHeader = resp.Content.Headers.ContentType?.MediaType ?? "";
            return ok && ctHeader.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    
    private static string BuildCaption(MenuItem it)
    {
        // I) название — наверху
        var sb = new StringBuilder();
        sb.AppendLine($"*{it.Name}*");                 
        
        // II) под фото в телеге — это всё равно «caption», но идёт сразу после названия
        if (!string.IsNullOrWhiteSpace(it.Description))
            sb.AppendLine(it.Description);
        
        if (it.Ingredients?.Count > 0)
            sb.AppendLine($"\n*Состав:* {string.Join(", ", it.Ingredients)}");
        
        // III) низ карточки — вес/цена (логически «низ», фактически это последняя строка подписи)
        var weight = it.Weight is > 0 ? $"Вес: {it.Weight} г" : "Вес: —";
        var price  = $"Цена: {it.Price:0.#} ₽";
        
        sb.AppendLine($"\n_{weight}_                                 *{price}*");
        return sb.ToString();
    }
    
    private async Task<int> SendItemCardAsync(long chatId, MenuItem item, CancellationToken ct)
    {
        Message msg;
        var caption = BuildCaption(item);

        var s = await sessions.GetOrCreateAsync(chatId);
        s.DraftQty.TryGetValue(item.Id, out var qty);
        var kb = qty > 0 ? Kb.CardQty(item.Id, qty) : Kb.CardAdd(item.Id);

        // 1) Локальный файл по ImageKey
        var local = FindLocalAsset(item.ImageKey);
        if (local is not null && File.Exists(local))
        {
            await using var fs = File.OpenRead(local);
            msg = await bot.SendPhoto(chatId, InputFile.FromStream(fs, Path.GetFileName(local)), 
                caption, ParseMode.Markdown, replyMarkup: kb, cancellationToken: ct);
            return msg.Id;
        }

        // 2) Иначе попробуем прямой URL с image/*
        if (await IsImageUrlAsync(item.ImageUrl, ct))
        {
            try
            {
                msg = await bot.SendPhoto(chatId, InputFile.FromUri(item.ImageUrl!), 
                    caption, ParseMode.Markdown, replyMarkup: kb, cancellationToken: ct);
                return msg.Id;
            }
            catch { /* пойдём в текст */ }
        }
        
        // 3) no-photo
        if (File.Exists(NoPhotoPath))
        {
            await using var fs = File.OpenRead(NoPhotoPath);
            msg = await bot.SendPhoto(chatId, InputFile.FromStream(fs, Path.GetFileName(NoPhotoPath)),
                caption, ParseMode.Markdown, replyMarkup: kb, cancellationToken: ct);
            return msg.Id;
        }

        // 3) Фоллбек — текстовая карточка
        msg = await bot.SendMessage(
            chatId, caption, ParseMode.Markdown, 
            replyMarkup: kb, cancellationToken: ct);
        return msg.Id;
    }
    
#endregion

}