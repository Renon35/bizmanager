using BizManager.Data;
using BizManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ─── Database: PostgreSQL (Supabase) ─────────────────────────────────────────
// Railway sets DATABASE_URL env var; fallback to appsettings for local dev.
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DATABASE_URL is not set.");

// Npgsql requires the URI scheme to be 'postgresql' or 'postgres'
// and does NOT support double-@ in the URL, so we normalise here.
var normalised = dbUrl.Replace("postgresql://", "").Replace("postgres://", "");
// If user accidentally typed @@ instead of @, trim it:
var atIdx = normalised.LastIndexOf('@');
var connStr = atIdx >= 0
    ? "Host=" + normalised[(atIdx + 1)..]
        .Replace("/", ";Database=")
        .Replace(":", ";Port=") + ";Username="
        + normalised[..atIdx].Split(':')[0] + ";Password="
        + string.Join(":", normalised[..atIdx].Split(':').Skip(1))
        + ";SSL Mode=Require;Trust Server Certificate=true"
    : dbUrl;

// Prefer using the raw URI directly — Npgsql supports it natively.
// Just make sure the scheme is 'postgresql' (not 'postgres').
var pgConnectionString = dbUrl.StartsWith("postgres://")
    ? "postgresql://" + dbUrl["postgres://".Length..]
    : dbUrl;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(pgConnectionString, npgsql =>
        npgsql.EnableRetryOnFailure(3)));

// ─── Supabase Storage client ──────────────────────────────────────────────────
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
                  ?? builder.Configuration["Supabase:Url"] ?? "";
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
                  ?? builder.Configuration["Supabase:Key"] ?? "";

builder.Services.AddSingleton<SupabaseStorageService>(sp =>
    new SupabaseStorageService(supabaseUrl, supabaseKey));

builder.Services.AddSingleton<PdfService>();

builder.Services.AddHttpClient<ProductImageScraperService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
    client.DefaultRequestHeaders.Add("DNT", "1");
    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = int.MaxValue;
});

var app = builder.Build();

// Apply DB schema at startup
DbMigrator.ApplyMigrations(app);

app.UseCors();
app.UseStaticFiles();
app.MapControllers();

// Serve index.html for all non-API routes (SPA fallback)
app.MapFallbackToFile("index.html");

// Railway sets PORT env var; ASP.NET Core respects ASPNETCORE_URLS automatically.
// When running locally without that var, fall back to 5000.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");
