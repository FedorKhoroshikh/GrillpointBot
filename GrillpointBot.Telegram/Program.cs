namespace GrillpointBot.Telegram;

public enum ExitCodes
{
    Success = 0,        // Success
    CfgSetupErr = 1,    // Application configuration setup error
    UnhldErr = 2        // Unhandled error
}

internal static class Program
{
    private static void Main()
    {
        if (DependencyInjection.InitBot() is not ExitCodes.Success)
            return;
    }
}