using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using mydental.domain.IRepositories;
using mydental.infrastructure.Data;
using mydental.infrastructure.Repositories;
using FluentValidation;
using mydental.application.DTO.PatientDTO;
using mydental.application.Validators;
using mydental.application.Services.PatientServices;
using mydental.application.Common;
using Microsoft.Extensions.DependencyInjection;
using mydental.infrastructure.Configurations;
using mydental.application.Services.IServices;
using mydental.application.Services.Services;
using mydental.application.DTO.AuthDTO;
using mydental.application.Helpers;
using mydental.API.Common;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);


// ✅ Setup Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Debug()
    .WriteTo.File("Logs/myapp.log", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .CreateLogger();

builder.Host.UseSerilog(); // ✅ Replaces .NET default logger with Serilog

// ✅ 1. Configure Database Connection (Only Once)
builder.Services.AddDbContext<MyDentalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.MigrationsAssembly("mydental.infrastructure"))); // ✅ Set migration assembly

// ✅ 2. Register Application & Domain Services
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IAuthService, AuthService>();


// ✅ 3. Register FluentValidation Validators
builder.Services.AddScoped<IValidator<PatientDto>, PatientDtoValidator>();
builder.Services.AddScoped<IValidator<PatientUpdatePayloadDto>, PatientUpdatePayloadValidator>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IValidator<RegisterDto>, RegisterDtoValidator>(); // ✅ Added this
builder.Services.AddTransient<IValidator<LoginDto>, LoginDtoValidator>();
builder.Services.AddSingleton<JwtHelper>();

// ✅ 4. Add Controllers & API Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // ✅ Use the correct way to register a DateTime converter
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// ✅ 5. Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ✅ 6. Add Swagger Configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MyDental API",
        Version = "v1",
        Description = "An API for managing dental patients"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {your_token}'"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

// ✅ Read JWT settings from appsettings.json
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // ... your existing JWT config ...
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("YourVerySecureLongerSecretKey123!")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        // ✅ OVERRIDE the default 401 challenge
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                // Skip the default logic
                context.HandleResponse();

                // Return a structured JSON response
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var unauthorizedResult = ServiceResult<object>.Unauthorized(
                    "Unauthorized",
                    new List<string> { "Access token is missing or invalid." }
                );

                var json = JsonSerializer.Serialize(unauthorizedResult);
                await context.Response.WriteAsync(json);
            },
            // (Optional) You can also override OnForbidden for 403 responses
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var forbiddenResult = ServiceResult<object>.Forbidden(
                    "Forbidden",
                    new List<string> { "You do not have permission to access this resource." }
                );
                var forbiddenJson = JsonSerializer.Serialize(forbiddenResult);
                await context.Response.WriteAsync(forbiddenJson);
            }
        };
    });


// ✅ 7. Build Application
var app = builder.Build();

// ✅ Enable Serilog Request Logging
app.UseSerilogRequestLogging();

// ✅ 8. Enable Middleware for API Documentation
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyDental API v1"));
}

// ✅ 9. Enable CORS & Authorization
app.UseCors("AllowAll"); // ✅ Ensure the CORS policy exists
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();

// ✅ 10. Run Application
app.Run();
