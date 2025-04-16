using HealthTech.DTOs;
using HealthTech.IService;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HealthTech.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> ChatWithDoctor([FromBody] ChatInput input)
        {
            if (input == null || string.IsNullOrEmpty(input.UserId))
            {
                return BadRequest("UserId is required.");
            }

            try
            {
                var result = await _chatService.ChatWithDoctorAsync(input, input.ConversationId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing chat: {ex.Message}");
            }
        }
    }
}