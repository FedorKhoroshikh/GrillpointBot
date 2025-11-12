using GrillpointBot.Core.Common;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using GrillpointBot.Telegram.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrillpointBot.Telegram.BotHandlers;

public static class CallbackPrefixes
{
    public const string MainMenu = "home:menu";
    public const string AboutUs = "home:about";
    public const string Feedback = "home:feedback";
    public const string BackToMain = "home:back";
    
    public const string Category    = "cat:";           // префикс категории      
    
    public const string AddStart    = "item:add;";      // показать [-] 1 [+]
    public const string AddInc      = "item:inc;";      // +1 (в панели карточки)
    public const string AddDec      = "item:dec;";      // -1 (в панели карточки)
    
    public const string OpenCart     = "item:open;cart";   // показать корзину (из драфтов)
    public const string CartEdit     = "cart:edit";        // вернуться к выбору категорий (c сохранением текущего состояния)
    public const string CartContinue = "cart:continue";    // переход к комментарию
    public const string CartCheckout = "cart:checkout";    // продолжить оформление (следующий шаг)

    public const string RestartSession = "session:restart";
    public const string KeepSession    = "session:keep";

    public const string SkipComment = "comment:skip";      // не добавлять комментарий
    public const string SaveComment = "comment:save";      // сохранить комментарий
    public const string EditComment = "comment:edit";      // изменить комментарий
    
    public const string CheckoutMethodDelivery = "checkout:method:delivery";
    public const string CheckoutMethodPickup   = "checkout:method:pickup";

    public const string ChooseDate = "time:date";
    public const string ChooseTime = "time:choose";
    public const string SaveTime = "time:save";
    public const string EditTime = "time:edit";

    public const string SendPhone = "checkout:phone";
    public const string CheckoutConfirm = "checkout:confirm";
    public const string CheckoutEdit    = "checkout:edit";
    public const string CheckoutCancel  = "checkout:cancel";
}

public class CallbackHandler(
    ITelegramBotClient bot,
    CartHandler cartHandler,
    ISessionStore sessions,
    IMenuService menu,
    CatalogHandler catalogHandler,
    MessageHandler messageHandler,
    CheckoutHandler checkoutHandler,
    ConfirmHandler confirmHandler,
    MessagePipeline pipeline)
{
    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Data)) return;
        var data = query.Data;
        
        try
        {
            if (data.StartsWith(CallbackPrefixes.Category))
            {
                var category = data.Split(':')[2];
                await catalogHandler.ShowItemsAsync(query.Message!.Chat.Id, category, ct);
                await bot.AnswerCallbackQuery(query.Id, $"Открываю: {category}", cancellationToken: ct);
                return;
            }
            
            if (data.StartsWith("home:")) await HandleHome(data, query, ct);
            if (data.StartsWith("item:")) await HandleCardQty(data, query, ct);
            if (data.StartsWith("session:")) await HandleSession(data, query, ct);
            if (data.StartsWith("cart:") || data.StartsWith("item:")) await HandleCart(data, query, ct);
            if (data.StartsWith("comment:")) await HandleComment(data, query, ct);
            if (data.StartsWith("time:")) await HandleDateTimeSelection(data, query, ct);
            if (data.StartsWith("checkout:")) await HandleCheckout(data, query, ct);
            if (data.StartsWith("confirm:") || data == CallbackPrefixes.CheckoutConfirm
                                            || data == CallbackPrefixes.CheckoutEdit
                                            || data == CallbackPrefixes.CheckoutCancel)
                await HandleConfirm(data, query, ct);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task HandleHome(string data, CallbackQuery query, CancellationToken ct)
    {
        switch (data)
        {
            case CallbackPrefixes.MainMenu:
                await pipeline.RemoveKb(query.Message!.Chat.Id, query.Message.MessageId, ct);
                await catalogHandler.ShowCategoriesAsync(query.Message!.Chat.Id, ct);
                break;
            case CallbackPrefixes.AboutUs:
                await bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
                    "ℹ️ Grillpoint — уютное место с горячими сэндвичами и любовью к деталям. " +
                    "\n\nМы готовим простую и честную еду: короткое меню, стабильный вкус и быстрая подача.",
                    replyMarkup: Kb.BackToMain(), cancellationToken: ct);
                break;
            case CallbackPrefixes.Feedback:
                await bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
                    "⭐ Оставьте отзыв после доставки ..." +
                    "это очень помогает нам стать улучшаться 🙏",
                    replyMarkup: Kb.BackToMain(), cancellationToken: ct);
                break;
            case CallbackPrefixes.BackToMain:
                await bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
                    "Выберите действие:", replyMarkup: Kb.MainInline(), cancellationToken: ct);
                break;
        }
        await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
    }

    private async Task HandleCardQty(string data, CallbackQuery query, CancellationToken ct)
    {
        if (data.StartsWith(CallbackPrefixes.AddStart)) 
        { var id = data[CallbackPrefixes.AddStart.Length..]; 
            await cartHandler.StartInlineQtyAsync(query, id, ct); return; }
        
        if (data.StartsWith(CallbackPrefixes.AddInc))
        { var id = data[CallbackPrefixes.AddInc.Length..];   
            await cartHandler.ChangeInlineQtyAsync(query, id, +1, ct); return; }
        
        if (data.StartsWith(CallbackPrefixes.AddDec))   
        { var id = data[CallbackPrefixes.AddDec.Length..];   
            await cartHandler.ChangeInlineQtyAsync(query, id, -1, ct); return; }
    }
    private async Task HandleCart(string data, CallbackQuery query, CancellationToken ct)
    {
        var userId = query.From.Id;
        switch (data)
        {
            case CallbackPrefixes.OpenCart:
            {
                var s = await sessions.GetOrCreateAsync(query.From.Id);
                if (s.DraftQty.Count == 0)
                {
                    await bot.AnswerCallbackQuery(query.Id, "Корзина пуста", cancellationToken: ct);
                    return;
                }
                await cartHandler.ShowCartAsync(query, ct);
                return;
            }

            case CallbackPrefixes.CartEdit:
            {
                // возвращаем к категориям, qty в Draft остаются
                var s = await sessions.GetOrCreateAsync(userId);
                
                // удаляем корзину
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                await pipeline.DeleteIfExistsAsync(query.Message!.Chat.Id, s.CartMessageId, ct);
                s.CartMessageId = null;

                // показываем категории (reply-клава)
                var categories = await menu.GetCategoriesAsync();
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                var msg = await bot.SendMessage(query.Message!.Chat.Id, 
                    "Выберите категорию:", 
                    replyMarkup: Kb.Categories(categories), 
                    cancellationToken: ct);

                s.State = FlowState.Browsing;
                s.CategoriesMessageId = msg.Id;
                await sessions.UpsertAsync(s);
                return;
            }
            
            case CallbackPrefixes.CartContinue:
            {
                var s = await sessions.GetOrCreateAsync(query.From.Id);
                s.State = FlowState.CommentPending;
                await sessions.UpsertAsync(s);

                if (s.CartMessageId is { } cartMid)
                {
                    try
                    {
                        await bot.EditMessageReplyMarkup(
                            query.Message.Chat.Id,
                            cartMid,
                            replyMarkup: null,
                            cancellationToken: ct);
                    } 
                    catch { /* ignore */ }
                }
                
                var msg = await bot.SendMessage(query.Message.Chat.Id, 
                    "✏️ Хотите оставить комментарий к заказу?\nЕсли да — напишите его сейчас сообщением 👇",
                    replyMarkup: Kb.SkipComment(), 
                    cancellationToken: ct);
                s.CommentMessageIds.Add(msg.MessageId);
                await sessions.UpsertAsync(s);
                return;
            }
            
            case CallbackPrefixes.CartCheckout:
                // здесь можно дернуть следующий шаг CheckoutHandler (способ получения и т.д.)
                await bot.AnswerCallbackQuery(query.Id, 
                    "Оформление: выберите способ получения (доставка/самовывоз).", 
                    cancellationToken: ct);
                // TODO: checkoutHandler.StartAsync(query, ct);
                return;
            
            default:
                // неизвестный callback — просто закрыть всплывашку
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                break;
        }
    }
    
    private async Task HandleSession(string data, CallbackQuery query, CancellationToken ct)
    {
        if (data.StartsWith(CallbackPrefixes.RestartSession))
        {
            await sessions.RemoveAsync(query.From.Id);
            var s = new Session { UserId = query.From.Id };
            await sessions.UpsertAsync(s);
                
            await pipeline.DeleteIfExistsAsync(query.Message!.Chat.Id, query.Message.MessageId, ct);
                
            await bot.AnswerCallbackQuery(query.Id, "Сессия очищена", cancellationToken: ct);
            await messageHandler.SendWelcomeAsync(query.Message!.Chat.Id, ct);
            return;
        }
            
        if (data.StartsWith(CallbackPrefixes.KeepSession))
        {
            await bot.AnswerCallbackQuery(query.Id, "Продолжаем текущую сессию", cancellationToken: ct);
        }
    }

    private async Task HandleComment(string data, CallbackQuery query, CancellationToken ct)
    {
        if (data.StartsWith(CallbackPrefixes.SkipComment))
        {
            var s = await sessions.GetOrCreateAsync(query.From.Id);
            s.Comment = null;
            s.DraftComment = null;
            s.State = FlowState.CheckoutMethod;
            await sessions.UpsertAsync(s);
            
            if (s.CommentMessageIds.Count > 0)
                await pipeline.DeleteManyAsync(query.Message!.Chat.Id, s.CommentMessageIds, ct);
            s.CommentMessageIds.Clear();
            await sessions.UpsertAsync(s);

            await checkoutHandler.StartAsync(query.Message!.Chat.Id, query.From.Id, ct);
            await bot.AnswerCallbackQuery(query.Id, "Заказ без комментария", cancellationToken: ct);
            return;
        }
        
        if (data.StartsWith(CallbackPrefixes.EditComment))
        {
            var s = await sessions.GetOrCreateAsync(query.From.Id);
            s.State = FlowState.CommentPending;
            await sessions.UpsertAsync(s);

            var msg = await bot.EditMessageText(query.Message!.Chat.Id, 
                query.Message.MessageId,  
                "Введите новый комментарий:",
                replyMarkup: Kb.SkipComment(),
                cancellationToken: ct);
            
            s.CommentMessageIds.Add(msg.MessageId);
            await sessions.UpsertAsync(s);

            await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
            return;
        }
        
        if (data.StartsWith(CallbackPrefixes.SaveComment))
        {
            var s = await sessions.GetOrCreateAsync(query.From.Id);
            s.Comment = s.DraftComment;
            s.DraftComment = null;
            s.State = FlowState.CheckoutMethod;
            await sessions.UpsertAsync(s);
            
            // удаляем корзину + историю диалога по комменту
            if (s.CartMessageId is { } cmid)
                await pipeline.DeleteIfExistsAsync(query.Message!.Chat.Id, cmid, ct);
            if (s.CommentMessageIds.Count > 0)
                await pipeline.DeleteManyAsync(query.Message.Chat.Id, s.CommentMessageIds, ct);

            s.CartMessageId = null;
            s.CommentMessageIds.Clear();
            await sessions.UpsertAsync(s);
            
            await bot.SendMessage(
                query.Message!.Chat.Id,
                "Комментарий сохранён \u2705\n\nПереходим к выбору способа получения.",
                cancellationToken: ct);
            await checkoutHandler.StartAsync(query.Message!.Chat.Id, query.From.Id, ct);
        }
    }
    
    private async Task HandleCheckout(string data, CallbackQuery query, CancellationToken ct)
    {
        switch (data)
        {
            case CallbackPrefixes.CheckoutMethodDelivery:
                await checkoutHandler.HandleMethodAsync(query, isDelivery: true, ct); return;
            case CallbackPrefixes.CheckoutMethodPickup:
                await checkoutHandler.HandleMethodAsync(query, isDelivery: false, ct); return;
            
            // переходим к шагу "указать телефон"
            case CallbackPrefixes.SendPhone:
                var s = await sessions.GetOrCreateAsync(query.From.Id);
                s.State = FlowState.Confirm;
                await sessions.UpsertAsync(s);
                await checkoutHandler.SendConfirmCard(query.Message!.Chat.Id, s, ct);
                await bot.AnswerCallbackQuery(query.Id, "Телефон получен ✅", cancellationToken: ct);
                return;
        }
    }
    
    private async Task HandleConfirm(string data, CallbackQuery query, CancellationToken ct)
    {
        switch (data)
        {
            case CallbackPrefixes.CheckoutConfirm:
                await confirmHandler.HandleConfirm(query, ct); 
                await bot.SendMessage(query.Message!.Chat.Id,
                    text: "Выберите действие:",
                    replyMarkup: Kb.MainInline(),
                    cancellationToken: ct);
                return;
            case CallbackPrefixes.CheckoutEdit:
                // Возврат к выбору способа
                await checkoutHandler.StartAsync(query.Message!.Chat.Id, query.From.Id, ct);
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                return;
            case CallbackPrefixes.CheckoutCancel:
            {
                var s = await sessions.GetOrCreateAsync(query.From.Id);
                s.State = FlowState.Browsing;
                await sessions.UpsertAsync(s);
                await bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
                    "Оформление отменено. Выберите категорию:", cancellationToken: ct);
                var categories = await menu.GetCategoriesAsync();
                await bot.SendMessage(query.Message.Chat.Id, "📋 Выберите категорию:",
                    replyMarkup: Kb.Categories(categories), cancellationToken: ct);
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                break;
            }
        }
    }

    private async Task HandleDateTimeSelection(string data, CallbackQuery query, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(query.From.Id);

        if (data.StartsWith(CallbackPrefixes.ChooseDate))
        {
            var date = DateTime.ParseExact(data.Split(':')[2], "yyyyMMdd", null);
            s.DraftDelivery.ScheduledTime = date;
            await sessions.UpsertAsync(s);

            var tmsg = await bot.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Выберите время:",
                replyMarkup: Kb.TimeKb(date),
                cancellationToken: ct);
            
            await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
            s.CheckoutMessageIds.Add(tmsg.MessageId);
            await sessions.UpsertAsync(s);
            return;
        }
        
        if (data.StartsWith(CallbackPrefixes.ChooseTime))
        {
            var dt = DateTime.ParseExact(data.Split(':')[2], "yyyyMMddHHmm", null);
            s.DraftDelivery.ScheduledTime = dt;
            s.State = FlowState.CheckoutPhone;
            await sessions.UpsertAsync(s);

            // очищаем клавиатуру с часами
            await pipeline.RemoveKb(query.Message!.Chat.Id, query.Message.MessageId, ct);
            
            // подтверждаем выбранное время
            var tmsg = await bot.SendMessage(
                query.Message!.Chat.Id,
                $"Вы выбрали: <b>{dt:dd.MM HH:mm}</b>",
                parseMode: ParseMode.Html,
                replyMarkup: Kb.SaveOrEdit(CallbackPrefixes.SaveTime, CallbackPrefixes.EditTime),
                cancellationToken: ct);
            
            s.CheckoutMessageIds.Add(tmsg.MessageId);
            await sessions.UpsertAsync(s);
            return;
        }
        
        switch (data)
        {
            case CallbackPrefixes.SaveTime:
                await pipeline.RemoveKb(query.Message!.Chat.Id, query.Message.MessageId, ct);
            
                s.State = FlowState.CheckoutPhone;
                await sessions.UpsertAsync(s);
                await checkoutHandler.AskPhoneAsync(query.Message.Chat.Id, query.From.Id, ct);
                await bot.AnswerCallbackQuery(query.Id, "Время сохранено ✅", cancellationToken: ct);
                return;
            
            case CallbackPrefixes.EditTime:
            {
                var emsg = await bot.EditMessageText(
                    query.Message!.Chat.Id, query.Message.MessageId,
                    "Выберите новую дату:",
                    replyMarkup: Kb.DateKb(),
                    cancellationToken: ct);
            
                s.CheckoutMessageIds.Add(emsg.MessageId);
                await sessions.UpsertAsync(s);
                break;
            }
        }
    }
}
