using System.ComponentModel.DataAnnotations;

namespace ApiFacturaConcurrente.Models.Entities;

public class ApiEmpresa
{
    [Key]
    [MaxLength(50)]
    public string EmpresaId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string EmpresaSecret { get; set; } = string.Empty;

    // ==================== NUEVOS CAMPOS ====================
    [MaxLength(100)]
    public string? ClientId { get; set; }      // ← Agregar esta propiedad

    [MaxLength(512)]
    public string? ClientSecret { get; set; }  // ← Agregar esta propiedad
                                               // ======================================================

    public bool Activo { get; set; } = true;

    public string? TokenActual { get; set; }

    public DateTime? TokenExpiracion { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}