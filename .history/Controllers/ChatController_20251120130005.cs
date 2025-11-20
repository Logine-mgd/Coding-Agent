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

            // Post-process responses to convert Markdown to HTML
            var processed = responses.Select(r =>
            {
                var full = r ?? string.Empty;
                var snippet = ExtractSnippet(full, out bool hasMore);
                return new { full, snippet, hasMore };
            }).ToList();

            return Json(new { responses = processed });
        }

        // Extracts code block if present, otherwise converts Markdown to HTML
        private static string ExtractSnippet(string text, out bool hasMore)
        {
            hasMore = false;
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // If there's a fenced code block, extract it and wrap in <pre><code>
            var fenceMatch = Regex.Match(text, "```(?:[a-zA-Z0-9+-]*)\\r?\\n([\\s\\S]*?)```", RegexOptions.Multiline);
            if (fenceMatch.Success)
            {
                var code = fenceMatch.Groups[1].Value.Replace("\r\n", "\n").TrimEnd();
                return $"<pre><code>{System.Web.HttpUtility.HtmlEncode(code)}</code></pre>";
            }

            // No fenced block — convert Markdown to HTML
            var normalized = text.Replace("\r\n", "\n");
            var html = ConvertMarkdownToHtml(normalized);
            return html;
        }

        // Converts common Markdown formatting to HTML
        private static string ConvertMarkdownToHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Escape HTML first to prevent injection
            text = System.Web.HttpUtility.HtmlEncode(text);

            // Convert headers (must be done before bold/italic to avoid conflicts)
            text = Regex.Replace(text, @"^######\s*(.+)$", "<h6>$1</h6>", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^#####\s*(.+)$", "<h5>$1</h5>", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^####\s*(.+)$", "<h4>$1</h4>", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^###\s*(.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^##\s*(.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^#\s*(.+)$", "<h1>$1</h1>", RegexOptions.Multiline);

            // Convert bold: **text** or __text__
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            text = Regex.Replace(text, @"__(.+?)__", "<strong>$1</strong>");

            // Convert italic: *text* or _text_
            text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
            text = Regex.Replace(text, @"_(.+?)_", "<em>$1</em>");

            // Convert strikethrough: ~~text~~
            text = Regex.Replace(text, @"~~(.+?)~~", "<del>$1</del>");

            // Convert inline code: `code`
            text = Regex.Replace(text, @"`(.+?)`", "<code>$1</code>");

            // Convert links: [text](url)
            text = Regex.Replace(text, @"
$$
(.+?)
$$$(.+?)$", "<a href=\"$2\" target=\"_blank\">$1</a>");

            // Convert unordered lists
            text = Regex.Replace(text, @"^[\*\-\+]\s+(.+)$", "<li>$1</li>", RegexOptions.Multiline);
            text = Regex.Replace(text, @"(<li>.*</li>)", "<ul>$1</ul>", RegexOptions.Singleline);

            // Convert ordered lists
            text = Regex.Replace(text, @"^\d+\.\s+(.+)$", "<li>$1</li>", RegexOptions.Multiline);
            // Merge consecutive <ul> tags and convert numbered lists to <ol>
            text = Regex.Replace(text, @"</ul>\s*<ul>", "");
            
            // Convert blockquotes: > text
            text = Regex.Replace(text, @"^&gt;\s*(.+)$", "<blockquote>$1</blockquote>", RegexOptions.Multiline);
            text = Regex.Replace(text, @"</blockquote>\s*<blockquote>", "");

            // Convert line breaks (double newline = paragraph, single = <br>)
            text = Regex.Replace(text, @"\n\n", "</p><p>");
            text = Regex.Replace(text, @"\n", "<br>");
            text = $"<p>{text}</p>";

            // Clean up empty paragraphs
            text = Regex.Replace(text, @"<p>\s*</p>", "");

            return text;
        }
    }

    public class ChatSendRequest
    {
        public string Message { get; set; } = string.Empty;
        // Number of additional cycles (model-to-model exchanges). 0 = only one agent response.
        public int Cycles { get; set; } = 0;
    }
}