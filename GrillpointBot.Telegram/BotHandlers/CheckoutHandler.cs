using System.Text.RegularExpressions;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using GrillpointBot.Telegram.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageFormatter = GrillpointBot.Telegram.Utilities.MessageFormatter;

namespace GrillpointBot.Telegram.BotHandlers;

public class CheckoutHandler(
    ITelegramBotClient bot,
    IMenuService menu,
    MessagePipeline pipeline,
    ISessionStore sessions)
{
    // Начало выбора способа получения заказа
    public async Task StartAsync(long chatId, long userId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);
        await pipeline.DeleteIfExistsAsync(chatId, s.CartMessageId, ct); // Удаляем сообщение с корзиной 
        s.State = FlowState.CheckoutMethod;
        await sessions.UpsertAsync(s);

        await bot.SendMessage(
            chatId,
            "Оформление заказа\nКак хотите получить заказ?",
            replyMarkup: Kb.CheckoutMethod(),
            cancellationToken: ct);
    }

    public async Task HandleMethodAsync(CallbackQuery cq, bool isDelivery, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(cq.From.Id);
        s.DraftDelivery.Method = isDelivery ? DeliveryMethod.Delivery : DeliveryMethod.Pickup;
        s.State = isDelivery ? FlowState.CheckoutAddress : FlowState.CheckoutPhone;
        await sessions.UpsertAsync(s);

        Message msg;
        if (isDelivery)
        {
            msg = await bot.EditMessageText(
                cq.Message!.Chat.Id, cq.Message.MessageId,
                "Укажите адрес доставки.\n\nНапример: «улица Такая-то, дом 10».",
                cancellationToken: ct);
        }
        else
        {
            msg = await bot.EditMessageText(
                cq.Message!.Chat.Id, cq.Message.MessageId,
                "📱 Укажите телефон для связи:",
                cancellationToken: ct);
            
            await AskPhoneAsync(cq.Message.Chat.Id, cq.From.Id, ct);
        }
        
        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        s.CheckoutMessageIds.Add(msg.MessageId);
        await sessions.UpsertAsync(s);
    }

    public async Task HandleUserInputAsync(Message msg, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(msg.From!.Id);

        switch (s.State)
        {
            case FlowState.CheckoutAddress:
                s.CheckoutMessageIds.Add(msg.MessageId);
                // город отсутствует в MVP - "СПб, Александровская" по умолчанию
                s.DraftDelivery.City = "СПб, пос. Александровская";
                s.DraftDelivery.Street = msg.Text;
                s.DraftDelivery.House = null;
                s.State = FlowState.CheckoutTime;
                await sessions.UpsertAsync(s);

                var dmsg = await bot.SendMessage(
                    msg.Chat.Id,
                    "Выберите дату получения:",
                    replyMarkup: Kb.DateKb(), cancellationToken: ct);
                s.CheckoutMessageIds.Add(dmsg.MessageId);
                await sessions.UpsertAsync(s);
                break;

            case FlowState.CheckoutTime:
                // если юзер ввёл текстом — пробуем распарсить
                if (DateTime.TryParse(msg.Text, out var when))
                    s.DraftDelivery.ScheduledTime = when;

                s.State = FlowState.CheckoutPhone;
                await sessions.UpsertAsync(s);
                await AskPhoneAsync(msg.Chat.Id, msg.From.Id, ct);
                break;

            case FlowState.CheckoutPhone:
                var digits = Regex.Replace(msg.Text, @"\D", "");
                if (digits.Length < 10)
                {
                    var emsg = await bot.SendMessage(msg.Chat.Id, 
                        "❌ Неверный формат телефона. Пример: +7 999 999-99-99", 
                        cancellationToken: ct);
                    s.CheckoutMessageIds.Add(emsg.MessageId);
                    await sessions.UpsertAsync(s);
                    return;
                }
                s.DraftDelivery.Phone =  digits.StartsWith("8") ? "+7" + digits[1..] : "+" + digits;
                s.State = FlowState.Confirm;
                await sessions.UpsertAsync(s);
                
                await SendConfirmCard(msg.Chat.Id, s, ct);
                break;
        }
    }
    
    public async Task SendConfirmCard(long chatId, Session s, CancellationToken ct)
    {
        var orderBlock = await MessageFormatter.BuildOrderSummaryAsync(s, menu);
        var deliveryBlock = await MessageFormatter.BuildDeliverySummaryAsync(s);
        
        var lines = new List<string>
        {
            "<b>✅ Проверьте заказ</b>\n",
            orderBlock,
            deliveryBlock
        };
        
        await bot.SendMessage(chatId, string.Join("\n", lines),
            parseMode: ParseMode.Html, replyMarkup: Kb.ConfirmOrder(), cancellationToken: ct);
        
        if (s.CheckoutMessageIds.Count > 0)
            await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
        s.CheckoutMessageIds.Clear();
        await sessions.UpsertAsync(s);
    }

    public async Task AskPhoneAsync(long chatId, long userId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);
        s.State = FlowState.CheckoutPhone;
        await sessions.UpsertAsync(s);

        var nick = string.IsNullOrWhiteSpace(s.UserNick) ? "" : $"@{s.UserNick}";
        
        var pmsg = await bot.SendMessage(chatId,
            $"📱 Укажите телефон для связи{(string.IsNullOrWhiteSpace(nick) ? "" : $" (или напишите: {nick})")}:",
            replyMarkup: Kb.Phone(),
            cancellationToken: ct);
        
        s.CheckoutMessageIds.Add(pmsg.MessageId);
        await sessions.UpsertAsync(s);
    }
}