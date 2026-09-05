---
name: ovo-financial-safety
description: Finansal hesap, hakediş modeli, senaryo, öneri, karar kuralı, anlaşma koşulu veya aylık kapanış davranışı değişirken kullan.
---

# OVO finansal güvenlik

`docs/AI_GELISTIRME_KURALLARI.md` içindeki değiştirilemez iş kurallarını önce oku.

- Para ve oran hesaplarında yalnızca `decimal` kullan. Yuvarlamayı iş kuralının açık sınırında yap.
- Domain ve veritabanında oranları `0–1`, arayüzde `0–100` olarak ele al.
- Net ciro, katkı kârı, OVO hakedişi, asgari ücret, kademeli pay ve düzeltme kalemlerini ayrı ve açıklanabilir tut.
- Geçmiş anlaşma snapshot'larını ve kilitli/faturalanmış/ödenmiş dönemleri değiştirme.
- Hazırlayan–onaylayan ayrımını ve marka–anlaşma–dönem bütünlüğünü koru.
- Karar sırası deterministik olsun; kritik ret/eksik veri kuralı daha düşük öncelikli öneriyle ezilmesin.

En az şu örnekleri test et: sıfır değer, eşik altı/eşiğe eşit/eşik üstü, negatif giriş reddi, minimum ücret, her komisyon modeli, kademeli sınırlar, kilitli dönem ve aynı kişinin onayı. Beklenen tutarları testte açıkça yaz; yalnızca “sonuç pozitif” gibi zayıf kontrol kullanma.
