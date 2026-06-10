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

    public ConsultaController(MasterDbContext context, ConsultaService consultaService)
    {
        _context = context;
        _consultaService = consultaService;
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




    /// <summary>
    /// Obtiene las facturas de una sucursal
    /// </summary>
    //[HttpGet("facturas")]
    //public async Task<IActionResult> GetFacturas([FromQuery] string sucursal,[FromQuery] DateTime? desde = null,[FromQuery] DateTime? hasta = null,[FromQuery] int limite = 100)
    //{
    //    var empresaId = User.FindFirst(ClaimTypes.Name)?.Value;
    //    if (string.IsNullOrEmpty(empresaId))
    //        return Unauthorized(new { error = "Token inválido" });

    //    var sucursalPermitida = await _context.ApiEmpresaSucursales
    //        .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == sucursal && s.Activo);

    //    if (!sucursalPermitida)
    //        return BadRequest(new { error = $"Sucursal {sucursal} no permitida" });

    //    var sucursalConfig = await _context.SucursalesServidores
    //        .FirstOrDefaultAsync(s => s.SucursalCodigo == sucursal && s.Activo);

    //    if (sucursalConfig == null)
    //        return BadRequest(new { error = $"No hay configuración para sucursal {sucursal}" });

    //    var resultado = await _consultaService.GetFacturas(sucursalConfig, sucursal, desde, hasta, limite);

    //    if (!resultado.success)
    //        return StatusCode(500, new { success = false, mensaje = resultado.mensaje });

    //    return Ok(new
    //    {
    //        success = true,
    //        sucursal,
    //        total = resultado.total,
    //        limite,
    //        desde = desde?.ToString("yyyy-MM-dd"),
    //        hasta = hasta?.ToString("yyyy-MM-dd"),
    //        data = resultado.data
    //    });
    //}

    /// <summary>
    /// Obtiene los clientes de una sucursal
    ///// </summary>
    //[HttpGet("clientes")]
    //public async Task<IActionResult> GetClientes([FromQuery] string sucursal,[FromQuery] string? codigo = null,[FromQuery] string? nombre = null,[FromQuery] int limite = 100)
    //{
    //    var empresaId = User.FindFirst(ClaimTypes.Name)?.Value;
    //    if (string.IsNullOrEmpty(empresaId))
    //        return Unauthorized(new { error = "Token inválido" });

    //    var sucursalPermitida = await _context.ApiEmpresaSucursales
    //        .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == sucursal && s.Activo);

    //    if (!sucursalPermitida)
    //        return BadRequest(new { error = $"Sucursal {sucursal} no permitida" });

    //    var sucursalConfig = await _context.SucursalesServidores
    //        .FirstOrDefaultAsync(s => s.SucursalCodigo == sucursal && s.Activo);

    //    if (sucursalConfig == null)
    //        return BadRequest(new { error = $"No hay configuración para sucursal {sucursal}" });

    //    var resultado = await _consultaService.GetClientes(sucursalConfig, codigo, nombre, limite);

    //    if (!resultado.success)
    //        return StatusCode(500, new { success = false, mensaje = resultado.mensaje });

    //    return Ok(new
    //    {
    //        success = true,
    //        sucursal,
    //        total = resultado.total,
    //        limite,
    //        data = resultado.data
    //    });
    //}

    ///// <summary>
    ///// Obtiene las líneas de una factura específica
    ///// </summary>
    //[HttpGet("factura/{docNumero}")]
    //public async Task<IActionResult> GetFacturaDetalle( [FromQuery] string sucursal,[FromQuery] decimal docNumero)
    //{
    //    var empresaId = User.FindFirst(ClaimTypes.Name)?.Value;
    //    if (string.IsNullOrEmpty(empresaId))
    //        return Unauthorized(new { error = "Token inválido" });

    //    var sucursalPermitida = await _context.ApiEmpresaSucursales
    //        .AnyAsync(s => s.EmpresaId == empresaId && s.SucursalCodigo == sucursal && s.Activo);

    //    if (!sucursalPermitida)
    //        return BadRequest(new { error = $"Sucursal {sucursal} no permitida" });

    //    var sucursalConfig = await _context.SucursalesServidores
    //        .FirstOrDefaultAsync(s => s.SucursalCodigo == sucursal && s.Activo);

    //    if (sucursalConfig == null)
    //        return BadRequest(new { error = $"No hay configuración para sucursal {sucursal}" });

    //    var resultado = await _consultaService.GetFacturaDetalle(sucursalConfig, sucursal, docNumero);

    //    if (!resultado.success)
    //        return StatusCode(500, new { success = false, mensaje = resultado.mensaje });

    //    return Ok(new
    //    {
    //        success = true,
    //        sucursal,
    //        docNumero,
    //        cabecera = resultado.cabecera,
    //        lineas = resultado.lineas,
    //        totalLineas = resultado.totalLineas
    //    });
    //}
}