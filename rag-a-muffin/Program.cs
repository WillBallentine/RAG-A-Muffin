using RagAMuffin.Models;
using RagAMuffin.Auth;
using RagAMuffin.Services.ExternalApps;
using RagAMuffin.Services.Interfaces;
using RagAMuffin.Services;
using RagAMuffin.Qdrant;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"]!);
});

builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"]!);
    client.Timeout = TimeSpan.FromMinutes(10); // LLMs can be slow locally
});

var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "qdrant";
var qdrantPort = int.TryParse(builder.Configuration["Qdrant:Port"], out var configuredPort) ? configuredPort : 6334;

builder.Services.AddSingleton<QdrantClient>(sp =>
    new QdrantClient(qdrantHost, qdrantPort));

builder.Services.AddScoped<QdrantCollectionInitializer>();
builder.Services.AddScoped<IRagQueryService, RagQueryService>();
builder.Services.AddScoped<IVectorStore, QdrantVectorStore>();
builder.Services.AddScoped<IChunker>(sp => new TextChunker(sp.GetRequiredService<ILogger<TextChunker>>(), 100, 25));
builder.Services.AddScoped<IEmailParser, EmailParser>();
builder.Services.AddScoped<IIngestionPipeline, IngestionPipeline>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Rag-A-Muffin application...");

var initializer = app.Services.GetRequiredService<QdrantCollectionInitializer>();
await initializer.InitializeAsync();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();  // serves index.html at /
app.UseStaticFiles();   // serves wwwroot contents


app.UseCors();

//this is for testing purposes only. need to implement proper onboarding flow
app.MapGet("/authorize", async (HttpRequest request) =>
{
    var userId = request.Query["userId"].ToString();
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new
        {
            Message = "Missing required query parameter 'userId'.",
            Example = "/authorize?userId=you@example.com"
        });
    }

    var redirectUri = $"{request.Scheme}://{request.Host}/oauth2callback";
    var authUrl = await GoogleAuth.GetAuthorizationUrlAsync(userId, redirectUri);
    return Results.Redirect(authUrl);
});

app.MapGet("/oauth2callback", async (HttpRequest request, string code, string? userId, string? state) =>
{
    var resolvedUserId = userId;
    if (string.IsNullOrWhiteSpace(resolvedUserId) && !string.IsNullOrWhiteSpace(state))
    {
        resolvedUserId = Uri.UnescapeDataString(state);
    }

    if (string.IsNullOrWhiteSpace(resolvedUserId))
    {
        return Results.BadRequest(new
        {
            Message = "Missing required userId. Provide either query parameter 'userId' or let Google return the OAuth state.",
            Example = "/oauth2callback?code=...&userId=you@example.com"
        });
    }

    var redirectUri = $"{request.Scheme}://{request.Host}/oauth2callback";
    await GoogleAuth.ExchangeCodeForTokenAsync(resolvedUserId, code, redirectUri);
    return Results.Text("Authentication complete. You may close this page.");
});

app.MapGet("/inbox", async (HttpRequest request) =>
{
    var userId = request.Query["userId"].ToString();
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new
        {
            Message = "Missing required query parameter 'userId'.",
            Example = "/inbox?userId=you@example.com"
        });
    }

    if (!await GoogleAuth.HasStoredCredentialAsync(userId))
    {
        var authorizationUrl = $"{request.Scheme}://{request.Host}/authorize?userId={Uri.EscapeDataString(userId)}";
        return Results.BadRequest(new { Message = "No stored credentials found.", AuthorizationUrl = authorizationUrl });
    }

    var gmailService = await GoogleAuth.CreateGmailServiceAsync(userId);
    var messages = await Gmail.FetchInboxAsync(gmailService, maxResults: 10);
    var parser = new EmailParser(app.Services.GetRequiredService<ILogger<EmailParser>>());
    logger.LogInformation("Fetched {Count} messages for user {UserId}. Starting ingestion...", messages.Count(), userId);
    var ingestionPipeline = app.Services.GetRequiredService<IIngestionPipeline>();
    await ingestionPipeline.IngestAsync(messages);
    return Results.Ok(messages.Select(m => new
    {
        Id = m.Id,
        Subject = parser.GetHeader(m, "Subject"),
        From = parser.GetHeader(m, "From"),
        BodyPreview = parser.GetBody(m).Substring(0, 100) + "..."
    }));
});

app.MapPost("/query", async (QueryRequest request, IRagQueryService queryService, CancellationToken ct) =>
{
    var response = await queryService.QueryAsync(request, ct);
    return Results.Ok(response);
});

app.MapPost("/query/stream", async (QueryRequest request, IRagQueryService queryService, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no"; // disables nginx buffering if you add a reverse proxy later

    await foreach (var token in queryService.StreamQueryAsync(request, ct))
    {
        await ctx.Response.WriteAsync($"data: {token}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    // Signal the client the stream is done
    await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
    await ctx.Response.Body.FlushAsync(ct);
});

app.Run();
