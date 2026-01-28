using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using RealtimeAuction.Api.Hubs;
using RealtimeAuction.Api.Jobs;
using Quartz;
using RealtimeAuction.Api.Settings;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authentication.Google;

// Load .env file
Env.Load();

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

// Configure Email Settings
var emailSettings = new EmailSettings
{
    FromEmail = Environment.GetEnvironmentVariable("EMAIL_FROM") 
        ?? builder.Configuration["Email:FromEmail"] 
        ?? "noreply@realtimeauction.com",
    FromName = Environment.GetEnvironmentVariable("EMAIL_FROM_NAME") 
        ?? builder.Configuration["Email:FromName"] 
        ?? "Realtime Auction Platform",
    ApiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY") 
        ?? builder.Configuration["Email:ApiKey"] 
        ?? string.Empty
};

// Add services to the container.
builder.Services.AddSingleton(mongoDbSettings);
builder.Services.Configure<JwtSettings>(options =>
{
    options.Secret = jwtSettings.Secret;
    options.Issuer = jwtSettings.Issuer;
    options.Audience = jwtSettings.Audience;
});
builder.Services.Configure<EmailSettings>(options =>
{
    options.FromEmail = emailSettings.FromEmail;
    options.FromName = emailSettings.FromName;
    options.ApiKey = emailSettings.ApiKey;
});

// Register MongoDB Database
// Register MongoDB Client & Database
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<MongoDbSettings>();
    return new MongoClient(settings.ConnectionString);
});
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var client = serviceProvider.GetRequiredService<IMongoClient>();
    var settings = serviceProvider.GetRequiredService<MongoDbSettings>();
    return client.GetDatabase(settings.DatabaseName);
});

// Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();

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
// Add Controllers
builder.Services.AddControllers();

// Add SignalR
builder.Services.AddSignalR();

// Add Quartz
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("AuctionStatusJob");
    q.AddJob<AuctionStatusJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("AuctionStatusJob-trigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(1)
            .RepeatForever()));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

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
// Map Controllers
app.MapControllers();
app.MapHub<AuctionHub>("/auctionHub");

app.Run();
