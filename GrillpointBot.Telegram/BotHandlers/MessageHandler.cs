using System.Text.RegularExpressions;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GrillpointBot.Telegram.BotHandlers;

public class MessageHandler(
    ITelegramBotClient bot,
    CatalogHandler catalogHandler,
    CheckoutHandler checkoutHandler,
    ISessionStore sessions,
    IMenuService menuService)
{
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var userId = msg.From!.Id;
        var session = await sessions.GetOrCreateAsync(userId);
        if(string.IsNullOrWhiteSpace(session.UserNick)) session.UserNick = msg.From?.Username;
        await sessions.UpsertAsync(session);
        
        // Location для адреса
        if (msg.Location is not null && session.State == FlowState.CheckoutAddressGeo)
        {
            session.CheckoutMessageIds.Add(msg.MessageId);
            await sessions.UpsertAsync(session);
            
            await checkoutHandler.HandleGeoAsync(msg, ct);
            return;
        }
        
        // Contact для телефона
        if (msg.Contact is not null && session.State == FlowState.CheckoutPhone)
        {
            session.CheckoutMessageIds.Add(msg.MessageId);
            session.DraftDelivery.PhoneDisplay = FormatPhone(msg.Contact.PhoneNumber);
            session.DraftDelivery.Phone = msg.Contact.PhoneNumber;
            session.State = FlowState.Confirm;
            await sessions.UpsertAsync(session);
            
            await checkoutHandler.SendConfirmCard(chatId, session, ct);
            return;
            
            string FormatPhone(string raw)
            {
                var digits = Regex.Replace(raw, @"\D", "");
                if (digits.StartsWith("8")) digits = "7" + digits[1..];
                if (!digits.StartsWith("7")) digits = "7" + digits;

                return digits.Length == 11
                    ? $"+7 ({digits[1..4]}) {digits[4..7]}-{digits[7..9]}-{digits[9..11]}"
                    : "+" + digits;
            }
        }
        
        var text = msg.Text ?? string.Empty;
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        
        // 2) Команды
        if (text == "/start")
        {
            // await AskNewSessionAsync(chatId, userId, ct);
            await SendWelcomeAsync(chatId, ct);
            return;
        }

        // 3) Категория (всегда раньше "сохранить комментарий")
        var categories = await menuService.GetCategoriesAsync();
        var category = categories.FirstOrDefault(c =>
            c.Category.Equals(text, StringComparison.OrdinalIgnoreCase));

        if (category is not null)
        {
            session.State = FlowState.ViewingItems;
            await sessions.UpsertAsync(session);
            await catalogHandler.ShowItemsAsync(chatId, category.Category, ct);
            return;
        }

        switch (session.State)
        {
            // 4) Если пользователь в корзине — трактуем текст как комментарий
            case FlowState.CommentPending:
            {
                // сохраняем ID сообщения с комментарием пользователя
                session.CommentMessageIds.Add(msg.MessageId);
            
                session.DraftComment = text;
                await sessions.UpsertAsync(session);
            
                var preview = await bot.SendMessage(
                    chatId,
                    $"Ваш комментарий:\n\n<blockquote>{System.Net.WebUtility.HtmlEncode(text)}</blockquote>",
                    ParseMode.Html,
                    replyMarkup: Kb.SaveOrEdit(CallbackPrefixes.SaveComment, CallbackPrefixes.EditComment),
                    cancellationToken: ct);
            
                // сохраняем ID сообщения с ответом бота
                session.CommentMessageIds.Add(preview.MessageId);
                await sessions.UpsertAsync(session);
                return;
            }
            
            case FlowState.CheckoutAddressManual:
                await checkoutHandler.HandleManualAddressAsync(msg, ct);
                return;
            
            // 5) Шаги чекаута: адрес / время / телефон
            case FlowState.CheckoutAddress or FlowState.CheckoutTime or FlowState.CheckoutPhone:
                await checkoutHandler.HandleUserInputAsync(msg, ct);
                return;
            
            default:
                await HandleFallback(chatId, ct);
                break;
        }
    }

#region Home page methods

    public async Task AskNewSessionAsync(long chatId, long userId, CancellationToken ct)
    {
        // TODO: исправить обработку начала новой сессии
        // Тут надо делать только GetSession
        // Если сессии не нашлось - не показываем Kb.Restart()
        // Если сессия нашлась и пустая (ничего в qty || в адресе || номере телефона)
        // Если не пустая - пишем, что нашлось в сессии (например "старая сессия:
        // if exists - BuildOrderSummaryAsync(), if exists - BuildDeliverySummaryAsync()"), другие данные session ...
        
        var s = await sessions.GetAsync(userId);
        
        // 1) Сессии не было → сразу Welcome
        if (s is null)
        {
            var ns = new Session { UserId = userId };
            await sessions.UpsertAsync(ns);
            await SendWelcomeAsync(chatId, ct);
            return;
        }

        // 2) TTL: если старая → очистить → Welcome
        if ((DateTime.UtcNow - s.LastUpdatedUtc).TotalHours > 4)
        {
            await sessions.RemoveAsync(userId);
            s = new Session { UserId = userId };
            await sessions.UpsertAsync(s);
        }

        // 3) Сессия есть, но пустая → Welcome
        var sessionIsEmpty =
            s.DraftQty.Count == 0 &&
            string.IsNullOrWhiteSpace(s.DraftDelivery.Street) &&
            string.IsNullOrWhiteSpace(s.DraftDelivery.Phone);

        if (sessionIsEmpty)
        {
            await SendWelcomeAsync(chatId, ct);
            return;
        }
        
        // 4) Сессия есть и НЕ пустая → спросить "Начать заново?"
        await bot.SendMessage(
            chatId,
            "Вы уже формировали заказ ранее.\nХотите продолжить или начать заново?",
            replyMarkup: Kb.Restart,
            cancellationToken: ct);
    }

    public async Task SendWelcomeAsync(long chatId, CancellationToken ct)
    {
        await bot.SendPhoto(chatId,
            photo: InputFile.FromUri("https://i.pinimg.com/originals/a6/13/a0/a613a0855cf198699926a8bcbb1e21a7.jpg"),
            caption: "## 👋 Добро пожаловать в *Grillpoint!*\n\nГорячие сэндвичи, приготовленные с душой.",
            parseMode: ParseMode.Markdown,
            replyMarkup: Kb.MainInline,
            cancellationToken: ct);
    }

    private async Task HandleFallback(long chatId, CancellationToken ct)
    {
        await bot.SendMessage(chatId, 
            "Пожалуйста, выберите действие из меню 👇",
            replyMarkup: Kb.Main, 
            cancellationToken: ct);
    }
    
#endregion
}
