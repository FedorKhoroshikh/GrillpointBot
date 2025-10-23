using GrillpointBot.Core.Common;
using GrillpointBot.Core.Config;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrillpointBot.Telegram.BotHandlers;

public class CallbackHandler(
    ITelegramBotClient bot, 
    IMenuService menuService, 
    IOrderService orderService, 
    DeliveryHandler deliveryHandler, 
    AppSettings config)
{
    private long _adminChatId => config.AdminChatId;

   private const string CmdSelect = "select_";
   private const string  CmdConfirm = "confirm_";
   private const string CmdMenuBack = "menu_back";

    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        var chatId = query.Message!.Chat.Id;
        var data = query.Data ?? "";

        try
        {
            if (data.StartsWith(CmdSelect))
            {
                await HandleSelectAsync(chatId, data, query, ct);
            }
            else if (data.StartsWith(CmdConfirm))
            {
                await HandleConfirmAsync(data, query, ct);   
            }
            else if (data == CmdMenuBack)
            {
                await HandleMenuBackAsync(query, ct);
            }
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
        await orderService.CreateOrderAsync(order);
            
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

        await deliveryHandler.StartDeliveryFlowAsync(order, ct);
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
        var item = await menuService.GetItemByIdAsync(id);
        if (item == null) return (null, null)!;

        var order = new Order
        {
            ItemId = item.Id,
            ItemName = item.Name,
            Price = item.Price,
            UserId = query.From.Id,
            UserName = string.Join(' ',
                    new[] { query.From.FirstName, query.From.LastName }
                        .Where(s => !string.IsNullOrWhiteSpace(s)))
                .Trim()
        };

        return (order, item);
    }
}
