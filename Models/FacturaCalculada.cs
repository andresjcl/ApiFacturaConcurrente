namespace ApiFacturaConcurrente.Models;

public class FacturaCalculada
{
    public decimal TotalBaseIva { get; set; }
    public decimal TotalBaseSinIva { get; set; }
    public decimal TotalIva { get; set; }
    public decimal TotalGeneral { get; set; }
    public decimal DescuentoGeneralPorcentaje { get; set; }
    public decimal DescuentoGeneralValor { get; set; }
    public string? DescuentoGeneralMotivo { get; set; }
    public decimal PorcentajeIva { get; set; }
    public List<LineaCalculada> Lineas { get; set; } = new();
    public List<LineaCalculada> TodasLineas { get; set; } = new();
}