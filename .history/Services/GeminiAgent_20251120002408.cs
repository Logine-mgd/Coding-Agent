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

            var apiKey = _config["Gemini:ApiKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                return "Gemini is not configured. Set `Gemini:ApiKey`.";

            // اسم الموديل الصحيح: gemini‑1.5‑flash‑latest
            var modelName = "gemini-1.5-flash-latest";

            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";
            var urlWithKey = $"{apiUrl}?key={Uri.EscapeDataString(apiKey)}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, urlWithKey);

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

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                _log.LogDebug("Gemini response status {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    // إذا كان الخطأ 404، نجرب نعمل ListModels لكي نعرض الموديلات المتاحة
                    if ((int)response.StatusCode == 404)
                    {
                        _log.LogWarning("Got 404 from Gemini. Trying to list available models.");

                        var listUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey)}";
                        using var listReq = new HttpRequestMessage(HttpMethod.Get, listUrl);
                        var listResp = await _http.SendAsync(listReq);
                        var listBody = await listResp.Content.ReadAsStringAsync();

                        if (listResp.IsSuccessStatusCode)
                        {
                            return $"Gemini model not found. Available models:\n{listBody}";
                        }
                        else
                        {
                            return $"Gemini request failed ({(int)response.StatusCode}): {body}\nAlso failed to list models: {listBody}";
                        }
                    }

                    return $"Gemini request failed ({(int)response.StatusCode}): {body}";
                }

                // إذا نجح الاستجابة، نقوم بالبارسينج
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    if (first.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            _log.LogDebug("Gemini replied: {Snippet}", text.Length > 80 ? text[..80] + "..." : text);
                            return text!;
                        }
                    }
                }

                // إذا الهيكل مختلف، نرجّع الجسم كامل
                return body;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Gemini request exception");
                return $"Gemini request failed: {ex.Message}";
            }
        }
    }
}
