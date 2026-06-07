using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ApiFacturaConcurrente.Data;
using ApiFacturaConcurrente.Models.DTOs;
using ApiFacturaConcurrente.Services;

namespace ApiFacturaConcurrente.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacturaController : ControllerBase
{
    private readonly MasterDbContext _context;
    private readonly FacturaService _facturaService;

    public FacturaController(MasterDbContext context, FacturaService facturaService)
    {
        _context = context;
        _facturaService = facturaService;
    }

    [HttpPost("emitir")]
    public async Task<IActionResult> EmitirFactura([FromBody] FacturaRequestDto request)
    {
        var empresaId = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(empresaId))
            return Unauthorized(new { error = "Token inválido" });

        var sucursalPermitida = await _context.ApiEmpresaSucursales
            .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == request.Sucursal && s.Activo);

        if (!sucursalPermitida)
            return BadRequest(new { error = $"Sucursal {request.Sucursal} no permitida" });

        var sucursalConfig = await _context.SucursalesServidores
            .FirstOrDefaultAsync(s => s.SucursalCodigo == request.Sucursal && s.Activo);

        if (sucursalConfig == null)
            return BadRequest(new { error = $"No hay configuración para sucursal {request.Sucursal}" });

        if (request.Lineas == null || !request.Lineas.Any())
            return BadRequest(new { error = "Debe enviar al menos una línea" });

        if (string.IsNullOrEmpty(request.Cliente?.Codigo))
            return BadRequest(new { error = "Debe enviar código de cliente" });

        var resultado = await _facturaService.CrearFactura(sucursalConfig, request, empresaId);

        if (!resultado.Success)
            return StatusCode(500, resultado);

        return Ok(resultado);
    }
}