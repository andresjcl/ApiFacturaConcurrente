using System.ComponentModel.DataAnnotations;

namespace ApiFacturaConcurrente.Models.Entities;

public class Identificacion
{
    [Key]
    [MaxLength(15)]
    public string Codigo { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? EmpresaSecret { get; set; }

    [MaxLength(50)]
    public string? TipoIdentificacion { get; set; }

    [MaxLength(20)]
    public string? CedulaIdentidadRuc { get; set; }

    [MaxLength(150)]
    public string? NombreImpresion { get; set; }

    [MaxLength(250)]
    public string? Domicilio { get; set; }

    [MaxLength(20)]
    public string? Telefono1 { get; set; }

    [MaxLength(100)]
    public string? CorreoElectrónico { get; set; }

    public bool EsCliente { get; set; }
}