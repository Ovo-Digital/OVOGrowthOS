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
    public DbSet<DealTemplate> DealTemplates => Set<DealTemplate>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

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
            e.Property<uint>("xmin").IsRowVersion();
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
            d.Property<uint>("xmin").IsRowVersion();
            d.ToTable("PartnershipDeals");
            d.HasIndex(x => new { x.BrandId, x.Status });
            d.HasIndex(x => x.BrandId).HasFilter("\"Status\" = 6").IsUnique();
            d.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<MonthlyPerformance>(p =>
        {
            p.Property<uint>("xmin").IsRowVersion();
            p.ToTable(t =>
            {
                t.HasCheckConstraint("CK_MonthlyPerformances_Month", "\"Month\" BETWEEN 1 AND 12");
                t.HasCheckConstraint("CK_MonthlyPerformances_Year", "\"Year\" BETWEEN 2020 AND 2100");
                t.HasCheckConstraint("CK_MonthlyPerformances_Counts", "\"Orders\" >= 0 AND \"Sessions\" >= 0 AND \"NewCustomers\" >= 0 AND \"ReturningCustomers\" >= 0");
                t.HasCheckConstraint("CK_MonthlyPerformances_Money", "\"GrossSales\" >= 0 AND \"Vat\" >= 0 AND \"Refunds\" >= 0 AND \"Cancellations\" >= 0 AND \"Chargebacks\" >= 0 AND \"Cogs\" >= 0 AND \"PaymentFees\" >= 0 AND \"FulfillmentCosts\" >= 0 AND \"ShippingSubsidy\" >= 0 AND \"OtherVariableCosts\" >= 0 AND \"MetaSpend\" >= 0 AND \"GoogleSpend\" >= 0 AND \"TikTokSpend\" >= 0 AND \"InfluencerSpend\" >= 0 AND \"OtherAdSpend\" >= 0");
            });
            p.HasIndex(x => new { x.BrandId, x.Year, x.Month }).IsUnique();
            p.HasMany(x => x.Adjustments).WithOne().HasForeignKey(x => x.MonthlyPerformanceId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AuditRecord>(a =>
        {
            a.ToTable("AuditTrail");
            a.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
        });
        modelBuilder.Entity<GeneralSettings>().Property<uint>("xmin").IsRowVersion();
        modelBuilder.Entity<DealTemplate>(t =>
        {
            t.HasIndex(x => new { x.Enabled, x.DisplayOrder });
            t.Property(x => x.Name).HasMaxLength(160);
        });
        modelBuilder.Entity<DocumentAttachment>(a =>
        {
            a.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            a.Property(x => x.FileName).HasMaxLength(255);
            a.Property(x => x.ContentType).HasMaxLength(120);
        });
        modelBuilder.Entity<UserAccount>(u =>
        {
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.Email).HasMaxLength(320);
            u.Property(x => x.Name).HasMaxLength(160);
            u.Property(x => x.Role).HasMaxLength(32);
            u.Property(x => x.PasswordHash).HasMaxLength(256);
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
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("Database")
            ?? "Host=localhost;Database=ovo_growth_os;Username=ovo;Password=ovo_dev_password";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "growth"))
            .Options;
        return new AppDbContext(options);
    }
}
