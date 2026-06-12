using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ApiFacturaConcurrente.Data;
using ApiFacturaConcurrente.Models.DTOs;
using System.Security.Cryptography;

namespace ApiFacturaConcurrente.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MasterDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(MasterDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Autenticación con ClientId y ClientSecret
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> GetToken([FromBody] TokenRequestDto request)
    {
        // Validar ClientId y ClientSecret
        var empresa = await _context.ApiEmpresas
            .FirstOrDefaultAsync(e => e.ClientId == request.ClientId && e.Activo);

        if (empresa == null)
            return Unauthorized(new { error = "ClientId inválido" });

        // Validar ClientSecret (comparación segura)
        if (!VerifyClientSecret(request.ClientSecret, empresa.ClientSecret))
            return Unauthorized(new { error = "ClientSecret inválido" });

        // Obtener sucursales permitidas
        var sucursales = await _context.ApiEmpresaSucursales
            .Where(s => s.EmpresaId == empresa.EmpresaId && s.Activo)
            .Select(s => s.SucursalCodigo)
            .ToListAsync();

        // Generar token JWT
        var token = GenerarToken(empresa.EmpresaId, sucursales);
        var expiresIn = _configuration.GetValue<int>("Jwt:ExpiresInMinutes") * 60;

        // Guardar token en BD
        empresa.TokenActual = token;
        empresa.TokenExpiracion = DateTime.Now.AddMinutes(_configuration.GetValue<int>("Jwt:ExpiresInMinutes"));
        await _context.SaveChangesAsync();

        return Ok(new TokenResponseDto
        {
            AccessToken = token,
            ExpiresIn = expiresIn,
            TokenType = "Bearer",
            SucursalesPermitidas = sucursales
        });
    }

    private bool VerifyClientSecret(string inputSecret, string? storedSecret)
    {
        if (string.IsNullOrEmpty(storedSecret))
            return false;

        // Comparación de strings segura (tiempo constante)
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(inputSecret),
            Encoding.UTF8.GetBytes(storedSecret));
    }

    private string GenerarToken(string empresaId, List<string> sucursales)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, empresaId),
            new Claim("sub", empresaId),
            new Claim("clientId", empresaId)
        };

        foreach (var suc in sucursales)
        {
            claims.Add(new Claim("sucursal", suc));
        }

        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            jwtKey = "qwertyuiop`+asdfghjklñ´zxcvbnm123456789012347qazxswcdevfrbgtnhymjuZAQWSXCDERFV";
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            expires: DateTime.Now.AddMinutes(_configuration.GetValue<int>("Jwt:ExpiresInMinutes")),
            signingCredentials: creds,
            claims: claims
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}