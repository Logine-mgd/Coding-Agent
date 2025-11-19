using AIAgentMvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Configure HttpClient for GeminiAgent (if used)
builder.Services.AddHttpClient("gemini");

// Prefer GeminiAgent if an API key is present, otherwise fall back to local AIAgent.
var apiKey = builder.Configuration["Gemini:ApiKey"];
if (!string.IsNullOrWhiteSpace(apiKey))
{
    builder.Services.AddScoped<IAgent>(sp =>
    {
        var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var http = clientFactory.CreateClient("gemini");
        var cfg = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<AIAgent>>();
        // GeminiAgent requires ILogger<GeminiAgent>; resolve and pass it.
        var gemLogger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<GeminiAgent>();
        return new GeminiAgent(http, cfg, gemLogger);
    });
}
else
{
    builder.Services.AddScoped<IAgent, AIAgent>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Index}/{id?}");

app.Run();
