using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AIAgentMvc.Services
{
    // Lightweight generative AIAgent that produces varied, helpful responses without external APIs.
    // This is not a real neural model — it's a template-based generator that creates dynamic replies,
    // code examples, and suggested next steps. If you configure a real Gemini API key the app will
    // use GeminiAgent instead.
    public class AIAgent : IAgent
    {
        private readonly Random _rnd = new Random();

        public string GenerateCodeSnippet(string userMessage)
        {
            // Very small heuristic: if user mentions C# or ASP.NET produce a tiny sample.
            var m = userMessage.ToLowerInvariant();
            if (m.Contains("asp.net") || m.Contains("aspnet") || m.Contains("asp.net core") || m.Contains("mvc"))
            {
                return @"// Minimal ASP.NET Core controller sample
public class HelloController : Microsoft.AspNetCore.Mvc.Controller
{
    [Microsoft.AspNetCore.Mvc.HttpGet("/hello")]
    public string Get() => ""Hello from ASP.NET Core!"";
}
";
            }

            if (m.Contains("c#") || m.Contains("csharp") || m.Contains("dotnet") || m.Contains("dotnet"))
            {
                return @"// C# example: simple method
public static string Greet(string name)
{
    return $"Hello, {name}!";
}
";
            }

            // Generic code snippet
            return "// Example pseudocode\nfunction example() {\n  // replace with your implementation\n}\n";
        }

        public string Respond(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return "Please enter a message so I can respond.";

            var m = userMessage.Trim();
            var lower = m.ToLowerInvariant();
            var sb = new StringBuilder();

            // Start with a friendly, varied opening
            var openings = new[] {
                "Here's a concise answer:",
                "I can help with that — summary:",
                "Sure — quick response:",
                "Got it. Here's what I suggest:"
            };
            sb.AppendLine(openings[_rnd.Next(openings.Length)]);
            sb.AppendLine();

            // If it's a greeting question, include a greeting
            if (Regex.IsMatch(lower, "\\b(hi|hello|hey)\\b"))
            {
                sb.AppendLine("Hello! I'm your coding assistant. I can generate examples, explain concepts, or help debug.");
            }

            // If user asked about code, provide a snippet and explanation
            if (Regex.IsMatch(lower, "\\b(code|c#|csharp|asp.net|aspnet|dotnet|example|snippet)\\b"))
            {
                sb.AppendLine("Explanation:");
                sb.AppendLine("I detected you want code help. Here's a small sample you can try:");
                sb.AppendLine();
                sb.AppendLine(GenerateCodeSnippet(lower));
                sb.AppendLine("Next steps:");
                sb.AppendLine("- Copy the snippet into a suitable project file and run it.");
                sb.AppendLine("- Tell me if you want it adapted to a specific scenario or framework.");
                return sb.ToString();
            }

            // If user asks about integrating Gemini/Google, provide steps
            if (Regex.IsMatch(lower, "\\b(gemini|google generative|generative language|api key)\\b"))
            {
                sb.AppendLine("Integration guidance:");
                sb.AppendLine("1) Obtain an API key from Google Cloud Console and enable the Generative Language API.");
                sb.AppendLine("2) Store the key securely (user-secrets or environment variables).");
                sb.AppendLine("3) Update your app configuration and restart.");
                sb.AppendLine("If you want, I can generate sample code to call the API from C#.");
                return sb.ToString();
            }

            // If user asked a question (contains '?') provide an analytical answer
            if (m.EndsWith("?") || Regex.IsMatch(lower, "\\bhow\\b|\\bwhat\\b|\\bwhy\\b|\\bwhen\\b"))
            {
                sb.AppendLine("Analysis:");
                sb.AppendLine($"You asked: '{m}'");
                sb.AppendLine();
                sb.AppendLine("Short answer:");
                sb.AppendLine("- I recommend the following approach:");
                sb.AppendLine("  1) Break the problem into smaller steps.");
                sb.AppendLine("  2) Implement a minimal prototype and verify behavior.");
                sb.AppendLine("  3) Iterate and add error handling and tests.");
                sb.AppendLine();
                sb.AppendLine("If you'd like, I can expand any step or generate example code.");
                return sb.ToString();
            }

            // Fallback: produce a helpful multi-part response
            sb.AppendLine("Summary:");
            sb.AppendLine($"I understood: '{m}'.");
            sb.AppendLine();
            sb.AppendLine("Suggestions:");
            sb.AppendLine("- Clarify the exact goal or provide an example input/output.");
            sb.AppendLine("- Ask me to generate a sample implementation or a step-by-step plan.");

            // Slight variation to make it feel generative
            var closings = new[] {
                "Would you like a code sample or a step-by-step walkthrough?",
                "Shall I generate a concrete example for this?",
                "Do you want this expanded into runnable code?"
            };
            sb.AppendLine();
            sb.AppendLine(closings[_rnd.Next(closings.Length)]);

            return sb.ToString();
        }

        public Task<string> RespondAsync(string userMessage)
        {
            return Task.FromResult(Respond(userMessage));
        }
    }
}
