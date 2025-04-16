using HealthTech.Data;
using HealthTech.DTOs;
using HealthTech.IService;
using HealthTech.Models;
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
    public class HealthAIController : ControllerBase
    {
        private readonly IHealthAIService _healthAIService;
        private readonly ApplicationDbContext _context;
        private readonly JsonSerializerOptions _jsonOptions;

        public HealthAIController(IHealthAIService healthAIService, ApplicationDbContext context)
        {
            _healthAIService = healthAIService;
            _context = context;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        [HttpPost("diagnose")]
        public async Task<IActionResult> Diagnose([FromBody] PatientInput input)
        {
            if (input == null || string.IsNullOrEmpty(input.PresentingComplaint))
                return BadRequest("Invalid input data. Presenting complaint is required.");

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var response = await _healthAIService.GetDiagnosesAsync(input, userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("history")]
        public IActionResult GetHistory()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var rawHistory = _context.DiagnosisHistories
                .Where(h => h.UserId == userId)
                .Select(h => new
                {
                    h.Id,
                    h.CreatedAt,
                    h.PatientInputJson,
                    h.DiagnosisResponseJson
                })
                .ToList();

            var history = rawHistory.Select(h => new
            {
                h.Id,
                h.CreatedAt,
                Input = h.PatientInputJson != null ? JsonSerializer.Deserialize<PatientInput>(h.PatientInputJson, _jsonOptions) : null,
                Response = h.DiagnosisResponseJson != null ? JsonSerializer.Deserialize<DiagnosisResponseDto>(h.DiagnosisResponseJson, _jsonOptions) : null
            }).ToList();

            return Ok(history);
        }

        [HttpGet("history/{id}")]
        public IActionResult GetHistoryById(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var rawHistory = _context.DiagnosisHistories
                .Where(h => h.UserId == userId && h.Id == id)
                .Select(h => new
                {
                    h.Id,
                    h.CreatedAt,
                    h.PatientInputJson,
                    h.DiagnosisResponseJson
                })
                .FirstOrDefault();

            if (rawHistory == null)
                return NotFound();

            var history = new
            {
                rawHistory.Id,
                rawHistory.CreatedAt,
                Input = rawHistory.PatientInputJson != null ? JsonSerializer.Deserialize<PatientInput>(rawHistory.PatientInputJson, _jsonOptions) : null,
                Response = rawHistory.DiagnosisResponseJson != null ? JsonSerializer.Deserialize<DiagnosisResponseDto>(rawHistory.DiagnosisResponseJson, _jsonOptions) : null
            };

            return Ok(history);
        }
    }
}