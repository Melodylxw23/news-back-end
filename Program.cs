using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using News_Back_end;
using News_Back_end.Models;
using News_Back_end.Services;
using System.Security.Cryptography;


var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
// Configure controllers to serialize/deserialize enums as strings for readability
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

//gmail service
builder.Services.AddTransient<GmailEmailService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer <token>'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });

    // Helpful for dev: register common local server URLs so Swagger posts to the correct scheme/port
    // Adjust ports if your launch profile uses other ports.
    c.AddServer(new OpenApiServer { Url = "https://localhost:7191" });
    c.AddServer(new OpenApiServer { Url = "http://localhost:5216" });
});

// Register EF Core DbContext for SQL Server
builder.Services.AddDbContext<MyDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyConnection")));

// CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
{
    throw new Exception("AllowedOrigins is required for CORS policy.");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // In Development allow any origin to avoid CORS issues while testing Swagger/UI locally
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// Identity
builder.Services.AddIdentity<News_Back_end.Models.SQLServer.ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<MyDBContext>()
    .AddDefaultTokenProviders();

// JWT Authentication: require a base64-encoded symmetric key in configuration and validate length (>256 bits)
var jwtKeyBase64 = builder.Configuration["Jwt:KeyBase64"];
byte[] jwtKeyBytes = null;

bool isDev = builder.Environment.IsDevelopment();

if (string.IsNullOrWhiteSpace(jwtKeyBase64))
{
    if (!isDev)
        throw new Exception("Missing configuration: Jwt:KeyBase64 (base64-encoded symmetric key is required).");

    // In Development auto-generate a sufficiently strong key and warn the developer
    jwtKeyBytes = new byte[48]; // 48 bytes = 384 bits
    RandomNumberGenerator.Fill(jwtKeyBytes);
    jwtKeyBase64 = Convert.ToBase64String(jwtKeyBytes);
    Console.WriteLine("[Development] Generated Jwt:KeyBase64 (store this securely if you need reproducible tokens): " + jwtKeyBase64);
}

if (jwtKeyBytes == null)
{
    try
    {
        jwtKeyBytes = Convert.FromBase64String(jwtKeyBase64!);
    }
    catch (FormatException ex)
    {
        if (!isDev)
            throw new Exception("Invalid Jwt:KeyBase64: not a valid Base64 string.", ex);

        // In Development, generate a key instead of failing
        jwtKeyBytes = new byte[48];
        RandomNumberGenerator.Fill(jwtKeyBytes);
        jwtKeyBase64 = Convert.ToBase64String(jwtKeyBytes);
        Console.WriteLine("[Development] Provided Jwt:KeyBase64 was invalid Base64. Generated new Jwt:KeyBase64: " + jwtKeyBase64);
    }
}

int keyBits = jwtKeyBytes.Length * 8;
if (keyBits <= 256)
{
    if (!isDev)
        throw new Exception($"Jwt key is too short; require >256 bits but was {keyBits} bits. Provide a longer base64-encoded key in Jwt:KeyBase64.");

    // In Development, generate a larger key
    jwtKeyBytes = new byte[48];
    RandomNumberGenerator.Fill(jwtKeyBytes);
    jwtKeyBase64 = Convert.ToBase64String(jwtKeyBytes);
    Console.WriteLine($"[Development] Jwt:KeyBase64 was too short ({keyBits} bits). Generated new {jwtKeyBytes.Length * 8}-bit Jwt:KeyBase64: " + jwtKeyBase64);
}

var signingKey = new SymmetricSecurityKey(jwtKeyBytes);
// register the signing key for reuse via DI
builder.Services.AddSingleton(signingKey);

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NewsBackEnd";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NewsBackEndAudience";

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
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey
        };
    });

// Register crawler/translation services (typed HttpClients and scoped factory)
builder.Services.AddHttpClient<RSSCrawlerService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("NewsCrawler/1.0 (+you@domain)");
});
builder.Services.AddHttpClient<APICrawlerservice>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("NewsCrawler/1.0 (+you@domain)");
});
builder.Services.AddHttpClient<HTMLCrawlerService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("NewsCrawler/1.0 (+you@domain)");
});

builder.Services.AddScoped<CrawlerFactory, CrawlersFactory>();
// Register unified crawler that composes RSS/API/HTML crawlers and processor
builder.Services.AddScoped<UnifiedCrawlerService>();
// Register ArticleProcessor for processing fetched articles
builder.Services.AddScoped<ArticleProcessor>();

// Hosted background crawler
builder.Services.AddHostedService<NewsCrawlerBackgroundService>();

// OpenAI translator (optional) - configure via appsettings or env
var openAIApiKey = builder.Configuration["OpenAI:ApiKey"];
var openAIBase = builder.Configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/";
if (!string.IsNullOrWhiteSpace(openAIApiKey))
{
    // Register OpenAITranslationService as a typed HttpClient implementation of ITranslationService
    builder.Services.AddHttpClient<ITranslationService, OpenAITranslationService>(c =>
    {
        c.BaseAddress = new Uri(openAIBase);
        c.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAIApiKey}");
        // Increase default timeout to accommodate longer OpenAI calls
        c.Timeout = TimeSpan.FromSeconds(120);
    });
}

// Image generation service (optional) - use separate config section OpenAIHeroImageCreation
var openAIHeroKey = builder.Configuration["OpenAIHeroImageCreation:ApiKey"];
var openAIHeroBase = builder.Configuration["OpenAIHeroImageCreation:BaseUrl"] ?? openAIBase;
if (!string.IsNullOrWhiteSpace(openAIHeroKey))
{
    builder.Services.AddHttpClient<IImageGenerationService, OpenAIImageService>(c =>
    {
        c.BaseAddress = new Uri(openAIHeroBase);
        c.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAIHeroKey}");
        c.Timeout = TimeSpan.FromSeconds(120);
    });
}

// Register dedicated OpenAIBroadcastService from OpenAIBroadcasr configuration section (note intentional key name to match user's file)
var openAIBroadcastKey = builder.Configuration["OpenAIBroadcast:ApiKey"];
if (!string.IsNullOrWhiteSpace(openAIBroadcastKey))
{
    var openAIBroadcastBase = builder.Configuration["OpenAIBroadcast:BaseUrl"] ?? "https://api.openai.com/";
    builder.Services.AddHttpClient<IAiBroadcastService, OpenAIBroadcastService>(c =>
    {
        c.BaseAddress = new Uri(openAIBroadcastBase);
        c.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAIBroadcastKey}");
        c.Timeout = TimeSpan.FromSeconds(30);
    });
}

// Register PublicationService and ScheduledPublishHostedService
builder.Services.AddScoped<IPublicationService, PublicationService>();
builder.Services.AddHostedService<ScheduledPublishHostedService>();

// Register dedicated OpenAIContentCreationService from OpenAIContentCreation configuration section
var openAIContentCreationKey = builder.Configuration["OpenAIContentCreation:ApiKey"];
if (!string.IsNullOrWhiteSpace(openAIContentCreationKey))
{
    // legacy content creation registration removed - Summarization handled by ITranslationService.SummarizeAsync
}

builder.Services.AddAuthorization();

// Validate required config for frontend reset URL
if (string.IsNullOrWhiteSpace(builder.Configuration["Frontend:ResetPasswordUrl"]))
{
    var msg = "Missing configuration: Frontend:ResetPasswordUrl";
    Console.WriteLine(msg);
    if (!builder.Environment.IsDevelopment())
        throw new Exception(msg);
}

var app = builder.Build();

// Seed roles and apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyDBContext>();
    try
    {
        db.Database.Migrate();

        // Explicit runtime checks to catch LocalDB/migration problems early
        var conn = db.Database.GetDbConnection();
        Console.WriteLine($"[DB] Provider: {db.Database.ProviderName}, DataSource: {conn.DataSource}, Database: {conn.Database}");
        try
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Sources'";
            var cntObj = cmd.ExecuteScalar();
            var count = cntObj == null ? 0 : Convert.ToInt32(cntObj);
            if (count == 0)
            {
                throw new Exception("Required table 'Sources' does not exist in the target database. Ensure migrations were applied to the correct database/instance.");
            }
        }
        finally
        {
            try { conn.Close(); } catch { }
        }
    }
    catch (Exception ex)
    {
        // Log full exception and fail startup so the problem is visible and addressed
        Console.WriteLine($"Database migrate/check failed: {ex}");
        throw;
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var roles = new[] { "Admin", "Consultant", "Member" };
    foreach (var r in roles)
    {
        if (!await roleManager.RoleExistsAsync(r))
            await roleManager.CreateAsync(new IdentityRole(r));
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();











