namespace ApiFacturaConcurrente.Models;

public class LineaCalculada
{
    public int NumLineaGlobal { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DescuentoPorcentaje { get; set; }
    public decimal DescuentoValor { get; set; }
    public string? DescuentoMotivo { get; set; }
    public decimal SubtotalConDescuento { get; set; }
    public decimal IvaPorcentaje { get; set; }
    public decimal IvaValor { get; set; }
    public decimal Total { get; set; }
    public bool TieneIva { get; set; }
    public bool Sncomp { get; set; }
    public string ArtClase { get; set; } = string.Empty;
    public bool EsServicio { get; set; }
}