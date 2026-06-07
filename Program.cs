using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ApiFacturaConcurrente.Data;
using ApiFacturaConcurrente.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar límites para alta concurrencia
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 200;
    options.Limits.MaxConcurrentUpgradedConnections = 200;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();  // Ahora funciona

// DbContext para MasterDB
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterDB")));

// Servicios
builder.Services.AddScoped<FacturaService>();

// Configurar JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "MiClaveSuperSecretaDe32Caracteres1234567890";
var keyBytes = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();      
    app.UseSwaggerUI();    
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();