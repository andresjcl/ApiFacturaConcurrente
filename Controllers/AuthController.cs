using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ApiFacturaConcurrente.Data;
using ApiFacturaConcurrente.Models.DTOs;

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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var empresa = await _context.ApiEmpresas
            .FirstOrDefaultAsync(e => e.EmpresaId == request.Username && e.Activo);

        if (empresa == null)
        {
            return Unauthorized(new { error = "Usuario inválido" });
        }

        // Verificar contraseña con BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, empresa.EmpresaSecret))
        {
            return Unauthorized(new { error = "Contraseña incorrecta" });
        }

        var sucursales = await _context.ApiEmpresaSucursales
            .Where(s => s.EmpresaId == empresa.EmpresaId && s.Activo)
            .Select(s => s.SucursalCodigo)
            .ToListAsync();

        var token = GenerarToken(empresa.EmpresaId, sucursales);
        var expiresIn = _configuration.GetValue<int>("Jwt:ExpiresInMinutes") * 60;

        return Ok(new LoginResponseDto
        {
            AccessToken = token,
            ExpiresIn = expiresIn,
            TokenType = "Bearer",
            SucursalesPermitidas = sucursales
        });
    }

    // Endpoint temporal para generar hash (después lo eliminas)
    //[HttpGet("generar-hash")]
    //public IActionResult GenerarHash()
    //{
    //    string password = "123456";
    //    string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    //    return Ok(new { password, hash });
    //}

    private string GenerarToken(string empresaId, List<string> sucursales)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, empresaId),
            new Claim("sub", empresaId)
        };

        foreach (var suc in sucursales)
        {
            claims.Add(new Claim("sucursal", suc));
        }

        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new InvalidOperationException("JWT Key no está configurada");
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