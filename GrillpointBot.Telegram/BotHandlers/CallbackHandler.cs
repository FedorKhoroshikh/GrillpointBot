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
    
    public const string SaveComment = "comment:save";
    public const string EditComment = "comment:edit";
}

public class CallbackHandler(
    ITelegramBotClient bot,
    CartHandler cartHandler,
    ISessionStore sessions,
    IMenuService menu,
    CatalogHandler catalogHandler,
    MessageHandler messageHandler,
    MessagePipeline pipeline)
{
    private const string CmdSelect = "select_";
    private const string  CmdConfirm = "confirm_";
    private const string CmdMenuBack = "menu_back";

    public async Task HandleAsync(CallbackQuery query, CancellationToken ct)
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

            if (data.StartsWith("item:")) await HandleCardQty(data, query, ct);
            if (data.StartsWith("session:")) await HandleSession(data, query, ct);
            if (data.StartsWith("cart:") || data.StartsWith("item:")) await HandleCart(data, query, ct);
            if (data.StartsWith("comment:")) await HandleComment(data, query, ct);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task HandleSelectAsync(long chatId, string data, CallbackQuery query, CancellationToken ct)
    {
        var (order, item) = await CreateOrder(data, CmdSelect, query);
        //await orderService.CreateAsync(order);
            
        string text = $"🍔 *{item.Name}*\n💰 {item.Price} ₽\n\nПодтвердить заказ?";
        var buttons = new InlineKeyboardMarkup(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", $"confirm_{item.Id}"),
                InlineKeyboardButton.WithCallbackData("🔁 Изменить", CmdMenuBack)
            ]
        ]);
            
        await bot.EditMessageText(chatId, query.Message!.MessageId, text,
            parseMode: ParseMode.Markdown, replyMarkup: buttons, cancellationToken: ct);
    }

    private async Task HandleConfirmAsync(string data, CallbackQuery query, CancellationToken ct)
    {
        var (order, item) = await CreateOrder(data, CmdConfirm, query);
        
        await bot.EditMessageText(query.Message!.Chat.Id, query.Message!.MessageId,
            $"✅ Заказ подтверждён: *{item.Name}* — {item.Price} ₽\n\nТеперь выберите способ получения:",
            parseMode: ParseMode.Markdown, cancellationToken: ct);

        //await deliveryHandler.StartDeliveryFlowAsync(order, ct);
        await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
    }

    private async Task HandleMenuBackAsync(CallbackQuery query, CancellationToken ct)
    {
        await bot.SendMessage(query.Message!.Chat.Id,
            "Выберите категорию из меню 📋",
            replyMarkup: new ReplyKeyboardMarkup(
                [
                    [ new KeyboardButton(Constants.MenuCmd) ],
                    [ new KeyboardButton(Constants.AboutUsCmd), new KeyboardButton(Constants.FeedbackCmd) ]
                ])
                { ResizeKeyboard = true },
            cancellationToken: ct);

        await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
    }

    private async Task<(Order order, MenuItem item)> CreateOrder(string data, string cmdForReplace, CallbackQuery query)
    {
        var id = data.Replace(cmdForReplace, string.Empty);
        var item = new MenuItem();
        if (item == null) return (null, null)!;

        var order = new Order
        {
            UserId = query.From.Id,
            UserName = string.Join(' ',
                    new[] { query.From.FirstName, query.From.LastName }
                        .Where(s => !string.IsNullOrWhiteSpace(s)))
                .Trim()
        };

        return (order, item);
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
                await cartHandler.ShowCartAsync(query, ct);
                return;
            
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

                var msg = await bot.SendMessage(
                    query.Message!.Chat.Id,
                    "✏️ Хотите оставить комментарий к заказу?\nЕсли да — напишите его сейчас сообщением 👇",
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
            return;
        }
    }

    private async Task HandleComment(string data, CallbackQuery query, CancellationToken ct)
    {
        if (data.StartsWith(CallbackPrefixes.EditComment))
        {
            var s = await sessions.GetOrCreateAsync(query.From.Id);
            s.State = FlowState.CommentPending;
            await sessions.UpsertAsync(s);

            var msg = await bot.EditMessageText(query.Message!.Chat.Id, 
                query.Message.MessageId, "Введите новый комментарий:", 
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
            return;
        }
    }
}
