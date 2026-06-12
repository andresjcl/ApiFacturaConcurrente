namespace ApiFacturaConcurrente.Models.DTOs
{
    public class AgregadorProductoDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Cantidad { get; set; } = 1;
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal ValorIva { get; set; }
        public decimal Total { get; set; }

        public List<string> ModificadoresTexto { get; set; } = new();
    }

}
