namespace ApiFacturaConcurrente.Models.DTOs;

public class FacturaRequestDto
{
    public string Sucursal { get; set; } = string.Empty;
    public DateTime? Fecha { get; set; }
    public string? Detalle { get; set; }
    public ClienteDto Cliente { get; set; } = new();
    public List<LineaDto> Lineas { get; set; } = new();
    public List<PagoDto> Pagos { get; set; } = new();
}

public class ClienteDto
{
    public string Codigo { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Ruc { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
}

public class LineaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal Precio { get; set; }
    public decimal Iva { get; set; }
    public string? Bodega { get; set; }
}

public class PagoDto
{
    public string Tipo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}