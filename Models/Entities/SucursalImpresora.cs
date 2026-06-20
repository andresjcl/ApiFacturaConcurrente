using System.ComponentModel.DataAnnotations;

namespace ApiFacturaConcurrente.Models.Entities;

public class SucursalImpresora
{
    [Key]
    [MaxLength(3)]
    public string SucursalCodigo { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ImpresoraIP { get; set; }  // Ahora permite NULL

    public int? ImpresoraPuerto { get; set; }  // Ahora permite NULL

    [MaxLength(20)]
    public string? TipoImpresora { get; set; } = "LOCAL";

    [MaxLength(100)]
    public string? ImpresoraNombre { get; set; }

    public bool Activo { get; set; } = true;
}