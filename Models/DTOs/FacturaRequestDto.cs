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

    // ==================== TOTALES (OPCIONALES - EL SISTEMA LOS CALCULA) ====================
    public decimal PorcenIva { get; set; }      // Si viene 0, el sistema lo obtiene de Ivaretdax
    public decimal ValorIva { get; set; }       // Si viene 0, el sistema lo calcula
    public decimal TotCiva { get; set; }        // Si viene 0, el sistema lo calcula
    public decimal TotSiva { get; set; }        // Si viene 0, el sistema lo calcula
    public decimal ValorTotal { get; set; }     // Si viene 0, el sistema lo calcula

    // ==================== DESCUENTOS GENERALES ====================
    public decimal DescuentoPorcentaje { get; set; }
    public decimal DescuentoValor { get; set; }
    public string? DescuentoMotivo { get; set; }

    // ==================== INDICADOR DE PRECIO ====================
    public bool PrecioIncluyeIva { get; set; } = true; // true = precio CON IVA, false = precio SIN IVA

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
    public decimal Subtotal { get; set; }      // Opcional - el sistema lo calcula
    public decimal Iva { get; set; }           // Opcional - el sistema lo calcula
    public decimal ValorIva { get; set; }      // Opcional - el sistema lo calcula
    public decimal Total { get; set; }         // Opcional - el sistema lo calcula
    public bool AfectaBaseImponible { get; set; } = true;
}

public class FacturaLineaSimpleDto
{
    public int NumLinea { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }  // ← Puede ser CON o SIN IVA según PrecioIncluyeIva
    public decimal Subtotal { get; set; }        // Opcional - el sistema lo calcula
    public decimal PrecioTotal { get; set; }     // Opcional - el sistema lo calcula
    public decimal Iva { get; set; }             // Opcional - el sistema lo calcula
    public decimal ValorIva { get; set; }        // Opcional - el sistema lo calcula
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
    public decimal Subtotal { get; set; }        // Opcional - el sistema lo calcula
    public decimal Iva { get; set; }             // Opcional - el sistema lo calcula
    public decimal ValorIva { get; set; }        // Opcional - el sistema lo calcula
    public decimal Total { get; set; }           // Opcional - el sistema lo calcula
    public List<string> ModificadoresTexto { get; set; } = new();
}

public class FacturaPagoDto
{
    public string? IdPago { get; set; }
    public string? Descripcion { get; set; }
}