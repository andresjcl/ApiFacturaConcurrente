using ApiFacturaConcurrente.Models.DTOs;

namespace ApiFacturaConcurrente.Services.Adapters;

public class UberToFacturaAdapter
{
    public FacturaRequestDto Convertir(dynamic uberRequest, string sucursal)
    {
        // Extraer totales de payments.totals (primer elemento en USD)
        decimal subtotal = uberRequest.payments.totals[0].subtotalWithoutTaxes;
        decimal taxPercentage = uberRequest.payments.totals[0].taxesPercentage;
        decimal taxValue = uberRequest.payments.totals[0].taxValue;
        decimal total = uberRequest.payments.totals[0].total;

        // Extraer cliente
        string nombreCliente = uberRequest.client.name?.ToString() ?? "";
        string apellidoCliente = uberRequest.client.lastName?.ToString() ?? "";
        string ciRuc = uberRequest.client.govIdNumber?.ToString() ?? "";
        string telefono = uberRequest.client.phone?.ToString() ?? "";
        string email = uberRequest.client.email?.ToString() ?? "";
        string direccion = uberRequest.client.billingInformation?.address?.ToString() ?? "";

        // Construir líneas de productos
        var lineas = new List<FacturaLineaDto>();
        int numLinea = 1;

        foreach (var product in uberRequest.order.products)
        {
            // Producto principal
            lineas.Add(new FacturaLineaDto
            {
                NumLinea = numLinea++,
                Codigo = product.productId?.ToString() ?? "",
                Nombre = product.product?.ToString() ?? "",
                Cantidad = product.quantity ?? 1,
                PrecioUnitario = product.price.unitPrice.subtotalWithoutTaxes / (product.quantity ?? 1),
                Subtotal = product.price.unitPrice.subtotalWithoutTaxes,
                Iva = product.price.unitPrice.taxesPercentage,
                ValorIva = product.price.unitPrice.taxValue,
                PrecioTotal = product.price.unitPrice.total,
                ModificadoresTexto = ExtraerModificadoresTexto(product.modifierGroups),
                AgregadoresProducto = ExtraerAgregadoresProducto(product.modifierGroups)
            });
        }

        // Extraer formas de pago
        var pagos = new List<FacturaPagoDto>();
        foreach (var payment in uberRequest.payments.paymentMethods)
        {
            string idPago = payment.processor?.ToString() switch
            {
                "CASH" => "EFE",
                "CARD" => "TRJ",
                _ => "UBE"
            };

            pagos.Add(new FacturaPagoDto
            {
                Valor = payment.totalBill ?? 0,
                TipoPago = "4",
                Descripcion = uberRequest.account?.ToString() ?? "UBER EATS",
                IdPago = idPago
            });
        }

        return new FacturaRequestDto
        {
            Sucursal = sucursal,
            Fecha = uberRequest.createdAt,
            NombreCliente = string.IsNullOrEmpty(apellidoCliente) ? nombreCliente : $"{nombreCliente} {apellidoCliente}",
            CiRuc = ciRuc,
            Direccion = direccion,
            Telefono1 = telefono,
            CorreoCliente = email,
            PorcenIva = taxPercentage,
            ValorIva = taxValue,
            TotCiva = subtotal,
            ValorTotal = total,
            DescuentoPorcentaje = uberRequest.discountPercentage ?? 0,
            DescuentoMotivo = uberRequest.discountReason?.ToString(),
            Lineas = lineas,
            Pagos = pagos
        };
    }

    private List<string> ExtraerModificadoresTexto(dynamic modifierGroups)
    {
        var modificadores = new List<string>();

        if (modifierGroups == null) return modificadores;

        foreach (var group in modifierGroups)
        {
            if (group.selectedModifiers != null)
            {
                foreach (var modifier in group.selectedModifiers)
                {
                    if (modifier.price.unitPrice.total == 0)
                    {
                        modificadores.Add(modifier.product?.ToString() ?? "");
                    }
                }
            }
        }

        return modificadores;
    }

    private List<AgregadorProductoDto> ExtraerAgregadoresProducto(dynamic modifierGroups)
    {
        var agregadores = new List<AgregadorProductoDto>();

        if (modifierGroups == null) return agregadores;

        foreach (var group in modifierGroups)
        {
            if (group.selectedModifiers != null)
            {
                foreach (var modifier in group.selectedModifiers)
                {
                    if (modifier.price.unitPrice.total > 0)
                    {
                        agregadores.Add(new AgregadorProductoDto
                        {
                            Codigo = modifier.productId?.ToString() ?? "",
                            Nombre = modifier.product?.ToString() ?? "",
                            Cantidad = modifier.quantity ?? 1,
                            PrecioUnitario = modifier.price.unitPrice.subtotalWithoutTaxes / (modifier.quantity ?? 1),
                            Subtotal = modifier.price.unitPrice.subtotalWithoutTaxes,
                            Iva = modifier.price.unitPrice.taxesPercentage,
                            ValorIva = modifier.price.unitPrice.taxValue,
                            Total = modifier.price.unitPrice.total
                        });
                    }
                }
            }
        }

        return agregadores;
    }
}