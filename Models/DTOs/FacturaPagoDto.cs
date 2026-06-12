namespace ApiFacturaConcurrente.Models.DTOs
{
    public class FacturaPagoDto
    {
        public decimal Valor { get; set; }
        public string? TipoPago { get; set; }
        public string? Descripcion { get; set; }
        public string? IdPago { get; set; }
    }
}
