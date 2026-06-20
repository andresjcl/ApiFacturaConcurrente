namespace ApiFacturaConcurrente.Models.DTOs;

public class FacturaResponseDto
{
    public bool Success { get; set; }
    public string? Sucursal { get; set; }
    public decimal DocNumero { get; set; }
    public decimal IdClaveDoc { get; set; }
    public decimal Total { get; set; }
    public string? Mensaje { get; set; }
}