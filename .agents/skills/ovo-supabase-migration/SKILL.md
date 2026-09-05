---
name: ovo-supabase-migration
description: OVO Growth OS'ta EF Core modeli, PostgreSQL şeması, indeks, constraint, seed veya Supabase migration işi yaparken kullan.
---

# OVO Supabase migration

Ana kurallar için `docs/AI_GELISTIRME_KURALLARI.md` dosyasını oku.

1. Mevcut model, `AppDbContext`, migration geçmişi ve etkilenecek veriyi incele.
2. Değişikliği mümkün olduğunca geriye uyumlu ve geri alınabilir tasarla. Veri silme/dönüştürme gerekiyorsa kullanıcıdan açık izin al.
3. Uygulama tablolarını `growth` şemasında tut. Doğrudan Npgsql kullanılan bu mimaride Supabase Data API/RLS varsayımı yapma.
4. EF Core migration oluştur; uygulanmış eski migration'ı düzenleme. Üretilen SQL/Up/Down kapsamını gözden geçir.
5. Bağlantı parolasını yalnız user-secrets veya ortam değişkeninden al; komuta, loga, dokümana veya repoya yazma.
6. Önce build ve testleri geçir; sonra migration'ı yapılandırılmış Supabase'e uygula ve migration listesini doğrula.
7. Uygulamayı gerçek bağlantıyla başlatıp `/health` ve değişen okuma akışını kontrol et.

Yeni benzersizlik veya bütünlük kuralında kullanıcı dostu çakışma mesajını API katmanında da sağla.
