using HealthTech.DTOs;
using System.Threading.Tasks;

namespace HealthTech.IService
{
    public interface IDietaryLifestyle
    {
        Task<LifestyleRecommendationDto> GetLifestyleRecommendationsAsync(LifestyleInputDto input, string userId);
        Task<LifestyleRecommendationDto> GetUserLifestyleHistoryAsync(string userId);
    }
}