using FarmAndFriends.Api.Infrastructure.Auth;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FarmAndFriends.Api.Configuration;
using FarmAndFriends.Api.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
builder.Services
    .AddOptions<CropCareOptions>()
    .Bind(builder.Configuration.GetSection(CropCareOptions.SectionName))
    .Validate(options => options.VisitorPlotCareCooldownHours > 0)
    .Validate(options => options.VisitorFarmRewardCycleHours > 0)
    .Validate(options =>
        options.VisitorFarmRewardRollingWindowHours > 0)
    .Validate(options =>
        options.MaxRewardedCyclesPerVisitorFarmWindow > 0)
    .Validate(options =>
        options.OwnerNotificationDeduplicationWindowHours > 0)
    .Validate(options => options.CoinsReward > 0)
    .Validate(options => options.XpReward > 0)
    .ValidateOnStart();
builder.Services
    .AddOptions<PestOptions>()
    .Bind(builder.Configuration.GetSection(PestOptions.SectionName))
    .Validate(options => options.SafetyPeriodMinutes >= 0)
    .Validate(options => options.ReactionWindowMinutes > 0)
    .Validate(options => options.DamageAmount == 1)
    .Validate(options => options.MaxActivePestsPerFarm > 0)
    .Validate(options =>
        options.MinimumInfestationIntervalMinutes >= 0)
    .Validate(options => options.ProtectionDurationHours > 0)
    .Validate(options =>
        options.NaturalRepellentBuyPrice > 0
        && options.NaturalRepellentMinLevel > 0)
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.NaturalRepellentName)
        && !string.IsNullOrWhiteSpace(options.NaturalRepellentIcon)
        && !string.IsNullOrWhiteSpace(
            options.NaturalRepellentDescription))
    .Validate(options => options.RemovalCoinsReward >= 0)
    .Validate(options => options.RemovalXpReward >= 0)
    .Validate(options =>
        options.MaxRewardedRemovalsPerWindow > 0)
    .Validate(options =>
        options.RemovalRewardRollingWindowHours > 0)
    .ValidateOnStart();
builder.Services
    .AddOptions<LandExpansionOptions>()
    .Bind(builder.Configuration.GetSection(
        LandExpansionOptions.SectionName));

var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings!.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();

// Add Authorize buttun
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Minha API", Version = "v1" });

    // Configuração do JWT
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira seu token"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Progression 
builder.Services.AddScoped<ExperienceService>();
// Theft
builder.Services.AddScoped<TheftService>();
// Friendships
builder.Services.AddScoped<FriendshipService>();
// Social crop care
builder.Services.AddScoped<CropCareService>();
// Crop pests
builder.Services.AddScoped<PestService>();
// Land expansion
builder.Services.AddScoped<LandExpansionService>();
builder.Services.AddSingleton(TimeProvider.System);

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();
