---
name: ovo-ui-guide
description: OVO Growth OS Next.js arayüzü, kullanıcı metni, navigasyon, responsive davranış veya kullanım rehberi değişirken kullan.
---

# OVO arayüzü ve rehber

Önce `docs/AI_GELISTIRME_KURALLARI.md`, `apps/web/AGENTS.md` ve ilgili mevcut bileşenleri oku. Next.js sürüm davranışı belirsizse `apps/web/node_modules/next/dist/docs/` içindeki yerel dokümana bak.

- Mevcut OVO görsel dilini ve UI bileşenlerini yeniden kullan; yalnız bu görev için yeni tasarım sistemi kurma.
- Tüm görünen metni teknik olmayan çalışanın anlayacağı doğal Türkçe yaz; enumları `turkce` eşlemeleriyle göster.
- Ana eylem, yükleniyor, boş, hata, başarı, yetkisiz ve mobil durumlarını ele al.
- Formlarda insan biçimini kullan: oran alanında `5 = %5`; API'ye gönderirken `0.05`.
- Geri dönüşü zor veya finansal işlemlerde açık onay/gerekçe ve sonrasında bildirim göster.
- Menüye yalnız kalıcı ve sık kullanılan üst seviye alanları ekle; küçük ekran davranışını doğrula.

Kullanıcı akışı değiştiğinde kökteki `OVO_GROWTH_OS_KULLANIM_REHBERI.md` dosyasına ne işe yaradığı, nereden açıldığı, işlem sırası ve uyarıları ekle. `/guide` ikinci kopya değil bu dosyanın doğrudan görünümüdür. `typecheck`, `lint`, üretim build'i ve ilgili sayfanın açılışını doğrula.
