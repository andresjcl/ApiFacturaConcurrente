using Microsoft.Data.SqlClient;
using ApiFacturaConcurrente.Models.DTOs;
using ApiFacturaConcurrente.Models.Entities;
using System.Collections.Concurrent;

namespace ApiFacturaConcurrente.Services;

public class ConsultaService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public async Task<(bool success, string mensaje, List<ArticuloGrupoDto> data, int total)>GetArticulosGrupos(SucursalServidor sucursalConfig, string? codigo = null, string? nombre = null,string? clase = null, int limite = 100)
    {
        var semaphore = _semaphores.GetOrAdd(sucursalConfig.SucursalCodigo, new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        SqlConnection connection = null;
        var resultados = new List<ArticuloGrupoDto>();

        try
        {
            connection = new SqlConnection(sucursalConfig.ConnectionString);
            await connection.OpenAsync();

            // Categoría quemada: siempre 'ALI' (Alimentos)
            string sql = "SELECT * FROM ArticulosGrupos WHERE CodCategoria = 'ALI'";

            // Filtrar por clase (BEB = Bebidas, PAR = Parrillas, etc.)
            if (!string.IsNullOrEmpty(clase))
            {
                sql += " AND CodCLase = @clase";
            }

            // Filtrar por código
            if (!string.IsNullOrEmpty(codigo))
            {
                sql += " AND Codigo LIKE @codigo";
            }

            // Filtrar por nombre
            if (!string.IsNullOrEmpty(nombre))
            {
                sql += " AND nombre LIKE @nombre";
            }

            // Ordenar y limitar
            sql += " ORDER BY Codigo OFFSET 0 ROWS FETCH NEXT @limite ROWS ONLY";

            using var cmd = new SqlCommand(sql, connection);

            // Agregar parámetros
            if (!string.IsNullOrEmpty(clase))
            {
                cmd.Parameters.AddWithValue("@clase", clase);
            }
            if (!string.IsNullOrEmpty(codigo))
            {
                cmd.Parameters.AddWithValue("@codigo", $"%{codigo}%");
            }
            if (!string.IsNullOrEmpty(nombre))
            {
                cmd.Parameters.AddWithValue("@nombre", $"%{nombre}%");
            }
            cmd.Parameters.AddWithValue("@limite", limite);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var item = new ArticuloGrupoDto
                {
                    Codigo = reader["Codigo"]?.ToString() ?? "",
                    Nombre = reader["nombre"]?.ToString(),
                    Categoria = reader["Categoria"]?.ToString(),
                    Clase = reader["Clase"]?.ToString(),
                    Grupo = reader["Grupo"]?.ToString(),
                    Subgrupo = reader["Subgrupo"]?.ToString(),
                    CodigoBase = reader["CodigoBase"]?.ToString(),
                    COLOR = reader["COLOR"]?.ToString(),
                    TALLA = reader["TALLA"]?.ToString(),
                    Medida = reader["Medida"]?.ToString(),
                    CodCategoria = reader["CodCategoria"]?.ToString(),
                    CodCLase = reader["CodCLase"]?.ToString(),
                    CodGrupo = reader["CodGrupo"]?.ToString(),
                    CodSubgrupo = reader["CodSubgrupo"]?.ToString(),
                    NomCategoria = reader["NomCategoria"]?.ToString(),
                    NomCLase = reader["NomCLase"]?.ToString(),
                    NomGrupo = reader["NomGrupo"]?.ToString(),
                    NomSubgrupo = reader["NomSubgrupo"]?.ToString(),
                    Art_precvta1 = reader["Art_precvta1"] as decimal?,
                    Art_precvta1_inc = reader["Art_precvta1_inc"] as decimal?,
                    Art_precvta2 = reader["Art_precvta2"] as decimal?,
                    Art_precvta2_inc = reader["Art_precvta2_inc"] as decimal?,
                    Art_precvta3 = reader["Art_precvta3"] as decimal?,
                    Art_precvta3_inc = reader["Art_precvta3_inc"] as decimal?,
                    Art_precvta4 = reader["Art_precvta4"] as decimal?,
                    Art_precvta4_inc = reader["Art_precvta4_inc"] as decimal?,
                    Art_precvta5 = reader["Art_precvta5"] as decimal?,
                    Art_precvta5_inc = reader["Art_precvta5_inc"] as decimal?,
                    Art_maxbod = reader["Art_maxbod"] as int?,
                    Art_minbod = reader["Art_minbod"] as int?,
                    Art_CostoEstandard = reader["Art_CostoEstandard"] as decimal?,
                    Art_descuen = reader["Art_descuen"] as decimal?,
                    art_limDescuento = reader["art_limDescuento"] as decimal?,
                    Art_idcontable = reader["Art_idcontable"]?.ToString(),
                    codProveedor = reader["codProveedor"]?.ToString(),
                    NomProveedor = reader["NomProveedor"]?.ToString(),
                    Art_sniva = reader["Art_sniva"] as bool?
                };
                resultados.Add(item);
            }

            return (true, "Consulta exitosa", resultados, resultados.Count);
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}", resultados, 0);
        }
        finally
        {
            if (connection != null) await connection.DisposeAsync();
            semaphore.Release();
        }
    }
    public async Task<(bool success, string mensaje, List<Dictionary<string, object>> data, int total)>GetFacturas(SucursalServidor sucursalConfig, string sucursal, DateTime? desde, DateTime? hasta, int limite)
    {
        var semaphore = _semaphores.GetOrAdd(sucursalConfig.SucursalCodigo, new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        SqlConnection connection = null;
        var resultados = new List<Dictionary<string, object>>();

        try
        {
            connection = new SqlConnection(sucursalConfig.ConnectionString);
            await connection.OpenAsync();

            string sql = "SELECT * FROM AdcDoc WHERE Doc_sucursal = @sucursal";

            if (desde.HasValue)
                sql += " AND Doc_fecha >= @desde";
            if (hasta.HasValue)
                sql += " AND Doc_fecha <= @hasta";

            sql += " ORDER BY Doc_numero DESC OFFSET 0 ROWS FETCH NEXT @limite ROWS ONLY";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@sucursal", sucursal);
            cmd.Parameters.AddWithValue("@limite", limite);

            if (desde.HasValue)
                cmd.Parameters.AddWithValue("@desde", desde.Value);
            if (hasta.HasValue)
                cmd.Parameters.AddWithValue("@hasta", hasta.Value);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var fila = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    fila[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? "" : reader.GetValue(i);
                }
                resultados.Add(fila);
            }

            return (true, "Consulta exitosa", resultados, resultados.Count);
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}", resultados, 0);
        }
        finally
        {
            if (connection != null) await connection.DisposeAsync();
            semaphore.Release();
        }
    }

    public async Task<(bool success, string mensaje, List<Dictionary<string, object>> data, int total)>GetClientes(SucursalServidor sucursalConfig, string? codigo, string? nombre, int limite)
    {
        var semaphore = _semaphores.GetOrAdd(sucursalConfig.SucursalCodigo, new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        SqlConnection connection = null;
        var resultados = new List<Dictionary<string, object>>();

        try
        {
            connection = new SqlConnection(sucursalConfig.ConnectionString);
            await connection.OpenAsync();

            string sql = "SELECT * FROM Identificacion WHERE EsCliente = 1";

            if (!string.IsNullOrEmpty(codigo))
                sql += " AND Codigo LIKE @codigo";
            if (!string.IsNullOrEmpty(nombre))
                sql += " AND Nombres LIKE @nombre";

            sql += $" ORDER BY Codigo OFFSET 0 ROWS FETCH NEXT {limite} ROWS ONLY";

            using var cmd = new SqlCommand(sql, connection);

            if (!string.IsNullOrEmpty(codigo))
                cmd.Parameters.AddWithValue("@codigo", $"%{codigo}%");
            if (!string.IsNullOrEmpty(nombre))
                cmd.Parameters.AddWithValue("@nombre", $"%{nombre}%");

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var fila = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    fila[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? "" : reader.GetValue(i);
                }
                resultados.Add(fila);
            }

            return (true, "Consulta exitosa", resultados, resultados.Count);
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}", resultados, 0);
        }
        finally
        {
            if (connection != null) await connection.DisposeAsync();
            semaphore.Release();
        }
    }

    public async Task<(bool success, string mensaje, Dictionary<string, object>? cabecera, List<Dictionary<string, object>> lineas, int totalLineas)>GetFacturaDetalle(SucursalServidor sucursalConfig, string sucursal, decimal docNumero)
    {
        var semaphore = _semaphores.GetOrAdd(sucursalConfig.SucursalCodigo, new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        SqlConnection connection = null;
        Dictionary<string, object>? cabecera = null;
        var lineas = new List<Dictionary<string, object>>();

        try
        {
            connection = new SqlConnection(sucursalConfig.ConnectionString);
            await connection.OpenAsync();

            // Obtener cabecera
            string sqlCabecera = "SELECT * FROM AdcDoc WHERE Doc_sucursal = @sucursal AND Doc_numero = @docNumero";
            using var cmdCabecera = new SqlCommand(sqlCabecera, connection);
            cmdCabecera.Parameters.AddWithValue("@sucursal", sucursal);
            cmdCabecera.Parameters.AddWithValue("@docNumero", docNumero);

            using var readerCabecera = await cmdCabecera.ExecuteReaderAsync();
            if (await readerCabecera.ReadAsync())
            {
                cabecera = new Dictionary<string, object>();
                for (int i = 0; i < readerCabecera.FieldCount; i++)
                {
                    cabecera[readerCabecera.GetName(i)] = readerCabecera.GetValue(i) == DBNull.Value ? "" : readerCabecera.GetValue(i);
                }
            }
            await readerCabecera.DisposeAsync();

            // Obtener líneas
            string sqlLineas = "SELECT * FROM AdcTra WHERE Doc_sucursal = @sucursal AND Doc_numero = @docNumero ORDER BY Tra_numlinea";
            using var cmdLineas = new SqlCommand(sqlLineas, connection);
            cmdLineas.Parameters.AddWithValue("@sucursal", sucursal);
            cmdLineas.Parameters.AddWithValue("@docNumero", docNumero);

            using var readerLineas = await cmdLineas.ExecuteReaderAsync();
            while (await readerLineas.ReadAsync())
            {
                var fila = new Dictionary<string, object>();
                for (int i = 0; i < readerLineas.FieldCount; i++)
                {
                    fila[readerLineas.GetName(i)] = readerLineas.GetValue(i) == DBNull.Value ? "" : readerLineas.GetValue(i);
                }
                lineas.Add(fila);
            }

            return (true, "Consulta exitosa", cabecera, lineas, lineas.Count);
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}", null, lineas, 0);
        }
        finally
        {
            if (connection != null) await connection.DisposeAsync();
            semaphore.Release();
        }
    }
}