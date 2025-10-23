using GrillpointBot.Core.Config;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Services;
using GrillpointBot.Infrastructure.Repositories;
using GrillpointBot.Telegram.BotHandlers;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace GrillpointBot.Telegram;

public static class DependencyInjection
{
    public static void InitBot()
    {
        var config = AppSettings.LoadConfig();
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging();
        
        // Config
        services.AddSingleton(config);
        
        //Telegram client
        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(config?.BotToken));

        // Core services
        services.AddSingleton<IMenuService, MenuService>();
        services.AddSingleton<IOrderService, OrderService>();
        
        // Data (Infrastructure)
        services.AddSingleton<IMenuRepository, JsonMenuRepository>();
        services.AddSingleton<IOrderRepository, JsonOrderRepository>();
        
        // Handlers
        services.AddSingleton<UpdateRouter>();
        services.AddSingleton<MessageHandler>();
        services.AddSingleton<CallbackHandler>();
        services.AddSingleton<DeliveryHandler>();

        var provider = services.BuildServiceProvider();
        var bot = provider.GetRequiredService<UpdateRouter>();
        
        bot.Start();

        Console.ReadLine();
    }
}