using HealthTech.DTOs;

namespace HealthTech.IService
{
    public interface IQuizService
    {
        Task<List<QuizCategoryDto>> GetCategoriesAsync();
        Task<List<QuizQuestionDto>> GenerateQuizQuestionsAsync(int categoryId, string userId);
        Task<QuizResultDto> SubmitQuizAsync(QuizSubmissionDto submission, string userId);
        Task<List<QuizScoreDto>> GetScoreHistoryAsync(string userId);
    }
}