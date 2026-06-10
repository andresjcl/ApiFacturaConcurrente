using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiFacturaConcurrente.Models.Entities;

[Table("PorcentajeIva", Schema = "dbo")]
public class PorcentajeIva
{
    [Key]
    public decimal Porcentaje { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime FechaFin { get; set; }

    public int clave { get; set; }
}