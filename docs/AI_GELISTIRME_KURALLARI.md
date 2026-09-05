# OVO Growth OS — AI Geliştirme Kuralları

Bu dosya Codex, Claude, Cursor ve diğer kodlama asistanları için projenin ortak ve araçtan bağımsız geliştirme sözleşmesidir.

## Ürün amacı

OVO Growth OS; marka değerlendirmesi, finansal senaryo, anlaşma, aylık performans ve hakediş süreçlerini yöneten iç SaaS uygulamasıdır. Kullanıcıların çoğu teknik değildir. Görünen tüm metinler açık, doğal ve iş odaklı Türkçe olmalıdır.

## Mimari sınırlar

- Backend: ASP.NET Core Minimal API ve .NET; iş hesapları `OvoGrowthOS.Domain` içinde tutulur.
- Veri: EF Core ve Supabase PostgreSQL; uygulama tabloları `growth` şemasındadır.
- Web: Next.js App Router, React, TypeScript ve mevcut UI bileşenleri.
- Kimlik: JWT ve `UserAccounts`; roller `Admin`, `Partner`, `Analyst`.
- Yeni microservice, event bus, generic repository veya gereksiz soyutlama ekleme.
- Finansal hesapları tarayıcıya taşıma; sunucu/domain sonucu tek doğruluk kaynağıdır.

## Değişiklik ilkeleri

1. Önce mevcut davranışı ve ilgili testleri incele; varsayımı açıkça belirt.
2. İstenen sonucu sağlayan en küçük sorumlu katmanı değiştir.
3. İlgisiz dosyaları biçimlendirme, yeniden adlandırma veya temizleme.
4. Mevcut kullanıcı verisini ve geriye dönük uyumu koru.
5. Gizli bilgileri, bağlantı parolalarını ve tokenları repoya yazma veya çıktıda gösterme.
6. Kullanıcıya dönük hata ve doğrulama mesajlarını anlaşılır Türkçe yaz.
7. Her değişikliği ölçülebilir kabul koşullarıyla doğrula.

## Değiştirilemez iş kuralları

- Ret veya ek bilgi gerekli kararı olan değerlendirme onaylanamaz.
- Aynı markanın aynı anda yalnızca bir etkin anlaşması olabilir.
- Kabul edilmiş, etkin veya kapanmış anlaşmanın ticari koşulları geriye dönük değiştirilemez.
- Kilitli, faturalanmış ve ödenmiş dönemler normal akışta değiştirilemez.
- Faturalanmış veya ödenmiş dönem kilidi açılamaz.
- Aylık sonucu hazırlayan kişi aynı dönemi onaylayamaz.
- Marka, değerlendirme, anlaşma ve aylık performans ilişkileri API sınırında doğrulanır.
- Para hesaplarında `decimal` kullan; `float` veya `double` kullanma.
- Oranlar domain ve veritabanında `0–1`, kullanıcı arayüzünde `0–100` biçimindedir.
- Finansal sonuçlar açıklanabilir ve deterministik olmalıdır; gizli/AI tabanlı karar üretme.

## PostgreSQL ve migration

- Şema değişikliklerinde EF Core migration oluştur; uygulanmış migration dosyasını elle değiştirme.
- Migration'ı önce derle ve test et, sonra yapılandırılmış Supabase veritabanına uygula.
- Destructive migration veya veri temizliği açık kullanıcı izni olmadan yapılmaz.
- Benzersizlik, zorunluluk ve temel finansal bütünlüğü mümkün olduğunda veritabanı kısıtlarıyla da koru.
- Supabase bağlantısını `dotnet user-secrets` veya ortam değişkeninde tut; repoya taşıma.

## Arayüz ve erişilebilirlik

- Mevcut OVO görsel dilini, logo kullanımını ve bileşen kalıplarını koru.
- Teknik enum veya İngilizce sistem terimlerini kullanıcıya doğrudan gösterme; Türkçe karşılığını kullan.
- Mobil görünümü, klavye kullanımını, odak durumlarını, yükleniyor/boş/hata durumlarını düşün.
- Önemli işlemlerde açık başarı/hata bildirimi ve geri dönüşü zor işlemlerde onay/gerekçe iste.
- Yeni bir kullanıcı akışı eklenirse sol menü ve görev merkeziyle ilişkisini değerlendir.

## Kullanım rehberi zorunluluğu

Kullanıcının gördüğü özellik, ekran, rol, hesaplama, terim veya iş akışı değiştiğinde kökteki `OVO_GROWTH_OS_KULLANIM_REHBERI.md` aynı değişiklik kapsamında güncellenir. Eski ve çelişkili açıklamalar temizlenir. `/guide` sayfası bu dosyayı doğrudan okur; ikinci içerik kopyası oluşturulmaz.

## Doğrulama kapısı

Değişikliğe göre ilgili kontrolleri çalıştır:

- Backend/domain: `dotnet build OvoGrowthOS.sln` ve `dotnet test OvoGrowthOS.sln`.
- Web: `npm run typecheck`, `npm run lint` ve `npm run build` (`apps/web` içinde).
- Migration: migration listesi ve gerçek Supabase bağlantısında sağlık kontrolü.
- Kullanıcı akışı: ilgili sayfanın hata vermeden açılması ve ana kabul senaryosunun çalışması.
- Rehber etkisi varsa `/guide` sayfasının derlenmesi.

Başarısız doğrulama varken işi tamamlandı olarak bildirme. Alakasız, önceden var olan bir hata varsa açıkça ayır ve belgeleyerek kullanıcıya bildir.

## Proje becerileri

- Genel özellik geliştirme: `.agents/skills/ovo-feature-development/SKILL.md`
- Finansal hesap ve iş kuralları: `.agents/skills/ovo-financial-safety/SKILL.md`
- Supabase/EF migration: `.agents/skills/ovo-supabase-migration/SKILL.md`
- Web arayüzü ve kullanım rehberi: `.agents/skills/ovo-ui-guide/SKILL.md`

Bir görev birden fazla alanı kapsıyorsa yalnızca ilgili becerileri birlikte kullan.
