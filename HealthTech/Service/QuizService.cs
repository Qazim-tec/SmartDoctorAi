using HealthTech.Data;
using HealthTech.DTOs;
using HealthTech.IService;
using HealthTech.Models;
using Microsoft.EntityFrameworkCore;
using RestSharp;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace HealthTech.Service
{
    public class QuizService : IQuizService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<QuizService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public QuizService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<QuizService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<List<QuizCategoryDto>> GetCategoriesAsync()
        {
            return await _context.QuizCategories
                .Select(c => new QuizCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsMedical = c.IsMedical
                })
                .ToListAsync();
        }

        public async Task<List<QuizQuestionDto>> GenerateQuizQuestionsAsync(int categoryId, string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("UserId is null or empty for category {CategoryId}", categoryId);
                throw new ArgumentException("User ID is required.");
            }

            var category = await _context.QuizCategories.FindAsync(categoryId);
            if (category == null)
            {
                _logger.LogWarning("Category {CategoryId} not found", categoryId);
                throw new ArgumentException("Invalid category ID");
            }

            var questions = await GenerateQuestionsFromAI(category.Name, userId, categoryId);
            if (questions.Count < 50)
            {
                _logger.LogWarning("Generated only {Count} questions for category {Category}", questions.Count, category.Name);
                throw new InvalidOperationException($"Generated only {questions.Count} questions; 50 required.");
            }

            // Log correct answers for debugging
            _logger.LogInformation("Generated questions for category {CategoryId}: {CorrectAnswers}",
                categoryId, string.Join(", ", questions.Take(50).Select(q => $"Q{q.Id}: {q.CorrectOption}")));

            return questions.Select((q, index) => new QuizQuestionDto
            {
                Id = index + 1,
                Text = q.Text,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD
            }).Take(50).ToList();
        }

        public async Task<QuizResultDto> SubmitQuizAsync(QuizSubmissionDto submission, string userId)
        {
            // Validate submission
            if (submission == null || submission.Answers == null)
            {
                _logger.LogWarning("Submission or answers are null for user {UserId}", userId);
                throw new ArgumentException("Submission and answers are required.");
            }

            if (submission.CategoryId <= 0)
            {
                _logger.LogWarning("Invalid CategoryId: {CategoryId} for user {UserId}", submission.CategoryId, userId);
                throw new ArgumentException("Category ID must be greater than zero.");
            }

            if (submission.Answers.Count != 50)
            {
                _logger.LogWarning("Invalid answers count: {Count} for user {UserId}", submission.Answers.Count, userId);
                throw new ArgumentException("Exactly 50 answers are required.");
            }

            // Verify all keys are 1 to 50
            var expectedKeys = Enumerable.Range(1, 50).ToHashSet();
            if (!submission.Answers.Keys.All(k => expectedKeys.Contains(k)))
            {
                _logger.LogWarning("Invalid answer keys for user {UserId}. Expected 1-50.", userId);
                throw new ArgumentException("Answer keys must be numbers 1 to 50.");
            }

            var category = await _context.QuizCategories.FindAsync(submission.CategoryId);
            if (category == null)
            {
                _logger.LogWarning("Category {CategoryId} not found for user {UserId}", submission.CategoryId, userId);
                throw new ArgumentException("Invalid category ID.");
            }

            // Retrieve quiz session
            var quizSession = await _context.QuizSessions
                .Where(s => s.UserId == userId && s.CategoryId == submission.CategoryId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (quizSession == null || quizSession.Questions == null || quizSession.Questions.Count != 50)
            {
                _logger.LogWarning("Quiz session not found or invalid for user {UserId}, category {CategoryId}", userId, submission.CategoryId);
                throw new InvalidOperationException("Quiz session not found or incomplete. Please generate a new quiz.");
            }

            // Log session questions for debugging
            _logger.LogInformation("Quiz session questions for user {UserId}, category {CategoryId}: {CorrectAnswers}",
                userId, submission.CategoryId, string.Join(", ", quizSession.Questions.Select(q => $"Q{q.Id}: {q.CorrectOption}")));

            int score = 0;
            var missedQuestions = new List<MissedQuestionDto>();

            foreach (var answer in submission.Answers)
            {
                int questionId = answer.Key;
                char userAnswerChar = answer.Value;
                char userAnswerUpper = char.ToUpper(userAnswerChar, CultureInfo.InvariantCulture);
                string userAnswer = userAnswerUpper.ToString();

                // Validate answer
                if (!"ABCD".Contains(userAnswer))
                {
                    _logger.LogWarning("Invalid answer value: {Value} for question {QuestionId}, user {UserId}", userAnswer, questionId, userId);
                    throw new ArgumentException($"Invalid answer for question {questionId}. Must be A, B, C, or D.");
                }

                var question = quizSession.Questions.FirstOrDefault(q => q.Id == questionId);
                if (question == null)
                {
                    _logger.LogWarning("Question {QuestionId} not found in session for user {UserId}", questionId, userId);
                    missedQuestions.Add(new MissedQuestionDto
                    {
                        Text = $"Question {questionId} (not found)",
                        UserAnswer = userAnswer,
                        CorrectAnswer = "Unknown"
                    });
                    continue;
                }

                // Compare answers
                string correctOptionUpper = question.CorrectOption?.ToUpper(CultureInfo.InvariantCulture) ?? "";
                if (string.IsNullOrEmpty(correctOptionUpper))
                {
                    _logger.LogWarning("Correct option missing for question {QuestionId}, user {UserId}", questionId, userId);
                    missedQuestions.Add(new MissedQuestionDto
                    {
                        Text = question.Text,
                        UserAnswer = userAnswer,
                        CorrectAnswer = "Unknown"
                    });
                    continue;
                }

                bool isCorrect = userAnswer == correctOptionUpper;
                _logger.LogDebug("Question {QuestionId}: UserAnswer={UserAnswer}, CorrectOption={CorrectOption}, IsCorrect={IsCorrect}",
                    questionId, userAnswer, correctOptionUpper, isCorrect);

                if (isCorrect)
                {
                    score++;
                }
                else
                {
                    missedQuestions.Add(new MissedQuestionDto
                    {
                        Text = question.Text,
                        UserAnswer = userAnswer,
                        CorrectAnswer = question.CorrectOption
                    });
                }
            }

            // Use a transaction to ensure atomic operations
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Store score
                var quizScore = new QuizScore
                {
                    UserId = userId,
                    CategoryId = submission.CategoryId,
                    TakenAt = DateTime.UtcNow,
                    Score = score
                };
                _context.QuizScores.Add(quizScore);

                // Delete quiz session
                _context.QuizSessions.Remove(quizSession);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error saving quiz score or deleting session for user {UserId}, category {CategoryId}", userId, submission.CategoryId);
                throw new InvalidOperationException("Failed to save quiz results.", ex);
            }

            _logger.LogInformation("Quiz submitted for user {UserId}, category {CategoryId}: Score={Score}/50", userId, submission.CategoryId, score);

            return new QuizResultDto
            {
                Score = score,
                TotalQuestions = 50,
                MissedQuestions = missedQuestions
            };
        }

        public async Task<List<QuizScoreDto>> GetScoreHistoryAsync(string userId)
        {
            return await _context.QuizScores
                .Where(s => s.UserId == userId)
                .Include(s => s.Category)
                .Select(s => new QuizScoreDto
                {
                    Id = s.Id,
                    CategoryName = s.Category.Name,
                    TakenAt = s.TakenAt,
                    Score = s.Score
                })
                .OrderByDescending(s => s.TakenAt)
                .ToListAsync();
        }

        private async Task<List<QuizQuestion>> GenerateQuestionsFromAI(string category, string userId, int categoryId)
        {
            try
            {
                // Remove any existing quiz sessions for this user and category
                var existingSessions = await _context.QuizSessions
                    .Where(s => s.UserId == userId && s.CategoryId == categoryId)
                    .ToListAsync();
                if (existingSessions.Any())
                {
                    _context.QuizSessions.RemoveRange(existingSessions);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Removed {Count} existing quiz sessions for user {UserId}, category {CategoryId}", existingSessions.Count, userId, categoryId);
                }

                string apiKey = _configuration["GeminiAI:ApiKey"];
                string apiUrl = _configuration["GeminiAI:ApiUrl"];

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl))
                {
                    _logger.LogError("Gemini AI configuration missing");
                    throw new InvalidOperationException("AI service configuration missing");
                }

                var options = new RestClientOptions($"{apiUrl}?key={apiKey}")
                {
                    MaxTimeout = 60000
                };
                var client = new RestClient(options);
                var request = new RestRequest("", Method.Post);
                request.AddHeader("Content-Type", "application/json");

                string prompt = $@"You are a medical education expert. Generate exactly 50 multiple-choice questions for the {category} category. Each question must have:
- A clear, accurate question relevant to {category}.
- Four answer options (labeled A, B, C, D).
- A correct answer (indicated as A, B, C, or D).
Return the response in the following JSON format (no Markdown, no additional text):
[
  {{
    ""id"": 1,
    ""text"": ""Question text"",
    ""optionA"": ""Option A"",
    ""optionB"": ""Option B"",
    ""optionC"": ""Option C"",
    ""optionD"": ""Option D"",
    ""correctOption"": ""A""
  }},
  ...
]
Ensure all questions are unique, factually correct, and appropriate for medical/dental students.";

                var body = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = prompt } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topP = 0.9,
                        maxOutputTokens = 15000
                    }
                };

                request.AddJsonBody(body);
                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogError("Gemini API call failed. Status: {StatusCode}, Content: {Content}", response.StatusCode, response.Content);
                    throw new InvalidOperationException($"API Error: {response.StatusCode} - {response.Content}");
                }

                _logger.LogDebug("Raw API response: {Response}", response.Content);

                var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(response.Content, _jsonOptions);
                if (geminiResponse?.Candidates == null || geminiResponse.Candidates.Length == 0 || geminiResponse.Candidates[0].Content?.Parts == null)
                {
                    _logger.LogError("Invalid or empty response from API: {Response}", response.Content);
                    throw new InvalidOperationException("AI service returned no valid candidates");
                }

                string rawText = geminiResponse.Candidates[0].Content.Parts[0].Text;
                string jsonText = rawText
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                _logger.LogDebug("Cleaned JSON text: {JsonText}", jsonText);

                var questions = JsonSerializer.Deserialize<List<QuizQuestion>>(jsonText, _jsonOptions);
                if (questions == null || questions.Count < 50)
                {
                    _logger.LogError("Failed to generate 50 questions from: {JsonText}", jsonText);
                    throw new InvalidOperationException($"AI returned {questions?.Count ?? 0} questions; 50 required.");
                }

                // Assign IDs
                for (int i = 0; i < questions.Count; i++)
                {
                    questions[i].Id = i + 1;
                }

                // Store questions temporarily
                var quizSession = new QuizSession
                {
                    UserId = userId,
                    CategoryId = categoryId,
                    Questions = questions.Take(50).ToList(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.QuizSessions.Add(quizSession);
                await _context.SaveChangesAsync();

                return questions.Take(50).ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse AI response for category {Category}, user {UserId}", category, userId);
                throw new InvalidOperationException("Invalid response format from AI service", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quiz questions for category {Category}, user {UserId}", category, userId);
                throw;
            }
        }

       
    }
}