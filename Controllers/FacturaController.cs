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

        // Validar que la sucursal esté permitida
        var sucursalPermitida = await _context.ApiEmpresaSucursales
            .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == request.Sucursal && s.Activo);

        if (!sucursalPermitida)
            return BadRequest(new { error = $"Sucursal {request.Sucursal} no permitida" });

        // Obtener configuración de la sucursal (Bodega, PuntoVta, NroIdDoc)
        var sucursalConfigData = await _context.SucursalesConfig
            .FirstOrDefaultAsync(s => s.SucursalCodigo == request.Sucursal && s.Activo);

        if (sucursalConfigData == null)
            return BadRequest(new { error = $"No hay configuración para sucursal {request.Sucursal}" });

        // Asignar valores automáticos si no vienen en el request
        if (string.IsNullOrEmpty(request.Bodega))
            request.Bodega = sucursalConfigData.Bodega;

        if (string.IsNullOrEmpty(request.PuntoVta))
            request.PuntoVta = sucursalConfigData.PuntoVta;

        if (string.IsNullOrEmpty(request.NroIdDoc))
            request.NroIdDoc = sucursalConfigData.NroIdDoc;

        // Obtener configuración del servidor
        var sucursalServidor = await _context.SucursalesServidores
            .FirstOrDefaultAsync(s => s.SucursalCodigo == request.Sucursal && s.Activo);

        if (sucursalServidor == null)
            return BadRequest(new { error = $"No hay configuración de servidor para sucursal {request.Sucursal}" });

        // Validar datos mínimos
        if (request.Lineas == null || !request.Lineas.Any())
            return BadRequest(new { error = "Debe enviar al menos una línea" });

        // La validación del cliente se hace con ciRuc
        if (string.IsNullOrEmpty(request.CiRuc))
            return BadRequest(new { error = "Debe enviar ciRuc (cédula, RUC o pasaporte)" });

        // Crear factura
        var resultado = await _facturaService.CrearFactura(sucursalServidor, request, empresaId);

        if (!resultado.Success)
            return StatusCode(500, resultado);

        return Ok(resultado);
    }
}