using System.ComponentModel.DataAnnotations;

namespace ApiFacturaConcurrente.Models.Entities;

public class ApiEmpresa
{
    [Key]
    [MaxLength(50)]
    public string EmpresaId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string EmpresaSecret { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}