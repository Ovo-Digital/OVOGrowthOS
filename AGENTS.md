# OVO Growth OS — Agent Talimatları

Her görevden önce `docs/AI_GELISTIRME_KURALLARI.md` dosyasını oku ve uygula. Bu dosya projenin araçtan bağımsız ana geliştirme sözleşmesidir.

## Zorunlu davranış

- Değişiklikleri küçük, amaca bağlı ve mevcut mimariyle uyumlu tut.
- Finansal hesapları domain katmanında ve `decimal` ile yap; iş kurallarını atlama.
- Supabase `growth` şemasını ve mevcut migration geçmişini koru; gizli bilgileri repoya yazma.
- Kullanıcıya görünen tüm içerikleri teknik olmayan bir çalışanın anlayacağı Türkçe ile yaz.
- Kullanıcıya dönük değişiklikte `OVO_GROWTH_OS_KULLANIM_REHBERI.md` dosyasını da güncelle.
- İlgili build/test kontrolleri geçmeden işi tamamlandı sayma.

## Skill yönlendirmesi

- Yeni özellik veya uçtan uca değişiklik: `$ovo-feature-development`
- Finansal hesap, hakediş, karar veya dönem kapanışı: `$ovo-financial-safety`
- EF Core, PostgreSQL veya Supabase şema işi: `$ovo-supabase-migration`
- Next.js arayüzü, Türkçeleştirme veya rehber: `$ovo-ui-guide`

Görev birden fazla alanı kapsıyorsa ilgili skill'leri birlikte kullan. İlgisiz skill yükleme.
