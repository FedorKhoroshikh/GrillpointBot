using GrillpointBot.Core.Config;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Services;
using GrillpointBot.Infrastructure.Repositories;
using GrillpointBot.Telegram.BotHandlers;
using GrillpointBot.Telegram.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace GrillpointBot.Telegram;

public static class DependencyInjection
{
    public static ExitCodes InitBot()
    {
        var config = AppSettings.LoadConfig();

        if (config is null)
        {
            Console.WriteLine("Config loading failed. Application shutdown...");
            return ExitCodes.CfgSetupErr;
        }
        
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
        services.AddSingleton<ISessionStore, InMemorySessionStore>();
        
        services.AddHttpClient();
        services.AddSingleton<IImageService, ImageService>();
        
        // Data (Infrastructure)
        services.AddSingleton<IMenuRepository, JsonMenuRepository>();
        services.AddSingleton<IOrderRepository, JsonOrderRepository>();
        
        // Handlers
        services.AddSingleton<UpdateRouter>();
        services.AddSingleton<MessageHandler>();
        services.AddSingleton<CallbackHandler>();
        services.AddSingleton<DeliveryHandler>();
        services.AddSingleton<CatalogHandler>();
        services.AddSingleton<CartHandler>();
        services.AddSingleton<ConfirmHandler>();
        services.AddSingleton<CheckoutHandler>();
        services.AddSingleton<MessagePipeline>();

        var provider = services.BuildServiceProvider();
        var bot = provider.GetRequiredService<UpdateRouter>();
        
        bot.Start();

        Console.ReadLine();

        return ExitCodes.Success;
    }
}