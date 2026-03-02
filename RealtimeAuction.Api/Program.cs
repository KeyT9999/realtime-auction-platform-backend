using System.Text;
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
        ?? "http://localhost:5173/payment/success",
    CancelUrl = Environment.GetEnvironmentVariable("PAYOS_CANCEL_URL") 
        ?? builder.Configuration["PayOS:CancelUrl"] 
        ?? "http://localhost:5173/payment/cancel"
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



// Kiểm tra xem có cấu hình SMTP không
var useSmtp = !string.IsNullOrWhiteSpace(mailHost) && 
              !string.IsNullOrWhiteSpace(mailUsername) && 
              !string.IsNullOrWhiteSpace(mailPassword);

// Log để debug
Console.WriteLine($"[Email Config] MAIL_HOST: {(string.IsNullOrWhiteSpace(mailHost) ? "NOT SET" : mailHost)}");
Console.WriteLine($"[Email Config] MAIL_USERNAME: {(string.IsNullOrWhiteSpace(mailUsername) ? "NOT SET" : "SET")}");
Console.WriteLine($"[Email Config] MAIL_PASSWORD: {(string.IsNullOrWhiteSpace(mailPassword) ? "NOT SET" : "SET")}");
Console.WriteLine($"[Email Config] UseSmtp: {useSmtp}");



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
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
builder.Services.AddScoped<IBankAccountRepository, BankAccountRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IProvinceService, ProvinceService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IBidService, BidService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();

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

// Add Controllers
builder.Services.AddControllers();

// Add Background Services
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.AuctionEndBackgroundService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.AuctionEndingSoonNotificationService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.TransactionReminderService>();
builder.Services.AddHostedService<RealtimeAuction.Api.BackgroundServices.WithdrawalReminderService>();

// Add SignalR
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Seed categories on startup
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await SeedCategories.SeedAsync(database);
}

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

// Map SignalR Hub
app.MapHub<RealtimeAuction.Api.Hubs.AuctionHub>("/auctionHub");

app.Run();
