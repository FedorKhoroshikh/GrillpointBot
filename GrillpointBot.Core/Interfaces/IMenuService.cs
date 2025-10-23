using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface IMenuService
{
    Task<IEnumerable<MenuCategory>> GetCategoriesAsync();
    Task<IEnumerable<MenuItem>> GetItemsByCategoryAsync(string category);
    Task<MenuItem?> GetItemByIdAsync(string id);
    Task ReloadMenuAsync();
}