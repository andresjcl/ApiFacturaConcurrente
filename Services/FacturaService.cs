using Microsoft.Data.SqlClient;
using ApiFacturaConcurrente.Models.DTOs;
using ApiFacturaConcurrente.Models.Entities;
using System.Collections.Concurrent;

namespace ApiFacturaConcurrente.Services;

public class FacturaService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public async Task<FacturaResponseDto> CrearFactura(SucursalServidor sucursalConfig, FacturaRequestDto request, string empresaId)
    {
        var response = new FacturaResponseDto { Success = false };
        var semaphore = _semaphores.GetOrAdd(sucursalConfig.SucursalCodigo, new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();

        SqlConnection connection = null;
        SqlTransaction transaction = null;

        try
        {
            connection = new SqlConnection(sucursalConfig.ConnectionString);
            await connection.OpenAsync();
            transaction = (SqlTransaction)await connection.BeginTransactionAsync();

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

            // 4. INSERTAR LINEAS (PRODUCTOS, AGREGADORES, SERVICIOS)
            await InsertarLineas(connection, transaction, request, docNumero, idClaveDoc);

            // 5. INSERTAR PAGOS
            if (request.Pagos != null && request.Pagos.Any())
                await InsertarPagos(connection, transaction, request, docNumero, idClaveDoc);

            await transaction.CommitAsync();

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

    // ==================== MÉTODOS PRINCIPALES ====================

    private async Task InsertarLineas(SqlConnection connection, SqlTransaction transaction,
        FacturaRequestDto request, decimal docNumero, decimal idClaveDoc)
    {
        int anio = DateTime.Now.Year, mes = DateTime.Now.Month, dia = DateTime.Now.Day;
        int numLinea = 1;
        decimal totalFactura = request.ValorTotal;

        foreach (var linea in request.Lineas)
        {
            // Validar producto
            var producto = await ValidarProducto(connection, transaction, linea.Codigo);
            if (!producto.existe)
                throw new Exception($"Producto {linea.Codigo} no encontrado");

            // Construir nombre con modificadores de texto
            string nombreCompleto = producto.nombre;
            if (linea.ModificadoresTexto != null && linea.ModificadoresTexto.Any())
                nombreCompleto += " + " + string.Join(" + ", linea.ModificadoresTexto);

            // Calcular descuento de línea
            decimal descuentoValor = 0;
            if (linea.DescuentoPorcentaje > 0)
                descuentoValor = linea.Subtotal * (linea.DescuentoPorcentaje / 100);
            else if (linea.DescuentoValor > 0)
                descuentoValor = linea.DescuentoValor;

            // Insertar producto principal
            await InsertarTraLinea(connection, transaction, request, docNumero, idClaveDoc,
                numLinea++, linea.Codigo, nombreCompleto, linea.Cantidad,
                linea.PrecioUnitario, linea.PrecioTotal, linea.Iva, linea.ValorIva,
                totalFactura, anio, mes, dia, "A", -1, 1, 0,
                linea.DescuentoMotivo, linea.DescuentoPorcentaje, descuentoValor);

            // Insertar agregadores de producto
            foreach (var agg in linea.AgregadoresProducto)
            {
                var aggProducto = await ValidarProducto(connection, transaction, agg.Codigo);
                if (!aggProducto.existe)
                    throw new Exception($"Agregador {agg.Codigo} no encontrado");

                // Construir nombre con modificadores
                string nombreAgregador = agg.Nombre;
                if (agg.ModificadoresTexto != null && agg.ModificadoresTexto.Any())
                {
                    nombreAgregador += " + " + string.Join(" + ", agg.ModificadoresTexto);
                }

                // ✅ CORRECTO - usar nombreAgregador en lugar de agg.Nombre
                await InsertarTraLinea(connection, transaction, request, docNumero, idClaveDoc,
                    numLinea++, agg.Codigo, nombreAgregador, agg.Cantidad,
                    agg.PrecioUnitario, agg.Total, agg.Iva, agg.ValorIva,
                    totalFactura, anio, mes, dia, "A", -1, 1, 0,
                    null, 0, 0);
            }
        }

        // Insertar agregadores de pedido
        foreach (var agg in request.AgregadoresPedido)
        {
            var servicio = await ValidarServicio(connection, transaction, agg.Codigo);
            string quetipo = servicio.existe ? "S" : "A";
            int inventario = servicio.existe ? 0 : -1;
            decimal ivaPorcentaje = servicio.existe ? servicio.porcentajeIva : agg.Iva;
            decimal ivaValor = servicio.existe ? agg.Subtotal * (ivaPorcentaje / 100) : agg.ValorIva;
            string nombre = servicio.existe ? servicio.nombre : agg.Nombre;
            decimal precio = servicio.existe ? servicio.precio : agg.PrecioUnitario;
            decimal total = servicio.existe ? agg.Subtotal + ivaValor : agg.Total;

            await InsertarTraLinea(connection, transaction, request, docNumero, idClaveDoc,
                numLinea++, agg.Codigo, nombre, agg.Cantidad, precio, total,
                ivaPorcentaje, ivaValor, totalFactura, anio, mes, dia,
                quetipo, inventario, 1, 0, null, 0, 0);
        }
    }

    private async Task InsertarTraLinea(SqlConnection connection, SqlTransaction transaction,
        FacturaRequestDto request, decimal docNumero, decimal idClaveDoc, int numLinea,
        string codigo, string nombre, decimal cantidad, decimal precioUnitario, decimal precioTotal,
        decimal ivaPorcentaje, decimal ivaValor, decimal totalFactura,
        int anio, int mes, int dia, string quetipo, int inventario, int ventas, int compras,
        string descuentoMotivo, decimal descuentoPorcentaje, decimal descuentoValor)
    {
        decimal traPrectot = cantidad * precioUnitario;

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
        cmd.Parameters.AddWithValue("@totalFactura", totalFactura);
        cmd.Parameters.AddWithValue("@precUni", precioUnitario);
        cmd.Parameters.AddWithValue("@traPrectot", traPrectot);
        cmd.Parameters.AddWithValue("@fecha", request.Fecha);
        cmd.Parameters.AddWithValue("@inventario", inventario);
        cmd.Parameters.AddWithValue("@ventas", ventas);
        cmd.Parameters.AddWithValue("@compras", compras);
        cmd.Parameters.AddWithValue("@quetipo", quetipo);
        cmd.Parameters.AddWithValue("@porcenIva", ivaPorcentaje);
        cmd.Parameters.AddWithValue("@valorIva", ivaValor);
        cmd.Parameters.AddWithValue("@anio", anio);
        cmd.Parameters.AddWithValue("@mes", mes);
        cmd.Parameters.AddWithValue("@dia", dia);
        cmd.Parameters.AddWithValue("@descuentoMotivo", descuentoMotivo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@descuentoPorcentaje", descuentoPorcentaje);
        cmd.Parameters.AddWithValue("@descuentoValor", descuentoValor);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertarCabecera(SqlConnection connection, SqlTransaction transaction,
        FacturaRequestDto request, decimal docNumero, decimal idClaveDoc, string empresaId)
    {
        // Calcular valor del descuento
        decimal descuentoValorCalculado = 0;
        decimal subtotalGeneral = request.TotCiva + request.TotSiva;
        if (request.DescuentoPorcentaje > 0)
            descuentoValorCalculado = subtotalGeneral * (request.DescuentoPorcentaje / 100);
        else if (request.DescuentoValor > 0)
            descuentoValorCalculado = request.DescuentoValor;

        var sql = @"
        INSERT INTO AdcDoc (
            Doc_sucursal, Doc_Bodega, Opc_documento, Doc_numero, IdClaveDoc,
            Doc_fecha, Doc_Hora, Doc_codper, Doc_codusu, Doc_porceniva, 
            Doc_valoriva, Doc_totciva, Doc_totsiva, Doc_valor, Doc_detalle,
            Doc_NombreImp, Doc_CiRuc, Doc_Direccion, Doc_Telefono1, Doc_Telefono2,
            Doc_NroIdDoc, PuntoVta, AuxVar1, Doc_Estado, Doc_FecGraba,
            Doc_TipoDoc, Doc_Contado, Doc_Contabilidad, Doc_Inventario, Doc_Ventas,
            Doc_docnombre, BaseImp1, PorcImp1, AuxNum1,
            Doc_nombredes1, Doc_porcendes1, Doc_valordes1
        ) VALUES (
            @sucursal, @bodega, 'FAC', @docNumero, @idClaveDoc,
            @fecha, GETDATE(), @codCliente, 'API', @porcenIva, 
            @valorIva, @totCiva, @totSiva, @valorTotal, @detalle,
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
        cmd.Parameters.AddWithValue("@valorIva", request.ValorIva);
        cmd.Parameters.AddWithValue("@totCiva", request.TotCiva);
        cmd.Parameters.AddWithValue("@totSiva", request.TotSiva);
        cmd.Parameters.AddWithValue("@valorTotal", request.ValorTotal);
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
        cmd.Parameters.AddWithValue("@descuentoValorCalculado", descuentoValorCalculado);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertarPagos(SqlConnection connection, SqlTransaction transaction,
        FacturaRequestDto request, decimal docNumero, decimal idClaveDoc)
    {
        var sql = @"
        INSERT INTO AdcPag (
            Doc_sucursal, Opc_documento, Doc_numero, IdClaveDoc, Pag_Numero,
            Pag_Valor, Pag_TipoPago, Pag_Descripcion, Pag_Idpago, Pag_Formapago,
            Pag_Autoriza, Doc_Fecha, Pag_Cuotas
        ) VALUES (
            @sucursal, 'FAC', @docNumero, @idClaveDoc, @numPago,
            @valor, '4', @descripcion, @idPago, 2,
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
            cmd.Parameters.AddWithValue("@valor", pago.Valor);
            cmd.Parameters.AddWithValue("@descripcion", descripcion);
            cmd.Parameters.AddWithValue("@idPago", idPago);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ==================== MÉTODOS AUXILIARES ====================

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

    private async Task<decimal> ObtenerPorcentajeIva(SqlConnection connection, SqlTransaction transaction, DateTime fecha)
    {
        try
        {
            using var cmd = new SqlCommand(@"
                SELECT Porcentaje FROM Ivaretdax.dbo.PorcentajeIva 
                WHERE @fecha BETWEEN FechaInicio AND FechaFin", connection, transaction);
            cmd.Parameters.AddWithValue("@fecha", fecha);
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return 15m;
            return Convert.ToDecimal(result) * 100;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener porcentaje de IVA: {ex.Message}");
        }
    }

    private async Task<(bool existe, string nombre, bool tieneIva, decimal precio, bool sncomp, string artClase)>
        ValidarProducto(SqlConnection connection, SqlTransaction transaction, string codigoProducto)
    {
        try
        {
            using var cmd = new SqlCommand(@"
                SELECT Art_nombre, Art_sniva, Art_precvta1, Art_sncomp, Art_clase
                FROM ADCART WHERE Art_codigo = @codigo", connection, transaction);
            cmd.Parameters.AddWithValue("@codigo", codigoProducto);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    true,
                    reader["Art_nombre"]?.ToString() ?? "",
                    reader["Art_sniva"] != DBNull.Value && Convert.ToInt32(reader["Art_sniva"]) == 1,
                    reader["Art_precvta1"] != DBNull.Value ? Convert.ToDecimal(reader["Art_precvta1"]) : 0,
                    reader["Art_sncomp"] != DBNull.Value && Convert.ToInt32(reader["Art_sncomp"]) == 1,
                    reader["Art_clase"]?.ToString() ?? ""
                );
            }
            return (false, "", false, 0, false, "");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al validar producto {codigoProducto}: {ex.Message}");
        }
    }

    private async Task<(bool existe, string nombre, decimal precio, bool tieneIva, decimal porcentajeIva)>
        ValidarServicio(SqlConnection connection, SqlTransaction transaction, string codigoServicio)
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
                return (
                    true,
                    reader["Sev_nombre"]?.ToString() ?? "",
                    reader["Sev_precvta"] != DBNull.Value ? Convert.ToDecimal(reader["Sev_precvta"]) : 0,
                    reader["Sev_sniva"] != DBNull.Value && Convert.ToInt32(reader["Sev_sniva"]) == 1,
                    reader["Sev_PorIVA"] != DBNull.Value ? Convert.ToDecimal(reader["Sev_PorIVA"]) : 0
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

        if (limpio.Length == 10) return ("C", "N");      // Cédula → Persona Natural
        if (limpio.Length == 13) return ("R", "J");      // RUC → Persona Jurídica
        return ("P", "N");                                // Pasaporte → Persona Natural
    }

    private string ObtenerCodigoCliente(string ciRuc)
    {
        if (string.IsNullOrEmpty(ciRuc)) return ciRuc;
        if (ciRuc.Any(char.IsLetter))
            return ciRuc.Length > 15 ? ciRuc.Substring(0, 15) : ciRuc;
        string soloDigitos = new string(ciRuc.Where(char.IsDigit).ToArray());
        return soloDigitos.Length >= 10 ? soloDigitos.Substring(0, 10) : soloDigitos;
    }

    private async Task<string> ObtenerOInsertarCliente(SqlConnection connection, SqlTransaction transaction,
     string ciRuc, string nombres, string domicilio, string telefono1, string correo)
    {
        string codigoCliente = ObtenerCodigoCliente(ciRuc);
        var (tipoIdentificacion, tipoPersona) = DeterminarTipoIdentificacion(ciRuc);

        // Verificar si existe
        using var cmdValidar = new SqlCommand(@"
        SELECT Codigo FROM Identificacion 
        WHERE CedulaIdentidadRuc = @cedulaRuc OR Codigo = @codigo", connection, transaction);
        cmdValidar.Parameters.AddWithValue("@cedulaRuc", ciRuc);
        cmdValidar.Parameters.AddWithValue("@codigo", codigoCliente);
        var existe = await cmdValidar.ExecuteScalarAsync();
        if (existe != null && existe != DBNull.Value)
            return existe.ToString();

        // Insertar nuevo cliente
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

}