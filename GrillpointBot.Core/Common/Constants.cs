namespace GrillpointBot.Core.Common;

public static class Constants
{
    private const string JsonExt = ".json";

    public const string CfgPath = "Config/appsettings" + JsonExt;
    
    public const string Menu = "Menu" + JsonExt;
    public const string Orders = "orders" + JsonExt;

    // Значения конфигурации по умолчанию
    public const long AdminChatId = 1831114730;
    public const string BotToken = "8103338668:AAGyaYtySbqB5J-pyh2KMqWqbX_g6hNhrO0";
    
    // Команды (кнопки основного меню)
    public const string StartCmd = "/start";
    public const string MenuCmd = "📋 Меню";
    public const string AboutUsCmd = "ℹ️ О нас";
    public const string FeedbackCmd = "💬 Отзывы";
    
    // Переменные процесса доставки
    public const string Pickup = "Самовывоз";
    public const string Delivery = "Доставка";
}