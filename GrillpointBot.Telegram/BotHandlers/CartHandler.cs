using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using GrillpointBot.Telegram.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
        
        decimal total = 0;
        int counter = 0;
        var lines = new List<string> { "<b>Ваш заказ:</b>\n" };
        foreach (var (id, qty) in s.DraftQty)
        {
            counter++;
            var it = await menu.GetItemByIdAsync(id);
            if (it is null) continue;
            var sum = it.Price * qty;
            total += sum;

            lines.Add($"({counter})  <b>{it.Category}</b> — {it.Name}\n" +
                      $"     <i>{it.Weight} г</i> · {it.Price:0.#} ₽ × {qty} = <b>{sum:0.#} ₽</b>\n");
        }
        lines.Add($"<b>Итого:</b> {total:0.#} ₽");
        
        var body = lines.Count == 0
            ? "Корзина пуста."
            : string.Join("\n", lines);
        
        // если корзина уже есть — редактируем
        if (s.CartMessageId is { } existing)
        {
            await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
            await bot.EditMessageText(
                chatId: cq.Message.Chat.Id,
                messageId: existing,
                text: body,
                parseMode: ParseMode.Html,
                replyMarkup: Kb.CartSummary(),
                cancellationToken: ct);
            await sessions.UpsertAsync(s);
            return;
        }
        
        if (s.DraftQty.Count == 0)
        {
            await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
            await bot.SendMessage(
                cq.Message!.Chat.Id,
                "Корзина пуста.",
                replyMarkup: Kb.CartSummary(),
                cancellationToken: ct);
            return;
        }

        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        var sent = await bot.SendMessage(
            chatId: cq.Message!.Chat.Id,
            text: body,
            parseMode: ParseMode.Html,
            replyMarkup: Kb.CartSummary(),
            cancellationToken: ct);

        s.CartMessageId = sent.MessageId;
        await sessions.UpsertAsync(s);
    }
}