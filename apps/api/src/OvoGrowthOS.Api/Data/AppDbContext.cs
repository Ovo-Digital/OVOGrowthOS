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
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<BrandFollowUp> BrandFollowUps => Set<BrandFollowUp>();
    public DbSet<BrandContactNote> BrandContactNotes => Set<BrandContactNote>();
    public DbSet<CollectionAccount> CollectionAccounts => Set<CollectionAccount>();
    public DbSet<CollectionPayment> CollectionPayments => Set<CollectionPayment>();
    public DbSet<ServiceCostAccount> ServiceCostAccounts => Set<ServiceCostAccount>();
    public DbSet<ServiceCostEntry> ServiceCostEntries => Set<ServiceCostEntry>();
    public DbSet<ServiceCostReview> ServiceCostReviews => Set<ServiceCostReview>();
    public DbSet<InvestmentAccount> InvestmentAccounts => Set<InvestmentAccount>();
    public DbSet<InvestmentEntry> InvestmentEntries => Set<InvestmentEntry>();
    public DbSet<PortalAccess> PortalAccesses => Set<PortalAccess>();
    public DbSet<PortalReport> PortalReports => Set<PortalReport>();
    public DbSet<PortalDocumentShare> PortalDocumentShares => Set<PortalDocumentShare>();
    public DbSet<PortalQuestion> PortalQuestions => Set<PortalQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("growth");
        modelBuilder.Entity<PortalAccess>(a =>
        {
            a.HasKey(x => x.UserId);
            a.HasAlternateKey(x => new { x.UserId, x.BrandId });
            a.HasOne(x => x.User).WithOne().HasForeignKey<PortalAccess>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            a.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
            a.HasIndex(x => x.BrandId);
        });
        modelBuilder.Entity<PortalReport>(r =>
        {
            r.HasAlternateKey(x => new { x.Id, x.BrandId });
            r.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne<MonthlyPerformance>().WithMany().HasForeignKey(x => x.PerformanceId).OnDelete(DeleteBehavior.Restrict);
            r.HasIndex(x => new { x.BrandId, x.Year, x.Month, x.Version }).IsUnique();
            r.ToTable(t => t.HasCheckConstraint("CK_PortalReports_Period", "\"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12 AND \"Version\" > 0"));
        });
        modelBuilder.Entity<PortalDocumentShare>(d =>
        {
            d.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
            d.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            d.HasIndex(x => x.BrandId);
            d.HasIndex(x => x.DocumentId).HasFilter("\"RevokedAt\" IS NULL").IsUnique();
        });
        modelBuilder.Entity<PortalQuestion>(q =>
        {
            q.HasOne<PortalReport>().WithMany().HasForeignKey(x => new { x.ReportId, x.BrandId }).HasPrincipalKey(x => new { x.Id, x.BrandId }).OnDelete(DeleteBehavior.Restrict);
            q.HasOne<PortalAccess>().WithMany().HasForeignKey(x => new { x.UserId, x.BrandId }).HasPrincipalKey(x => new { x.UserId, x.BrandId }).OnDelete(DeleteBehavior.Restrict);
            q.Property(x => x.Question).HasMaxLength(2000); q.Property(x => x.Answer).HasMaxLength(4000);
            q.Property(x => x.AnsweredAt).IsConcurrencyToken();
            q.HasIndex(x => new { x.BrandId, x.CreatedAt });
        });
        modelBuilder.Entity<ServiceCostAccount>(a =>
        {
            a.HasKey(x => x.MonthlyPerformanceId);
            a.HasOne<MonthlyPerformance>().WithOne().HasForeignKey<ServiceCostAccount>(x => x.MonthlyPerformanceId).OnDelete(DeleteBehavior.Restrict);
            a.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.MonthlyPerformanceId).OnDelete(DeleteBehavior.Restrict);
            a.HasMany(x => x.Reviews).WithOne().HasForeignKey(x => x.MonthlyPerformanceId).OnDelete(DeleteBehavior.Restrict);
            a.Property(x => x.LastReviewReason).HasMaxLength(1000);
            a.Property(x => x.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<ServiceCostReview>(r =>
        {
            r.Property(x => x.Reason).HasMaxLength(1000);
            r.HasIndex(x => new { x.MonthlyPerformanceId, x.CreatedAt });
        });
        modelBuilder.Entity<ServiceCostEntry>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(160); e.Property(x => x.Description).HasMaxLength(1000); e.Property(x => x.VoidReason).HasMaxLength(1000);
            e.HasIndex(x => x.Reference).HasFilter("\"VoidedAt\" IS NULL").IsUnique();
            e.HasIndex(x => new { x.MonthlyPerformanceId, x.IncurredOn });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ServiceCostEntries_Amount", "\"Amount\" > 0");
                t.HasCheckConstraint("CK_ServiceCostEntries_Kind", "(\"Kind\" = 0 AND \"Hours\" IS NULL AND \"HourlyCost\" IS NULL) OR (\"Kind\" = 1 AND \"Hours\" IS NOT NULL AND \"HourlyCost\" IS NOT NULL AND \"Hours\" > 0 AND \"HourlyCost\" > 0 AND \"Amount\" = round(\"Hours\" * \"HourlyCost\", 4))");
                t.HasCheckConstraint("CK_ServiceCostEntries_Reference", "length(btrim(\"Reference\")) > 0");
                t.HasCheckConstraint("CK_ServiceCostEntries_Void", "\"VoidedAt\" IS NULL OR (length(btrim(\"VoidReason\")) > 0 AND length(btrim(\"VoidedBy\")) > 0)");
            });
        });
        modelBuilder.Entity<InvestmentAccount>(a =>
        {
            a.HasKey(x => x.DealId);
            a.HasOne<Deal>().WithOne().HasForeignKey<InvestmentAccount>(x => x.DealId).OnDelete(DeleteBehavior.Restrict);
            a.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Restrict);
            a.Property(x => x.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<InvestmentEntry>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(160); e.Property(x => x.Description).HasMaxLength(1000); e.Property(x => x.VoidReason).HasMaxLength(1000);
            e.HasIndex(x => x.Reference).HasFilter("\"VoidedAt\" IS NULL").IsUnique();
            e.HasIndex(x => new { x.DealId, x.OccurredOn });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InvestmentEntries_Amount", "\"Amount\" > 0 AND \"Kind\" IN (0, 1)");
                t.HasCheckConstraint("CK_InvestmentEntries_Reference", "length(btrim(\"Reference\")) > 0");
                t.HasCheckConstraint("CK_InvestmentEntries_Void", "\"VoidedAt\" IS NULL OR (length(btrim(\"VoidReason\")) > 0 AND length(btrim(\"VoidedBy\")) > 0)");
            });
        });
        modelBuilder.Entity<CollectionAccount>(a =>
        {
            a.HasKey(x => x.MonthlyPerformanceId);
            a.HasOne<MonthlyPerformance>().WithOne(x => x.Collection).HasForeignKey<CollectionAccount>(x => x.MonthlyPerformanceId).OnDelete(DeleteBehavior.Restrict);
            a.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.MonthlyPerformanceId).OnDelete(DeleteBehavior.Restrict);
            a.Property(x => x.InvoiceReference).HasMaxLength(100);
            a.Property(x => x.Currency).HasMaxLength(3);
            a.Property(x => x.Revision).IsConcurrencyToken();
            a.HasIndex(x => x.DueOn);
            a.ToTable(t => t.HasCheckConstraint("CK_CollectionAccounts_Amounts", "\"ReceivableAmount\" >= 0 AND \"LegacyPaidAmount\" >= 0 AND \"LegacyPaidAmount\" <= \"ReceivableAmount\""));
        });
        modelBuilder.Entity<CollectionPayment>(p =>
        {
            p.Property(x => x.Reference).HasMaxLength(160);
            p.Property(x => x.Note).HasMaxLength(1000);
            p.Property(x => x.VoidReason).HasMaxLength(1000);
            p.HasIndex(x => new { x.MonthlyPerformanceId, x.PaidOn });
            p.HasIndex(x => x.Reference).HasFilter("\"VoidedAt\" IS NULL").IsUnique();
            p.ToTable(t =>
            {
                t.HasCheckConstraint("CK_CollectionPayments_Amount", "\"Amount\" > 0");
                t.HasCheckConstraint("CK_CollectionPayments_Reference", "length(btrim(\"Reference\")) > 0");
                t.HasCheckConstraint("CK_CollectionPayments_Void", "\"VoidedAt\" IS NULL OR (length(btrim(\"VoidReason\")) > 0 AND length(btrim(\"VoidedBy\")) > 0)");
            });
        });
        modelBuilder.Entity<BrandFollowUp>(b =>
        {
            b.HasKey(x => x.BrandId);
            b.HasOne<Brand>().WithOne().HasForeignKey<BrandFollowUp>(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.WaitingReason).HasMaxLength(1000);
            b.Property(x => x.NextStep).HasMaxLength(1000);
            b.Property(x => x.Revision).IsConcurrencyToken();
            b.HasIndex(x => new { x.OwnerId, x.NextContactOn });
        });
        modelBuilder.Entity<BrandContactNote>(n =>
        {
            n.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
            n.Property(x => x.Text).HasMaxLength(4000);
            n.HasIndex(x => new { x.BrandId, x.ContactOn, x.CreatedAt });
        });
        modelBuilder.Entity<WorkTask>(t =>
        {
            t.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne<Deal>().WithMany().HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.Restrict);
            t.Property(x => x.Title).HasMaxLength(200);
            t.Property(x => x.Description).HasMaxLength(4000);
            t.Property(x => x.Revision).IsConcurrencyToken();
            t.HasIndex(x => new { x.AssigneeId, x.CompletedAt, x.DueOn });
            t.HasIndex(x => new { x.BrandId, x.CompletedAt, x.DueOn });
            t.HasIndex(x => new { x.BrandId, x.Year, x.Month }).HasFilter("\"Kind\" = 1").IsUnique();
            t.HasIndex(x => x.DealId).HasFilter("\"Kind\" = 2").IsUnique();
            t.ToTable(table => table.HasCheckConstraint("CK_WorkTasks_Target", "(\"Kind\" = 0 AND \"DealId\" IS NULL AND \"Year\" IS NULL AND \"Month\" IS NULL) OR (\"Kind\" = 1 AND \"DealId\" IS NOT NULL AND \"Year\" IS NOT NULL AND \"Month\" IS NOT NULL AND \"Year\" BETWEEN 2020 AND 2100 AND \"Month\" BETWEEN 1 AND 12) OR (\"Kind\" = 2 AND \"DealId\" IS NOT NULL AND \"Year\" IS NULL AND \"Month\" IS NULL)"));
        });
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
