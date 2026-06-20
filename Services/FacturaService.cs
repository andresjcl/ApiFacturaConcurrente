using ApiFacturaConcurrente.Data;
using ApiFacturaConcurrente.Models;
using ApiFacturaConcurrente.Models.DTOs;
using ApiFacturaConcurrente.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ApiFacturaConcurrente.Services;

public class FacturaService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
    private readonly MasterDbContext _context;
    private readonly ImpresionService _impresionService;
    private readonly IConfiguration _configuration;

    public FacturaService(MasterDbContext context, ImpresionService impresionService, IConfiguration configuration)
    {
        _context = context;
        _impresionService = impresionService;
        _configuration = configuration;
    }

    public async Task<FacturaResponseDto> CrearFactura(SucursalServidor sucursalConfig, FacturaRequestDto request, string empresaId)
    {
        var response = new FacturaResponseDto { Success = false };
        var semaphore = _semaphores.GetOrAdd(sucursalConfig.SucursalCodigo, new SemaphoreSlim(5, 5));

        if (!await semaphore.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            throw new Exception("Tiempo de espera agotado. Intente nuevamente.");
        }

        await semaphore.WaitAsync();

        SqlConnection connection = null;
        SqlTransaction transaction = null;

        try
        {
            connection = new SqlConnection(sucursalConfig.ConnectionString);
            await connection.OpenAsync();
            transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            // Calcular totales si no vienen
            if (request.ValorTotal == 0)
            {
                var (totCiva, valorIva, valorTotal, porcenIvaUsado) = await CalcularTotalesAsync(connection, transaction, sucursalConfig, request);
                request.TotCiva = totCiva;
                request.ValorIva = valorIva;
                request.ValorTotal = valorTotal;
                request.PorcenIva = porcenIvaUsado;
            }

            // 1. VALIDAR O CREAR CLIENTE
            if (string.IsNullOrEmpty(request.CiRuc))
                throw new Exception("El campo ciRuc es obligatorio");

            var codigoCliente = await ObtenerOInsertarCliente(connection, transaction,
                request.CiRuc, request.NombreCliente ?? "", request.Direccion, request.Telefono1, request.CorreoCliente);
            request.CodigoCliente = codigoCliente;

            // 2. GENERAR NUMERO DE FACTURA
            string idLugar = $"{request.Sucursal}{request.NroIdDoc ?? "001-001"}";
            var docNumero = await ObtenerSiguienteNumero(connection, transaction, idLugar);
            var idClaveDoc = await ObtenerSiguienteIdClaveDoc(connection, transaction);

            // 3. INSERTAR CABECERA
            await InsertarCabecera(connection, transaction, request, docNumero, idClaveDoc, empresaId);

            // 4. INSERTAR LINEAS
            await InsertarLineas(connection, transaction, request, docNumero, idClaveDoc, sucursalConfig);

            // 5. INSERTAR PAGOS
            if (request.Pagos != null && request.Pagos.Any())
                await InsertarPagos(connection, transaction, request, docNumero, idClaveDoc);

            await transaction.CommitAsync();

            // ==================== IMPRIMIR (FIRE AND FORGET) ====================
            // La impresión se ejecuta en background sin bloquear la respuesta
            _ = Task.Run(async () =>
            {
                try
                {
                    var (cabecera, lineas, empresa) = await ConsultarFacturaParaImprimir(
                        connection, idClaveDoc, request.Sucursal, "FAC", docNumero);

                    bool impreso = await _impresionService.ImprimirFactura(request.Sucursal, cabecera, lineas, empresa);

                    if (impreso)
                        Console.WriteLine($"✅ Factura {docNumero} impresa correctamente");
                    else
                        Console.WriteLine($"⚠️ Factura {docNumero} NO se imprimió");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al imprimir: {ex.Message}");
                }
            });

            response.Success = true;
            response.Sucursal = request.Sucursal;
            response.DocNumero = docNumero;
            response.IdClaveDoc = idClaveDoc;
            response.Total = request.ValorTotal;
            response.Mensaje = "Factura creada exitosamente";
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync();
            response.Mensaje = $"Error: {ex.Message}";
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
            if (connection != null) await connection.DisposeAsync();
            semaphore.Release();
        }
        return response;
    }

    private async Task InsertarLineas(SqlConnection connection, SqlTransaction transaction, FacturaRequestDto request, decimal docNumero, decimal idClaveDoc, SucursalServidor sucursalConfig)
    {
        int anio = DateTime.Now.Year, mes = DateTime.Now.Month, dia = DateTime.Now.Day;
        int numLinea = 1;
        decimal totalFactura = request.ValorTotal;

        decimal ivaGeneral = await ObtenerPorcentajeIva(sucursalConfig, request.Fecha);

        foreach (var linea in request.Lineas)
        {
            var producto = await ValidarProducto(connection, transaction, linea.Codigo);
            if (!producto.existe)
                throw new Exception($"Producto {linea.Codigo} no encontrado");

            string nombreCompleto = producto.nombre;
            if (linea.ModificadoresTexto != null && linea.ModificadoresTexto.Any())
                nombreCompleto += " + " + string.Join(" + ", linea.ModificadoresTexto);

            decimal subtotal = linea.Cantidad * linea.PrecioUnitario;

            decimal descuentoValor = 0;
            if (linea.DescuentoPorcentaje > 0)
                descuentoValor = subtotal * (linea.DescuentoPorcentaje / 100);
            else if (linea.DescuentoValor > 0)
                descuentoValor = linea.DescuentoValor;

            decimal subtotalConDescuento = subtotal - descuentoValor;

            decimal ivaPorcentaje = producto.tieneIva ? (producto.porcentajeIva > 0 ? producto.porcentajeIva : ivaGeneral) : 0;

            decimal ivaValor = subtotalConDescuento * (ivaPorcentaje / 100);
            decimal precioTotal = subtotalConDescuento + ivaValor;

            await InsertarTraLinea(connection, transaction, request, docNumero, idClaveDoc,
                numLinea++, linea.Codigo, nombreCompleto, linea.Cantidad,
                linea.PrecioUnitario, precioTotal, ivaPorcentaje, ivaValor,
                totalFactura, anio, mes, dia, "A", -1, 1, 0,
                linea.DescuentoMotivo, linea.DescuentoPorcentaje, descuentoValor);

            foreach (var agg in linea.AgregadoresProducto)
            {
                var aggProducto = await ValidarProducto(connection, transaction, agg.Codigo);
                if (!aggProducto.existe)
                    throw new Exception($"Agregador {agg.Codigo} no encontrado");

                string nombreAgregador = agg.Nombre;
                if (agg.ModificadoresTexto != null && agg.ModificadoresTexto.Any())
                    nombreAgregador += " + " + string.Join(" + ", agg.ModificadoresTexto);

                decimal subtotalAgg = agg.Cantidad * agg.PrecioUnitario;

                decimal ivaPorcentajeAgg = aggProducto.tieneIva ? (aggProducto.porcentajeIva > 0 ? aggProducto.porcentajeIva : ivaGeneral) : 0;

                decimal ivaValorAgg = subtotalAgg * (ivaPorcentajeAgg / 100);
                decimal precioTotalAgg = subtotalAgg + ivaValorAgg;

                await InsertarTraLinea(connection, transaction, request, docNumero, idClaveDoc,
                    numLinea++, agg.Codigo, nombreAgregador, agg.Cantidad,
                    agg.PrecioUnitario, precioTotalAgg, ivaPorcentajeAgg, ivaValorAgg,
                    totalFactura, anio, mes, dia, "A", -1, 1, 0,
                    null, 0, 0);
            }
        }

        foreach (var agg in request.AgregadoresPedido)
        {
            var servicio = await ValidarServicio(connection, transaction, agg.Codigo);
            string quetipo = servicio.existe ? "S" : "A";
            int inventario = servicio.existe ? 0 : -1;

            decimal subtotal = agg.Cantidad * agg.PrecioUnitario;

            decimal ivaPorcentaje = 0;
            if (servicio.existe && servicio.tieneIva)
                ivaPorcentaje = servicio.porcentajeIva > 0 ? servicio.porcentajeIva : ivaGeneral;
            else if (!servicio.existe && agg.Iva > 0)
                ivaPorcentaje = agg.Iva;

            decimal ivaValor = subtotal * (ivaPorcentaje / 100);
            decimal total = subtotal + ivaValor;
            string nombre = servicio.existe ? servicio.nombre : agg.Nombre;

            await InsertarTraLinea(connection, transaction, request, docNumero, idClaveDoc,
                numLinea++, agg.Codigo, nombre, agg.Cantidad, agg.PrecioUnitario, total,
                ivaPorcentaje, ivaValor, totalFactura, anio, mes, dia,
                quetipo, inventario, 1, 0, null, 0, 0);
        }
    }

    private async Task InsertarTraLinea(SqlConnection connection, SqlTransaction transaction, FacturaRequestDto request, decimal docNumero, decimal idClaveDoc, int numLinea, string codigo, string nombre, decimal cantidad, decimal precioUnitario, decimal precioTotal, decimal ivaPorcentaje, decimal ivaValor, decimal totalFactura, int anio, int mes, int dia, string quetipo, int inventario, int ventas, int compras, string descuentoMotivo, decimal descuentoPorcentaje, decimal descuentoValor)
    {
        decimal traPrectot = Math.Round(cantidad * precioUnitario, 4);
        decimal precUniRedondeado = Math.Round(precioUnitario, 4);
        decimal totalFacturaRedondeado = Math.Round(totalFactura, 2);
        decimal ivaValorRedondeado = Math.Round(ivaValor, 2);
        decimal descuentoValorRedondeado = Math.Round(descuentoValor, 2);

        var sql = @"
        INSERT INTO AdcTra (
            Doc_sucursal, Doc_Bodega, Opc_documento, Doc_numero, IdClaveDoc,
            Tra_numlinea, Tra_Codigo, Tra_nombre, Tra_cantidad, Tra_valor,
            Tra_precuni, Tra_prectot, Tra_fecha, Tra_TipoDoc, Tra_Estado,
            Tra_Inventario, Tra_Ventas, Tra_Compras, Tra_sniva, Tra_Individual, 
            Tra_quetipo, Tra_medida, Tra_multiplo, Tra_piezas, Tra_porceniva, Tra_valoriva,
            Tra_numprecio, Tra_costuni, Tra_costtot, Tra_Oculto, Tra_Activo, 
            tra_producto, Tra_Despachado, tra_anio, tra_mes, tra_dia,
            Tra_descdes, Tra_porcendes, Tra_valordes
        ) VALUES (
            @sucursal, @bodega, 'FAC', @docNumero, @idClaveDoc,
            @numLinea, @codigo, @nombre, @cantidad, @totalFactura,
            @precUni, @traPrectot, @fecha, 'FAC', 1,
            @inventario, @ventas, @compras, 1, 'N', 
            @quetipo, 'und', 1, 0, @porcenIva, @valorIva,
            2, 0, 0, 0, 0,
            0, '', @anio, @mes, @dia,
            @descuentoMotivo, @descuentoPorcentaje, @descuentoValor
        )";

        using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("@sucursal", request.Sucursal);
        cmd.Parameters.AddWithValue("@bodega", request.Bodega ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@docNumero", docNumero);
        cmd.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
        cmd.Parameters.AddWithValue("@numLinea", numLinea);
        cmd.Parameters.AddWithValue("@codigo", codigo);
        cmd.Parameters.AddWithValue("@nombre", nombre);
        cmd.Parameters.AddWithValue("@cantidad", cantidad);
        cmd.Parameters.AddWithValue("@totalFactura", totalFacturaRedondeado);
        cmd.Parameters.AddWithValue("@precUni", precUniRedondeado);
        cmd.Parameters.AddWithValue("@traPrectot", traPrectot);
        cmd.Parameters.AddWithValue("@fecha", request.Fecha);
        cmd.Parameters.AddWithValue("@inventario", inventario);
        cmd.Parameters.AddWithValue("@ventas", ventas);
        cmd.Parameters.AddWithValue("@compras", compras);
        cmd.Parameters.AddWithValue("@quetipo", quetipo);
        cmd.Parameters.AddWithValue("@porcenIva", ivaPorcentaje);
        cmd.Parameters.AddWithValue("@valorIva", ivaValorRedondeado);
        cmd.Parameters.AddWithValue("@anio", anio);
        cmd.Parameters.AddWithValue("@mes", mes);
        cmd.Parameters.AddWithValue("@dia", dia);
        cmd.Parameters.AddWithValue("@descuentoMotivo", descuentoMotivo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@descuentoPorcentaje", descuentoPorcentaje);
        cmd.Parameters.AddWithValue("@descuentoValor", descuentoValorRedondeado);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertarCabecera(SqlConnection connection, SqlTransaction transaction, FacturaRequestDto request, decimal docNumero, decimal idClaveDoc, string empresaId)
    {
        decimal descuentoValorCalculado = 0;
        decimal subtotalGeneral = request.TotCiva + request.TotSiva;

        if (request.DescuentoPorcentaje > 0)
            descuentoValorCalculado = subtotalGeneral * (request.DescuentoPorcentaje / 100);
        else if (request.DescuentoValor > 0)
            descuentoValorCalculado = request.DescuentoValor;

        decimal totCivaRedondeado = Math.Round(request.TotCiva, 2);
        decimal valorIvaRedondeado = Math.Round(request.ValorIva, 2);
        decimal valorTotalRedondeado = Math.Round(request.ValorTotal, 2);
        decimal descuentoValorCalculadoRedondeado = Math.Round(descuentoValorCalculado, 2);

        var sql = @"
        INSERT INTO AdcDoc (
            Doc_sucursal, Doc_Bodega, Opc_documento, Doc_numero, IdClaveDoc,
            Doc_fecha, Doc_Hora, Doc_codper, Doc_codusu, Doc_porceniva, 
            Doc_valoriva, Doc_totciva, Doc_totsiva, Doc_valor, Doc_valorabon, Doc_detalle,
            Doc_NombreImp, Doc_CiRuc, Doc_Direccion, Doc_Telefono1, Doc_Telefono2,
            Doc_NroIdDoc, PuntoVta, AuxVar1, Doc_Estado, Doc_FecGraba,
            Doc_TipoDoc, Doc_Contado, Doc_Contabilidad, Doc_Inventario, Doc_Ventas,
            Doc_docnombre, BaseImp1, PorcImp1, AuxNum1,
            Doc_nombredes1, Doc_porcendes1, Doc_valordes1
        ) VALUES (
            @sucursal, @bodega, 'FAC', @docNumero, @idClaveDoc,
            @fecha, GETDATE(), @codCliente, 'API', @porcenIva, 
            @valorIva, @totCiva, @totSiva, @valorTotal, 0, @detalle,
            @nombreCliente, @ciRuc, @direccion, @telefono1, @telefono2,
            @nroIdDoc, @puntoVta, @empresaId, 1, GETDATE(),
            'FAC', 0, 1, -1, 1,
            'Factura cliente', @totCiva, @porcenIva, 1,
            @descuentoMotivo, @descuentoPorcentaje, @descuentoValorCalculado
        )";

        using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("@sucursal", request.Sucursal);
        cmd.Parameters.AddWithValue("@bodega", request.Bodega ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@docNumero", docNumero);
        cmd.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
        cmd.Parameters.AddWithValue("@fecha", request.Fecha);
        cmd.Parameters.AddWithValue("@codCliente", request.CodigoCliente ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@porcenIva", request.PorcenIva);
        cmd.Parameters.AddWithValue("@valorIva", valorIvaRedondeado);
        cmd.Parameters.AddWithValue("@totCiva", totCivaRedondeado);
        cmd.Parameters.AddWithValue("@totSiva", request.TotSiva);
        cmd.Parameters.AddWithValue("@valorTotal", valorTotalRedondeado);
        cmd.Parameters.AddWithValue("@detalle", request.Detalle ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@nombreCliente", request.NombreCliente ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ciRuc", request.CiRuc ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@direccion", request.Direccion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@telefono1", request.Telefono1 ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@telefono2", request.Telefono2 ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@nroIdDoc", request.NroIdDoc ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@puntoVta", request.PuntoVta ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@empresaId", empresaId);
        cmd.Parameters.AddWithValue("@descuentoMotivo", request.DescuentoMotivo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@descuentoPorcentaje", request.DescuentoPorcentaje);
        cmd.Parameters.AddWithValue("@descuentoValorCalculado", descuentoValorCalculadoRedondeado);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertarPagos(SqlConnection connection, SqlTransaction transaction, FacturaRequestDto request, decimal docNumero, decimal idClaveDoc)
    {
        decimal totalFactura = request.ValorTotal;

        var sql = @"
        INSERT INTO AdcPag (
            Doc_sucursal, Opc_documento, Doc_numero, IdClaveDoc, Pag_Numero,
            Pag_Valor, Pag_TipoPago, Pag_Descripcion, Pag_Idpago, Pag_Formapago,
            Pag_Autoriza, Doc_Fecha, Pag_Cuotas
        ) VALUES (
            @sucursal, 'FAC', @docNumero, @idClaveDoc, @numPago,
            @totalFactura, '4', @descripcion, @idPago, 2,
            1, GETDATE(), 0
        )";

        for (int i = 0; i < request.Pagos.Count; i++)
        {
            var pago = request.Pagos[i];
            string idPago = pago.IdPago ?? "GLO";
            string descripcion = pago.Descripcion ?? (idPago switch
            {
                "GLO" => "GLOVO",
                "RAP" => "RAPPI",
                "UBE" => "UBER",
                _ => "PAGO CON TARJETA"
            });

            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@sucursal", request.Sucursal);
            cmd.Parameters.AddWithValue("@docNumero", docNumero);
            cmd.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
            cmd.Parameters.AddWithValue("@numPago", i + 1);
            cmd.Parameters.AddWithValue("@totalFactura", Math.Round(totalFactura, 2));
            cmd.Parameters.AddWithValue("@descripcion", descripcion);
            cmd.Parameters.AddWithValue("@idPago", idPago);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<decimal> ObtenerSiguienteNumero(SqlConnection connection, SqlTransaction transaction, string idLugar)
    {
        string sqlUpdate = @"
            UPDATE AdcDocNum 
            SET UltimoNumero = UltimoNumero + 1, UltimaFecha = GETDATE()
            WHERE Id_Lugar = @idLugar AND id_Documento = 'FAC'
            SELECT UltimoNumero FROM AdcDocNum WHERE Id_Lugar = @idLugar AND id_Documento = 'FAC'";

        using var cmd = new SqlCommand(sqlUpdate, connection, transaction);
        cmd.Parameters.AddWithValue("@idLugar", idLugar);
        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            string sqlInsert = @"
                INSERT INTO AdcDocNum (Id_Lugar, id_Documento, UltimoNumero, UltimaFecha)
                VALUES (@idLugar, 'FAC', 1, GETDATE())
                SELECT 1";
            using var cmdInsert = new SqlCommand(sqlInsert, connection, transaction);
            cmdInsert.Parameters.AddWithValue("@idLugar", idLugar);
            result = await cmdInsert.ExecuteScalarAsync();
        }
        return Convert.ToDecimal(result);
    }

    private async Task<decimal> ObtenerSiguienteIdClaveDoc(SqlConnection connection, SqlTransaction transaction)
    {
        using var cmd = new SqlCommand("SELECT ISNULL(MAX(IdClaveDoc), 0) + 1 FROM AdcDoc", connection, transaction);
        return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
    }

    private async Task<(bool existe, string nombre, bool tieneIva, decimal precio, decimal porcentajeIva, bool sncomp, string artClase)> ValidarProducto(SqlConnection connection, SqlTransaction transaction, string codigoProducto)
    {
        try
        {
            using var cmd = new SqlCommand(@"
            SELECT Art_nombre, Art_sniva, Art_precvta2, Art_sncomp, Art_clase, Art_PorIVA
            FROM ADCART WHERE Art_codigo = @codigo", connection, transaction);
            cmd.Parameters.AddWithValue("@codigo", codigoProducto);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                decimal porcentajeIva = reader["Art_PorIVA"] != DBNull.Value ? Convert.ToDecimal(reader["Art_PorIVA"]) : 0;
                bool tieneIva = reader["Art_sniva"] != DBNull.Value && Convert.ToInt32(reader["Art_sniva"]) == 1;

                return (
                    true,
                    reader["Art_nombre"]?.ToString() ?? "",
                    tieneIva,
                    reader["Art_precvta2"] != DBNull.Value ? Convert.ToDecimal(reader["Art_precvta2"]) : 0,
                    porcentajeIva,
                    reader["Art_sncomp"] != DBNull.Value && Convert.ToInt32(reader["Art_sncomp"]) == 1,
                    reader["Art_clase"]?.ToString() ?? ""
                );
            }
            return (false, "", false, 0, 0, false, "");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al validar producto {codigoProducto}: {ex.Message}");
        }
    }

    private async Task<(bool existe, string nombre, decimal precio, bool tieneIva, decimal porcentajeIva)> ValidarServicio(SqlConnection connection, SqlTransaction transaction, string codigoServicio)
    {
        try
        {
            using var cmd = new SqlCommand(@"
            SELECT Sev_nombre, Sev_precvta, Sev_sniva, Sev_PorIVA
            FROM AdcServ WHERE Sev_codigo = @codigo AND Sev_ventas = 1", connection, transaction);
            cmd.Parameters.AddWithValue("@codigo", codigoServicio);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                decimal porcentajeIva = reader["Sev_PorIVA"] != DBNull.Value ? Convert.ToDecimal(reader["Sev_PorIVA"]) : 0;
                bool tieneIva = reader["Sev_sniva"] != DBNull.Value && Convert.ToInt32(reader["Sev_sniva"]) == 1;

                return (
                    true,
                    reader["Sev_nombre"]?.ToString() ?? "",
                    reader["Sev_precvta"] != DBNull.Value ? Convert.ToDecimal(reader["Sev_precvta"]) : 0,
                    tieneIva,
                    porcentajeIva
                );
            }
            return (false, "", 0, false, 0);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al validar servicio {codigoServicio}: {ex.Message}");
        }
    }

    private (string tipoIdentificacion, string tipoPersona) DeterminarTipoIdentificacion(string ciRuc)
    {
        if (string.IsNullOrEmpty(ciRuc)) return ("C", "N");
        string limpio = new string(ciRuc.Where(char.IsDigit).ToArray());
        if (limpio.Length == 10) return ("C", "N");
        if (limpio.Length == 13) return ("R", "J");
        return ("P", "N");
    }

    private string ObtenerCodigoCliente(string ciRuc)
    {
        if (string.IsNullOrEmpty(ciRuc)) return ciRuc;
        if (ciRuc.Any(char.IsLetter))
            return ciRuc.Length > 15 ? ciRuc.Substring(0, 15) : ciRuc;
        string soloDigitos = new string(ciRuc.Where(char.IsDigit).ToArray());
        return soloDigitos.Length >= 10 ? soloDigitos.Substring(0, 10) : soloDigitos;
    }

    private async Task<string> ObtenerOInsertarCliente(SqlConnection connection, SqlTransaction transaction, string ciRuc, string nombres, string domicilio, string telefono1, string correo)
    {
        string codigoCliente = ObtenerCodigoCliente(ciRuc);
        var (tipoIdentificacion, tipoPersona) = DeterminarTipoIdentificacion(ciRuc);

        using var cmdValidar = new SqlCommand(@"
            SELECT Codigo FROM Identificacion 
            WHERE CedulaIdentidadRuc = @cedulaRuc OR Codigo = @codigo", connection, transaction);
        cmdValidar.Parameters.AddWithValue("@cedulaRuc", ciRuc);
        cmdValidar.Parameters.AddWithValue("@codigo", codigoCliente);
        var existe = await cmdValidar.ExecuteScalarAsync();
        if (existe != null && existe != DBNull.Value)
            return existe.ToString();

        string sqlInsert = @"
            INSERT INTO Identificacion (
                TipoPersona, EsCliente, EsProveedor, EsEmpleado, EsBanco, EsAsociado, EsVendedor,
                Codigo, TipoIdentificacion, CedulaIdentidadRuc, Nombres, Apellidos, NombreImpresion,
                Telefono1, CorreoElectrónico, CodGrabo, ComisionVenta, ExoneradoIva, EsDirecta,
                Grupo1, Grupo2, Grupo3, esRise, ObligLlevarConta, RegimenMicroempresas
            ) VALUES (
                @tipoPersona, 1, 0, 0, 0, 0, 0,
                @codigo, @tipoIdentificacion, @cedulaRuc, @nombres, '', @nombres,
                @telefono1, @correo, 'API', 0.00, 0, 'NO',
                'CLIENTE', '', '', 0, 0, 0
            )";

        using var cmdInsert = new SqlCommand(sqlInsert, connection, transaction);
        cmdInsert.Parameters.AddWithValue("@tipoPersona", tipoPersona);
        cmdInsert.Parameters.AddWithValue("@codigo", codigoCliente);
        cmdInsert.Parameters.AddWithValue("@tipoIdentificacion", tipoIdentificacion);
        cmdInsert.Parameters.AddWithValue("@cedulaRuc", ciRuc);
        cmdInsert.Parameters.AddWithValue("@nombres", nombres);
        cmdInsert.Parameters.AddWithValue("@telefono1", telefono1 ?? (object)DBNull.Value);
        cmdInsert.Parameters.AddWithValue("@correo", correo ?? (object)DBNull.Value);

        await cmdInsert.ExecuteNonQueryAsync();
        return codigoCliente;
    }

    private (decimal totCiva, decimal valorIva, decimal valorTotal) CalcularTotales(FacturaRequestDto request)
    {
        decimal totalBaseIva = 0;
        decimal totalBaseSinIva = 0;
        decimal totalIva = 0;

        foreach (var linea in request.Lineas)
        {
            decimal subtotal = linea.Cantidad * linea.PrecioUnitario;

            if (linea.DescuentoPorcentaje > 0)
                subtotal -= subtotal * (linea.DescuentoPorcentaje / 100);
            else if (linea.DescuentoValor > 0)
                subtotal -= linea.DescuentoValor;

            decimal ivaPorcentaje = linea.Iva > 0 ? linea.Iva : request.PorcenIva;

            if (ivaPorcentaje > 0)
            {
                totalBaseIva += subtotal;
                totalIva += subtotal * (ivaPorcentaje / 100);
            }
            else
            {
                totalBaseSinIva += subtotal;
            }
        }

        foreach (var agg in request.AgregadoresPedido)
        {
            decimal subtotal = agg.Cantidad * agg.PrecioUnitario;
            if (agg.AfectaBaseImponible)
            {
                totalBaseIva += subtotal;
                totalIva += subtotal * (agg.Iva / 100);
            }
            else
            {
                totalBaseSinIva += subtotal;
            }
        }

        decimal totalGeneral = totalBaseIva + totalBaseSinIva + totalIva;

        if (request.DescuentoPorcentaje > 0)
        {
            decimal descuento = totalGeneral * (request.DescuentoPorcentaje / 100);
            totalGeneral -= descuento;
        }
        else if (request.DescuentoValor > 0)
        {
            totalGeneral -= request.DescuentoValor;
        }

        return (totalBaseIva, totalIva, totalGeneral);
    }

    private async Task<(ImpresionCabeceraDto cabecera, List<ImpresionLineaDto> lineas, ImpresionEmpresaDto empresa)> ConsultarFacturaParaImprimir(SqlConnection connection, decimal idClaveDoc, string sucursal, string opcDocumento, decimal docNumero)
    {
        ImpresionCabeceraDto cabecera = null;
        ImpresionEmpresaDto empresa = null;
        var lineas = new List<ImpresionLineaDto>();

        // 1. Consultar cabecera
        string sqlCabecera = @"
        SELECT Doc_numero, Doc_sucursal, Opc_documento, Doc_fecha, Doc_NombreImp, Doc_CiRuc, 
               Doc_valor, Doc_totciva, Doc_valoriva, Doc_porceniva, Doc_Direccion,
               Doc_porcendes1, Doc_valordes1, Doc_NroIdDoc, IdClaveDoc
        FROM AdcDoc 
        WHERE IdClaveDoc = @idClaveDoc 
          AND Doc_sucursal = @sucursal 
          AND Opc_documento = @opcDocumento 
          AND Doc_numero = @docNumero";

        using (var cmdCabecera = new SqlCommand(sqlCabecera, connection))
        {
            cmdCabecera.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
            cmdCabecera.Parameters.AddWithValue("@sucursal", sucursal);
            cmdCabecera.Parameters.AddWithValue("@opcDocumento", opcDocumento);
            cmdCabecera.Parameters.AddWithValue("@docNumero", docNumero);

            using (var readerCabecera = await cmdCabecera.ExecuteReaderAsync())
            {
                if (await readerCabecera.ReadAsync())
                {
                    cabecera = new ImpresionCabeceraDto
                    {
                        Doc_numero = Convert.ToDecimal(readerCabecera["Doc_numero"]),
                        Doc_sucursal = readerCabecera["Doc_sucursal"].ToString(),
                        Opc_documento = readerCabecera["Opc_documento"].ToString(),
                        Doc_fecha = Convert.ToDateTime(readerCabecera["Doc_fecha"]),
                        Doc_NombreImp = readerCabecera["Doc_NombreImp"].ToString(),
                        Doc_CiRuc = readerCabecera["Doc_CiRuc"].ToString(),
                        Doc_valor = Convert.ToDecimal(readerCabecera["Doc_valor"]),
                        Doc_totciva = Convert.ToDecimal(readerCabecera["Doc_totciva"]),
                        Doc_valoriva = Convert.ToDecimal(readerCabecera["Doc_valoriva"]),
                        Doc_porceniva = Convert.ToDecimal(readerCabecera["Doc_porceniva"]),
                        Doc_Direccion = readerCabecera["Doc_Direccion"]?.ToString() ?? "",
                        Doc_porcendes1 = Convert.ToDecimal(readerCabecera["Doc_porcendes1"]),
                        Doc_valordes1 = Convert.ToDecimal(readerCabecera["Doc_valordes1"]),
                        Doc_NroIdDoc = readerCabecera["Doc_NroIdDoc"].ToString(),
                        IdClaveDoc = Convert.ToDecimal(readerCabecera["IdClaveDoc"])
                    };
                }
            }
        }

        if (cabecera == null)
            return (null, null, null);

        // 2. Consultar líneas
        string sqlLineas = @"
        SELECT Tra_numlinea, Tra_Codigo, Tra_nombre, Tra_cantidad, Tra_precuni, Tra_prectot, Tra_valor
        FROM AdcTra 
        WHERE IdClaveDoc = @idClaveDoc 
          AND Doc_sucursal = @sucursal 
          AND Opc_documento = @opcDocumento 
          AND Doc_numero = @docNumero
        ORDER BY Tra_numlinea";

        using (var cmdLineas = new SqlCommand(sqlLineas, connection))
        {
            cmdLineas.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
            cmdLineas.Parameters.AddWithValue("@sucursal", sucursal);
            cmdLineas.Parameters.AddWithValue("@opcDocumento", opcDocumento);
            cmdLineas.Parameters.AddWithValue("@docNumero", docNumero);

            using (var readerLineas = await cmdLineas.ExecuteReaderAsync())
            {
                while (await readerLineas.ReadAsync())
                {
                    lineas.Add(new ImpresionLineaDto
                    {
                        Tra_numlinea = Convert.ToDecimal(readerLineas["Tra_numlinea"]),
                        Tra_Codigo = readerLineas["Tra_Codigo"].ToString(),
                        Tra_nombre = readerLineas["Tra_nombre"].ToString(),
                        Tra_cantidad = Convert.ToDecimal(readerLineas["Tra_cantidad"]),
                        Tra_precuni = Convert.ToDecimal(readerLineas["Tra_precuni"]),
                        Tra_prectot = Convert.ToDecimal(readerLineas["Tra_prectot"]),
                        Tra_valor = Convert.ToDecimal(readerLineas["Tra_valor"])
                    });
                }
            }
        }

        // 3. Datos de la empresa
        var sucursalConfig = await _context.SucursalesServidores
            .FirstOrDefaultAsync(s => s.SucursalCodigo == sucursal && s.Activo);

        string empresaId = sucursalConfig?.EmpresaId ?? "1793066070001";

        string sqlEmpresa = @"
        SELECT Codigo, NombreImpresion, Domicilio, Telefono1, CorreoElectrónico, CedulaIdentidadRuc
        FROM Identificacion 
        WHERE CedulaIdentidadRuc = @empresaId";

        using (var cmdEmpresa = new SqlCommand(sqlEmpresa, connection))
        {
            cmdEmpresa.Parameters.AddWithValue("@empresaId", empresaId);

            using (var readerEmpresa = await cmdEmpresa.ExecuteReaderAsync())
            {
                if (await readerEmpresa.ReadAsync())
                {
                    empresa = new ImpresionEmpresaDto
                    {
                        Ruc = readerEmpresa["CedulaIdentidadRuc"].ToString(),
                        Nombre = readerEmpresa["NombreImpresion"].ToString(),
                        Direccion = readerEmpresa["Domicilio"]?.ToString() ?? "",
                        Telefono = readerEmpresa["Telefono1"]?.ToString() ?? "",
                        Email = readerEmpresa["CorreoElectrónico"]?.ToString() ?? ""
                    };
                }
            }
        }

        if (empresa == null)
        {
            empresa = new ImpresionEmpresaDto
            {
                Ruc = empresaId,
                Nombre = "ECUAVICHE S.A.",
                Direccion = "",
                Telefono = ""
            };
        }

        return (cabecera, lineas, empresa);
    }

    public async Task<(dynamic cabecera, dynamic lineas, dynamic empresa)> ConsultarFacturaParaImprimirTest(SqlConnection connection, string sucursal, decimal docNumero)
    {
        dynamic cabecera = null;
        dynamic empresa = null;
        var lineas = new List<dynamic>();

        try
        {
            // 1. Consultar cabecera
            string sqlCabecera = @"
            SELECT Doc_numero, Doc_sucursal, Doc_fecha, Doc_NombreImp, Doc_CiRuc, 
                   Doc_valor, Doc_totciva, Doc_valoriva, Doc_porceniva, Doc_Direccion,
                   Doc_porcendes1, Doc_valordes1, Doc_NroIdDoc, IdClaveDoc
            FROM AdcDoc 
            WHERE Doc_sucursal = @sucursal AND Doc_numero = @docNumero";

            using (var cmdCabecera = new SqlCommand(sqlCabecera, connection))
            {
                cmdCabecera.Parameters.AddWithValue("@sucursal", sucursal);
                cmdCabecera.Parameters.AddWithValue("@docNumero", docNumero);

                using (var readerCabecera = await cmdCabecera.ExecuteReaderAsync())
                {
                    if (await readerCabecera.ReadAsync())
                    {
                        cabecera = new
                        {
                            Doc_numero = readerCabecera["Doc_numero"],
                            Doc_sucursal = readerCabecera["Doc_sucursal"],
                            Doc_fecha = readerCabecera["Doc_fecha"],
                            Doc_NombreImp = readerCabecera["Doc_NombreImp"],
                            Doc_CiRuc = readerCabecera["Doc_CiRuc"],
                            Doc_valor = readerCabecera["Doc_valor"],
                            Doc_totciva = readerCabecera["Doc_totciva"],
                            Doc_valoriva = readerCabecera["Doc_valoriva"],
                            Doc_porceniva = readerCabecera["Doc_porceniva"],
                            Doc_Direccion = readerCabecera["Doc_Direccion"],
                            Doc_porcendes1 = readerCabecera["Doc_porcendes1"],
                            Doc_valordes1 = readerCabecera["Doc_valordes1"],
                            Doc_NroIdDoc = readerCabecera["Doc_NroIdDoc"],
                            IdClaveDoc = readerCabecera["IdClaveDoc"]
                        };
                    }
                }
            }

            if (cabecera == null)
                return (null, null, null);

            // 2. Consultar líneas
            string sqlLineas = @"
            SELECT Tra_numlinea, Tra_Codigo, Tra_nombre, Tra_cantidad, Tra_precuni, Tra_prectot, Tra_valor
            FROM AdcTra 
            WHERE Doc_sucursal = @sucursal AND Doc_numero = @docNumero
            ORDER BY Tra_numlinea";

            using (var cmdLineas = new SqlCommand(sqlLineas, connection))
            {
                cmdLineas.Parameters.AddWithValue("@sucursal", sucursal);
                cmdLineas.Parameters.AddWithValue("@docNumero", docNumero);

                using (var readerLineas = await cmdLineas.ExecuteReaderAsync())
                {
                    while (await readerLineas.ReadAsync())
                    {
                        lineas.Add(new
                        {
                            Tra_numlinea = readerLineas["Tra_numlinea"],
                            Tra_Codigo = readerLineas["Tra_Codigo"],
                            Tra_nombre = readerLineas["Tra_nombre"],
                            Tra_cantidad = readerLineas["Tra_cantidad"],
                            Tra_precuni = readerLineas["Tra_precuni"],
                            Tra_prectot = readerLineas["Tra_prectot"],
                            Tra_valor = readerLineas["Tra_valor"]
                        });
                    }
                }
            }

            // 3. Datos de la empresa
            var sucursalConfig = await _context.SucursalesServidores
                .FirstOrDefaultAsync(s => s.SucursalCodigo == sucursal && s.Activo);

            string empresaId = sucursalConfig?.EmpresaId ?? "1793066070001";

            string sqlEmpresa = @"
            SELECT Codigo, NombreImpresion, Domicilio, Telefono1, CorreoElectrónico
            FROM Identificacion 
            WHERE Codigo = @empresaId";

            using (var cmdEmpresa = new SqlCommand(sqlEmpresa, connection))
            {
                cmdEmpresa.Parameters.AddWithValue("@empresaId", empresaId);

                using (var readerEmpresa = await cmdEmpresa.ExecuteReaderAsync())
                {
                    if (await readerEmpresa.ReadAsync())
                    {
                        empresa = new
                        {
                            Ruc = readerEmpresa["Codigo"],
                            Nombre = readerEmpresa["NombreImpresion"],
                            Direccion = readerEmpresa["Domicilio"],
                            Telefono = readerEmpresa["Telefono1"],
                            Email = readerEmpresa["CorreoElectrónico"]
                        };
                    }
                }
            }

            if (empresa == null)
            {
                empresa = new
                {
                    Ruc = empresaId,
                    Nombre = "ECUAVICHE S.A.",
                    Direccion = "",
                    Telefono = ""
                };
            }

            return (cabecera, lineas, empresa);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al consultar factura: {ex.Message}");
        }
    }
       
    public async Task<decimal> ObtenerPorcentajeIva(SucursalServidor sucursalConfig, DateTime fecha)
    {
        try
        {
            using var connection = new SqlConnection(sucursalConfig.ConnectionStringIvaretdax);
            await connection.OpenAsync();

            string sql = @"
            SELECT Porcentaje 
            FROM dbo.PorcentajeIva 
            WHERE @fecha BETWEEN FechaInicio AND FechaFin";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@fecha", fecha);

            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return 15m;

            decimal porcentaje = Convert.ToDecimal(result);
            return porcentaje * 100;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener porcentaje de IVA: {ex.Message}");
        }
    }

    private async Task<(decimal totCiva, decimal valorIva, decimal valorTotal, decimal porcenIvaUsado)> CalcularTotalesAsync(SqlConnection connection, SqlTransaction transaction, SucursalServidor sucursalConfig, FacturaRequestDto request)
    {
        decimal totalBaseIva = 0;
        decimal totalBaseSinIva = 0;
        decimal totalIva = 0;

        // Obtener IVA de IVARETDAX
        decimal porcenIvaUsado = await ObtenerPorcentajeIva(sucursalConfig, request.Fecha);

        // Procesar productos
        foreach (var linea in request.Lineas)
        {
            var producto = await ValidarProducto(connection, transaction, linea.Codigo);
            if (!producto.existe)
                throw new Exception($"Producto {linea.Codigo} no encontrado");

            decimal subtotal = linea.Cantidad * linea.PrecioUnitario;

            if (linea.DescuentoPorcentaje > 0)
                subtotal -= subtotal * (linea.DescuentoPorcentaje / 100);
            else if (linea.DescuentoValor > 0)
                subtotal -= linea.DescuentoValor;

            if (producto.tieneIva && porcenIvaUsado > 0)
            {
                totalBaseIva += subtotal;
                totalIva += subtotal * (porcenIvaUsado / 100);
            }
            else
            {
                totalBaseSinIva += subtotal;
            }

            // Procesar agregadores de producto
            foreach (var agg in linea.AgregadoresProducto)
            {
                var aggProducto = await ValidarProducto(connection, transaction, agg.Codigo);
                if (!aggProducto.existe)
                    throw new Exception($"Agregador {agg.Codigo} no encontrado");

                decimal subtotalAgg = agg.Cantidad * agg.PrecioUnitario;

                if (aggProducto.tieneIva && porcenIvaUsado > 0)
                {
                    totalBaseIva += subtotalAgg;
                    totalIva += subtotalAgg * (porcenIvaUsado / 100);
                }
                else
                {
                    totalBaseSinIva += subtotalAgg;
                }
            }
        }

        // Procesar agregadores de pedido
        foreach (var agg in request.AgregadoresPedido)
        {
            decimal subtotal = agg.Cantidad * agg.PrecioUnitario;

            if (agg.AfectaBaseImponible && porcenIvaUsado > 0)
            {
                totalBaseIva += subtotal;
                totalIva += subtotal * (porcenIvaUsado / 100);
            }
            else
            {
                totalBaseSinIva += subtotal;
            }
        }

        // Calcular total general
        decimal totalGeneral = totalBaseIva + totalBaseSinIva + totalIva;

        // Aplicar descuento general
        if (request.DescuentoPorcentaje > 0)
        {
            decimal descuento = totalGeneral * (request.DescuentoPorcentaje / 100);
            totalGeneral -= descuento;
        }
        else if (request.DescuentoValor > 0)
        {
            totalGeneral -= request.DescuentoValor;
        }

        return (
            Math.Round(totalBaseIva, 2),
            Math.Round(totalIva, 2),
            Math.Round(totalGeneral, 2),
            porcenIvaUsado
        );
    }

    public async Task<decimal> ObtenerIvaActual()
    {
        try
        {
            DateTime fecha = DateTime.Now;

            using var connection = new SqlConnection(_configuration.GetConnectionString("Ivaretdax"));
            await connection.OpenAsync();

            string sql = @"
            SELECT Porcentaje 
            FROM Ivaretdax.dbo.PorcentajeIva 
            WHERE @fecha BETWEEN FechaInicio AND FechaFin";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@fecha", fecha);

            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return 15m;

            decimal porcentaje = Convert.ToDecimal(result);
            return porcentaje * 100;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener IVA: {ex.Message}");
        }
    }

    public async Task<decimal> ObtenerIvaPorFecha(DateTime fecha)
    {
        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("Ivaretdax"));
            await connection.OpenAsync();

            string sql = @"
            SELECT Porcentaje 
            FROM Ivaretdax.dbo.PorcentajeIva 
            WHERE @fecha BETWEEN FechaInicio AND FechaFin";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@fecha", fecha);

            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return 15m;

            decimal porcentaje = Convert.ToDecimal(result);
            return porcentaje * 100;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener IVA por fecha: {ex.Message}");
        }
    }
}