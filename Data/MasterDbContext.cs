using Microsoft.EntityFrameworkCore;
using ApiFacturaConcurrente.Models.Entities;

namespace ApiFacturaConcurrente.Data;

public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApiEmpresa> ApiEmpresas { get; set; }
    public DbSet<SucursalServidor> SucursalesServidores { get; set; }
    public DbSet<ApiEmpresaSucursal> ApiEmpresaSucursales { get; set; }
}