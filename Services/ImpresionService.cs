using ApiFacturaConcurrente.Data;
using ApiFacturaConcurrente.Models;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Printing;
using System.Net.Sockets;
using System.Text;

namespace ApiFacturaConcurrente.Services;

public class ImpresionService
{
    private readonly MasterDbContext _context;
    private readonly IConfiguration _configuration;

    public ImpresionService(MasterDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // ==================== IMPRIMIR LOCAL (USB) ====================

    private bool ImprimirLocal(string nombreImpresora, string ticket)
    {
        try
        {
            Console.WriteLine($"Intentando imprimir en impresora: {nombreImpresora}");

            using var printDocument = new PrintDocument();

            // Buscar la impresora por nombre exacto o por coincidencia parcial
            var impresoraEncontrada = PrinterSettings.InstalledPrinters
                .Cast<string>()
                .FirstOrDefault(p => p.Equals(nombreImpresora, StringComparison.OrdinalIgnoreCase) ||
                                      p.Contains(nombreImpresora) ||
                                      nombreImpresora.Contains(p));

            if (impresoraEncontrada == null)
            {
                Console.WriteLine($"❌ Impresora '{nombreImpresora}' no encontrada");
                Console.WriteLine($"Impresoras disponibles: {string.Join(", ", PrinterSettings.InstalledPrinters.Cast<string>())}");
                return false;
            }

            printDocument.PrinterSettings.PrinterName = impresoraEncontrada;
            Console.WriteLine($"Usando impresora: {impresoraEncontrada}");

            printDocument.PrintPage += (sender, e) =>
            {
                using var font = new Font("Courier New", 9, FontStyle.Regular);
                using var brush = new SolidBrush(Color.Black);

                float y = 0;
                float lineHeight = font.GetHeight(e.Graphics);
                var lines = ticket.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    e.Graphics.DrawString(line, font, brush, 0, y);
                    y += lineHeight;
                }
            };

            printDocument.Print();
            Console.WriteLine($"✅ Impreso LOCAL: {impresoraEncontrada}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en ImprimirLocal: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            return false;
        }
    }

    // ==================== IMPRIMIR RED (IP) ====================
    private async Task<bool> ImprimirRed(string ip, int puerto, string ticket)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ip, puerto);
            using var stream = client.GetStream();

            byte[] data = Encoding.UTF8.GetBytes(ticket);
            await stream.WriteAsync(data, 0, data.Length);

            Console.WriteLine($"✅ Impreso RED: {ip}:{puerto}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error impresión red: {ex.Message}");
            return false;
        }
    }

    // ==================== MÉTODO PRINCIPAL ====================

    //public async Task<bool> ImprimirFactura(string sucursal, ImpresionCabeceraDto cabecera, List<ImpresionLineaDto> lineas, ImpresionEmpresaDto empresa)
    //{
    //    try
    //    {
    //        var impresora = await _context.SucursalesImpresoras
    //            .FirstOrDefaultAsync(i => i.SucursalCodigo == sucursal && i.Activo);

    //        if (impresora == null)
    //        {
    //            Console.WriteLine($"No hay impresora configurada para sucursal {sucursal}");
    //            return false;
    //        }

    //        string ticket = GenerarTicket(cabecera, lineas, empresa);

    //        Console.WriteLine("========== TICKET A IMPRIMIR ==========");
    //        Console.WriteLine(ticket);
    //        Console.WriteLine("========================================");

    //        if (impresora.TipoImpresora == "RED" && !string.IsNullOrEmpty(impresora.ImpresoraIP))
    //        {
    //            int puerto = impresora.ImpresoraPuerto ?? 9100;
    //            return await ImprimirRed(impresora.ImpresoraIP, puerto, ticket);
    //        }
    //        else
    //        {
    //            string nombreImpresora = impresora.ImpresoraNombre ?? "Microsoft Print to PDF";
    //            return ImprimirLocal(nombreImpresora, ticket);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Error en ImprimirFactura: {ex.Message}");
    //        Console.WriteLine($"StackTrace: {ex.StackTrace}");
    //        return false;
    //    }
    //}

    //public async Task<bool> ImprimirFactura(string sucursal, ImpresionCabeceraDto cabecera, List<ImpresionLineaDto> lineas, ImpresionEmpresaDto empresa)
    //{
    //    // ==================== VERIFICAR SI LA IMPRESIÓN ESTÁ HABILITADA ====================
    //    bool impresionHabilitada = _configuration.GetValue<bool>("Impresion:Habilitada", true);

    //    if (!impresionHabilitada)
    //    {
    //        Console.WriteLine("⚠️ Impresión deshabilitada por configuración");
    //        return false;
    //    }

    //    try
    //    {
    //        var impresora = await _context.SucursalesImpresoras
    //            .FirstOrDefaultAsync(i => i.SucursalCodigo == sucursal && i.Activo);

    //        if (impresora == null)
    //        {
    //            Console.WriteLine($"No hay impresora configurada para sucursal {sucursal}");
    //            return false;
    //        }

    //        string ticket = GenerarTicket(cabecera, lineas, empresa);

    //        Console.WriteLine("========== TICKET A IMPRIMIR ==========");
    //        Console.WriteLine(ticket);
    //        Console.WriteLine("========================================");

    //        if (impresora.TipoImpresora == "RED" && !string.IsNullOrEmpty(impresora.ImpresoraIP))
    //        {
    //            int puerto = impresora.ImpresoraPuerto ?? 9100;
    //            return await ImprimirRed(impresora.ImpresoraIP, puerto, ticket);
    //        }
    //        else
    //        {
    //            string nombreImpresora = impresora.ImpresoraNombre ??
    //                _configuration.GetValue<string>("Impresion:NombreImpresoraLocal") ??
    //                "Microsoft Print to PDF";
    //            return ImprimirLocal(nombreImpresora, ticket);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Error en ImprimirFactura: {ex.Message}");
    //        return false;
    //    }
    //}

    public async Task<bool> ImprimirFactura(string sucursal, ImpresionCabeceraDto cabecera, List<ImpresionLineaDto> lineas, ImpresionEmpresaDto empresa)
    {
        bool impresionHabilitada = _configuration.GetValue<bool>("Impresion:Habilitada", true);

        if (!impresionHabilitada)
        {
            Console.WriteLine("⚠️ Impresión deshabilitada por configuración");
            return false;
        }

        try
        {
            var impresora = await _context.SucursalesImpresoras
                .FirstOrDefaultAsync(i => i.SucursalCodigo == sucursal && i.Activo);

            if (impresora == null)
            {
                Console.WriteLine($"No hay impresora configurada para sucursal {sucursal}");
                return false;
            }

            int numeroCopias = _configuration.GetValue<int>("Impresion:NumeroCopias", 3);

            // Generar los tickets una sola vez
            string ticketOriginal = GenerarTicket(cabecera, lineas, empresa, 1);
            string ticketCopiaCliente = GenerarTicket(cabecera, lineas, empresa, 2);
            string ticketCopiaArchivo = GenerarTicket(cabecera, lineas, empresa, 3);

            // Lista de tickets a imprimir
            var tickets = new List<(int copia, string ticket)>
        {
            (1, ticketOriginal),
            (2, ticketCopiaCliente),
            (3, ticketCopiaArchivo)
        };

            bool todasImpresas = true;

            foreach (var (copia, ticket) in tickets.Take(numeroCopias))
            {
                Console.WriteLine($"========== COPIA {copia} ==========");
                Console.WriteLine(ticket);
                Console.WriteLine("====================================");

                bool impreso = false;
                if (impresora.TipoImpresora == "RED" && !string.IsNullOrEmpty(impresora.ImpresoraIP))
                {
                    int puerto = impresora.ImpresoraPuerto ?? 9100;
                    impreso = await ImprimirRed(impresora.ImpresoraIP, puerto, ticket);
                }
                else
                {
                    string nombreImpresora = impresora.ImpresoraNombre ??
                        _configuration.GetValue<string>("Impresion:NombreImpresoraLocal") ??
                        "Microsoft Print to PDF";
                    impreso = ImprimirLocal(nombreImpresora, ticket);
                }

                if (!impreso)
                {
                    Console.WriteLine($"❌ Error al imprimir copia {copia}");
                    todasImpresas = false;
                }
                else
                {
                    Console.WriteLine($"✅ Copia {copia} impresa correctamente");
                }

                if (copia < numeroCopias)
                    await Task.Delay(500);
            }

            return todasImpresas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en ImprimirFactura: {ex.Message}");
            return false;
        }
    }


    private string GenerarTicket(ImpresionCabeceraDto cabecera, List<ImpresionLineaDto> lineas, ImpresionEmpresaDto empresa, int numeroCopia = 1)
    {
        var sb = new StringBuilder();
        int ancho = 48;

        // ==================== ENCABEZADO ====================
        sb.AppendLine(new string('=', ancho));
        sb.AppendLine(Centrar("ECUAVICHE S.A.", ancho));

        // RUC de la empresa
        if (!string.IsNullOrEmpty(empresa.Ruc))
            sb.AppendLine(Centrar("RUC: " + empresa.Ruc, ancho));

        if (!string.IsNullOrEmpty(empresa.Direccion))
            sb.AppendLine(Centrar("DIRECCIÓN: " + empresa.Direccion, ancho));

        sb.AppendLine();

        // Número de factura
        string nroFactura = cabecera.Doc_NroIdDoc + "-" + cabecera.Doc_numero.ToString("0").PadLeft(9, '0');
        sb.AppendLine(Centrar("FACTURA N°: " + nroFactura, ancho));

        // ==================== INDICADOR DE COPIA ====================
        if (numeroCopia > 1)
        {
            string textoCopia = numeroCopia == 2 ? "COPIA 2" : "COPIA 3";
            sb.AppendLine(Centrar("*** " + textoCopia + " ***", ancho));
        }
        else
        {
            sb.AppendLine(Centrar("*** ORIGINAL ***", ancho));
        }

        sb.AppendLine(new string('-', ancho));

        // ==================== CLIENTE ====================
        sb.AppendLine("CLIENTE: " + cabecera.Doc_NombreImp);
        sb.AppendLine("FECHA: " + cabecera.Doc_fecha.ToString("dd/MM/yyyy HH:mm:ss"));
        sb.AppendLine("RUC/CI: " + cabecera.Doc_CiRuc);
        if (!string.IsNullOrEmpty(cabecera.Doc_Direccion))
            sb.AppendLine("DIR: " + cabecera.Doc_Direccion);
        if (!string.IsNullOrEmpty(empresa.Email))
            sb.AppendLine("EMAIL: " + empresa.Email);
        sb.AppendLine(new string('-', ancho));

        // ==================== PRODUCTOS ====================
        sb.AppendLine("CANT DESCRIPCION                 P.UNIT   TOTAL");
        sb.AppendLine(new string('-', ancho));

        foreach (var linea in lineas)
        {
            string nombre = linea.Tra_nombre ?? "";
            int anchoDescripcion = 24;
            int anchoCantidad = 4;
            int anchoPrecio = 8;
            int anchoTotal = 8;

            string cantidad = linea.Tra_cantidad.ToString("0").PadLeft(anchoCantidad);
            string precioUnitario = linea.Tra_precuni.ToString("0.00").PadLeft(anchoPrecio);
            string subtotal = linea.Tra_prectot.ToString("0.00").PadLeft(anchoTotal);

            List<string> lineasDescripcion = DividirTexto(nombre, anchoDescripcion);

            sb.AppendLine(cantidad + " " + lineasDescripcion[0].PadRight(anchoDescripcion) + " " + precioUnitario + " " + subtotal);

            for (int i = 1; i < lineasDescripcion.Count; i++)
            {
                sb.AppendLine("".PadLeft(anchoCantidad + 1) + lineasDescripcion[i].PadRight(anchoDescripcion));
            }
        }

        sb.AppendLine(new string('-', ancho));

        // ==================== TOTALES ====================
        int anchoTicket = 48;
        var totales = new List<(string etiqueta, decimal valor)>();
        totales.Add(("SUBTOTAL:", cabecera.Doc_totciva));
        totales.Add(($"IVA {cabecera.Doc_porceniva.ToString("0.00")}%:", cabecera.Doc_valoriva));
        totales.Add(("TOTAL:", cabecera.Doc_valor));

        if (cabecera.Doc_porcendes1 > 0)
        {
            totales.Add(($"DESC {cabecera.Doc_porcendes1.ToString("0.00")}%:", cabecera.Doc_valordes1));
        }

        int anchoMaxEtiqueta = totales.Max(t => t.etiqueta.Length);
        int anchoNumero = 10;

        foreach (var item in totales)
        {
            string linea = item.etiqueta.PadRight(anchoMaxEtiqueta + 4) + item.valor.ToString("0.00").PadLeft(anchoNumero);
            sb.AppendLine(linea.PadLeft(anchoTicket));
        }

        // ==================== PIE DE PÁGINA ====================
        sb.AppendLine(new string('=', ancho));
        sb.AppendLine(Centrar("¡GRACIAS POR SU COMPRA!", ancho));
        sb.AppendLine(Centrar("VUELVA PRONTO", ancho));
        sb.AppendLine(new string('=', ancho));

        // ==================== INDICADOR DE COPIA AL FINAL ====================
        if (numeroCopia > 1)
        {
            string textoCopia = numeroCopia == 2 ? "COPIA CLIENTE" : "COPIA ARCHIVO";
            sb.AppendLine(Centrar("*** " + textoCopia + " ***", ancho));
            sb.AppendLine(new string('=', ancho));
        }

        return sb.ToString();
    }

    //private string GenerarTicket(ImpresionCabeceraDto cabecera, List<ImpresionLineaDto> lineas, ImpresionEmpresaDto empresa)
    //{
    //    var sb = new StringBuilder();
    //    int ancho = 48;

    //    // ==================== ENCABEZADO ====================
    //    sb.AppendLine(new string('=', ancho));
    //    sb.AppendLine(Centrar("ECUAVICHE S.A.", ancho));

    //    // RUC de la empresa debajo del nombre
    //    if (!string.IsNullOrEmpty(empresa.Ruc))
    //        sb.AppendLine(Centrar("RUC: " + empresa.Ruc, ancho));        

    //    // RUC de la empresa debajo del nombre
    //    if (!string.IsNullOrEmpty(empresa.Direccion))
    //        sb.AppendLine(Centrar("DIRECCIÓN: " + empresa.Direccion, ancho));

    //    sb.AppendLine();

    //    // Número de factura con formato (manejo seguro)
    //    string nroFactura = cabecera.Doc_NroIdDoc + "-" + cabecera.Doc_numero.ToString("0").PadLeft(9, '0');
    //    sb.AppendLine(Centrar("FACTURA N°: " + nroFactura, ancho));
    //    sb.AppendLine(new string('-', ancho));



    //    // ==================== CLIENTE ====================
    //    sb.AppendLine("CLIENTE: " + cabecera.Doc_NombreImp);
    //    sb.AppendLine("FECHA: " + cabecera.Doc_fecha.ToString("dd/MM/yyyy HH:mm:ss"));
    //    sb.AppendLine("RUC/CI: " + cabecera.Doc_CiRuc);
    //    if (!string.IsNullOrEmpty(cabecera.Doc_Direccion))
    //        sb.AppendLine("DIR: " + cabecera.Doc_Direccion);
    //    if (!string.IsNullOrEmpty(empresa.Email))
    //        sb.AppendLine("EMAIL: " + empresa.Email);
    //    sb.AppendLine(new string('-', ancho));

    //    // ==================== PRODUCTOS (con salto de línea automático) ====================
    //    sb.AppendLine("CANT DESCRIPCION                 P.UNIT   TOTAL");
    //    sb.AppendLine(new string('-', ancho));

    //    foreach (var linea in lineas)
    //    {
    //        string nombre = linea.Tra_nombre ?? "";
    //        int anchoDescripcion = 24;
    //        int anchoCantidad = 4;
    //        int anchoPrecio = 8;
    //        int anchoTotal = 8;

    //        string cantidad = linea.Tra_cantidad.ToString("0").PadLeft(anchoCantidad);
    //        string precioUnitario = linea.Tra_precuni.ToString("0.00").PadLeft(anchoPrecio);
    //        string subtotal = linea.Tra_prectot.ToString("0.00").PadLeft(anchoTotal);

    //        // Dividir la descripción en líneas
    //        List<string> lineasDescripcion = DividirTexto(nombre, anchoDescripcion);

    //        // Mostrar primera línea con todos los datos
    //        sb.AppendLine(cantidad + " " + lineasDescripcion[0].PadRight(anchoDescripcion) + " " + precioUnitario + " " + subtotal);

    //        // Mostrar líneas adicionales solo con descripción
    //        for (int i = 1; i < lineasDescripcion.Count; i++)
    //        {
    //            sb.AppendLine("".PadLeft(anchoCantidad + 1) + lineasDescripcion[i].PadRight(anchoDescripcion));
    //        }
    //    }

    //    sb.AppendLine(new string('-', ancho));

    //    // ==================== TOTALES (alineación perfecta) ====================
    //    int anchoTicket = 48;

    //    // Lista de totales
    //    var totales = new List<(string etiqueta, decimal valor)>();
    //    totales.Add(("SUBTOTAL:", cabecera.Doc_totciva));
    //    totales.Add(($"IVA {cabecera.Doc_porceniva.ToString("0.00")}%:", cabecera.Doc_valoriva));
    //    totales.Add(("TOTAL:", cabecera.Doc_valor));

    //    if (cabecera.Doc_porcendes1 > 0)
    //    {
    //        totales.Add(($"DESC {cabecera.Doc_porcendes1.ToString("0.00")}%:", cabecera.Doc_valordes1));
    //    }

    //    // Calcular el ancho máximo de las etiquetas
    //    int anchoMaxEtiqueta = totales.Max(t => t.etiqueta.Length);

    //    // Ancho fijo para los números (para que queden alineados)
    //    int anchoNumero = 10;

    //    // Generar líneas con alineación perfecta
    //    foreach (var item in totales)
    //    {
    //        // La etiqueta ocupa el ancho máximo + 4 espacios de separación
    //        string linea = item.etiqueta.PadRight(anchoMaxEtiqueta + 4) + item.valor.ToString("0.00").PadLeft(anchoNumero);
    //        sb.AppendLine(linea.PadLeft(anchoTicket));
    //    }

    //    // ==================== PIE DE PÁGINA ====================
    //    sb.AppendLine(new string('=', ancho));
    //    sb.AppendLine(Centrar("¡GRACIAS POR SU COMPRA!", ancho));
    //    sb.AppendLine(Centrar("VUELVA PRONTO", ancho));
    //    sb.AppendLine(new string('=', ancho));

    //    return sb.ToString();
    //}



    private string Centrar(string texto, int ancho)
    {
        if (string.IsNullOrEmpty(texto)) return new string(' ', ancho);
        if (texto.Length >= ancho) return texto;

        int espacios = (ancho - texto.Length) / 2;
        return new string(' ', espacios) + texto;
    }

    /// <summary>
    /// Divide un texto en líneas sin cortar palabras
    /// </summary>
    private List<string> DividirTexto(string texto, int maxLength)
    {
        List<string> lineas = new List<string>();

        if (string.IsNullOrEmpty(texto))
        {
            lineas.Add("");
            return lineas;
        }

        if (texto.Length <= maxLength)
        {
            lineas.Add(texto);
            return lineas;
        }

        string[] palabras = texto.Split(' ');
        string lineaActual = "";

        foreach (string palabra in palabras)
        {
            // Si la palabra es más larga que el máximo, dividirla
            if (palabra.Length > maxLength)
            {
                if (lineaActual.Length > 0)
                {
                    lineas.Add(lineaActual);
                    lineaActual = "";
                }

                // Dividir palabra larga en partes
                for (int i = 0; i < palabra.Length; i += maxLength)
                {
                    if (i + maxLength < palabra.Length)
                        lineas.Add(palabra.Substring(i, maxLength));
                    else
                        lineaActual = palabra.Substring(i);
                }
                continue;
            }

            // Verificar si la palabra cabe en la línea actual
            if (lineaActual.Length + palabra.Length + 1 <= maxLength)
            {
                if (lineaActual.Length > 0)
                    lineaActual += " " + palabra;
                else
                    lineaActual = palabra;
            }
            else
            {
                if (lineaActual.Length > 0)
                    lineas.Add(lineaActual);

                lineaActual = palabra;
            }
        }

        if (lineaActual.Length > 0)
            lineas.Add(lineaActual);

        return lineas;
    }



    
}