using HealthTech.DTOs;
using HealthTech.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HealthTech.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quizService;
        private readonly ILogger<QuizController> _logger;

        public QuizController(IQuizService quizService, ILogger<QuizController> logger)
        {
            _quizService = quizService;
            _logger = logger;
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetQuizCategories()
        {
            try
            {
                var categories = await _quizService.GetCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching quiz categories.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("questions/{categoryId}")]
        public async Task<IActionResult> GetQuizQuestions(int categoryId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User not authenticated for quiz questions request, category {CategoryId}.", categoryId);
                    return Unauthorized("User not authenticated.");
                }

                var questions = await _quizService.GenerateQuizQuestionsAsync(categoryId, userId);
                _logger.LogInformation("Generated {Count} quiz questions for user {UserId}, category {CategoryId}.", questions.Count, userId, categoryId);
                return Ok(questions);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for quiz questions, category {CategoryId}: {Message}", categoryId, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to generate quiz questions, category {CategoryId}: {Message}", categoryId, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quiz questions for category {CategoryId}.", categoryId);
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionDto submission)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User not authenticated for quiz submission, category {CategoryId}.", submission?.CategoryId);
                    return Unauthorized("User not authenticated.");
                }

                if (submission == null)
                {
                    _logger.LogWarning("Null submission received for user {UserId}.", userId);
                    return BadRequest("Submission is required.");
                }

                var result = await _quizService.SubmitQuizAsync(submission, userId);
                _logger.LogInformation("Quiz submitted successfully for user {UserId}, category {CategoryId}: Score={Score}/50.",
                    userId, submission.CategoryId, result.Score);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid quiz submission for user {UserId}, category {CategoryId}: {Message}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, submission?.CategoryId, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Quiz submission failed for user {UserId}, category {CategoryId}: {Message}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, submission?.CategoryId, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting quiz for user {UserId}, category {CategoryId}.",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, submission?.CategoryId);
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetQuizHistory()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User not authenticated for quiz history request.");
                    return Unauthorized("User not authenticated.");
                }

                var history = await _quizService.GetScoreHistoryAsync(userId);
                return Ok(history);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid quiz history request: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching quiz history for user {UserId}.", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return StatusCode(500, "Internal server error.");
            }
        }
    }
}