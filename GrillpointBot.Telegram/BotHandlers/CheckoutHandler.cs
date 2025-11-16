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
        if (s.CartMessageId is { } cmid)
            await pipeline.DeleteIfExistsAsync(chatId, cmid, ct); // Удаляем сообщение с корзиной
        s.CartMessageId = null;

        await pipeline.DeleteManyAsync(chatId, s.CommentMessageIds, ct);
        await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
        s.CheckoutMessageIds.Clear();
        
        s.State = FlowState.CheckoutMethod;
        await sessions.UpsertAsync(s);
        
        var cmMsg = await bot.SendMessage(chatId,
            "Как хотите получить заказ?",
            replyMarkup: Kb.CheckoutMethod,
            cancellationToken: ct);
        
        s.CheckoutMessageIds.Add(cmMsg.MessageId);
        await sessions.UpsertAsync(s);
    }
    
#region Handle address methods

    public async Task AskAddressModeAsync(long chatId, long userId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);
        s.State = FlowState.CheckoutAddressChoice;
        await sessions.UpsertAsync(s);
            
        var text =
            "⬆️ Зона доставки - на изображении.\n\n" +
            "Укажите адрес доставки:\n\n" +
            "Вы можете:\n" +
            "• 📍 Отправить текущую геопозицию\n" +
            "• 🗺 Указать дом на карте\n" +
            "• ✏️ Ввести адрес вручную\n\n" +
            "\n";
        
        var msg = await bot.SendPhoto(
            chatId,
            GetOutOfZoneImage,
            caption: text,
            parseMode: ParseMode.Html,
            replyMarkup: Kb.AddressChoice(),
            cancellationToken: ct);

        s.CheckoutMessageIds.Add(msg.MessageId);
        await sessions.UpsertAsync(s);
    }
    
    public async Task HandleMethodAsync(CallbackQuery cq, bool isDelivery, CancellationToken ct)
    {
        var userId = cq.From.Id;
        var s = await sessions.GetOrCreateAsync(userId);
        var chatId = cq.Message!.Chat.Id;
        
        s.DraftDelivery.Method = isDelivery ? DeliveryMethod.Delivery : DeliveryMethod.Pickup;

        if (!isDelivery)
        {
            s.State = FlowState.CheckoutPhone;
            await sessions.UpsertAsync(s);
            await AskPhoneAsync(chatId, userId, ct);
            await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
            return;
        }
        
        // Доставка → выбор способа указания адреса
        s.State = FlowState.CheckoutAddressChoice;
        await sessions.UpsertAsync(s);
        await AskAddressModeAsync(chatId, userId, ct);
        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
    }
    
    // ---------- РУЧНОЙ АДРЕС ----------
    
    public async Task HandleManualAddressAsync(Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var s = await sessions.GetOrCreateAsync(msg.From!.Id);

        var text = msg.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text)) return;

        var (lat, lon, parsed) = await GeoProcessor.ForwardParseAsync(text);

        if (lat == 0 && lon == 0)
        {
            await bot.SendMessage(chatId,
                "Не удалось распознать адрес 😕\n" +
                "Попробуйте уточнить формулировку (улица + номер дома).",
                cancellationToken: ct);
            return;
        }

        if (!GeoProcessor.IsInPolygon((lat, lon)))
        {
            await SendOutOfZoneWarningAsync(chatId, msg.From.Id, parsed.DisplayAddress, ct);
            return;
        }

        
        FillDraftDeliveryFromParsed(s, lat, lon, parsed);
        s.State = FlowState.CheckoutAddressConfirm;
        await sessions.UpsertAsync(s);

        await ShowAddressConfirm(chatId, s, ct);
    }
    
    // ---------- GEO: текущая геопозиция + “указать на карте” ----------
    
    public async Task HandleGeoAsync(Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var s = await sessions.GetOrCreateAsync(msg.From!.Id);
        if (msg.Location is null) return;

        var lat = msg.Location.Latitude;
        var lon = msg.Location.Longitude;

        var parsed = await GeoProcessor.ReverseParseAsync(lat, lon);
        
        if (!GeoProcessor.IsInPolygon((lat, lon)))
        {
            await SendOutOfZoneWarningAsync(chatId, msg.From.Id, parsed.DisplayAddress, ct);
            return;
        }
        
        FillDraftDeliveryFromParsed(s, lat, lon, parsed);
        s.State = FlowState.CheckoutAddressConfirm;
        await sessions.UpsertAsync(s);

        await ShowAddressConfirm(chatId, s, ct);
    }
    
    // через “указать на карте” сценарий
    public async Task AskManualMapInstruction(long chatId, long userId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);

        const string text = 
            "🗺 Чтобы указать адрес на карте:\n\n" +
            "1) Нажмите 📎 → [Геопозиция]\n" +
            "2) Выберите точку на карте\n" +
            "3) Нажмите «Отправить выбранную геопозицию»";

        var m = await bot.SendMessage(chatId, text,
            replyMarkup: Kb.Back(CallbackPrefixes.CheckoutMethodDelivery), 
            cancellationToken: ct);
        s.CheckoutMessageIds.Add(m.MessageId);
        s.State = FlowState.CheckoutAddressGeo;
        await sessions.UpsertAsync(s);
    }
    
    // ---------- Подтверждение адреса и переход к дате ----------
    
    private async Task ShowAddressConfirm(long chatId, Session s, CancellationToken ct)
    {
        await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
        s.CheckoutMessageIds.Clear();

        var lMdg = await bot.SendLocation(chatId, 
            s.DraftDelivery.Lat, s.DraftDelivery.Lon, cancellationToken: ct);

        var m = await bot.SendMessage(chatId,
            $"📍 Подтвердите выбранный адрес:\n\n{s.DraftDelivery.AddressDisplay}",
            parseMode: ParseMode.Html,
            replyMarkup: Kb.SaveOrEdit(CallbackPrefixes.AddressConfirm, CallbackPrefixes.AddressEdit),
            cancellationToken: ct);
        
        s.CheckoutMessageIds.AddRange([lMdg.MessageId, m.MessageId]);
        await sessions.UpsertAsync(s);
    }
    
    private static void FillDraftDeliveryFromParsed(Session s, double lat, double lon, ParsedAddress parsed)
    {
        s.DraftDelivery.FullAddress = parsed.FullAddress;
        s.DraftDelivery.AddressDisplay = parsed.DisplayAddress;
        
        s.DraftDelivery.City = parsed.City;
        s.DraftDelivery.Locality = parsed.Locality;
        s.DraftDelivery.Street = parsed.Road;
        s.DraftDelivery.House = parsed.HouseNumber;
        s.DraftDelivery.Postcode = parsed.Postcode;
        s.DraftDelivery.POI = parsed.POI;
        
        s.DraftDelivery.Lat = lat;
        s.DraftDelivery.Lon = lon;
    }

#endregion

    public async Task ShowPickupPointAsync(long chatId, long userId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);

        // Чистим старые шаги
        await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
        s.CheckoutMessageIds.Clear();

        // Координаты точки (Grillpoint)
        const double lat = 59.7345993;
        const double lon = 30.3348391;

        // Обратный геокодинг
        var parsed = await GeoProcessor.ReverseParseAsync(lat, lon);

        // 1. Карта
        var mapMsg = await bot.SendLocation(chatId, lat, lon, cancellationToken: ct);
        FillDraftDeliveryFromParsed(s, lat, lon, parsed);

        // 2. Текст + кнопки
        var txt =
            "<b>Адрес точки Grillpoint:</b>\n" +
            $"<i>{s.DraftDelivery.AddressDisplay}</i>\n\n" +
            "Подтвердите адрес самовывоза:";

        var msg = await bot.SendMessage(chatId, txt,
            parseMode: ParseMode.Html,
            replyMarkup: Kb.PickupConfirm,
            cancellationToken: ct);

        s.CheckoutMessageIds.AddRange([mapMsg.MessageId, msg.MessageId]);
        s.State = FlowState.PickupConfirm;
        await sessions.UpsertAsync(s);
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
        
        var msg = await bot.SendMessage(chatId, string.Join("\n", lines),
            parseMode: ParseMode.Html, replyMarkup: Kb.ConfirmOrder, cancellationToken: ct);
        
        if (s.CheckoutMessageIds.Count > 0)
            await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
        s.CheckoutMessageIds.Clear();
        s.CheckoutMessageIds.Add(msg.MessageId);
        await sessions.UpsertAsync(s);
    }

#region Date, time and phone handle methods

    public async Task AskDateAsync(long chatId, long userId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);
        await pipeline.DeleteManyAsync(chatId, s.CheckoutMessageIds, ct);
        s.CheckoutMessageIds.Clear();

        var dmsg = await bot.SendMessage(
            chatId,
            "Выберите дату получения:",
            replyMarkup: Kb.DateKb(), cancellationToken: ct);
        s.CheckoutMessageIds.Add(dmsg.MessageId);
        await sessions.UpsertAsync(s);
    }

    // ---------- Запрос и обработка ввода номера телефона ----------
    
    public async Task AskPhoneAsync(long chatId, long userId, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);
        s.State = FlowState.CheckoutPhone;
        await sessions.UpsertAsync(s);

        var nick = string.IsNullOrWhiteSpace(s.UserNick) ? "" : $"@{s.UserNick}";
        
        var pmsg = await bot.SendMessage(chatId, 
            "📱 Укажите телефон для связи." +
                "\n\nМожно:" +
                "\n > Ввести номер вручную (пример: +79998887766)" +
                "\n > Отправить номер, нажав на кнопку ниже 👇 ",
            replyMarkup: Kb.Phone,
            cancellationToken: ct);
        
        s.CheckoutMessageIds.Add(pmsg.MessageId);
        await sessions.UpsertAsync(s);
    }
    
    private async Task HandlePhoneAsync(Message msg, Session s, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var raw = msg.Text ?? "";
        var digits = Regex.Replace(raw, @"\D", "");

        if (digits.Length < 10)
        {
            var em = await bot.SendMessage(chatId,
                "❌ Неверный формат телефона. Пример: +7 999 999-99-99",
                cancellationToken: ct);
            s.CheckoutMessageIds.Add(em.MessageId);
            await sessions.UpsertAsync(s);
            return;
        }

        if (digits.StartsWith("8"))
            digits = "7" + digits[1..];
        if (!digits.StartsWith("7"))
            digits = "7" + digits;

        var backend = "+" + digits;
        s.DraftDelivery.Phone = backend;
        s.State = FlowState.Confirm;
        await sessions.UpsertAsync(s);

        await SendConfirmCard(chatId, s, ct);
    }
    
    // ---------- Телефон и подтверждение заказа ----------
    
    public async Task HandleUserInputAsync(Message msg, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(msg.From!.Id);

        switch (s.State)
        {
            case FlowState.CheckoutTime:
                if (DateTime.TryParse(msg.Text, out var when))
                    s.DraftDelivery.ScheduledTime = when;
                s.CheckoutMessageIds.Add(msg.MessageId);
                s.State = FlowState.CheckoutPhone;
                await sessions.UpsertAsync(s);
                await AskPhoneAsync(msg.Chat.Id, msg.From.Id, ct);
                break;

            case FlowState.CheckoutPhone:
                await HandlePhoneAsync(msg, s, ct);
                break;
        }
    }
    
    private async Task SendOutOfZoneWarningAsync(long chatId, long userId, string addressDisplay, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(userId);
        Message msg;

        const string t2 = "\n\nВыберите другой адрес или оформите самовывоз.";
        const string tMethod = "К способу получения";
        
        if (!File.Exists(OutOfZoneImgPath))
        {
            msg = await bot.SendMessage(chatId,
                "Адрес находится вне зоны доставки." + t2,
                replyMarkup: Kb.Back(CallbackPrefixes.AddressBackToMethod, tMethod),
                cancellationToken: ct);
            
            s.CheckoutMessageIds.Add(msg.MessageId);
            await sessions.UpsertAsync(s);
            return;
        }

        msg = await bot.SendPhoto(
            chatId,
            GetOutOfZoneImage,
            caption:
            $"⬆️ Зона доставки - на изображении.\n\n" +
            $"Адрес <b>{addressDisplay}</b>\n" +
            "находится вне зоны доставки." + t2,
            parseMode: ParseMode.Html,
            replyMarkup: Kb.Back(CallbackPrefixes.AddressBackToMethod, tMethod),
            cancellationToken: ct);
        
        s.CheckoutMessageIds.Add(msg.MessageId);
        await sessions.UpsertAsync(s);
    }

    private static InputFile GetOutOfZoneImage => 
        InputFile.FromStream(File.OpenRead(OutOfZoneImgPath), Path.GetFileName(OutOfZoneImgPath));

    private static string OutOfZoneImgPath => 
        Path.Combine(AppContext.BaseDirectory, "Assets", "delivery-zone.png");

    #endregion
}