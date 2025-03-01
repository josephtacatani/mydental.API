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

var builder = WebApplication.CreateBuilder(args);

// ✅ 1. Configure Database Connection (Only Once)
builder.Services.AddDbContext<MyDentalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.MigrationsAssembly("mydental.infrastructure"))); // ✅ Set migration assembly

// ✅ 2. Register Application & Domain Services
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();

// ✅ 3. Register FluentValidation Validators
builder.Services.AddScoped<IValidator<PatientDto>, PatientDtoValidator>();
builder.Services.AddScoped<IValidator<PatientUpdatePayloadDto>, PatientUpdatePayloadValidator>();

// ✅ 4. Add Controllers & API Services
builder.Services.AddControllers();
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

    // ✅ Correct way to use Swagger Examples (Only if method exists)
    options.MapType<ServiceResult<IEnumerable<PatientDto>>>(() => SwaggerExamples.PatientSuccessExample());
    options.MapType<ErrorResponseDto<IEnumerable<PatientDto>>>(() => SwaggerExamples.PatientNotFoundExample());

});

// ✅ 7. Build Application
var app = builder.Build();

// ✅ 8. Enable Middleware for API Documentation
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyDental API v1"));
}

// ✅ 9. Enable CORS & Authorization
app.UseCors("AllowAll"); // ✅ Ensure the CORS policy exists
app.UseAuthorization();
app.MapControllers();

// ✅ 10. Run Application
app.Run();
