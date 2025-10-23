using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Services;

public class MenuService(IMenuRepository menuRepository) : IMenuService
{
    private List<MenuCategory>? _cache;

    public async Task<IEnumerable<MenuCategory>> GetCategoriesAsync()
    {
        _cache ??= await menuRepository.LoadMenuAsync();
        return _cache;
    }

    public async Task<IEnumerable<MenuItem>> GetItemsByCategoryAsync(string category)
    {
        _cache ??= await menuRepository.LoadMenuAsync();
        return _cache.FirstOrDefault(c =>
                c.Category.Equals(category, StringComparison.OrdinalIgnoreCase))?.Items ?? [];
    }

    public async Task<MenuItem?> GetItemByIdAsync(string id)
    {
        _cache ??= await menuRepository.LoadMenuAsync();
        return _cache.SelectMany(c => c.Items).FirstOrDefault(i => i.Id == id);
    }
    
    public async Task ReloadMenuAsync() =>
        _cache = await menuRepository.LoadMenuAsync();
}