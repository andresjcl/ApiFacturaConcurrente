namespace ApiFacturaConcurrente.Models.DTOs;

public class ArticuloGrupoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public bool? Art_sniva { get; set; }
    public string? Categoria { get; set; }
    public string? Clase { get; set; }
    public string? Grupo { get; set; }
    public string? Subgrupo { get; set; }
    public string? CodigoBase { get; set; }
    public string? COLOR { get; set; }
    public string? TALLA { get; set; }
    public string? Medida { get; set; }
    public string? CodCategoria { get; set; }
    public string? CodCLase { get; set; }
    public string? CodGrupo { get; set; }
    public string? CodSubgrupo { get; set; }
    public string? NomCategoria { get; set; }
    public string? NomCLase { get; set; }
    public string? NomGrupo { get; set; }
    public string? NomSubgrupo { get; set; }
    public decimal? Art_precvta1 { get; set; }
    public decimal? Art_precvta1_inc { get; set; }
    public decimal? Art_precvta2 { get; set; }
    public decimal? Art_precvta2_inc { get; set; }
    public decimal? Art_precvta3 { get; set; }
    public decimal? Art_precvta3_inc { get; set; }
    public decimal? Art_precvta4 { get; set; }
    public decimal? Art_precvta4_inc { get; set; }
    public decimal? Art_precvta5 { get; set; }
    public decimal? Art_precvta5_inc { get; set; }
    public int? Art_maxbod { get; set; }
    public int? Art_minbod { get; set; }
    public decimal? Art_CostoEstandard { get; set; }
    public decimal? Art_descuen { get; set; }
    public decimal? art_limDescuento { get; set; }
    public string? Art_idcontable { get; set; }
    public string? codProveedor { get; set; }
    public string? NomProveedor { get; set; }
}