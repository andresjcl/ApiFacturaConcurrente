namespace ApiFacturaConcurrente.Models.DTOs;

public class ProveedorFacturaRequestDto
{
    // ==================== DATOS DE LA ORDEN ====================
    public string OrderId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;  // Uber, Rappi, Glovo
    public DateTime CreatedAt { get; set; }

    // ==================== DATOS DE LA SUCURSAL ====================
    public string Sucursal { get; set; } = string.Empty;
    public string? StoreCode { get; set; }

    // ==================== DATOS DEL CLIENTE ====================
    public string NombreCliente { get; set; } = string.Empty;
    public string? ApellidoCliente { get; set; }
    public string CiRuc { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    // ==================== TOTALES (ENVIADOS POR EL PROVEEDOR) ====================
    public decimal SubtotalWithoutTaxes { get; set; }    // Base imponible sin IVA
    public decimal TaxesPercentage { get; set; }         // Porcentaje de IVA (15, 8, 0)
    public decimal TaxValue { get; set; }                // Valor del IVA
    public decimal Total { get; set; }                   // Total de la factura

    // ==================== DESCUENTOS ====================
    public decimal DiscountPercentage { get; set; }      // Descuento porcentual
    public decimal DiscountValue { get; set; }           // Descuento en valor
    public string? DiscountReason { get; set; }          // Motivo del descuento

    // ==================== AGREGADORES DE PEDIDO ====================
    public List<ProveedorAgregadorDto> ExtraCharges { get; set; } = new();

    // ==================== PRODUCTOS ====================
    public List<ProveedorProductoDto> Products { get; set; } = new();

    // ==================== PAGOS ====================
    public List<ProveedorPagoDto> PaymentMethods { get; set; } = new();
}

public class ProveedorAgregadorDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal SubtotalWithoutTaxes { get; set; }
    public decimal TaxesPercentage { get; set; }
    public decimal TaxValue { get; set; }
    public decimal Total { get; set; }
}

public class ProveedorProductoDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }

    // Precios del producto principal
    public decimal SubtotalWithoutTaxes { get; set; }
    public decimal TaxesPercentage { get; set; }
    public decimal TaxValue { get; set; }
    public decimal Total { get; set; }

    // Modificadores (agregadores de texto)
    public List<string> Modifiers { get; set; } = new();

    // Agregadores producto
    public List<ProveedorProductoAgregadorDto> ProductAddons { get; set; } = new();
}

public class ProveedorProductoAgregadorDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal SubtotalWithoutTaxes { get; set; }
    public decimal TaxesPercentage { get; set; }
    public decimal TaxValue { get; set; }
    public decimal Total { get; set; }
}

public class ProveedorPagoDto
{
    public string Processor { get; set; } = string.Empty;  // CASH, CARD, UBER, RAPPI
    public decimal TotalBill { get; set; }
    public string? TransactionId { get; set; }
}