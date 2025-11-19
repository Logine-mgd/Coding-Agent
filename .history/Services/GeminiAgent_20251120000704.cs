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

            // Read ONLY the API key
            var apiKey = _config["Gemini:ApiKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                return "Gemini is not configured. Set `Gemini:ApiKey`.";

            // Fixed endpoint for Gemini Pro
            var apiUrl = "https://generativelanguage.googleapis.com/v1/models/gemini-pro:generate";

            try
            {
                var urlWithKey = $"{apiUrl}?key={Uri.EscapeDataString(apiKey)}";

                using var request = new HttpRequestMessage(HttpMethod.Post, urlWithKey);

                // Gemini-Pro JSON payload
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

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"Gemini request failed ({(int)response.StatusCode}): {body}";

                // Parse response
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("candidates", out var cand) &&
                    cand.GetArrayLength() > 0)
                {
                    var parts = cand[0].GetProperty("content").GetProperty("parts");
                    return parts[0].GetProperty("text").GetString() ?? "(empty)";
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
