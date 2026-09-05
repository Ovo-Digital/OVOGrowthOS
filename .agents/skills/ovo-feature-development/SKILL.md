---
name: ovo-feature-development
description: OVO Growth OS'ta yeni özellik, uçtan uca iş akışı veya birden fazla katmanı etkileyen davranış değişikliği geliştirirken kullan.
---

# OVO özellik geliştirme

Önce `docs/AI_GELISTIRME_KURALLARI.md` dosyasını ve değişecek akışın mevcut kodunu/testlerini oku.

1. Kullanıcı sonucunu ve doğrulanabilir kabul koşullarını yaz.
2. Değişikliğin domain, API, veri, web ve rehber etkisini belirle; yalnız gereken katmanları değiştir.
3. İş kararını domain/API içinde tut. Arayüz yalnızca veri toplasın ve sonucu anlaşılır biçimde göstersin.
4. Mevcut desenleri kullan; tek kullanım için yeni soyutlama, servis veya altyapı ekleme.
5. Kullanıcıya dönük davranış değiştiyse `OVO_GROWTH_OS_KULLANIM_REHBERI.md` dosyasını aynı işte güncelle.
6. Etkilenen birim/API testlerini ekle veya güncelle; backend ve web doğrulama kapılarını çalıştır.

Veri şeması değişiyorsa ayrıca `ovo-supabase-migration`; finansal sonuç değişiyorsa `ovo-financial-safety`; arayüz değişiyorsa `ovo-ui-guide` becerisini uygula.
