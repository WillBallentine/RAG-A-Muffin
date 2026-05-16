using RagAMuffin.Models;
using RagAMuffin.Auth;
using RagAMuffin.Services.ExternalApps;
using RagAMuffin.Services.Interfaces;
using RagAMuffin.Services;
using RagAMuffin.Services.Connectors;
using RagAMuffin.Services.Extractors;
using RagAMuffin.Services.Logging;
using RagAMuffin.Qdrant;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

// In-memory log buffer — created before Build() so logger provider can share the same instance
var logBuffer = new InMemoryLogBuffer();
builder.Services.AddSingleton(logBuffer);

// User profile — singleton so GmailConnector and setup endpoint share the same state
builder.Services.AddSingleton<UserProfileService>();

// Connector config — singleton so connectors and the config endpoint share the same state
builder.Services.AddSingleton<ConnectorConfigService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"]!);
});

builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"]!);
    client.Timeout = TimeSpan.FromMinutes(10);
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

// Named HttpClients for connectors that scrape the web
builder.Services.AddHttpClient("rss", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("web", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("RAG-A-Muffin/1.0");
});

// Connectors — add more here as new source types are implemented
builder.Services.AddScoped<IConnector, GmailConnector>();
builder.Services.AddScoped<IConnector, RssConnector>();
builder.Services.AddScoped<IConnector, WebConnector>();
builder.Services.AddScoped<IConnector, GoogleDriveConnector>();
builder.Services.AddScoped<IConnector, GoogleCalendarConnector>();

// Document extractors — each handles a specific file extension
builder.Services.AddScoped<IDocumentExtractor, PdfExtractor>();
builder.Services.AddScoped<IDocumentExtractor, DocxExtractor>();
builder.Services.AddScoped<IDocumentExtractor, PlainTextExtractor>();
builder.Services.AddScoped<FileIngestionService>();

builder.Services.AddHostedService<ConnectorSyncService>();
builder.Services.AddHostedService<FileWatcherService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new InMemoryLoggerProvider(logBuffer));
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

Directory.CreateDirectory("/app/data/tokens");
Directory.CreateDirectory("/app/data/uploads");
Directory.CreateDirectory("/app/data/watch");

var initializer = app.Services.GetRequiredService<QdrantCollectionInitializer>();
await initializer.InitializeAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapGet("/authorize", async (HttpRequest request) =>
{
    var userId = request.Query["userId"].ToString();
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(new { Message = "Missing 'userId'.", Example = "/authorize?userId=you@example.com" });

    var redirectUri = $"{request.Scheme}://{request.Host}/oauth2callback";
    var authUrl = await GoogleAuth.GetAuthorizationUrlAsync(userId, redirectUri);
    return Results.Redirect(authUrl);
});

app.MapGet("/oauth2callback", async (HttpRequest request, string code, string? userId, string? state) =>
{
    var resolvedUserId = userId;
    if (string.IsNullOrWhiteSpace(resolvedUserId) && !string.IsNullOrWhiteSpace(state))
        resolvedUserId = Uri.UnescapeDataString(state);

    if (string.IsNullOrWhiteSpace(resolvedUserId))
        return Results.BadRequest(new { Message = "Missing userId.", Example = "/oauth2callback?code=...&userId=you@example.com" });

    var redirectUri = $"{request.Scheme}://{request.Host}/oauth2callback";
    await GoogleAuth.ExchangeCodeForTokenAsync(resolvedUserId, code, redirectUri);
    return Results.Text("Authentication complete. You may close this page.");
});

// ── Setup endpoints ──────────────────────────────────────────────────────────

app.MapGet("/setup/status", (UserProfileService profile) =>
    Results.Ok(new { isConfigured = profile.IsConfigured, userId = profile.UserId }));

app.MapPost("/setup", async (HttpRequest request, UserProfileService profile) =>
{
    var body = await request.ReadFromJsonAsync<SetupRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.Email))
        return Results.BadRequest(new { Message = "email is required." });

    await profile.SetUserIdAsync(body.Email);
    return Results.Ok(new { userId = body.Email });
});

// ── Log endpoint ─────────────────────────────────────────────────────────────

app.MapGet("/logs", (InMemoryLogBuffer buffer) => Results.Ok(buffer.GetAll()));

// ── Connector config endpoints ────────────────────────────────────────────────

app.MapGet("/config/connectors",
    (ConnectorConfigService cfg) => Results.Ok(cfg.Current));

app.MapPut("/config/connectors",
    async (ConnectorConfig config, ConnectorConfigService cfg) =>
        Results.Ok(await cfg.SaveAsync(config)));

// Dev endpoint: triggers an immediate Gmail sync for the configured user
app.MapGet("/inbox", async (HttpRequest request, IIngestionPipeline pipeline, IEmailParser parser) =>
{
    var userId = request.Query["userId"].ToString();
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(new { Message = "Missing 'userId'.", Example = "/inbox?userId=you@example.com" });

    if (!await GoogleAuth.HasStoredCredentialAsync(userId))
    {
        var authUrl = $"{request.Scheme}://{request.Host}/authorize?userId={Uri.EscapeDataString(userId)}";
        return Results.BadRequest(new { Message = "No stored credentials.", AuthorizationUrl = authUrl });
    }

    var gmailService = await GoogleAuth.CreateGmailServiceAsync(userId);
    var messages = await Gmail.FetchInboxAsync(gmailService, maxResults: 10);

    logger.LogInformation("Fetched {Count} messages for {UserId}. Starting ingestion...", messages.Count, userId);

    var documents = messages
        .Select(m => parser.ParsedEmail(m))
        .Where(p => p is not null)
        .Select(p => new SourceDocument
        {
            Id          = p!.Id,
            SourceType  = "gmail",
            Title       = p.Subject,
            Author      = p.From,
            Recipient   = p.To,
            Cc          = p.Cc,
            Body        = p.Body,
            PublishedAt = p.Date,
            Metadata    = new Dictionary<string, string>
            {
                ["threadId"]       = p.ThreadId ?? string.Empty,
                ["labels"]         = p.Labels ?? string.Empty,
                ["hasAttachments"] = p.HasAttachments ? "true" : "false",
                ["direction"]      = p.Direction ?? "received"
            }
        })
        .ToList();

    await pipeline.IngestAsync(documents);

    return Results.Ok(documents.Select(d => new
    {
        id      = d.Id,
        title   = d.Title,
        author  = d.Author,
        preview = d.Body.Length > 100 ? d.Body[..100] + "..." : d.Body
    }));
});

app.MapPost("/ingest/upload", async (HttpRequest request, FileIngestionService ingestor, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { Message = "Expected multipart/form-data." });

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    if (file is null)
        return Results.BadRequest(new { Message = "No file field found in form data." });

    await using var stream = file.OpenReadStream();
    var id = await ingestor.IngestAsync(stream, file.FileName, ct);

    if (id is null)
        return Results.UnprocessableEntity(new { Message = "File could not be ingested. Unsupported format or empty content." });

    return Results.Ok(new { documentId = id, title = Path.GetFileNameWithoutExtension(file.FileName) });
}).DisableAntiforgery();

app.MapPost("/query", async (QueryRequest request, IRagQueryService queryService, CancellationToken ct) =>
{
    var response = await queryService.QueryAsync(request, ct);
    return Results.Ok(response);
});

app.MapPost("/query/stream", async (QueryRequest request, IRagQueryService queryService, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    await foreach (var token in queryService.StreamQueryAsync(request, ct))
    {
        if (token.StartsWith("[CITATIONS]:"))
        {
            await ctx.Response.WriteAsync($"event: citations\ndata: {token["[CITATIONS]:".Length..]}\n\n", ct);
        }
        else
        {
            await ctx.Response.WriteAsync($"data: {token}\n\n", ct);
        }
        await ctx.Response.Body.FlushAsync(ct);
    }

    await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
    await ctx.Response.Body.FlushAsync(ct);
});

app.Run();

record SetupRequest(string Email);
