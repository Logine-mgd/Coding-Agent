using Microsoft.AspNetCore.Mvc;
using AIAgentMvc.Models;
using AIAgentMvc.Services;
using System.Threading.Tasks;

namespace AIAgentMvc.Controllers
{
    public class ChatController : Controller
    {
        private readonly IAgent _agent;

        public ChatController(IAgent agent)
        {
            _agent = agent;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new ChatViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ChatViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.UserMessage))
            {
                model.AgentResponse = await _agent.RespondAsync(model.UserMessage);
            }

            return View(model);
        }

        // AJAX-friendly JSON endpoint
        [HttpPost]
        [Route("Chat/Send")]
        [IgnoreAntiforgeryToken]
        public async Task<JsonResult> Send([FromBody] ChatSendRequest req)
        {
            var resp = string.Empty;
            if (req != null && !string.IsNullOrWhiteSpace(req.Message))
            {
                resp = await _agent.RespondAsync(req.Message);
            }

            return Json(new { response = resp });
        }
    }

    public class ChatSendRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
