using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgentMvc.Services
{
    public class GeminiAgent : IAgent
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<GeminiAgent> _log;

        public GeminiAgent(HttpClient http, IConfiguration config, ILogger<GeminiAgent> log)
        {
            _http = http;
            _config = config;
            _log = log;
        }

        public async Task<string> RespondAsync(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return "Please enter a message so I can respond.";

            // 🔑 Read API key
            var apiKey = _config["Gemini:ApiKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                return "Gemini is not configured. Set `Gemini:ApiKey`.";

            var modelName = "gemini-2.5-pro"; // أو "gemini-pro-latest"
            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

            try
            {
                var urlWithKey = $"{apiUrl}?key={Uri.EscapeDataString(apiKey)}";
                using var request = new HttpRequestMessage(HttpMethod.Post, urlWithKey);

                // 📝 Payload in Gemini-Pro JSON structure
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = userMessage } }
                        }
                    }
                };

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                // 🌐 Send request
                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"Gemini request failed ({(int)response.StatusCode}): {body}";

                // 🔍 Parse response
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            _log.LogDebug("Gemini reply snippet: {Snippet}",
                                text.Length > 80 ? text.Substring(0, 80) + "..." : text);
                            return text;
                        }
                    }
                }

                return body;
            }
            catch (Exception ex)
            {
                return $"Gemini request failed: {ex.Message}";
            }
        }
    }
}
