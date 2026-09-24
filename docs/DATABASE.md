# Database

PostgreSQL 17 is mapped with EF Core Code First under schema `growth`. Core tables are Brands, BrandEconomics, Evaluations, RuleSets, RuleDefinitions, Scenarios, PartnershipDeals, PartnershipConditions, MonthlyPerformances, CommissionAdjustments, GeneralSettings, and AuditTrail. Money/rate columns use precision 18, scale 4.

Indexes cover brand name/status, evaluation history, ruleset name/version uniqueness, deal status, scenario evaluation/name, monthly brand/year/month uniqueness, and audit lookup.

`OperatingWorkflow` is a compatibility migration from the MVP schema. It renames deal/audit tables, converts text deal statuses through an explicit PostgreSQL `USING CASE`, copies the former evaluation score into `PartnershipScore`, preserves audit field meaning, and adds the operating entities. The former unversioned MVP rules table is retained as `LegacyRules`; the application reads the new versioned rulesets.

The API applies migrations on startup for local/Compose use. Production should run them once as a release job before scaling instances and should back up the database first.

## Portal iş birliği tablolarının erişim kontrolü

23 Eylül 2026 kontrolünde `20260919041908_PortalCollaboration` zaten uygulanmıştı; yeniden uygulanmadı. Yeni üç tablo `postgres`, önceki portal tabloları ise `ovo_growth_app` sahibindeydi. Migration geçmişinin güncel olması uygulama hesabının yeni tabloları kullanabildiğini kanıtlamaz: ilk gerçek yazma kontrolü `42501: permission denied for table PortalDataRequests` hatası verdi.

Bu kurulumda yalnız mevcut uygulama hesabının gereken izinleri tamamlandı. `PortalMessages` için `SELECT, INSERT`; `PortalDataRequests` ve `PortalReportReadings` için `SELECT, INSERT, UPDATE` verildi. Silme, mesaj değiştirme, genel şema erişimi veya gelecekteki bütün tablolara otomatik izin verilmedi. `anon` ve `authenticated` rollerinin `growth` şemasına erişimi kapalı kaldı. Bu bir izin düzeltmesidir; EF migration geçmişine ikinci kayıt eklenmez.

Yayın kontrolünü **API'nin kullandığı veritabanı hesabıyla** yapın:

```sql
SELECT current_user, c.relname,
       pg_get_userbyid(c.relowner) AS owner,
       has_table_privilege(c.oid, 'SELECT') AS can_read,
       has_table_privilege(c.oid, 'INSERT') AS can_insert,
       has_table_privilege(c.oid, 'UPDATE') AS can_update
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'growth'
  AND c.relname IN ('PortalMessages', 'PortalDataRequests', 'PortalReportReadings')
ORDER BY c.relname;
```

Sahip hesabıyla çalışan SQL editöründeki başarılı sorgu tek başına yeterli değildir. Farklı bir ortamda rol adını doğrulamadan izin kopyalamayın. Sonraki şema değişikliklerini tablo sahipliği ve migration yetkileriyle birlikte değerlendirin; uygulama hesabına DDL yetkisi verilmiş sayılmaz.

API ve panel aynı tamamlanmış sürümden yayımlanmalıdır. Önceki API yeni devam mesajlarını/takip alanlarını tanımadığından eski ve yeni yazıcıları birlikte çalıştırmak güvenli kabul edilmez. Geri dönüşte yeni tabloları ve geçmişi koruyun; veri silen `Down` adımını açık izin olmadan çalıştırmayın.
