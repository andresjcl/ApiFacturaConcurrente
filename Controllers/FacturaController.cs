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

        // ==================== 1. VALIDAR SUCURSAL ====================
        var sucursalPermitida = await _context.ApiEmpresaSucursales
            .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == request.Sucursal && s.Activo);

        if (!sucursalPermitida)
            return BadRequest(new { error = $"Sucursal {request.Sucursal} no permitida" });

        // ==================== 2. OBTENER CONFIGURACIÓN DE SUCURSAL (Bodega, PuntoVta, NroIdDoc) ====================
        var sucursalConfig = await _context.SucursalesConfig
            .FirstOrDefaultAsync(s => s.SucursalCodigo == request.Sucursal && s.Activo);

        if (sucursalConfig == null)
            return BadRequest(new { error = $"No hay configuración para sucursal {request.Sucursal}" });

        // Asignar valores desde la tabla SucursalesConfig (el request NO debe enviarlos)
        request.Bodega = sucursalConfig.Bodega;
        request.PuntoVta = sucursalConfig.PuntoVta;
        request.NroIdDoc = sucursalConfig.NroIdDoc;

        // ==================== 3. OBTENER CONFIGURACIÓN DEL SERVIDOR ====================
        var sucursalServidor = await _context.SucursalesServidores
            .FirstOrDefaultAsync(s => s.SucursalCodigo == request.Sucursal && s.Activo);

        if (sucursalServidor == null)
            return BadRequest(new { error = $"No hay configuración de servidor para sucursal {request.Sucursal}" });

        // ==================== 4. VALIDAR DATOS MÍNIMOS ====================
        if (request.Lineas == null || !request.Lineas.Any())
            return BadRequest(new { error = "Debe enviar al menos una línea" });

        if (string.IsNullOrEmpty(request.CiRuc))
            return BadRequest(new { error = "Debe enviar ciRuc (cédula, RUC o pasaporte)" });

        // ==================== 5. CREAR FACTURA ====================
        var resultado = await _facturaService.CrearFactura(sucursalServidor, request, empresaId);

        if (!resultado.Success)
            return StatusCode(500, resultado);

        return Ok(resultado);
    }
}