using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using RealtimeAuction.Api.Settings;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authentication.Google;
using System.Text.Json;

// #region agent log
try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "Program.cs:13", message = "Application startup - loading .env", data = new { currentDir = Directory.GetCurrentDirectory(), envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"), envExists = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env")) }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
// #endregion agent log

// Load .env file - thử nhiều đường dẫn
var envPath1 = Path.Combine(Directory.GetCurrentDirectory(), ".env");
var envPath2 = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
var envPath3 = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");

string? actualEnvPath = null;
if (File.Exists(envPath1))
{
    actualEnvPath = envPath1;
}
else if (File.Exists(envPath2))
{
    actualEnvPath = envPath2;
}
else if (File.Exists(envPath3))
{
    actualEnvPath = envPath3;
}

// Load bằng DotNetEnv trước
if (!string.IsNullOrWhiteSpace(actualEnvPath))
{
    Env.Load(actualEnvPath);
}
else
{
    Env.Load();
}

// #region agent log
try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "Program.cs:37", message = "After Env.Load - checking variables", data = new { actualEnvPath = actualEnvPath, mailHostAfterLoad = Environment.GetEnvironmentVariable("MAIL_HOST"), mailUsernameAfterLoad = Environment.GetEnvironmentVariable("MAIL_USERNAME") != null ? "SET" : "NULL" }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
// #endregion agent log

// Nếu Env.Load() không load được, đọc trực tiếp từ file và set vào Environment
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MAIL_HOST")) && !string.IsNullOrWhiteSpace(actualEnvPath) && File.Exists(actualEnvPath))
{
    // #region agent log
    try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "Program.cs:45", message = "Env.Load failed, reading .env file directly", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
    // #endregion agent log

    var envLines = File.ReadAllLines(actualEnvPath);
    var mailVariablesFound = new List<string>();
    var totalLines = envLines.Length;
    var processedLines = 0;
    
    // #region agent log
    try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "Program.cs:56", message = "Reading .env file", data = new { actualEnvPath = actualEnvPath, totalLines = totalLines }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
    // #endregion agent log
    
    foreach (var line in envLines)
    {
        var trimmedLine = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
            continue;

        var parts = trimmedLine.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            processedLines++;
            
            // Log các biến MAIL_*
            if (key.StartsWith("MAIL_"))
            {
                mailVariablesFound.Add($"{key}={value.Substring(0, Math.Min(20, value.Length))}...");
            }
            
            // Set vào Environment nếu chưa có
            if (Environment.GetEnvironmentVariable(key) == null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    // #region agent log
    try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "Program.cs:85", message = "After reading .env file directly", data = new { processedLines = processedLines, mailVariablesCount = mailVariablesFound.Count, mailVariablesFound = string.Join("; ", mailVariablesFound), mailHostAfterDirectRead = Environment.GetEnvironmentVariable("MAIL_HOST") ?? "NULL", mailUsernameAfterDirectRead = Environment.GetEnvironmentVariable("MAIL_USERNAME") != null ? "SET" : "NULL", mailPasswordAfterDirectRead = Environment.GetEnvironmentVariable("MAIL_PASSWORD") != null ? "SET" : "NULL" }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
    // #endregion agent log
}

var builder = WebApplication.CreateBuilder(args);

// #region agent log
try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "Program.cs:17", message = "Builder created - checking URL configuration", data = new { aspnetcoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS"), aspnetcoreHttpsPort = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT"), configUrls = builder.Configuration["ASPNETCORE_URLS"], configHttpsPort = builder.Configuration["ASPNETCORE_HTTPS_PORT"] }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
// #endregion agent log

// Configure MongoDB Settings
var mongoDbSettings = new MongoDbSettings
{
    ConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") 
        ?? builder.Configuration["MongoDB:ConnectionString"] 
        ?? "mongodb://localhost:27017",
    DatabaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME") 
        ?? builder.Configuration["MongoDB:DatabaseName"] 
        ?? "realtime-auction-platform"
};

// Configure JWT Settings
var jwtSettings = new JwtSettings
{
    Secret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
        ?? builder.Configuration["JWT:SecretKey"] 
        ?? "your-super-secret-key-change-this-in-production-min-32-characters",
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
        ?? builder.Configuration["JWT:Issuer"] 
        ?? "realtime-auction-platform",
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
        ?? builder.Configuration["JWT:Audience"] 
        ?? "realtime-auction-platform"
};

// Configure Cloudinary Settings
var cloudinarySettings = new CloudinarySettings
{
    CloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") 
        ?? builder.Configuration["Cloudinary:CloudName"] 
        ?? string.Empty,
    ApiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY") 
        ?? builder.Configuration["Cloudinary:ApiKey"] 
        ?? string.Empty,
    ApiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") 
        ?? builder.Configuration["Cloudinary:ApiSecret"] 
        ?? string.Empty
};

// Configure Email Settings
var mailHost = Environment.GetEnvironmentVariable("MAIL_HOST");
var mailPort = Environment.GetEnvironmentVariable("MAIL_PORT");
var mailUsername = Environment.GetEnvironmentVariable("MAIL_USERNAME");
var mailPassword = Environment.GetEnvironmentVariable("MAIL_PASSWORD");
var mailFrom = Environment.GetEnvironmentVariable("MAIL_FROM");
var mailReplyTo = Environment.GetEnvironmentVariable("MAIL_REPLY_TO");

// #region agent log
try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "Program.cs:51", message = "Reading email env variables", data = new { mailHost = mailHost ?? "NULL", mailPort = mailPort ?? "NULL", hasMailUsername = !string.IsNullOrWhiteSpace(mailUsername), hasMailPassword = !string.IsNullOrWhiteSpace(mailPassword), mailFrom = mailFrom ?? "NULL" }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
// #endregion agent log

// Kiểm tra xem có cấu hình SMTP không
var useSmtp = !string.IsNullOrWhiteSpace(mailHost) && 
              !string.IsNullOrWhiteSpace(mailUsername) && 
              !string.IsNullOrWhiteSpace(mailPassword);

// Log để debug
Console.WriteLine($"[Email Config] MAIL_HOST: {(string.IsNullOrWhiteSpace(mailHost) ? "NOT SET" : mailHost)}");
Console.WriteLine($"[Email Config] MAIL_USERNAME: {(string.IsNullOrWhiteSpace(mailUsername) ? "NOT SET" : "SET")}");
Console.WriteLine($"[Email Config] MAIL_PASSWORD: {(string.IsNullOrWhiteSpace(mailPassword) ? "NOT SET" : "SET")}");
Console.WriteLine($"[Email Config] UseSmtp: {useSmtp}");

// #region agent log
try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "Program.cs:67", message = "Email config calculated", data = new { useSmtp = useSmtp }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
// #endregion agent log

// Parse MAIL_FROM nếu có format "Name <email>"
string fromEmail = string.Empty;
string fromName = string.Empty;
if (!string.IsNullOrWhiteSpace(mailFrom))
{
    // Format: "Name <email@domain.com>" hoặc chỉ "email@domain.com"
    if (mailFrom.Contains('<') && mailFrom.Contains('>'))
    {
        var nameMatch = System.Text.RegularExpressions.Regex.Match(mailFrom, @"^(.+?)\s*<(.+?)>$");
        if (nameMatch.Success)
        {
            fromName = nameMatch.Groups[1].Value.Trim();
            fromEmail = nameMatch.Groups[2].Value.Trim();
        }
    }
    else
    {
        fromEmail = mailFrom.Trim();
    }
}

var emailSettings = new EmailSettings
{
    // SMTP settings
    UseSmtp = useSmtp,
    SmtpHost = mailHost ?? builder.Configuration["Email:SmtpHost"] ?? string.Empty,
    SmtpPort = int.TryParse(mailPort ?? builder.Configuration["Email:SmtpPort"], out var port) ? port : 587,
    SmtpUsername = mailUsername ?? builder.Configuration["Email:SmtpUsername"] ?? string.Empty,
    SmtpPassword = mailPassword ?? builder.Configuration["Email:SmtpPassword"] ?? string.Empty,
    
    // SendGrid settings (fallback)
    ApiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY") 
        ?? builder.Configuration["Email:ApiKey"] 
        ?? string.Empty,
    
    // Common settings
    FromEmail = !string.IsNullOrWhiteSpace(fromEmail) ? fromEmail 
        : (Environment.GetEnvironmentVariable("EMAIL_FROM") 
        ?? builder.Configuration["Email:FromEmail"] 
        ?? "noreply@realtimeauction.com"),
    FromName = !string.IsNullOrWhiteSpace(fromName) ? fromName
        : (builder.Configuration["Email:FromName"] 
        ?? "Realtime Auction Platform"),
    ReplyTo = mailReplyTo ?? Environment.GetEnvironmentVariable("EMAIL_REPLY_TO")
        ?? builder.Configuration["Email:ReplyTo"]
        ?? string.Empty
};

// Add services to the container.
builder.Services.AddSingleton(mongoDbSettings);
builder.Services.Configure<CloudinarySettings>(options =>
{
    options.CloudName = cloudinarySettings.CloudName;
    options.ApiKey = cloudinarySettings.ApiKey;
    options.ApiSecret = cloudinarySettings.ApiSecret;
});
builder.Services.Configure<JwtSettings>(options =>
{
    options.Secret = jwtSettings.Secret;
    options.Issuer = jwtSettings.Issuer;
    options.Audience = jwtSettings.Audience;
});
builder.Services.Configure<EmailSettings>(options =>
{
    options.UseSmtp = emailSettings.UseSmtp;
    options.SmtpHost = emailSettings.SmtpHost;
    options.SmtpPort = emailSettings.SmtpPort;
    options.SmtpUsername = emailSettings.SmtpUsername;
    options.SmtpPassword = emailSettings.SmtpPassword;
    options.ApiKey = emailSettings.ApiKey;
    options.FromEmail = emailSettings.FromEmail;
    options.FromName = emailSettings.FromName;
    options.ReplyTo = emailSettings.ReplyTo;
});

// Register MongoDB Database
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<MongoDbSettings>();
    var client = new MongoClient(settings.ConnectionString);
    return client.GetDatabase(settings.DatabaseName);
});

// Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAuctionRepository, AuctionRepository>();
builder.Services.AddScoped<IBidRepository, BidRepository>();
builder.Services.AddScoped<IWatchlistRepository, WatchlistRepository>();
builder.Services.AddScoped<IShippingInfoRepository, ShippingInfoRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IProvinceService, ProvinceService>();

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, "Admin");
    });
});

// Add Controllers
builder.Services.AddControllers();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable CORS - phải đặt TRƯỚC UseHttpsRedirection
app.UseCors("AllowReactApp");

// Only enable HTTPS redirection if HTTPS port is configured
// This prevents the warning when running with HTTP-only profile
var httpsPort = builder.Configuration["ASPNETCORE_HTTPS_PORT"];
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "";
var hasHttps = !string.IsNullOrEmpty(httpsPort) || urls.Contains("https://");

if (hasHttps)
{
    app.UseHttpsRedirection();
}

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// #region agent log
try 
{ 
    var appUrls = app.Urls;
    var configuredUrls = new List<string>();
    foreach (var url in appUrls) { configuredUrls.Add(url); }
    System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "Program.cs:174", message = "Before app.Run() - configured URLs", data = new { urls = configuredUrls, urlsCount = configuredUrls.Count, aspnetcoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS"), launchProfile = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); 
} 
catch (Exception ex) 
{ 
    System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "Program.cs:174", message = "Error logging URLs before Run()", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); 
}
// #endregion agent log

// #region agent log
try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "Program.cs:177", message = "About to start app.Run() - port binding will occur", data = new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
// #endregion agent log

app.Run();
