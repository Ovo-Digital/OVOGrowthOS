# Roadmap

## Tamamlanan — Faz 1: Finansal ve veri güvenliği

- Karar sonucu reddedilen değerlendirmelerin onaylanması engellendi.
- Değerlendirme, senaryo, anlaşma, aylık performans, kural ve genel ayar isteklerine Türkçe alan doğrulamaları eklendi.
- Marka–değerlendirme–anlaşma–aylık performans ilişkileri API sınırında korunuyor.
- Bir marka için yalnızca bir etkin anlaşmaya izin veren uygulama ve PostgreSQL kuralı eklendi.
- Kilitli, faturalanmış ve ödenmiş dönemler düzeltmeye kapatıldı; yalnızca faturalanmamış kilitli dönem yönetici gerekçesiyle açılabilir.
- Finansal karar eşikleri genel ayarlara taşındı ve kural önceliği belirgin hâle getirildi.
- PostgreSQL benzersizlik/kontrol ihlalleri ve eşzamanlı değişiklikler kullanıcıya Türkçe çakışma mesajı döndürüyor.
- Supabase PostgreSQL migration'ı uygulandı; domain, API ve web doğrulamaları çalıştırıldı.

## Tamamlanan — Faz 2: Günlük kullanım kolaylığı

- Üst arama alanı marka, değerlendirme ve anlaşmalarda gerçek arama yapıyor; ⌘/Ctrl + K ile açılabiliyor.
- Bildirim simgesi; bekleyen değerlendirmeleri, etkinleştirilecek anlaşmaları, onay bekleyen dönemleri ve eksik aylık girişleri gösteren yapılacak işler merkezine dönüştürüldü.
- Mobil menü açılır/kapanır çekmece olarak tamamlandı; tablolar küçük ekranlarda güvenli yatay kaydırma kullanıyor.
- Marka, değerlendirme, anlaşma, aylık sonuç, hakediş ve işlem geçmişi listelerine arama, durum filtresi, sıralama ve sayfalama eklendi.
- Marka ve iletişim bilgileri düzenlenebilir; etkin anlaşması olmayan markalar onay sorusuyla arşivlenebilir.
- Değerlendirme formuna adım kontrolleri, kaydedilmemiş değişiklik uyarısı ve insan dostu yüzde girişi eklendi.
- API doğrulama mesajları arayüzde Türkçe bildirim olarak gösteriliyor; önemli işlemlerde başarı bildirimi veriliyor.

## Tamamlanan — Faz 3: İş akışı ve kayıt izlenebilirliği

- Değerlendirme koşulları anlaşmaya taşınıyor; her koşul tamamlandı veya gerekçeyle feragat edildi olarak, kişi/tarih/not/kanıt bağlantısıyla kaydediliyor.
- Anlaşmalar kabul öncesi düzenlenebiliyor; iç inceleme, öneri, görüşme, kabul, etkinleştirme, yenileme, sonlandırma ve süresi doldu akışları gerekçeleriyle izleniyor.
- Anlaşma seçenekleri artık kod içine gömülü değil; yönetici tarafından Türkçe anlaşma şablonları ekranından düzenleniyor ve değerlendirmeden üretiliyor.
- Aylık dönem kapanışı hazırlayan ve onaylayan kişileri ayırıyor; aynı kişi kendi hazırladığı dönemi onaylayamıyor.
- Anlaşma ve finansal kayıtların PDF, görsel, CSV ve Excel belgeleri ile notları Supabase PostgreSQL'de saklanıyor ve denetim geçmişine yazılıyor.
- Faz 3 migration'ı Supabase PostgreSQL'e uygulandı; API/web derlemeleri ve 37 domain + 9 API testi başarılı.

## Tamamlanan — Faz 4: Kullanıcı ve rol yönetimi

- Kullanıcı hesapları Supabase PostgreSQL'deki `growth.UserAccounts` tablosuna taşındı.
- Yönetici yeni kullanıcı oluşturabiliyor; Analyst, Partner ve Admin rolleri API politikalarıyla ayrılıyor.
- Kullanıcılar etkin/pasif yapılabiliyor; şifreler PBKDF2 ile hashleniyor ve hesap değişikliğinde token sürümü artırılıyor.
- `/users` ekranı ve yönetici kullanıcı API'leri eklendi; mevcut geliştirme yöneticisi geriye dönük uyumlu biçimde seed ediliyor.
- Faz 4 migration'ı Supabase'e uygulandı; API ve web derlemeleri ile tüm mevcut testler başarılı.

Faz 1–3 kapsamındaki persisted operating flow, versioned rules, scenarios, deal choice, monthly close, audit, settings, role policies, DB dashboard, business workflow and regression tests are implemented.

Sonraki fazlar:

1. Add OIDC/ASP.NET Core Identity, refresh/revocation, and tenant boundaries before production multi-tenant deployment.
2. Add CSV import preview/mapping and provider adapters for Shopify/ad platforms when a real integration is scheduled.
3. Add PostgreSQL/Compose end-to-end CI, migration backup/restore rehearsal, observability/alerts, and security hardening.
4. Add print/PDF report templates and broader historical cohort/forecast reporting.
