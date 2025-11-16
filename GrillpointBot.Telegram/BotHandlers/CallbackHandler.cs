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
    // Префикс навигации стартовой страницы
    public const string MainMenu = "home:menu";
    public const string AboutUs  = "home:about";
    public const string Feedback = "home:feedback";
    public const string Back     = "home:back";

    // Префикс категории меню
    public const string Category    = "cat:";           // префикс категории
    
    // Префиксы карточки товара
    public const string AddStart    = "item:add;";      // показать [-] 1 [+]
    public const string AddInc      = "item:inc;";      // +1 (в панели карточки)
    public const string AddDec      = "item:dec;";      // -1 (в панели карточки)
    
    // Префиксы шагов работы с корзиной товаров
    public const string OpenCart     = "item:open;cart";   // показать корзину (из драфтов)
    public const string CartEdit     = "cart:edit";        // вернуться к выбору категорий (с сохранением текущего состояния)
    public const string CartContinue = "cart:continue";    // переход к комментарию
    public const string CartCheckout = "cart:checkout";    // продолжить оформление (следующий шаг)

    // Префиксы шагов обработки статуса сессии
    public const string RestartSession = "session:restart";
    public const string KeepSession    = "session:keep";

    // Префиксы шагов указания комментария к заказу
    public const string SkipComment = "comment:skip";      // не добавлять комментарий
    public const string SaveComment = "comment:save";      // сохранить комментарий
    public const string EditComment = "comment:edit";      // изменить комментарий
    
    // Префиксы шагов выбора получения заказа
    public const string CheckoutMethodDelivery = "checkout:method:delivery";
    public const string CheckoutMethodPickup   = "checkout:method:pickup";
    
    // Префиксы шагов указания адреса доставки
    public const string AddressManual       = "address:manual";
    public const string AddressGeoCurrent   = "address:geo:current";   // RequestLocation
    public const string AddressGeoManual    = "address:geo:manual";    // “указать на карте”
    public const string AddressConfirm      = "address:confirm";
    public const string AddressEdit         = "address:edit";
    public const string AddressBackToMethod = "address:back:method";
    public const string PickupConfirm       = "pickup:confirm";

    // Префиксы шагов выбора даты и времени получения заказа
    public const string ChooseDate = "time:date";
    public const string ChooseTime = "time:choose";
    public const string SaveTime   = "time:save";
    public const string EditTime   = "time:edit";

    // Префиксы шага подтверждения номера телефона
    public const string SendPhone = "checkout:phone";
    
    // Префикс шагов подтверждения заказа
    public const string CheckoutConfirm = "checkout:confirm";
    public const string CheckoutEdit    = "checkout:edit";
    public const string CheckoutCancel  = "checkout:cancel";
    public const string BackToWelcome   = "checkout:welcome";
}

public class CallbackHandler(
    ITelegramBotClient bot,
    ISessionStore sessions,
    IMenuService menu,
    CartHandler cartHandler,
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
            if (data.StartsWith("checkout:") || data.StartsWith("address:")
                                             || data == CallbackPrefixes.PickupConfirm) 
                await HandleCheckout(data, query, ct);
            
            if (data.StartsWith("confirm:") || data == CallbackPrefixes.CheckoutConfirm
                                            || data == CallbackPrefixes.CheckoutEdit
                                            || data == CallbackPrefixes.CheckoutCancel
                                            || data == CallbackPrefixes.BackToWelcome)
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
        const string backTxt = "На главную";   
        
        switch (data)
        {
            case CallbackPrefixes.MainMenu:
                // Удаляем приветственное сообщение
                await pipeline.DeleteIfExistsAsync(query.Message!.Chat.Id, query.Message.MessageId, ct);
                await catalogHandler.ShowCategoriesAsync(query.Message!.Chat.Id, ct);
                break;
            case CallbackPrefixes.AboutUs:
                await bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
                    "ℹ️ Grillpoint — уютное место с горячими сэндвичами и любовью к деталям. " +
                    "\n\nМы готовим простую и честную еду: короткое меню, стабильный вкус и быстрая подача.",
                    replyMarkup: Kb.Back(CallbackPrefixes.Back, backTxt), cancellationToken: ct);
                break;
            case CallbackPrefixes.Feedback:
                await bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
                    "⭐ Оставьте отзыв после доставки ..." +
                    "это очень помогает нам стать улучшаться 🙏",
                    replyMarkup: Kb.Back(CallbackPrefixes.Back, backTxt), cancellationToken: ct);
                break;
            case CallbackPrefixes.Back:
                await bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
                    "Выберите действие:", replyMarkup: Kb.MainInline, cancellationToken: ct);
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

            // возвращаем к категориям, qty в Draft остаются
            case CallbackPrefixes.CartEdit:
            {
                var s = await sessions.GetOrCreateAsync(userId);
                
                // удаляем корзину
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                await pipeline.DeleteIfExistsAsync(query.Message!.Chat.Id, s.CartMessageId, ct);
                s.CartMessageId = null;

                // показываем категории (reply-клава)
                var categories = await menu.GetCategoriesAsync();
                var msg = await bot.SendMessage(query.Message!.Chat.Id, 
                    "Выберите категорию:", 
                    replyMarkup: Kb.Categories(categories), 
                    cancellationToken: ct);

                s.State = FlowState.Browsing;
                s.CategoriesMessageId = msg.MessageId;
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
                            query.Message!.Chat.Id,
                            cartMid,
                            replyMarkup: null,
                            cancellationToken: ct);
                    } 
                    catch { /* ignore */ }
                }
                
                var msg = await bot.SendMessage(query.Message!.Chat.Id, 
                    "✏️ Хотите оставить комментарий к заказу?\nЕсли да — напишите его сейчас сообщением 👇",
                    replyMarkup: Kb.SkipComment, 
                    cancellationToken: ct);
                s.CommentMessageIds.Add(msg.MessageId);
                await sessions.UpsertAsync(s);
                return;
            }
            
            case CallbackPrefixes.CartCheckout:
                await checkoutHandler.StartAsync(query.Message!.Chat.Id, query.From.Id, ct);
                await bot.AnswerCallbackQuery(query.Id, "Оформление начато", cancellationToken: ct);
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
            await messageHandler.SendWelcomeAsync(query.Message!.Chat.Id, ct);
        }
    }

    private async Task HandleComment(string data, CallbackQuery query, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(query.From.Id);
        var chatId = query.Message!.Chat.Id;
        var msgId = query.Message.MessageId;
        var queryId = query.Id;
        var fromId = query.From.Id;
        Message? cMsg;
        
        if (data.StartsWith(CallbackPrefixes.SkipComment))
        {
            s.Comment = null;
            s.DraftComment = null;
            s.State = FlowState.CheckoutMethod;
            await sessions.UpsertAsync(s);
            
            if (s.CommentMessageIds.Count > 0)
                await pipeline.DeleteManyAsync(chatId, s.CommentMessageIds, ct);
            s.CommentMessageIds.Clear();
            await sessions.UpsertAsync(s);

            await checkoutHandler.StartAsync(chatId, fromId, ct);
            await bot.AnswerCallbackQuery(queryId, "Заказ без комментария", cancellationToken: ct);
            return;
        }
        
        if (data.StartsWith(CallbackPrefixes.EditComment))
        {
            s.State = FlowState.CommentPending;
            await sessions.UpsertAsync(s);

            cMsg = await bot.EditMessageText(chatId, 
                msgId,  
                "Введите новый комментарий:",
                replyMarkup: Kb.SkipComment,
                cancellationToken: ct);
            
            s.CommentMessageIds.Add(cMsg.MessageId);
            await sessions.UpsertAsync(s);

            await bot.AnswerCallbackQuery(queryId, cancellationToken: ct);
            return;
        }
        
        if (data.StartsWith(CallbackPrefixes.SaveComment))
        {
            s.Comment = s.DraftComment;
            s.DraftComment = null;
            s.State = FlowState.CheckoutMethod;
            await sessions.UpsertAsync(s);
            
            // удаляем корзину + историю диалога по комменту
            if (s.CartMessageId is { } cmid)
                await pipeline.DeleteIfExistsAsync(chatId, cmid, ct);
            if (s.CommentMessageIds.Count > 0)
                await pipeline.DeleteManyAsync(chatId, s.CommentMessageIds, ct);

            s.CartMessageId = null;
            s.CommentMessageIds.Clear();
            await sessions.UpsertAsync(s);
            
            cMsg = await bot.SendMessage(chatId,
                "Комментарий сохранён \u2705\n\nПереходим к выбору способа получения.",
                cancellationToken: ct);
            
            s.CommentMessageIds.Add(cMsg.MessageId);
            await sessions.UpsertAsync(s);
            
            await checkoutHandler.StartAsync(chatId, fromId, ct);
        }
    }
    
    private async Task HandleCheckout(string data, CallbackQuery query, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(query.From.Id);
        var chatId = query.Message!.Chat.Id;
        var userId =  query.From.Id;
        var queryId = query.Id;
        Message? aMsg;
        
        switch (data)
        {
            case CallbackPrefixes.CheckoutMethodDelivery:
                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                await checkoutHandler.HandleMethodAsync(query, isDelivery: true, ct); 
                return;
            
            case CallbackPrefixes.CheckoutMethodPickup:
                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                s.DraftDelivery.Method = DeliveryMethod.Pickup;
                s.State = FlowState.PickupPreview;
                await sessions.UpsertAsync(s);

                await checkoutHandler.ShowPickupPointAsync(chatId, userId, ct);
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                return;
            
            case CallbackPrefixes.AddressGeoCurrent:
                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                s.State = FlowState.CheckoutAddressGeo;
                await sessions.UpsertAsync(s);
                aMsg = await bot.SendMessage(chatId,
                    "📍 Отправьте свою текущую геопозицию." +
                    "\n\nℹ️ Для передачи своей текущей локации требуется доступ к местоположению. " +
                    "\n\n Если у вас отключена эта функция: " +
                    "\n  1) Перейдите в [⚙️ Настройки] > [Местоположение]" +
                    "\n  2) 🟢 активируйте доступ.",
                    replyMarkup: Kb.Back(CallbackPrefixes.CheckoutMethodDelivery),
                    cancellationToken: ct);
                
                var geoKb = await bot.SendMessage(chatId,
                    "👇 Нажмите кнопку ниже, чтобы отправить локацию.",
                    replyMarkup: Kb.GeoCurrent,
                    cancellationToken: ct);
                
                s.CheckoutMessageIds.AddRange([aMsg.MessageId,  geoKb.MessageId]);
                await sessions.UpsertAsync(s);
                return;

            case CallbackPrefixes.AddressGeoManual:
                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                await checkoutHandler.AskManualMapInstruction(chatId, query.From.Id, ct);
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                return;
            
            case CallbackPrefixes.AddressManual:
                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                s.State = FlowState.CheckoutAddressManual;
                await sessions.UpsertAsync(s);
                aMsg = await bot.SendMessage(chatId,
                    "✏️ Введите адрес вручную (улица + номер дома):",
                    replyMarkup: Kb.Back(CallbackPrefixes.CheckoutMethodDelivery),
                    cancellationToken: ct);
                s.CheckoutMessageIds.Add(aMsg.MessageId);
                await sessions.UpsertAsync(s);
                return;

            case CallbackPrefixes.AddressEdit:
                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                await checkoutHandler.AskAddressModeAsync(chatId, userId, ct);
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                return;
            
            case CallbackPrefixes.AddressBackToMethod:
                var msg = await bot.SendMessage(chatId,
                    "Возвращаемся к выбору способа получения.",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: ct);

                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                s.CheckoutMessageIds.Clear();
                s.CheckoutMessageIds.Add(msg.MessageId);
                await sessions.UpsertAsync(s);

                await checkoutHandler.StartAsync(chatId, query.From.Id, ct);
                await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
                return;
            
            case CallbackPrefixes.AddressConfirm:
                s.State = FlowState.CheckoutTime;
                await sessions.UpsertAsync(s);
                await checkoutHandler.AskDateAsync(chatId, userId, ct);
                return;
            
            case CallbackPrefixes.PickupConfirm:
                s.State = FlowState.CheckoutTime;
                await sessions.UpsertAsync(s);

                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                s.CheckoutMessageIds.Clear();

                await checkoutHandler.AskDateAsync(chatId, userId, ct);
                await bot.AnswerCallbackQuery(queryId, cancellationToken: ct);
                return;
            
            // переходим к шагу "указать телефон"
            case CallbackPrefixes.SendPhone:
                s.State = FlowState.Confirm;
                await sessions.UpsertAsync(s);
                await checkoutHandler.SendConfirmCard(chatId, s, ct);
                await bot.AnswerCallbackQuery(queryId, "Телефон получен ✅", cancellationToken: ct);
                return;
        }
    }
    
    private async Task HandleConfirm(string data, CallbackQuery query, CancellationToken ct)
    {
        var userId = query.From.Id;
        var s = await sessions.GetOrCreateAsync(userId);
        var chatId = query.Message!.Chat.Id;
        var msgId = query.Message.MessageId;
        var queryId = query.Id;

        switch (data)
        {
            case CallbackPrefixes.CheckoutConfirm:
                await confirmHandler.HandleConfirm(query, ct);
                await sessions.UpsertAsync(s);
                return;
            
            case CallbackPrefixes.CheckoutEdit:
                
                // Возврат к выбору способа
                await checkoutHandler.StartAsync(chatId, userId, ct);
                await bot.AnswerCallbackQuery(queryId, cancellationToken: ct);
                return;
            
            case CallbackPrefixes.CheckoutCancel:
            case CallbackPrefixes.BackToWelcome:
            {
                await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
                await messageHandler.SendWelcomeAsync(chatId, ct);
                // await messageHandler.AskNewSessionAsync(chatId, userId, ct); // TODO: Handle session saved info and userProfile data
                await bot.AnswerCallbackQuery(queryId, cancellationToken: ct);
                return;
            }
        }
    }

    private async Task HandleDateTimeSelection(string data, CallbackQuery query, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(query.From.Id);
        var chatId = query.Message!.Chat.Id;
        var msgId = query.Message.MessageId;
        var queryId = query.Id;
        Message tMsg;

        if (data.StartsWith(CallbackPrefixes.ChooseDate))
        {
            var date = DateTime.ParseExact(data.Split(':')[2], "yyyyMMdd", null);
            s.DraftDelivery.ScheduledTime = date;
            await sessions.UpsertAsync(s);

            tMsg = await bot.EditMessageText(chatId, msgId,
                "Выберите время:", replyMarkup: Kb.TimeKb(date), cancellationToken: ct);
            
            await bot.AnswerCallbackQuery(queryId, cancellationToken: ct);
            s.CheckoutMessageIds.Add(tMsg.MessageId);
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
            await pipeline.RemoveKb(chatId, msgId, ct);
            
            // подтверждаем выбранное время
            tMsg = await bot.SendMessage(chatId, 
                $"Вы выбрали: <b>{dt:dd.MM HH:mm}</b>",
                parseMode: ParseMode.Html,
                replyMarkup: Kb.SaveOrEdit(CallbackPrefixes.SaveTime, CallbackPrefixes.EditTime),
                cancellationToken: ct);
            
            s.CheckoutMessageIds.Add(tMsg.MessageId);
            await sessions.UpsertAsync(s);
            return;
        }
        
        switch (data)
        {
            case CallbackPrefixes.SaveTime:
                await pipeline.RemoveKb(chatId, msgId, ct);
                s.State = FlowState.CheckoutPhone;
                await sessions.UpsertAsync(s);
                await checkoutHandler.AskPhoneAsync(chatId, query.From.Id, ct);
                await bot.AnswerCallbackQuery(queryId, "Время сохранено ✅", cancellationToken: ct);
                return;
            
            case CallbackPrefixes.EditTime:
            {
                var eMsg = await bot.EditMessageText(
                    chatId, msgId, "Выберите новую дату:",
                    replyMarkup: Kb.DateKb(), cancellationToken: ct);
            
                s.CheckoutMessageIds.Add(eMsg.MessageId);
                await sessions.UpsertAsync(s);
                break;
            }
        }
    }
}
