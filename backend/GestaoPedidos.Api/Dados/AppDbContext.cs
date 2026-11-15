using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GestaoPedidos.Api.Dados;

// Mapeia o schema criado por database/01-schema.sql. Sem migrations.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>().ToTable("Clientes");

        modelBuilder.Entity<Produto>(produto =>
        {
            produto.ToTable("Produtos");
            produto.Property(p => p.Preco).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Pedido>(pedido =>
        {
            pedido.ToTable("Pedidos");
            pedido.Property(p => p.ValorTotal).HasPrecision(18, 2);
            pedido.HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId);
            pedido.HasMany(p => p.Itens).WithOne().HasForeignKey(i => i.PedidoId);
        });

        modelBuilder.Entity<ItemPedido>(item =>
        {
            item.ToTable("ItensPedido");
            item.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
            item.HasOne(i => i.Produto).WithMany().HasForeignKey(i => i.ProdutoId);
        });
    }

    // As datas são gravadas em UTC, mas o SQL Server não guarda o fuso.
    // Marcar como UTC na leitura faz o JSON sair com o "Z".
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<DataUtcConverter>();
    }

    private class DataUtcConverter() : ValueConverter<DateTime, DateTime>(
        data => data,
        data => DateTime.SpecifyKind(data, DateTimeKind.Utc));
}
