# AIAgentMvc

Minimal ASP.NET Core MVC app that implements a simple rule-based AI agent service (`AIAgent`) and a `ChatController`.

How to run:

1. Open a command prompt in the project folder (`c:\Users\DELL\Desktop\cop`).
2. Restore and run:

```cmd
dotnet restore
dotnet run
```

The app starts with the `Chat` controller as the default route. Open the shown URL in your browser and try sending messages.

Notes:
- `Services/AIAgent.cs` contains simple rule-based logic. Replace `Respond` with real Gemini/API calls for production usage.
- Project targets .NET 7.0.

Gemini integration:

- The project includes a `GeminiAgent` template in `Services/GeminiAgent.cs` that uses `HttpClient` and configuration values from `appsettings.json`.
- To enable Gemini, set the following in `appsettings.json`:

```json
"Gemini": {
  "Use": true,
  "ApiUrl": "https://generativelanguage.googleapis.com/v1/models/text-bison-001:generate",
  "ApiKey": "YOUR_API_KEY"
}
```

- When `Gemini:Use` is `true`, the app registers `GeminiAgent` as the active agent; otherwise it uses the local rule-based `AIAgent`.
- The `GeminiAgent` implementation is a template — adapt request/response shapes to the real Gemini API you target.

How to get a Google Generative Language (Gemini) API key
------------------------------------------------------

1. Create or select a Google Cloud project:
   - Visit the Google Cloud Console: https://console.cloud.google.com/
   - Select an existing project or click **New Project** and follow the prompts.

2. Enable the Generative AI / Generative Language API (name may vary):
   - From the Console, go to **APIs & Services > Library**.
   - Search for **Generative Language API**, **Generative AI**, or the name shown in your Cloud Console and click **Enable**.

3. Create credentials (API key):
   - In the Console go to **APIs & Services > Credentials**.
   - Click **Create Credentials > API key**.
   - A new API key will be shown. Copy it.
   - (Recommended) Click **Restrict key** to limit usage to your application's HTTP referrers or IP addresses and to specific APIs for security.

4. Configure your app locally (do not check the key into source control):
   - Open `appsettings.json` or use environment variables/secrets. Example `appsettings.json`:

```json
"Gemini": {
  "Use": true,
  "ApiUrl": "https://generativelanguage.googleapis.com/v1/models/text-bison-001:generate",
  "ApiKey": "YOUR_API_KEY"
}
```

Better option: don't store the key in `appsettings.json` in source control. Use one of these safer approaches:
- Use environment variables (Windows example):

```cmd
setx Gemini__ApiKey "YOUR_API_KEY"
```

- Use the .NET user secrets store for local development:

```cmd
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY"
```

5. Restart your app. When `Gemini:Use` is `true` the app will call the configured endpoint using the key.

Alternative (service account / OAuth):
- For production, prefer a service account with IAM roles and short-lived OAuth tokens instead of a static API key. Create a service account in **IAM & Admin > Service Accounts**, grant the appropriate roles, create a JSON key, then use Google APIs client libraries or OAuth flows to acquire access tokens. If you want, I can add sample code to use a service account JSON key to fetch an OAuth token and call the API securely.
