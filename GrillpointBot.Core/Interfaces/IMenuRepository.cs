using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface IMenuRepository
{
    Task<List<MenuCategory>> LoadMenuAsync();
}