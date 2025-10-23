using System.Text.Json;
using GrillpointBot.Core.Common;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Infrastructure.Repositories;

public class JsonMenuRepository : IMenuRepository
{
    private string MenuPath => Constants.Menu;
    
    public async Task<List<MenuCategory>> LoadMenuAsync()
    {
        try
        {
            var fullPath = Path.GetFullPath(MenuPath);
            Console.WriteLine($"[Menu] Loading from: {fullPath}");

            if (!File.Exists(MenuPath))
            {
                Console.WriteLine($"[Menu] file [{fullPath}] not found.");
                return new();
            }
            
            var json = await File.ReadAllTextAsync(MenuPath);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var menu = JsonSerializer.Deserialize<List<MenuCategory>>(json, options) ?? new();

            Console.WriteLine($"[Menu] Loaded categories: {menu.Count}");
            foreach (var c in menu)
                Console.WriteLine($"[Menu]  - {c.Category}: {c.Items.Count} items");

            return menu;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Menu] loading error: {e}");
            return new();
        }
    }
}