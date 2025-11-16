using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using GrillpointBot.Telegram.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageFormatter = GrillpointBot.Telegram.Utilities.MessageFormatter;

namespace GrillpointBot.Telegram.BotHandlers;

public class CartHandler(
    ITelegramBotClient bot, 
    ISessionStore sessions, 
    IMenuService menu,
    MessagePipeline pipeline)
{
    // 1) Нажали «Добавить» — открываем панель [-] 1 [+]
    public async Task StartInlineQtyAsync(CallbackQuery cq, string itemId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(cq.From.Id);
        s.DraftQty.TryGetValue(itemId, out var q);
        if (q <= 0) q = 1;
        s.DraftQty[itemId] = q;
        await sessions.UpsertAsync(s);

        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        await bot.EditMessageReplyMarkup(
            cq.Message!.Chat.Id, 
            cq.Message.MessageId, 
            Kb.CardQty(itemId, q), 
            cancellationToken: ct);
    }
    
    // 2) «+ / −» — меняем qty и редактируем клавиатуру в том же сообщении
    public async Task ChangeInlineQtyAsync(CallbackQuery cq, string itemId, int delta, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(cq.From.Id);
        s.DraftQty.TryGetValue(itemId, out var q);
        q += delta;
        
        if (q <= 0) s.DraftQty.Remove(itemId);
        else        s.DraftQty[itemId] = q;

        await sessions.UpsertAsync(s);
        
        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        await bot.EditMessageReplyMarkup(
            cq.Message!.Chat.Id, 
            cq.Message.MessageId,
            replyMarkup: q <= 0 ? Kb.CardAdd(itemId) : Kb.CardQty(itemId, q), 
            cancellationToken: ct);
    }

    // 3) Показ корзины: читаем DraftQty, считаем сумму, показываем «Изменить/Продолжить»
    public async Task ShowCartAsync(CallbackQuery cq, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(cq.From.Id);
        s.State = FlowState.InCart;
        
        await pipeline.DeleteIfExistsAsync(cq.Message!.Chat.Id, s.CategoriesMessageId, ct);
        await pipeline.DeleteManyAsync(cq.Message.Chat.Id, s.ItemMessageIds, ct);
        s.CategoriesMessageId = null;
        s.ItemMessageIds.Clear();
        await sessions.UpsertAsync(s);

        var msgBody = await MessageFormatter.BuildOrderSummaryAsync(s, menu);
        
        // если корзина уже есть — редактируем
        if (s.CartMessageId is { } existing)
        {
            await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
            await bot.EditMessageText(
                chatId: cq.Message.Chat.Id,
                messageId: existing,
                text: msgBody,
                parseMode: ParseMode.Html,
                replyMarkup: Kb.CartSummary,
                cancellationToken: ct);
            await sessions.UpsertAsync(s);
            return;
        }
        
        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        var sent = await bot.SendMessage(
            chatId: cq.Message!.Chat.Id,
            text: msgBody,
            parseMode: ParseMode.Html,
            replyMarkup: Kb.CartSummary,
            cancellationToken: ct);

        s.CartMessageId = sent.MessageId;
        await sessions.UpsertAsync(s);
    }
}