namespace ApiFacturaConcurrente.Models.DTOs;

public class FacturaRequestDto
{
    // Datos de la sucursal
    public string Sucursal { get; set; } = string.Empty;
    public string? Bodega { get; set; }
    public string? PuntoVta { get; set; }
    public string? NroIdDoc { get; set; }

    // Datos de la factura
    public DateTime Fecha { get; set; }
    public string? Detalle { get; set; }    

    // Datos del cliente factura
    public string? NombreCliente { get; set; }
    public string? CodigoCliente { get; set; }
    public string? CiRuc { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? CorreoCliente { get; set; }

    // Totales
    public decimal PorcenIva { get; set; }
    public decimal ValorIva { get; set; }
    public decimal TotCiva { get; set; }
    public decimal TotSiva { get; set; }
    public decimal ValorTotal { get; set; }

    // Líneas de detalle
    public List<FacturaLineaDto> Lineas { get; set; } = new();

    // Pagos
    public List<FacturaPagoDto> Pagos { get; set; } = new();

    public ClienteIdentificacionDto ClienteIdentificacion { get; set; } = new();
}

public class FacturaLineaDto
{
    public int NumLinea { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal PrecioTotal { get; set; }
    public decimal Iva { get; set; }
    public DateTime? FechaLinea { get; set; }
}

public class FacturaPagoDto
{
    public decimal Valor { get; set; }
    public string? TipoPago { get; set; }
    public string? Descripcion { get; set; }
    public string? IdPago { get; set; }
}

public class ClienteIdentificacionDto
{
    public string Codigo { get; set; } = string.Empty;           // 10 dígitos (cedula, ruc, pasaporte)
    public string TipoIdentificacion { get; set; } = string.Empty; // C, R, P
    public string CedulaIdentidadRuc { get; set; } = string.Empty; // Número completo
    public string Nombres { get; set; } = string.Empty;
    public string? Apellidos { get; set; }
    public string NombreImpresion { get; set; } = string.Empty;
    public string? Domicilio { get; set; }
    public string? NumeroDomicilio { get; set; }
    public string? Sector { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Telefono3 { get; set; }
    public string? CorreoElectronico { get; set; }
    public string? Pais { get; set; }
    public string? Provincia { get; set; }
    public string? Ciudad { get; set; }
}