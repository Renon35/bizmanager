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
// Railway sets DATABASE_URL; fall back to appsettings for local dev.
var rawDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
               ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(rawDbUrl))
    throw new InvalidOperationException(
        "DATABASE_URL environment variable is not set. " +
        "Add it in Railway → Service → Variables.");

var pgConnectionString = ConvertPostgresUri(rawDbUrl);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(pgConnectionString, npgsql =>
        npgsql.EnableRetryOnFailure(3)));

// ─── Supabase Storage client ──────────────────────────────────────────────────
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
                  ?? builder.Configuration["Supabase:Url"] ?? "";
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")
                  ?? builder.Configuration["Supabase:Key"] ?? "";

builder.Services.AddSingleton<SupabaseStorageService>(_ =>
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

// ─── Helper: convert postgres:// URI → Npgsql Key=Value string ───────────────
static string ConvertPostgresUri(string uri)
{
    // Normalise scheme
    uri = uri.Trim();
    if (uri.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        uri = "postgresql://" + uri["postgres://".Length..];

    if (!uri.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return uri; // already Key=Value format, pass through

    // Strip scheme
    var rest = uri["postgresql://".Length..];

    // Use LAST @ to safely handle passwords that contain @
    var atIdx = rest.LastIndexOf('@');
    if (atIdx < 0) throw new ArgumentException("Invalid PostgreSQL URI: missing @");

    var userInfo = rest[..atIdx];       // e.g. "postgres:apexM80039534"
    var hostInfo = rest[(atIdx + 1)..]; // e.g. "db.xxx.supabase.co:5432/postgres"

    // Split user:password (password may contain colons)
    var colonIdx = userInfo.IndexOf(':');
    var user = colonIdx >= 0 ? userInfo[..colonIdx] : userInfo;
    var pass = colonIdx >= 0 ? userInfo[(colonIdx + 1)..] : "";

    // Split host:port/database
    var slashIdx = hostInfo.IndexOf('/');
    var hostPort = slashIdx >= 0 ? hostInfo[..slashIdx] : hostInfo;
    var database = slashIdx >= 0 ? hostInfo[(slashIdx + 1)..] : "postgres";

    var portColonIdx = hostPort.LastIndexOf(':');
    var host = portColonIdx >= 0 ? hostPort[..portColonIdx] : hostPort;
    var portStr = portColonIdx >= 0 ? hostPort[(portColonIdx + 1)..] : "5432";

    return $"Host={host};Port={portStr};Database={database};Username={user};Password={pass};" +
           "SSL Mode=Require;Trust Server Certificate=true";
}
