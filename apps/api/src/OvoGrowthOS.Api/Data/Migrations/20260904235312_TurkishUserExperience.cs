using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OvoGrowthOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TurkishUserExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM growth."RuleDefinitions"
                WHERE "RuleSetId" IN (
                    SELECT tr."Id" FROM growth."RuleSets" tr
                    WHERE tr."Name" = 'OVO Varsayılan Karar Kuralları' AND tr."Version" = 1
                      AND EXISTS (SELECT 1 FROM growth."RuleSets" er WHERE er."Name" = 'OVO Default Rules' AND er."Version" = 1)
                );
                DELETE FROM growth."RuleSets"
                WHERE "Name" = 'OVO Varsayılan Karar Kuralları' AND "Version" = 1
                  AND EXISTS (SELECT 1 FROM growth."RuleSets" er WHERE er."Name" = 'OVO Default Rules' AND er."Version" = 1);

                UPDATE growth."RuleSets"
                SET "Name" = 'OVO Varsayılan Karar Kuralları',
                    "Description" = 'İş ortaklığı kararlarında kullanılan temel kurallar.'
                WHERE "Name" = 'OVO Default Rules' AND "Version" = 1;

                UPDATE growth."RuleDefinitions"
                SET "Name" = CASE "Name"
                    WHEN 'Gross margin below 25%' THEN 'Brüt kâr marjı %25''in altında'
                    WHEN 'Gross margin below 30%' THEN 'Brüt kâr marjı %30''un altında'
                    WHEN 'Gross margin 30%-40%' THEN 'Brüt kâr marjı %30-%40 arasında'
                    WHEN 'Gross margin 40%-50%' THEN 'Brüt kâr marjı %40-%50 arasında'
                    WHEN 'Gross margin 50%-60%' THEN 'Brüt kâr marjı %50-%60 arasında'
                    WHEN 'Gross margin at least 60%' THEN 'Brüt kâr marjı en az %60'
                    WHEN 'Return rate above 25%' THEN 'İade oranı %25''in üzerinde'
                    WHEN 'Stock coverage below 30 days' THEN 'Stok yeterliliği 30 günün altında'
                    WHEN 'Stock coverage 30-60 days' THEN 'Stok yeterliliği 30-60 gün arasında'
                    WHEN 'Founder cooperation is weak' THEN 'Kurucu iş birliği zayıf'
                    WHEN 'Operational readiness is weak' THEN 'Operasyonel hazırlık zayıf'
                    WHEN 'Product-market fit is weak' THEN 'Ürün-pazar uyumu zayıf'
                    ELSE "Name" END,
                    "Description" = CASE "Description"
                    WHEN 'Gross margin below 25%' THEN 'Brüt kâr marjı %25''in altında'
                    WHEN 'Gross margin below 30%' THEN 'Brüt kâr marjı %30''un altında'
                    WHEN 'Gross margin 30%-40%' THEN 'Brüt kâr marjı %30-%40 arasında'
                    WHEN 'Gross margin 40%-50%' THEN 'Brüt kâr marjı %40-%50 arasında'
                    WHEN 'Gross margin 50%-60%' THEN 'Brüt kâr marjı %50-%60 arasında'
                    WHEN 'Gross margin at least 60%' THEN 'Brüt kâr marjı en az %60'
                    WHEN 'Return rate above 25%' THEN 'İade oranı %25''in üzerinde'
                    WHEN 'Stock coverage below 30 days' THEN 'Stok yeterliliği 30 günün altında'
                    WHEN 'Stock coverage 30-60 days' THEN 'Stok yeterliliği 30-60 gün arasında'
                    WHEN 'Founder cooperation is weak' THEN 'Kurucu iş birliği zayıf'
                    WHEN 'Operational readiness is weak' THEN 'Operasyonel hazırlık zayıf'
                    WHEN 'Product-market fit is weak' THEN 'Ürün-pazar uyumu zayıf'
                    ELSE "Description" END
                WHERE "Name" IN ('Gross margin below 25%', 'Gross margin below 30%', 'Gross margin 30%-40%',
                    'Gross margin 40%-50%', 'Gross margin 50%-60%', 'Gross margin at least 60%',
                    'Return rate above 25%', 'Stock coverage below 30 days', 'Stock coverage 30-60 days',
                    'Founder cooperation is weak', 'Operational readiness is weak', 'Product-market fit is weak');

                UPDATE growth."Brands" SET "Industry" = 'Premium Takı' WHERE "Name" = 'Luna Jewelry' AND "Industry" = 'Premium Jewelry';
                UPDATE growth."Brands" SET "Industry" = 'Moda' WHERE "Name" = 'Mode Atelier' AND "Industry" = 'Fashion';
                UPDATE growth."Brands" SET "Industry" = 'Elektronik' WHERE "Name" = 'Volt Electronics' AND "Industry" = 'Electronics';
                UPDATE growth."PartnershipDeals" SET "Name" = 'Luna Büyüme İş Ortaklığı' WHERE "Name" = 'Luna Growth Partnership';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE growth."RuleSets"
                SET "Name" = 'OVO Default Rules', "Description" = 'Production baseline for partnership decisions.'
                WHERE "Name" = 'OVO Varsayılan Karar Kuralları' AND "Version" = 1;

                UPDATE growth."RuleDefinitions"
                SET "Name" = CASE "Name"
                    WHEN 'Brüt kâr marjı %25''in altında' THEN 'Gross margin below 25%'
                    WHEN 'Brüt kâr marjı %30''un altında' THEN 'Gross margin below 30%'
                    WHEN 'Brüt kâr marjı %30-%40 arasında' THEN 'Gross margin 30%-40%'
                    WHEN 'Brüt kâr marjı %40-%50 arasında' THEN 'Gross margin 40%-50%'
                    WHEN 'Brüt kâr marjı %50-%60 arasında' THEN 'Gross margin 50%-60%'
                    WHEN 'Brüt kâr marjı en az %60' THEN 'Gross margin at least 60%'
                    WHEN 'İade oranı %25''in üzerinde' THEN 'Return rate above 25%'
                    WHEN 'Stok yeterliliği 30 günün altında' THEN 'Stock coverage below 30 days'
                    WHEN 'Stok yeterliliği 30-60 gün arasında' THEN 'Stock coverage 30-60 days'
                    WHEN 'Kurucu iş birliği zayıf' THEN 'Founder cooperation is weak'
                    WHEN 'Operasyonel hazırlık zayıf' THEN 'Operational readiness is weak'
                    WHEN 'Ürün-pazar uyumu zayıf' THEN 'Product-market fit is weak'
                    ELSE "Name" END,
                    "Description" = CASE "Description"
                    WHEN 'Brüt kâr marjı %25''in altında' THEN 'Gross margin below 25%'
                    WHEN 'Brüt kâr marjı %30''un altında' THEN 'Gross margin below 30%'
                    WHEN 'Brüt kâr marjı %30-%40 arasında' THEN 'Gross margin 30%-40%'
                    WHEN 'Brüt kâr marjı %40-%50 arasında' THEN 'Gross margin 40%-50%'
                    WHEN 'Brüt kâr marjı %50-%60 arasında' THEN 'Gross margin 50%-60%'
                    WHEN 'Brüt kâr marjı en az %60' THEN 'Gross margin at least 60%'
                    WHEN 'İade oranı %25''in üzerinde' THEN 'Return rate above 25%'
                    WHEN 'Stok yeterliliği 30 günün altında' THEN 'Stock coverage below 30 days'
                    WHEN 'Stok yeterliliği 30-60 gün arasında' THEN 'Stock coverage 30-60 days'
                    WHEN 'Kurucu iş birliği zayıf' THEN 'Founder cooperation is weak'
                    WHEN 'Operasyonel hazırlık zayıf' THEN 'Operational readiness is weak'
                    WHEN 'Ürün-pazar uyumu zayıf' THEN 'Product-market fit is weak'
                    ELSE "Description" END;

                UPDATE growth."Brands" SET "Industry" = 'Premium Jewelry' WHERE "Name" = 'Luna Jewelry' AND "Industry" = 'Premium Takı';
                UPDATE growth."Brands" SET "Industry" = 'Fashion' WHERE "Name" = 'Mode Atelier' AND "Industry" = 'Moda';
                UPDATE growth."Brands" SET "Industry" = 'Electronics' WHERE "Name" = 'Volt Electronics' AND "Industry" = 'Elektronik';
                UPDATE growth."PartnershipDeals" SET "Name" = 'Luna Growth Partnership' WHERE "Name" = 'Luna Büyüme İş Ortaklığı';
                """);
        }
    }
}
