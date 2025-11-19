using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AIAgentMvc.Services
{
    // GeminiAgent wired to call Google Generative Language (Gemini) style APIs.
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

            var apiKey = _config["Gemini:ApiKey"] ?? string.Empty;
            var apiUrl = _config["Gemini:ApiUrl"] ?? string.Empty;

            // Provide a reasonable default for Google Generative Language (text-bison)
            if (string.IsNullOrEmpty(apiUrl))
            {
                apiUrl = apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent";;
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                return "Gemini is not configured. Set `Gemini:ApiKey` in appsettings (or env) to enable real calls.";
            }

            try
            {
                // If the apiUrl already contains query params, append with & otherwise ?
                var separator = apiUrl.Contains("?") ? "&" : "?";
                var urlWithKey = apiUrl + separator + "key=" + Uri.EscapeDataString(apiKey);

                using var request = new HttpRequestMessage(HttpMethod.Post, urlWithKey);

                // Google Generative Language expects a prompt shape like: { "prompt": { "text": "..." } }
                var payload = new { prompt = new { text = userMessage } };
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                _log.LogDebug("Sending Gemini request to {Url}", urlWithKey);
                var resp = await _http.SendAsync(request);
                var body = await resp.Content.ReadAsStringAsync();
                _log.LogDebug("Gemini response status: {StatusCode}; body length: {Len}", resp.StatusCode, body?.Length ?? 0);

                if (!resp.IsSuccessStatusCode)
                {
                    return $"Gemini request failed ({(int)resp.StatusCode}): {body}";
                }

                // Example Google GL response contains `candidates` array with `output`.
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var first = candidates[0];
                        if (first.TryGetProperty("output", out var output))
                        {
                            return output.GetString() ?? body;
                        }
                    }

                    // Fallback: try `output` at top-level or `text` property
                    if (root.TryGetProperty("output", out var outTop))
                        return outTop.GetString() ?? body;

                    if (root.TryGetProperty("text", out var textProp))
                        return textProp.GetString() ?? body;
                }
                catch
                {
                    // ignore parse errors and return raw body
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
