using System.Threading.Tasks;

namespace AIAgentMvc.Services
{
    // Simple rule-based AI agent service. Replace logic here to call Gemini/API later.
    public class AIAgent : IAgent
    {
        public string Respond(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return "Please enter a message so I can respond.";

            var m = userMessage.ToLowerInvariant();

            if (m.Contains("hello") || m.Contains("hi"))
                return "Hello! I'm your simple coding agent. How can I help today?";

            if (m.Contains("code") || m.Contains("c#") || m.Contains("asp.net"))
                return "I can help with C# and ASP.NET. Ask me to show sample code or explain a concept.";

            if (m.Contains("gemini") || m.Contains("google"))
                return "This example simulates connecting to Gemini. To integrate, replace this method with an API call to Gemini.";

            if (m.Contains("help") || m.Contains("how"))
                return "Try asking me to generate a small code snippet, explain a concept, or help debug an error.";

            // Fallback — mild transformation / echo
            return $"You said: \"{userMessage}\" — I can expand that into sample code or an explanation if you ask.";
        }

        public Task<string> RespondAsync(string userMessage)
        {
            return Task.FromResult(Respond(userMessage));
        }
    }
}
