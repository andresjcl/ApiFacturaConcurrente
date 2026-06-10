using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiFacturaConcurrente.Models.Entities;

[Table("ADCART")]
public class AdcArt
{
    [Key]
    [Column("Art_codigo")]
    [MaxLength(20)]
    public string ArtCodigo { get; set; } = string.Empty;

    [Column("Art_nombre")]
    [MaxLength(120)]
    public string? ArtNombre { get; set; }

    [Column("Art_sniva")]
    public int? ArtSniva { get; set; }  // 1 = tiene IVA, 0 = no tiene IVA

    [Column("Art_PorIVA")]
    public decimal? ArtPorIva { get; set; }  // Porcentaje de IVA del producto

    [Column("Art_precvta1")]
    public decimal? ArtPrecioVta1 { get; set; }
}