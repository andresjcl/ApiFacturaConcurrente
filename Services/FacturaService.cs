using Microsoft.Data.SqlClient;
using ApiFacturaConcurrente.Models.DTOs;
using ApiFacturaConcurrente.Models.Entities;
using System.Collections.Concurrent;

namespace ApiFacturaConcurrente.Services;

public class FacturaService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public async Task<FacturaResponseDto> CrearFactura(
        SucursalServidor sucursalConfig,
        FacturaRequestDto request,
        string empresaId)
    {
        var response = new FacturaResponseDto { Success = false };
        var semaphore = _semaphores.GetOrAdd(sucursalConfig.SucursalCodigo,
            new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();

        SqlConnection connection = null;
        SqlTransaction transaction = null;

        try
        {
            connection = new SqlConnection(sucursalConfig.ConnectionString);
            await connection.OpenAsync();

            // Iniciar transacción explicitamente como SqlTransaction
            transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            // 1. Obtener número de factura
            var docNumero = await ObtenerSiguienteNumero(connection, transaction, sucursalConfig.SucursalCodigo);

            // 2. Calcular totales
            var (totalConIva, totalBaseIva, totalBaseSinIva) = CalcularTotales(request.Lineas);

            // 3. Insertar cabecera
            var idClaveDoc = await InsertarCabecera(connection, transaction, sucursalConfig, request,
                docNumero, totalConIva, totalBaseIva, totalBaseSinIva, empresaId);

            // 4. Insertar líneas
            await InsertarLineas(connection, transaction, sucursalConfig, request, docNumero, idClaveDoc);

            // 5. Insertar pagos
            if (request.Pagos != null && request.Pagos.Any())
            {
                await InsertarPagos(connection, transaction, sucursalConfig, request, docNumero, idClaveDoc);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Sucursal = sucursalConfig.SucursalCodigo;
            response.DocNumero = docNumero;
            response.Total = totalConIva;
            response.Mensaje = "Factura creada exitosamente";
        }
        catch (Exception ex)
        {
            if (transaction != null)
                await transaction.RollbackAsync();

            response.Mensaje = $"Error: {ex.Message}";
            response.Success = false;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
            if (connection != null)
                await connection.DisposeAsync();

            semaphore.Release();
        }

        return response;
    }

    private async Task<decimal> ObtenerSiguienteNumero(
        SqlConnection connection,
        SqlTransaction transaction,
        string sucursal)
    {
        var sql = @"
            UPDATE AdcDocNum 
            SET UltimoNumero = UltimoNumero + 1,
                UltimaFecha = GETDATE()
            OUTPUT INSERTED.UltimoNumero AS NuevoNumero
            WHERE Id_Lugar = @sucursal AND id_Documento = 'FAC'";

        using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("@sucursal", sucursal);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToDecimal(result);
    }

    private (decimal totalConIva, decimal totalBaseIva, decimal totalBaseSinIva)
        CalcularTotales(List<LineaDto> lineas)
    {
        decimal totalConIva = 0, totalBaseIva = 0, totalBaseSinIva = 0;
        foreach (var linea in lineas)
        {
            var subtotal = linea.Cantidad * linea.Precio;
            if (linea.Iva > 0)
            {
                totalBaseIva += subtotal;
                totalConIva += subtotal * (1 + linea.Iva / 100);
            }
            else
            {
                totalBaseSinIva += subtotal;
                totalConIva += subtotal;
            }
        }
        return (totalConIva, totalBaseIva, totalBaseSinIva);
    }

    private async Task<decimal> InsertarCabecera(
        SqlConnection connection,
        SqlTransaction transaction,
        SucursalServidor sucursal,
        FacturaRequestDto request,
        decimal docNumero,
        decimal totalConIva,
        decimal totalBaseIva,
        decimal totalBaseSinIva,
        string empresaId)
    {
        // Obtener el siguiente IdClaveDoc
        var idClaveDoc = await ObtenerSiguienteIdClaveDoc(connection, transaction);

        var sql = @"
            INSERT INTO AdcDoc (
                Doc_sucursal, Opc_documento, Doc_numero, IdClaveDoc,
                Doc_fecha, Doc_detalle, Doc_codper, Doc_NombreImp, 
                Doc_CiRuc, Doc_Direccion, Doc_Telefono1, Doc_valor,
                Doc_TotArtCIva, Doc_TotArtSIva, Doc_Estado,
                Doc_Hora, Doc_FecGraba, AuxVar1
            ) VALUES (
                @sucursal, 'FAC', @docNumero, @idClaveDoc,
                @fecha, @detalle, @codPer, @nombreCliente,
                @ruc, @direccion, @telefono, @total,
                @baseIva, @baseSinIva, 1,
                @hora, @fechaGrabacion, @empresaId
            )";

        using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("@sucursal", sucursal.SucursalCodigo);
        cmd.Parameters.AddWithValue("@docNumero", docNumero);
        cmd.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
        cmd.Parameters.AddWithValue("@fecha", request.Fecha ?? DateTime.Now);
        cmd.Parameters.AddWithValue("@detalle", request.Detalle ?? "FACTURA API");
        cmd.Parameters.AddWithValue("@codPer", request.Cliente.Codigo);
        cmd.Parameters.AddWithValue("@nombreCliente", request.Cliente.Nombre ?? "");
        cmd.Parameters.AddWithValue("@ruc", request.Cliente.Ruc ?? "");
        cmd.Parameters.AddWithValue("@direccion", request.Cliente.Direccion ?? "");
        cmd.Parameters.AddWithValue("@telefono", request.Cliente.Telefono ?? "");
        cmd.Parameters.AddWithValue("@total", totalConIva);
        cmd.Parameters.AddWithValue("@baseIva", totalBaseIva);
        cmd.Parameters.AddWithValue("@baseSinIva", totalBaseSinIva);
        cmd.Parameters.AddWithValue("@hora", DateTime.Now);
        cmd.Parameters.AddWithValue("@fechaGrabacion", DateTime.Now);
        cmd.Parameters.AddWithValue("@empresaId", empresaId);

        await cmd.ExecuteNonQueryAsync();

        return idClaveDoc;
    }

    private async Task<decimal> ObtenerSiguienteIdClaveDoc(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        var sql = "SELECT ISNULL(MAX(IdClaveDoc), 0) + 1 FROM AdcDoc";
        using var cmd = new SqlCommand(sql, connection, transaction);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToDecimal(result);
    }

    private async Task InsertarLineas(
        SqlConnection connection,
        SqlTransaction transaction,
        SucursalServidor sucursal,
        FacturaRequestDto request,
        decimal docNumero,
        decimal idClaveDoc)
    {
        var sql = @"
            INSERT INTO AdcTra (
                Doc_sucursal, Opc_documento, Doc_numero, IdClaveDoc, Tra_numlinea,
                Tra_Codigo, Tra_nombre, Tra_cantidad, Tra_precuni,
                Tra_valor, Doc_Bodega, Tra_sniva, Tra_porceniva, Tra_valoriva, Tra_Estado
            ) VALUES (
                @sucursal, 'FAC', @docNumero, @idClaveDoc, @linea,
                @codigo, @nombre, @cantidad, @precio,
                @subtotal, @bodega, @sniva, @porcIva, @valorIva, 1
            )";

        for (int i = 0; i < request.Lineas.Count; i++)
        {
            var linea = request.Lineas[i];
            var subtotal = linea.Cantidad * linea.Precio;
            var valorIva = linea.Iva > 0 ? subtotal * (linea.Iva / 100) : 0;

            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@sucursal", sucursal.SucursalCodigo);
            cmd.Parameters.AddWithValue("@docNumero", docNumero);
            cmd.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
            cmd.Parameters.AddWithValue("@linea", i + 1);
            cmd.Parameters.AddWithValue("@codigo", linea.Codigo);
            cmd.Parameters.AddWithValue("@nombre", linea.Nombre);
            cmd.Parameters.AddWithValue("@cantidad", linea.Cantidad);
            cmd.Parameters.AddWithValue("@precio", linea.Precio);
            cmd.Parameters.AddWithValue("@subtotal", subtotal);
            cmd.Parameters.AddWithValue("@bodega", linea.Bodega ?? "01");
            cmd.Parameters.AddWithValue("@sniva", linea.Iva > 0);
            cmd.Parameters.AddWithValue("@porcIva", linea.Iva);
            cmd.Parameters.AddWithValue("@valorIva", valorIva);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task InsertarPagos(
        SqlConnection connection,
        SqlTransaction transaction,
        SucursalServidor sucursal,
        FacturaRequestDto request,
        decimal docNumero,
        decimal idClaveDoc)
    {
        if (request.Pagos == null || !request.Pagos.Any()) return;

        var sql = @"
            INSERT INTO AdcPag (
                Doc_sucursal, Opc_documento, Doc_numero, IdClaveDoc, Pag_Numero,
                Pag_TipoPago, Pag_Valor, Doc_Fecha
            ) VALUES (
                @sucursal, 'FAC', @docNumero, @idClaveDoc, @numPago,
                @tipoPago, @valor, @fecha
            )";

        for (int i = 0; i < request.Pagos.Count; i++)
        {
            var pago = request.Pagos[i];
            using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@sucursal", sucursal.SucursalCodigo);
            cmd.Parameters.AddWithValue("@docNumero", docNumero);
            cmd.Parameters.AddWithValue("@idClaveDoc", idClaveDoc);
            cmd.Parameters.AddWithValue("@numPago", i + 1);
            cmd.Parameters.AddWithValue("@tipoPago", pago.Tipo);
            cmd.Parameters.AddWithValue("@valor", pago.Valor);
            cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}