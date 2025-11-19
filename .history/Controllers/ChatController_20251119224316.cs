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
            // This endpoint supports an optional `Cycles` parameter. If Cycles > 0,
            // the server will send the message to the agent, get a reply, then send
            // that reply back as the next prompt for `Cycles` times. This allows a
            // self-chat / repeated generation behavior.
            var responses = new List<string>();

            if (req == null || string.IsNullOrWhiteSpace(req.Message))
            {
                return Json(new { responses });
            }

            var prompt = req.Message;
            // At least do one call to the agent
            var first = await _agent.RespondAsync(prompt);
            responses.Add(first ?? string.Empty);

            var cycles = req.Cycles <= 0 ? 0 : req.Cycles - 1; // already did first

            for (int i = 0; i < cycles; i++)
            {
                // send previous response back as prompt
                var next = await _agent.RespondAsync(responses[^1]);
                responses.Add(next ?? string.Empty);
            }

            return Json(new { responses });
        }
    }

    public class ChatSendRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
