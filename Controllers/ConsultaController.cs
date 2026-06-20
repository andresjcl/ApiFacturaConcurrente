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
public class ConsultaController : ControllerBase
{
    private readonly MasterDbContext _context;
    private readonly ConsultaService _consultaService;
    private readonly FacturaService _facturaService;

    public ConsultaController(MasterDbContext context, ConsultaService consultaService, FacturaService facturaService)
    {
        _context = context;
        _consultaService = consultaService;
        _facturaService = facturaService;
    }



    /// <summary>
    /// Obtiene los artículos de la vista ArticulosGrupos (solo alimentos - categoría ALI)
    /// </summary>
    [HttpGet("articulos")]
    public async Task<IActionResult> GetArticulos( [FromQuery] string sucursal, [FromQuery] string? codigo = null,[FromQuery] string? nombre = null,[FromQuery] string? clase = null,[FromQuery] int limite = 100)
    {
        // Validar token
        var empresaId = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(empresaId))
            return Unauthorized(new { error = "Token inválido" });

        // Validar sucursal
        var sucursalPermitida = await _context.ApiEmpresaSucursales
            .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == sucursal && s.Activo);

        if (!sucursalPermitida)
            return BadRequest(new { error = $"Sucursal {sucursal} no permitida" });

        // Obtener configuración del servidor
        var sucursalConfig = await _context.SucursalesServidores
            .FirstOrDefaultAsync(s => s.SucursalCodigo == sucursal && s.Activo);

        if (sucursalConfig == null)
            return BadRequest(new { error = $"No hay configuración para sucursal {sucursal}" });

        // Ejecutar consulta (ya no se envía categoria)
        var (success, mensaje, data, total) = await _consultaService.GetArticulosGrupos(
            sucursalConfig, codigo, nombre, clase, limite);

        if (!success)
            return StatusCode(500, new { success, mensaje });

        return Ok(new
        {
            success,
            mensaje,
            sucursal,
            total,
            limite,
            clase,
            data
        });
    }


    [HttpGet("iva-actual")]
    public async Task<IActionResult> GetIvaActual([FromQuery] string sucursal)
    {
        try
        {
            var empresaId = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(empresaId))
                return Unauthorized(new { error = "Token inválido" });

            var sucursalPermitida = await _context.ApiEmpresaSucursales
                .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == sucursal && s.Activo);

            if (!sucursalPermitida)
                return BadRequest(new { error = $"Sucursal {sucursal} no permitida" });

            var sucursalConfig = await _context.SucursalesServidores
                .FirstOrDefaultAsync(s => s.SucursalCodigo == sucursal && s.Activo);

            if (sucursalConfig == null)
                return BadRequest(new { error = $"No hay configuración para sucursal {sucursal}" });

            // Usar el método para obtener IVA con la configuración de la sucursal
            var iva = await _facturaService.ObtenerPorcentajeIva(sucursalConfig, DateTime.Now);

            return Ok(new
            {
                ivaPorcentaje = iva,
                ivaDecimal = iva / 100,
                fecha = DateTime.Now.ToString("yyyy-MM-dd"),
                sucursal = sucursal,
                empresaId = sucursalConfig.EmpresaId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

}