using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandEconomics> BrandEconomics => Set<BrandEconomics>();
    public DbSet<BrandEvaluation> Evaluations => Set<BrandEvaluation>();
    public DbSet<RuleSet> RuleSets => Set<RuleSet>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<PartnershipCondition> PartnershipConditions => Set<PartnershipCondition>();
    public DbSet<MonthlyPerformance> MonthlyPerformances => Set<MonthlyPerformance>();
    public DbSet<CommissionAdjustment> CommissionAdjustments => Set<CommissionAdjustment>();
    public DbSet<GeneralSettings> GeneralSettings => Set<GeneralSettings>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("growth");
        modelBuilder.Entity<Brand>(b =>
        {
            b.HasIndex(x => x.Name);
            b.HasIndex(x => x.Status);
            b.Property(x => x.Name).HasMaxLength(160);
            b.HasOne(x => x.Economics).WithOne().HasForeignKey<BrandEconomics>(x => x.BrandId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<BrandEvaluation>(e =>
        {
            e.HasIndex(x => new { x.BrandId, x.Status, x.CreatedAt });
            e.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.EvaluationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Scenarios).WithOne().HasForeignKey(x => x.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RuleSet>(r =>
        {
            r.HasIndex(x => new { x.Name, x.Version }).IsUnique();
            r.HasMany(x => x.Rules).WithOne(x => x.RuleSet).HasForeignKey(x => x.RuleSetId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Rule>().ToTable("RuleDefinitions");
        modelBuilder.Entity<Scenario>().HasIndex(x => new { x.EvaluationId, x.Name });
        modelBuilder.Entity<Deal>(d =>
        {
            d.ToTable("PartnershipDeals");
            d.HasIndex(x => new { x.BrandId, x.Status });
            d.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<MonthlyPerformance>(p =>
        {
            p.HasIndex(x => new { x.BrandId, x.Year, x.Month }).IsUnique();
            p.HasMany(x => x.Adjustments).WithOne().HasForeignKey(x => x.MonthlyPerformanceId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AuditRecord>(a =>
        {
            a.ToTable("AuditTrail");
            a.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
        });
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(4);
        }
    }
}

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=ovo_growth_os;Username=ovo;Password=ovo_dev_password",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "growth"))
            .Options;
        return new AppDbContext(options);
    }
}
