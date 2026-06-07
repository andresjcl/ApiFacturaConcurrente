using System.ComponentModel.DataAnnotations;

namespace ApiFacturaConcurrente.Models.Entities;

public class ApiEmpresaSucursal
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string EmpresaId { get; set; } = string.Empty;

    [MaxLength(3)]
    public string SucursalCodigo { get; set; } = string.Empty;

    [MaxLength(3)]
    public string TipoDocumento { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}