namespace ApiFacturaConcurrente.Models.DTOs
{

    public class FacturaLineaDto
    {
        public int NumLinea { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal ValorIva { get; set; }
        public decimal PrecioTotal { get; set; }

        public List<string> ModificadoresTexto { get; set; } = new();
        public List<AgregadorProductoDto> AgregadoresProducto { get; set; } = new();

        public decimal DescuentoPorcentaje { get; set; }
        public decimal DescuentoValor { get; set; }
        public string? DescuentoMotivo { get; set; }
        public DateTime? FechaLinea { get; set; }
    }

 
}
