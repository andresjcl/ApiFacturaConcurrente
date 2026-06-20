using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiFacturaConcurrente.Models.Entities;

[Table("SucursalesServidores")]
public class SucursalServidor
{
    [Key]
    [MaxLength(3)]
    public string SucursalCodigo { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DatabaseName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? IvaretdaxDatabase { get; set; }

    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EmpresaId { get; set; }  // ← Agregar esta propiedad

    public bool Activo { get; set; } = true;

    public string ConnectionString =>
        $"Server={ServerName};Database={DatabaseName};User Id={UserId};Password={Password};TrustServerCertificate=True;Max Pool Size=100;Min Pool Size=5;Connection Timeout=30;";

    public string ConnectionStringIvaretdax =>
        $"Server={ServerName};Database={IvaretdaxDatabase ?? "Ivaretdax"};User Id={UserId};Password={Password};TrustServerCertificate=True;";

}