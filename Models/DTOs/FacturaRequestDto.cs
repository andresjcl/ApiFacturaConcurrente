namespace ApiFacturaConcurrente.Models.DTOs;

public class FacturaRequestDto
{
    // ==================== DATOS DE SUCURSAL ====================
    public string Sucursal { get; set; } = string.Empty;
    public string? Bodega { get; set; }
    public string? PuntoVta { get; set; }
    public string? NroIdDoc { get; set; }

    // ==================== DATOS DEL CLIENTE ====================
    public string? CodigoCliente { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string CiRuc { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? CorreoCliente { get; set; }

    // ==================== DATOS DE LA FACTURA ====================
    public DateTime Fecha { get; set; }
    public string? Detalle { get; set; }

    // ==================== TOTALES ====================
    public decimal PorcenIva { get; set; }
    public decimal ValorIva { get; set; }
    public decimal TotCiva { get; set; }
    public decimal TotSiva { get; set; }
    public decimal ValorTotal { get; set; }

    // ==================== DESCUENTOS GENERALES ====================
    public decimal DescuentoPorcentaje { get; set; }
    public decimal DescuentoValor { get; set; }
    public string? DescuentoMotivo { get; set; }

    // ==================== AGREGADORES DE PEDIDO ====================
    public List<AgregadorPedidoDto> AgregadoresPedido { get; set; } = new();

    // ==================== LÍNEAS DE PRODUCTOS ====================
    public List<FacturaLineaSimpleDto> Lineas { get; set; } = new();

    // ==================== PAGOS ====================
    public List<FacturaPagoDto> Pagos { get; set; } = new();
}

public class AgregadorPedidoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal ValorIva { get; set; }
    public decimal Total { get; set; }
    public bool AfectaBaseImponible { get; set; } = true;
}

public class FacturaLineaSimpleDto
{
    public int NumLinea { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal PrecioTotal { get; set; }
    public decimal Iva { get; set; }
    public decimal ValorIva { get; set; }
    public decimal DescuentoPorcentaje { get; set; }
    public decimal DescuentoValor { get; set; }
    public string? DescuentoMotivo { get; set; }
    public List<string> ModificadoresTexto { get; set; } = new();
    public List<AgregadorProductoDto> AgregadoresProducto { get; set; } = new();
}

public class AgregadorProductoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal ValorIva { get; set; }
    public decimal Total { get; set; }
    public List<string> ModificadoresTexto { get; set; } = new();
}

public class FacturaPagoDto
{    
    public string? IdPago { get; set; }
    public string? Descripcion { get; set; }
}