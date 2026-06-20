using ApiFacturaConcurrente.Data;
using ApiFacturaConcurrente.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks; // ⭐ IMPORTANTE
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==================== 1. CONFIGURACIÓN BÁSICA ====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ==================== 2. LOGGING ====================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ==================== 3. SWAGGER (SOLO EN DESARROLLO) ====================
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ApiFacturaConcurrente",
            Version = "v3",
            Description = "API de facturación concurrente para ECUAVICHE S.A.",
            Contact = new OpenApiContact
            {
                Name = "Soporte",
                Email = "andresjcl@gmail.com"
            }
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Ingrese el token JWT con el formato: Bearer {token}"
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
                Array.Empty<string>()
            }
        });
    });
}

// ==================== 4. BASE DE DATOS ====================
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterConexiones")));

// ==================== 5. SERVICIOS ====================
builder.Services.AddScoped<FacturaService>();
builder.Services.AddScoped<ConsultaService>();
builder.Services.AddScoped<ImpresionService>();

// ==================== 6. JWT AUTHENTICATION ====================
var jwtKey = builder.Configuration["Jwt:Key"] ?? "MiClaveSuperSecretaDe32Caracteres1234567890";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning($"Challenge: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

// ==================== 7. RATE LIMITING ====================
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("FacturaLimit", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 20;
    });

    options.AddFixedWindowLimiter("ConsultaLimit", opt =>
    {
        opt.PermitLimit = 200;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 50;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ==================== 8. CORS ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
            "https://app.ecuaviche.com",
            "https://admin.ecuaviche.com"
        )
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

// ==================== 9. HEALTH CHECKS ====================
builder.Services.AddHealthChecks()
    .AddDbContextCheck<MasterDbContext>("Base de Datos"); // ⭐ AHORA FUNCIONA

// ==================== 10. RESPONSE COMPRESSION ====================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ==================== 11. MEMORY CACHE ====================
builder.Services.AddMemoryCache();

// ==================== CONSTRUIR APP ====================
var app = builder.Build();

// ==================== MIDDLEWARE ====================

// 1. Swagger - SOLO EN DESARROLLO
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiFacturaConcurrente v3");
        c.RoutePrefix = string.Empty;
    });
}

// 2. Redirección HTTPS (en producción)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 3. CORS
app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "Production");

// 4. Compresión
app.UseResponseCompression();

// 5. Rate Limiting
app.UseRateLimiter();

// 6. Autenticación y Autorización
app.UseAuthentication();
app.UseAuthorization();

// 7. Health Checks
app.MapHealthChecks("/api/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    AllowCachingResponses = false
});

// 8. Controladores
app.MapControllers();

// ==================== 9. LOG DE INICIO ====================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 API FacturaConcurrente iniciada exitosamente");
logger.LogInformation($"📅 Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
logger.LogInformation($"🌐 Ambiente: {app.Environment.EnvironmentName}");

if (app.Environment.IsDevelopment())
{
    logger.LogWarning("⚠️ Swagger habilitado (SOLO DESARROLLO)");
}
else
{
    logger.LogInformation("🔒 Swagger DESHABILITADO (PRODUCCIÓN)");
}

// ==================== EJECUTAR ====================
app.Run();