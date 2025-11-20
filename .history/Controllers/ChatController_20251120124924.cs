using Microsoft.AspNetCore.Mvc;
using AIAgentMvc.Models;
using AIAgentMvc.Services;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

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

            if (req == null || string.IsNullOrWhiteSpace(req.Message))
            {
                return Json(new { responses = new object[0] });
            }

            var responses = new List<string>();
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

            // Post-process responses to extract code snippets - NO TRUNCATION
            var processed = responses.Select(r =>
            {
                var full = r ?? string.Empty;
                var snippet = ExtractSnippet(full, out bool hasMore);
                return new { full, snippet, hasMore };
            }).ToList();

            return Json(new { responses = processed });
        }

        // Extracts code block if present, otherwise returns full text. NO TRUNCATION.
        private static string ExtractSnippet(string text, out bool hasMore)
        {
            hasMore = false;
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // If there's a fenced code block, extract it
            var fenceMatch = Regex.Match(text, "```(?:[a-zA-Z0-9+-]*)\\r?\\n([\\s\\S]*?)```", RegexOptions.Multiline);
            if (fenceMatch.Success)
            {
                var code = fenceMatch.Groups[1].Value.Replace("\r\n", "\n").TrimEnd();
                return code;
            }

            // No fenced block — return full text with normalized line endings
            var normalized = text.Replace("\r\n", "\n");
            return normalized;
        }
    }

    public class ChatSendRequest
    {
        public string Message { get; set; } = string.Empty;
        // Number of additional cycles (model-to-model exchanges). 0 = only one agent response.
        public int Cycles { get; set; } = 0;
    }
}