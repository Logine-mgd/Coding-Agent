using AIAgentMvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Configure HttpClient for GeminiAgent (if used)
builder.Services.AddHttpClient("gemini");

var useGemini = builder.Configuration.GetValue<bool>("Gemini:Use");
if (useGemini)
{
    builder.Services.AddScoped<IAgent, GeminiAgent>(sp =>
    {
        // Create HttpClient configured from named client
        var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var http = clientFactory.CreateClient("gemini");
        var cfg = sp.GetRequiredService<IConfiguration>();
        return new GeminiAgent(http, cfg);
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
