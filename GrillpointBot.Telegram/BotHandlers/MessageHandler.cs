using GrillpointBot.Core.Common;
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
        
        // 0) Проверка ввода номера телефона
        if (msg.Contact is not null && session.State == FlowState.CheckoutPhone)
        {
            session.CheckoutMessageIds.Add(msg.MessageId);
            session.DraftDelivery.Phone = msg.Contact.PhoneNumber;
            session.State = FlowState.Confirm;
            await sessions.UpsertAsync(session);
            
            await checkoutHandler.SendConfirmCard(chatId, session, ct);
            return;
        }
        
        var text = msg.Text ?? string.Empty;
        text = text.Trim();
        
        if (string.IsNullOrWhiteSpace(text)) return;
        
        // 1) Команды
        if (text == "/start")
        {
            await AskNewSessionAsync(chatId, userId, ct);
            return;
        }

        // 2) Категория (всегда раньше "сохранить комментарий")
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
        
        // 3) Если пользователь в корзине — трактуем текст как комментарий
        if (session.State == FlowState.CommentPending)
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
        
        // 4) Шаги чекаута: адрес / время / телефон
        if (session.State is FlowState.CheckoutAddress 
                          or FlowState.CheckoutTime 
                          or FlowState.CheckoutPhone)
        {
            // проксируем ввод в CheckoutHandler
            await checkoutHandler.HandleUserInputAsync(msg, ct);
            return;
        }

        await HandleFallback(chatId, ct);
    }

#region Home page methods

    private async Task AskNewSessionAsync(long chatId, long userId, CancellationToken ct)
    {
        var session = await sessions.GetOrCreateAsync(userId);

        // TTL очистка старой сессии (например, если >4ч)
        if ((DateTime.UtcNow - session.LastUpdatedUtc).TotalHours > 4)
        {
            await sessions.RemoveAsync(userId);
            session = new Session { UserId = userId };
            await sessions.UpsertAsync(session);
        }

        await bot.SendMessage(
            chatId,
            "Начать новую сессию без сохранения данных?",
            replyMarkup: Kb.Restart(),
            cancellationToken: ct);
    }

    public async Task SendWelcomeAsync(long chatId, CancellationToken ct)
    {
        await bot.SendPhoto(chatId,
            photo: InputFile.FromUri("https://i.pinimg.com/originals/a6/13/a0/a613a0855cf198699926a8bcbb1e21a7.jpg"),
            caption: "## 👋 Добро пожаловать в *Grillpoint!*\n\nГорячие сэндвичи, приготовленные с душой.",
            parseMode: ParseMode.Markdown,
            replyMarkup: Kb.MainInline(),
            cancellationToken: ct);
    }

    private async Task HandleFallback(long chatId, CancellationToken ct)
    {
        await bot.SendMessage(chatId, 
            "Пожалуйста, выберите действие из меню 👇",
            replyMarkup: Kb.Main(), 
            cancellationToken: ct);
    }
    
#endregion
}
