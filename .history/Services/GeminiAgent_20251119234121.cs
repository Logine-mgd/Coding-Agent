using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgentMvc.Services
{
    /// <summary>
    /// Calls Google Generative Language (Gemini Pro) API and returns text responses.
    /// </summary>
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

            // 🔑  Read configuration
            var apiKey = _config["Gemini:ApiKey"]?.Trim() ?? string.Empty;
            var apiUrl = _config["Gemini:ApiUrl"] ??
                         "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";

            if (string.IsNullOrEmpty(apiKey))
                return "Gemini is not configured. Set `Gemini:ApiKey` in appsettings, user‑secrets, or env variables.";

            // 🧠  Build full absolute URI
            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var baseUri))
                return $"Invalid Gemini API URL '{apiUrl}'";

            var separator = baseUri.Query.Length > 0 ? "&" : "?";
            var urlWithKey = $"{baseUri}{separator}key={Uri.EscapeDataString(apiKey)}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(urlWithKey));

                // 📨  Gemini‑Pro expected JSON payload
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

                var json = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                _log.LogDebug("Sending Gemini request to {Url}", urlWithKey);

                using var resp = await _http.SendAsync(request);
                var body = await resp.Content.ReadAsStringAsync();

                _log.LogDebug("Gemini response status {StatusCode}; body length {Length}",
                    resp.StatusCode, body?.Length ?? 0);

                if (!resp.IsSuccessStatusCode)
                    return $"Gemini request failed ({(int)resp.StatusCode}): {body}";

                // 🔍  Parse Gemini reply
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];

                    if (first.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // Optional log of first few characters
                            _log.LogDebug("Gemini reply snippet: {Snippet}",
                                text.Length > 80 ? text.Substring(0, 80) + "..." : text);
                            return text!;
                        }
                    }
                }

                // If structure is unexpected, just return raw body
                return body;
            }
            catch (Exception ex)
            {
                return $"Gemini request failed: {ex.Message}";
            }
        }
    }
}