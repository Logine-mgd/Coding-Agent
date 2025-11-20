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

            // Read optional configuration
            var modelName = _config["Gemini:Model"] ?? "gemini-2.5-pro";
            var apiBase = _config["Gemini:ApiUrl"];
            var apiUrl = string.IsNullOrWhiteSpace(apiBase)
                ? $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent"
                : apiBase;

            var maxOutputTokens = 0;
            if (!int.TryParse(_config["Gemini:MaxOutputTokens"], out maxOutputTokens))
                maxOutputTokens = 1024; // default for code outputs

            var temperature = 0.2;
            if (double.TryParse(_config["Gemini:Temperature"], out var t)) temperature = t;

            var maxAttempts = 4;
            if (int.TryParse(_config["Gemini:RetryAttempts"], out var ma)) maxAttempts = Math.Max(1, ma);

            var baseDelayMs = 500; // exponential backoff base
            var rnd = new Random();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    var urlWithKey = apiUrl;
                    if (!urlWithKey.Contains("?")) urlWithKey += "?";
                    if (!urlWithKey.Contains("key=")) urlWithKey += "key=" + Uri.EscapeDataString(apiKey);

                    using var request = new HttpRequestMessage(HttpMethod.Post, urlWithKey);

                    // Build prompt wrapper (optional) to request concise replies or token limits
                    var promptPrefix = _config["Gemini:PromptPrefix"];
                    var effectiveMessage = string.IsNullOrWhiteSpace(promptPrefix)
                        ? userMessage
                        : (promptPrefix + "\n" + userMessage);

                    var payload = new
                    {
                        // v1beta/generateContent style: provide `contents` as list of messages
                        contents = new[]
                        {
                            new
                            {
                                role = "user",
                                parts = new[] { new { text = effectiveMessage } }
                            }
                        }
                    };

                    request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    var response = await _http.SendAsync(request);
                    var body = await response.Content.ReadAsStringAsync();
                    sw.Stop();

                    _log.LogInformation("Gemini request to {Url} finished in {Elapsed}ms (status {Status})", apiUrl, sw.ElapsedMilliseconds, (int)response.StatusCode);

                    if (!response.IsSuccessStatusCode)
                    {
                        // If overloaded or rate-limited, retry with backoff
                        var bodyLower = body?.ToLowerInvariant() ?? string.Empty;
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode == 503 || bodyLower.Contains("model overloaded") || bodyLower.Contains("quota"))
                        {
                            if (attempt == maxAttempts)
                            {
                                _log.LogWarning("Gemini overloaded after {Attempts} attempts", attempt);
                                return $"Model overloaded or rate-limited after {attempt} attempts. Try again later or reduce parallel requests/cycles.";
                            }

                            var jitter = rnd.Next(0, 200);
                            var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1) + jitter;
                            _log.LogWarning("Gemini request returned {Status}. retrying in {Delay}ms (attempt {Attempt}/{Max})", (int)response.StatusCode, delay, attempt, maxAttempts);
                            await Task.Delay(delay);
                            continue;
                        }

                        return $"Gemini request failed ({(int)response.StatusCode}): {body}";
                    }

                    // Parse response
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var text = parts[0].GetProperty("text").GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                _log.LogDebug("Gemini reply length: {Len}", text.Length);
                                return text; // return full text as provided by Gemini
                            }
                        }
                    }

                    return body;
                }
                catch (Exception ex)
                {
                    if (attempt == maxAttempts)
                    {
                        _log.LogError(ex, "Gemini request failed after {Attempts} attempts", attempt);
                        return $"Gemini request failed: {ex.Message}";
                    }

                    var jitter = rnd.Next(0, 200);
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1) + jitter;
                    _log.LogWarning(ex, "Transient error calling Gemini. Retrying in {Delay}ms (attempt {Attempt}/{Max})", delay, attempt, maxAttempts);
                    await Task.Delay(delay);
                    continue;
                }
            }

            return "Gemini request failed: unexpected error";
        }
    }
}
