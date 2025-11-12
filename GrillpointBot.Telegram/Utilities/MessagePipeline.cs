using Telegram.Bot;

namespace GrillpointBot.Telegram.Utilities;

public class MessagePipeline(
    ITelegramBotClient bot)
{
    public async Task DeleteIfExistsAsync(long chatId, int? messageId, CancellationToken ct)
    {
        if (messageId is { } mid)
            try { await bot.DeleteMessage(chatId, mid, ct); } 
            catch { /* ignore */ }
    }

    public async Task DeleteManyAsync(long chatId, IEnumerable<int> ids, CancellationToken ct)
    {
        foreach (var id in ids)
            try { await bot.DeleteMessage(chatId, id, ct); } 
            catch { /* ignore */ }
    }

    public Task RemoveKb(long chatId, int messageId, CancellationToken ct) =>
        Task.FromResult(bot.EditMessageReplyMarkup(chatId, 
            messageId, replyMarkup: null, cancellationToken: ct));
}