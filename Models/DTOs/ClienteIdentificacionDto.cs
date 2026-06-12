namespace ApiFacturaConcurrente.Models.DTOs
{
    public class ClienteIdentificacionDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string TipoIdentificacion { get; set; } = string.Empty;
        public string CedulaIdentidadRuc { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string? Apellidos { get; set; }
        public string NombreImpresion { get; set; } = string.Empty;
        public string? Domicilio { get; set; }
        public string? NumeroDomicilio { get; set; }
        public string? Sector { get; set; }
        public string? Telefono1 { get; set; }
        public string? Telefono2 { get; set; }
        public string? Telefono3 { get; set; }
        public string? CorreoElectronico { get; set; }
        public string? Pais { get; set; }
        public string? Provincia { get; set; }
        public string? Ciudad { get; set; }
    }
}
