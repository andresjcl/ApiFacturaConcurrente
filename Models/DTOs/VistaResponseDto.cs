namespace ApiFacturaConcurrente.Models.DTOs;

public class VistaResponseDto
{
    public bool Success { get; set; }
    public string? Mensaje { get; set; }
    public List<Dictionary<string, object>> Data { get; set; } = new();
    public int TotalRegistros { get; set; }
}