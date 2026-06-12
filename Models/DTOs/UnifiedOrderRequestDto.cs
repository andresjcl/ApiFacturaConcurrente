namespace ApiFacturaConcurrente.Models.DTOs;

public class UnifiedOrderRequestDto
{
    public string Proveedor { get; set; } = string.Empty;  // UBER, RAPPI, GLOVO
    public string OrderId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Sucursal { get; set; } = string.Empty;

    public UnifiedClienteDto Cliente { get; set; } = new();
    public UnifiedTotalesDto Totales { get; set; } = new();
    public UnifiedDescuentosDto Descuentos { get; set; } = new();
    public List<UnifiedCargoExtraDto> CargosExtras { get; set; } = new();
    public List<UnifiedProductoDto> Productos { get; set; } = new();
    public List<UnifiedPagoDto> Pagos { get; set; } = new();
}

public class UnifiedClienteDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Apellido { get; set; }
    public string CiRuc { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
}

public class UnifiedTotalesDto
{
    public decimal Subtotal { get; set; }
    public decimal IvaPorcentaje { get; set; }
    public decimal IvaValor { get; set; }
    public decimal Total { get; set; }
}

public class UnifiedDescuentosDto
{
    public decimal Porcentaje { get; set; }
    public decimal Valor { get; set; }
    public string? Motivo { get; set; }
}

public class UnifiedCargoExtraDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal IvaPorcentaje { get; set; }
    public decimal Subtotal { get; set; }
    public decimal IvaValor { get; set; }
    public decimal Total { get; set; }
}

public class UnifiedProductoDto
{
    public int NumLinea { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal IvaPorcentaje { get; set; }
    public decimal IvaValor { get; set; }
    public decimal Total { get; set; }

    public List<string> ModificadoresTexto { get; set; } = new();
    public List<UnifiedAgregadorProductoDto> AgregadoresProducto { get; set; } = new();
}

public class UnifiedAgregadorProductoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal IvaPorcentaje { get; set; }
    public decimal IvaValor { get; set; }
    public decimal Total { get; set; }
}

public class UnifiedPagoDto
{
    public string Procesador { get; set; } = string.Empty;  // CASH, CARD, UBER, RAPPI
    public decimal Valor { get; set; }
    public string CodigoPago { get; set; } = string.Empty;  // EFE, TRJ, GLO, RAP, UBE
    public string? Descripcion { get; set; }
    public string? TransaccionId { get; set; }
}