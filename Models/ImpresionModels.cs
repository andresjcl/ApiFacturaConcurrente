using System;

namespace ApiFacturaConcurrente.Models;

public class ImpresionCabeceraDto
{
    public decimal Doc_numero { get; set; }
    public string Doc_sucursal { get; set; } = string.Empty;
    public string Opc_documento { get; set; } = string.Empty;
    public DateTime Doc_fecha { get; set; }
    public string Doc_NombreImp { get; set; } = string.Empty;
    public string Doc_CiRuc { get; set; } = string.Empty;
    public decimal Doc_valor { get; set; }
    public decimal Doc_totciva { get; set; }
    public decimal Doc_valoriva { get; set; }
    public decimal Doc_porceniva { get; set; }
    public string Doc_Direccion { get; set; } = string.Empty;
    public decimal Doc_porcendes1 { get; set; }
    public decimal Doc_valordes1 { get; set; }
    public string Doc_NroIdDoc { get; set; } = string.Empty;
    public decimal IdClaveDoc { get; set; }
}

public class ImpresionLineaDto
{
    public decimal Tra_numlinea { get; set; }
    public string Tra_Codigo { get; set; } = string.Empty;
    public string Tra_nombre { get; set; } = string.Empty;
    public decimal Tra_cantidad { get; set; }
    public decimal Tra_precuni { get; set; }
    public decimal Tra_prectot { get; set; }
    public decimal Tra_valor { get; set; }
}

public class ImpresionEmpresaDto
{
    public string Ruc { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}