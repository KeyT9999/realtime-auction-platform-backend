using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using RealtimeAuction.Api.Settings;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.SignalR;
using RealtimeAuction.Api.Hubs;
using System.Text.Json;
using FluentValidation;
using FluentValidation.AspNetCore;



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



// Nếu Env.Load() không load được, đọc trực tiếp từ file và set vào Environment
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MAIL_HOST")) && !string.IsNullOrWhiteSpace(actualEnvPath) && File.Exists(actualEnvPath))
{


    var envLines = File.ReadAllLines(actualEnvPath);
    var mailVariablesFound = new List<string>();
    var totalLines = envLines.Length;
    var processedLines = 0;
    

    
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


}

var builder = WebApplication.CreateBuilder(args);

// Cho phép upload file tới 50MB (Kestrel mặc định 28MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

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

// Configure FrontendUrl — used for CORS, email links, PayOS callbacks
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL")
    ?? builder.Configuration["FrontendUrl"]
    ?? "http://localhost:5173";
builder.Configuration["FrontendUrl"] = frontendUrl;

static bool ParseBoolean(string? value, bool defaultValue)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

// Configure PayOS Settings
var payOsSettings = new PayOsSettings
{
    ClientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID") 
        ?? builder.Configuration["PayOS:ClientId"] 
        ?? string.Empty,
    ApiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY") 
        ?? builder.Configuration["PayOS:ApiKey"] 
        ?? string.Empty,
    ChecksumKey = Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY") 
        ?? builder.Configuration["PayOS:ChecksumKey"] 
        ?? string.Empty,
    ReturnUrl = Environment.GetEnvironmentVariable("PAYOS_RETURN_URL") 
        ?? builder.Configuration["PayOS:ReturnUrl"] 
        ?? $"{frontendUrl}/payment/success",
    CancelUrl = Environment.GetEnvironmentVariable("PAYOS_CANCEL_URL") 
        ?? builder.Configuration["PayOS:CancelUrl"] 
        ?? $"{frontendUrl}/payment/cancel"
};

// Configure JWT Settings
var jwtSettings = new JwtSettings
{
    Secret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
        ?? builder.Configuration["JWT:SecretKey"] 
        ?? "",
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
        ?? builder.Configuration["JWT:Issuer"] 
        ?? "realtime-auction-platform",
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
        ?? builder.Configuration["JWT:Audience"] 
        ?? "realtime-auction-platform"
};

// Validate JWT secret — fail fast if insecure
if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Contains("your-super-secret-key") || jwtSettings.Secret.Length < 32)
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "JWT SecretKey is missing or insecure. Set JWT_SECRET_KEY environment variable with a strong key (>= 32 characters).");
    }
    if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
    {
        throw new InvalidOperationException(
            "JWT SecretKey is not configured. Set it in appsettings.Development.json or JWT_SECRET_KEY environment variable.");
    }
    Console.WriteLine("[WARNING] JWT SecretKey may be insecure. Set JWT_SECRET_KEY env variable for production.");
}

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

var captchaSettings = new CaptchaSettings
{
    Enabled = ParseBoolean(Environment.GetEnvironmentVariable("CAPTCHA_ENABLED")
        ?? builder.Configuration["Captcha:Enabled"], true),
    SecretKey = Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY")
        ?? builder.Configuration["Captcha:SecretKey"]
        ?? string.Empty,
    MinimumScore = double.TryParse(
        Environment.GetEnvironmentVariable("RECAPTCHA_MINIMUM_SCORE")
        ?? builder.Configuration["Captcha:MinimumScore"],
        out var captchaMinimumScore) ? captchaMinimumScore : 0.5d,
    VerifyUrl = Environment.GetEnvironmentVariable("RECAPTCHA_VERIFY_URL")
        ?? builder.Configuration["Captcha:VerifyUrl"]
        ?? "https://www.google.com/recaptcha/api/siteverify",
    TimeoutSeconds = int.TryParse(
        Environment.GetEnvironmentVariable("RECAPTCHA_TIMEOUT_SECONDS")
        ?? builder.Configuration["Captcha:TimeoutSeconds"],
        out var captchaTimeout) ? captchaTimeout : 10
};

var geminiSettings = new GeminiSettings
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
        ?? builder.Configuration["Gemini:ApiKey"]
        ?? string.Empty,
    Model = Environment.GetEnvironmentVariable("GEMINI_MODEL")
        ?? builder.Configuration["Gemini:Model"]
        ?? "gemini-2.5-flash",
    TimeoutSeconds = int.TryParse(
        Environment.GetEnvironmentVariable("GEMINI_TIMEOUT_SECONDS")
        ?? builder.Configuration["Gemini:TimeoutSeconds"],
        out var geminiTimeout) ? geminiTimeout : 60
};

var firebaseAuthSettings = new FirebaseAuthSettings
{
    ProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
        ?? builder.Configuration["Firebase:ProjectId"]
        ?? string.Empty,
    ClientEmail = Environment.GetEnvironmentVariable("FIREBASE_CLIENT_EMAIL")
        ?? builder.Configuration["Firebase:ClientEmail"]
        ?? string.Empty,
    PrivateKey = Environment.GetEnvironmentVariable("FIREBASE_PRIVATE_KEY")
        ?? builder.Configuration["Firebase:PrivateKey"]
        ?? string.Empty
};

// Configure Email Settings
var mailHost = Environment.GetEnvironmentVariable("MAIL_HOST");
var mailPort = Environment.GetEnvironmentVariable("MAIL_PORT");
var mailUsername = Environment.GetEnvironmentVariable("MAIL_USERNAME");
var mailPassword = Environment.GetEnvironmentVariable("MAIL_PASSWORD");
var mailFrom = Environment.GetEnvironmentVariable("MAIL_FROM");
var mailReplyTo = Environment.GetEnvironmentVariable("MAIL_REPLY_TO");



// Kiểm tra xem có cấu hình SMTP không
var useSmtp = !string.IsNullOrWhiteSpace(mailHost) && 
              !string.IsNullOrWhiteSpace(mailUsername) && 
              !string.IsNullOrWhiteSpace(mailPassword);

// Log để debug
if (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"[Email Config] MAIL_HOST: {(string.IsNullOrWhiteSpace(mailHost) ? "NOT SET" : mailHost)}");
    Console.WriteLine($"[Email Config] MAIL_USERNAME: {(string.IsNullOrWhiteSpace(mailUsername) ? "NOT SET" : "SET")}");
    Console.WriteLine($"[Email Config] MAIL_PASSWORD: {(string.IsNullOrWhiteSpace(mailPassword) ? "NOT SET" : "SET")}");
    Console.WriteLine($"[Email Config] UseSmtp: {useSmtp}");
}



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
builder.Services.Configure<CaptchaSettings>(options =>
{
    options.Enabled = captchaSettings.Enabled;
    options.SecretKey = captchaSettings.SecretKey;
    options.MinimumScore = captchaSettings.MinimumScore;
    options.VerifyUrl = captchaSettings.VerifyUrl;
    options.TimeoutSeconds = captchaSettings.TimeoutSeconds;
});
builder.Services.Configure<GeminiSettings>(options =>
{
    options.ApiKey = geminiSettings.ApiKey;
    options.Model = geminiSettings.Model;
    options.TimeoutSeconds = geminiSettings.TimeoutSeconds;
});
builder.Services.Configure<FirebaseAuthSettings>(options =>
{
    options.ProjectId = firebaseAuthSettings.ProjectId;
    options.ClientEmail = firebaseAuthSettings.ClientEmail;
    options.PrivateKey = firebaseAuthSettings.PrivateKey;
});

// Notifications: Realtime = SignalR; offline = email when SendEmailWhenOffline is true
var sendEmailWhenOffline = Environment.GetEnvironmentVariable("NOTIFICATIONS_SEND_EMAIL_WHEN_OFFLINE");
builder.Services.Configure<NotificationSettings>(options =>
{
    options.SendEmailWhenOffline = sendEmailWhenOffline != null
        ? string.Equals(sendEmailWhenOffline, "true", StringComparison.OrdinalIgnoreCase) || sendEmailWhenOffline == "1"
        : (builder.Configuration.GetValue<bool?>("Notifications:SendEmailWhenOffline") ?? true);
});

// Register MongoDB Database
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<MongoDbSettings>();
    return new MongoClient(settings.ConnectionString);
});
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<MongoDbSettings>();
    var client = serviceProvider.GetRequiredService<IMongoClient>();
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
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
builder.Services.AddScoped<IBankAccountRepository, BankAccountRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IContactMessageRepository, ContactMessageRepository>();
builder.Services.AddScoped<IDisputeRepository, DisputeRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICaptchaVerificationService, CaptchaVerificationService>();
builder.Services.AddScoped<IGeminiService, GeminiService>();
builder.Services.AddScoped<IFirebaseTokenService, FirebaseTokenService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IProvinceService, ProvinceService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();
builder.Services.AddScoped<IEscrowService, EscrowService>();

// Configure PayOS
builder.Services.Configure<PayOsSettings>(options =>
{
    options.ClientId = payOsSettings.ClientId;
    options.ApiKey = payOsSettings.ApiKey;
    options.ChecksumKey = payOsSettings.ChecksumKey;
    options.ReturnUrl = payOsSettings.ReturnUrl;
    options.CancelUrl = payOsSettings.CancelUrl;
});
builder.Services.AddHttpClient("PayOS");
builder.Services.AddHttpClient("Captcha", client =>
{
    client.Timeout = TimeSpan.FromSeconds(captchaSettings.TimeoutSeconds);
});
builder.Services.AddHttpClient("Gemini", client =>
{
    client.Timeout = TimeSpan.FromSeconds(geminiSettings.TimeoutSeconds);
});

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

    // Allow JWT to be passed via query string for SignalR WebSockets
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/auctionHub"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
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

// Add Controllers - JSON camelCase for frontend
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// FluentValidation — auto-validate [FromBody] DTOs before controller action executes
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add Background Services
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.AuctionStartBackgroundService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.AuctionEndBackgroundService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.AuctionEndingSoonNotificationService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.TransactionReminderService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.WithdrawalReminderService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.EscrowAutoReleaseService>();

// Add SignalR
builder.Services.AddSignalR();

// Add CORS — use configured FrontendUrl + dev fallback
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        var origins = new List<string> { frontendUrl };
        // Always allow dev ports as fallback
        if (!origins.Contains("http://localhost:5173")) origins.Add("http://localhost:5173");
        if (!origins.Contains("http://localhost:5174")) origins.Add("http://localhost:5174");
        
        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Health Checks — MongoDB connectivity
builder.Services.AddHealthChecks()
    .AddCheck<RealtimeAuction.Api.Helpers.MongoDbHealthCheck>("mongodb");

// Add Rate Limiting — protect auth endpoints from brute-force
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Policy for auth endpoints: 5 requests per minute per IP
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    
    // Stricter policy for sensitive endpoints: 3 requests per minute per IP
    options.AddFixedWindowLimiter("auth-strict", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

// Seed categories and ensure indexes on startup
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await SeedCategories.SeedAsync(database);
    var bids = database.GetCollection<RealtimeAuction.Api.Models.Bid>("Bids");
    var indexKeys = Builders<RealtimeAuction.Api.Models.Bid>.IndexKeys
        .Descending(b => b.AuctionId)
        .Descending(b => b.Amount);
    await bids.Indexes.CreateOneAsync(new CreateIndexModel<RealtimeAuction.Api.Models.Bid>(indexKeys));

    var auctions = database.GetCollection<RealtimeAuction.Api.Models.Auction>("Auctions");
    await auctions.Indexes.CreateManyAsync(new[]
    {
        new CreateIndexModel<RealtimeAuction.Api.Models.Auction>(
            Builders<RealtimeAuction.Api.Models.Auction>.IndexKeys.Ascending(a => a.Status)),
        new CreateIndexModel<RealtimeAuction.Api.Models.Auction>(
            Builders<RealtimeAuction.Api.Models.Auction>.IndexKeys.Ascending(a => a.CategoryId)),
        new CreateIndexModel<RealtimeAuction.Api.Models.Auction>(
            Builders<RealtimeAuction.Api.Models.Auction>.IndexKeys.Ascending(a => a.SellerId)),
        new CreateIndexModel<RealtimeAuction.Api.Models.Auction>(
            Builders<RealtimeAuction.Api.Models.Auction>.IndexKeys.Ascending(a => a.EndTime)),
        new CreateIndexModel<RealtimeAuction.Api.Models.Auction>(
            Builders<RealtimeAuction.Api.Models.Auction>.IndexKeys.Descending(a => a.StartTime)),
        new CreateIndexModel<RealtimeAuction.Api.Models.Auction>(
            Builders<RealtimeAuction.Api.Models.Auction>.IndexKeys.Ascending(a => a.CurrentPrice)),
    });

    var users = database.GetCollection<RealtimeAuction.Api.Models.User>("Users");
    await users.Indexes.CreateOneAsync(new CreateIndexModel<RealtimeAuction.Api.Models.User>(
        Builders<RealtimeAuction.Api.Models.User>.IndexKeys.Ascending(u => u.Email),
        new CreateIndexOptions { Unique = true }));
}

// Configure the HTTP request pipeline.

// Global exception handler — must be first in pipeline to catch all errors
app.UseMiddleware<RealtimeAuction.Api.Middleware.GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable CORS - phải đặt TRƯỚC UseHttpsRedirection
app.UseCors("AllowReactApp");

// Enable Rate Limiting — must be before auth middleware
app.UseRateLimiter();

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

// Map SignalR Hub
app.MapHub<RealtimeAuction.Api.Hubs.AuctionHub>("/auctionHub");

// Health check endpoint
app.MapHealthChecks("/health");

app.Run();
