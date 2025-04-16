using HealthTech.Data;
using HealthTech.DTOs;
using HealthTech.IService;
using HealthTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HealthTech.Services
{
    public class DietaryLifestyleService : IDietaryLifestyle
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DietaryLifestyleService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly JsonSerializerOptions _jsonOptions;

        public DietaryLifestyleService(IConfiguration configuration,
                                       ILogger<DietaryLifestyleService> logger,
                                       ApplicationDbContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<LifestyleRecommendationDto> GetLifestyleRecommendationsAsync(LifestyleInputDto input, string userId)
        {
            RestResponse response = null;
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(input.AgeGroup) ||
                    input.HeightCm <= 0 ||
                    input.WeightKg <= 0 ||
                    string.IsNullOrEmpty(input.Location))
                {
                    _logger.LogError("Missing required lifestyle input fields");
                    throw new ArgumentException("Age group, height, weight, and location are required.");
                }

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("UserId is null or empty");
                    throw new ArgumentException("UserId is required");
                }

                // Delete existing lifestyle records for the user
                var existingRecords = _context.LifestyleHistories.Where(h => h.UserId == userId);
                if (existingRecords.Any())
                {
                    _context.LifestyleHistories.RemoveRange(existingRecords);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Deleted existing lifestyle records for user {UserId}", userId);
                }

                // Get API configuration
                string apiKey = _configuration["GeminiAI:ApiKey"];
                string apiUrl = _configuration["GeminiAI:ApiUrl"];

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl))
                {
                    _logger.LogError("Gemini AI configuration missing");
                    throw new Exception("AI service configuration is missing");
                }

                // Configure API client
                var options = new RestClientOptions(apiUrl)
                {
                    MaxTimeout = 30000
                };

                var client = new RestClient(options);
                var request = new RestRequest("", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);

                // Build prompt
                string prompt = $@"You are a health and wellness AI. Based on the following user data:
Age Group: {input.AgeGroup}
Height: {input.HeightCm} cm
Weight: {input.WeightKg} kg
Location: {input.Location}
Health Goal: {input.HealthGoal}
Dietary Restrictions: {input.DietaryRestrictions}
Medical Conditions: {input.MedicalConditions}

Provide a 7-day lifestyle plan including:
1. A weekly meal plan with breakfast, lunch, dinner, and snacks for each day, using only foods commonly available in {input.Location}. For each meal and snack, include:
   - A description of the food.
   - The quantity to eat (e.g., grams, cups, pieces).
   - Estimated calorie count in kilocalories (kcal).
   - A brief reason why the calorie amount supports the user's health goal, respecting medical conditions and dietary restrictions.
2. A simple exercise routine (type, duration, frequency) suitable for the age group, health goal, and medical conditions.
3. General lifestyle advice covering sleep, stress management, hydration, and other relevant tips.

Return ONLY the following JSON format (no Markdown, no additional text):
{{
  ""weeklyMealPlan"": [
    {{
      ""day"": ""Day 1"",
      ""breakfast"": {{
        ""description"": ""Meal description"",
        ""quantity"": ""Quantity description"",
        ""calories"": 0,
        ""calorieReason"": ""Reason why calories support goal""
      }},
      ""lunch"": {{
        ""description"": ""Meal description"",
        ""quantity"": ""Quantity description"",
        ""calories"": 0,
        ""calorieReason"": ""Reason why calories support goal""
      }},
      ""dinner"": {{
        ""description"": ""Meal description"",
        ""quantity"": ""Quantity description"",
        ""calories"": 0,
        ""calorieReason"": ""Reason why calories support goal""
      }},
      ""snacks"": {{
        ""description"": ""Snack description"",
        ""quantity"": ""Quantity description"",
        ""calories"": 0,
        ""calorieReason"": ""Reason why calories support goal""
      }}
    }}
    // ... (other days omitted for brevity, same structure for Days 2-7)
  ],
  ""exerciseRoutine"": ""Exercise plan description"",
  ""lifestyleAdvice"": ""Sleep, stress, hydration, and other advice""
}}";

                var body = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topP = 0.9,
                        maxOutputTokens = 4500
                    }
                };

                request.AddJsonBody(body);

                // Execute API call
                response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    _logger.LogError("Gemini API call failed. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, response.Content);
                    throw new Exception($"API Error: {response.StatusCode} - {response.Content}");
                }

                _logger.LogDebug("Raw API response: {Response}", response.Content);

                // Parse API response
                var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(response.Content, _jsonOptions);

                if (geminiResponse?.Candidates == null || geminiResponse.Candidates.Length == 0)
                {
                    _logger.LogError("No candidates in response. Full response: {Response}", response.Content);
                    throw new Exception("AI service returned no candidates");
                }

                string rawText = geminiResponse.Candidates[0].Content.Parts[0].Text;
                string jsonText = rawText
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                _logger.LogDebug("Cleaned JSON text: {JsonText}", jsonText);

                // Parse JSON into DTO
                var lifestyleResponse = JsonSerializer.Deserialize<LifestyleRecommendationDto>(jsonText, _jsonOptions);

                if (lifestyleResponse?.WeeklyMealPlan == null || lifestyleResponse.WeeklyMealPlan.Length != 7)
                {
                    _logger.LogError("Failed to parse lifestyle recommendations from: {JsonText}", jsonText);
                    throw new Exception("AI returned invalid lifestyle recommendations");
                }

                // Save to database
                var history = new LifestyleHistory
                {
                    UserId = userId,
                    LifestyleInputJson = JsonSerializer.Serialize(input, _jsonOptions),
                    LifestyleResponseJson = jsonText,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.LifestyleHistories.AddAsync(history);
                await _context.SaveChangesAsync();

                return lifestyleResponse;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing failed. Response: {Response}", response?.Content);
                throw new Exception("Failed to process AI response: invalid data format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing lifestyle recommendation request");
                throw;
            }
        }

        public async Task<LifestyleRecommendationDto> GetUserLifestyleHistoryAsync(string userId)
        {
            try
            {
                // Validate userId
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("UserId is null or empty");
                    throw new ArgumentException("UserId is required");
                }

                // Query the latest (only) LifestyleHistories record for the user
                var history = await _context.LifestyleHistories
                    .Where(h => h.UserId == userId)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (history == null)
                {
                    _logger.LogInformation("No lifestyle recommendations found for user {UserId}", userId);
                    return null;
                }

                var recommendation = JsonSerializer.Deserialize<LifestyleRecommendationDto>(
                    history.LifestyleResponseJson, _jsonOptions);

                if (recommendation == null || recommendation.WeeklyMealPlan == null)
                {
                    _logger.LogWarning("Invalid or empty recommendation for history ID {Id}", history.Id);
                    throw new Exception("Invalid lifestyle recommendation data");
                }

                return recommendation;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to deserialize LifestyleResponseJson for user {UserId}", userId);
                throw new Exception("Failed to process lifestyle history");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving lifestyle history for user {UserId}", userId);
                throw;
            }
        }
    }
}