using HealthTech.Data;
using HealthTech.DTOs;
using HealthTech.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthTech.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LifestyleController : ControllerBase
    {
        private readonly IDietaryLifestyle _dietaryLifestyleService;
        private readonly ApplicationDbContext _context;
        private readonly JsonSerializerOptions _jsonOptions;

        public LifestyleController(IDietaryLifestyle dietaryLifestyleService, ApplicationDbContext context)
        {
            _dietaryLifestyleService = dietaryLifestyleService;
            _context = context;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        [HttpPost("lifestyle")]
        public async Task<ActionResult<LifestyleRecommendationDto>> GetLifestyleRecommendations([FromBody] LifestyleInputDto input)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User authentication required");
            }

            try
            {
                var result = await _dietaryLifestyleService.GetLifestyleRecommendationsAsync(input, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("history")]
        public async Task<ActionResult> GetLifestyleHistory()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User authentication required");
            }

            try
            {
                var recommendation = await _dietaryLifestyleService.GetUserLifestyleHistoryAsync(userId);
                if (recommendation == null)
                {
                    return NotFound("No lifestyle plan found");
                }

                var history = _context.LifestyleHistories
                    .Where(h => h.UserId == userId)
                    .Select(h => new
                    {
                        h.Id,
                        h.CreatedAt,
                        Input = h.LifestyleInputJson != null ? JsonSerializer.Deserialize<LifestyleInputDto>(h.LifestyleInputJson, _jsonOptions) : null,
                        Response = recommendation
                    })
                    .FirstOrDefault();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("history/{id}")]
        public async Task<ActionResult> GetLifestyleHistoryById(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User authentication required");
            }

            try
            {
                var history = _context.LifestyleHistories
                    .Where(h => h.UserId == userId && h.Id == id)
                    .Select(h => new
                    {
                        h.Id,
                        h.CreatedAt,
                        h.LifestyleInputJson,
                        h.LifestyleResponseJson
                    })
                    .FirstOrDefault();

                if (history == null)
                {
                    return NotFound("Lifestyle plan not found");
                }

                var recommendation = await _dietaryLifestyleService.GetUserLifestyleHistoryAsync(userId);
                if (recommendation == null)
                {
                    return NotFound("Lifestyle plan not found");
                }

                var result = new
                {
                    history.Id,
                    history.CreatedAt,
                    Input = history.LifestyleInputJson != null ? JsonSerializer.Deserialize<LifestyleInputDto>(history.LifestyleInputJson, _jsonOptions) : null,
                    Response = recommendation
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}