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

## Hesap e-postaları, bildirimler ve iki aşamalı giriş

25 Eylül 2026: `20260925133126_MailConfiguration` yalnız `growth.MailConfigurations` tablosunu ekler. Tek kayıt (`Id=1`), pozitif eşzamanlılık sürümü ve şifreli port eşleşmesi kısıtlıdır. Migration şifre veya gönderim ayarı eklemez. Uygulama şifresi ayrı Data Protection amacıyla şifreli tutulur; yalnız API uygulama hesabı erişir, genel Data API rolleri açılmaz. Eski API panel ayarını tanımadığından karma sürümde SMTP çalıştırılmamalıdır. Geri dönüşte tablo/anahtar korunur, veri silen `Down` otomatik uygulanmaz.

Doğrulama: migration mevcut Supabase veritabanına uygulandı; bekleyen migration/model değişikliği kalmadı. Gerçek PostgreSQL üzerinde tek kayıt kısıtı, eski sürümle güncelleme engeli ve ayar yanıtında şifre bulunmaması sınandı. Deneme kayıtları transaction sonunda geri alındı; gerçek SMTP ayarı oluşturulmadı ve mevcut iş verileri değiştirilmedi.

- `20260923163849_AccountMail`: hesapta davet bekleme durumu, tek kullanımlık hesap bağlantıları (`AccountLinks`) ve şifrelenmiş hesap e-postası kuyruğu (`MailDeliveries`). 23 Eylül 2026'da uygulandı.
- `20260923220416_UserNotifications`: kişisel e-posta tercihleri (`NotificationPreferences`) ve bildirim/gönderim durumu (`UserNotifications`). 24 Eylül 2026'da uygulandı. Kullanıcı/olay birleşimi benzersizdir; `Revision` eski sürümle gönderim talebi ve tercih yazmasını reddeder. Mesaj metni veya finansal rapor içeriği saklanmaz; görüntüleme/gönderimde kaynak erişimi yeniden doğrulanır.
- `20260924152241_AccountSecurity`: hesap başına isteğe bağlı ikinci aşama (`AccountSecurities`). 24 Eylül 2026'da uygulandı. TOTP anahtarı şifreli; kurtarma kodları ve geçici giriş isteği yalnız özet olarak saklanır. `Revision`, hesap satırı kilidi ve son kabul edilen zaman aralığı tek kullanımı korur. Mevcut hesaplara otomatik koruma açılmaz.

Bu eklemeli migration'lar mevcut finansal kayıtları dönüştürmez. Yeni tablolar özel `growth` şemasında kalır; `anon`/`authenticated` erişimi açılmaz. Yeni bildirim ve güvenlik tabloları kullanıcıya yabancı anahtarla bağlıdır; kontrol kısıtları geçerli durum/sürüm değerlerini korur. Migration'lar testlerden sonra uygulandı; son kontrolde bekleyen migration veya model farkı yoktu.

24 Eylül gerçek PostgreSQL kontrolünde yinelenen kullanıcı/olay, eski bildirim/güvenlik sürümüyle ikinci yazma ve kurtarma dizisinin eski sürümle tüketilmesi engellendi. API bildirim ve güvenlik okumaları hassas içeriği dışarı vermedi. Yalnız deneme için açılan işlem geri alındı; deneme kayıtlarının kalmadığı doğrulandı. Bu kanıt API yazma senaryolarının bellek içi testlerini tamamlar; gerçek eşzamanlı HTTP yük testi yapıldığı anlamına gelmez.

Geri dönüşte tabloları, geçmişi ve Data Protection anahtar deposunu koruyun. `Down` veri siler; açık izin olmadan uygulanmaz. İkinci aşama açılmış hesap varken bu korumayı tanımayan eski API'ye dönülmez; ayrıntılar [hesap güvenliği işletim rehberinde](HESAP_GUVENLIGI.md). `mail-keys` volume'u e-posta kapalıyken de gereklidir.
