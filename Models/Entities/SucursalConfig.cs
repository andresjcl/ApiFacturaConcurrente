using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiFacturaConcurrente.Models.Entities;

[Table("SucursalesConfig")]
public class SucursalConfig
{
    [Key]
    [MaxLength(3)]
    public string SucursalCodigo { get; set; } = string.Empty;

    [MaxLength(3)]
    public string Bodega { get; set; } = string.Empty;

    [MaxLength(10)]
    public string PuntoVta { get; set; } = string.Empty;

    [MaxLength(20)]
    public string NroIdDoc { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}