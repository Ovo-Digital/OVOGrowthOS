# OVO Growth OS — İş Geliştirme Raporu ve Analiz Planı (V7)

**Tarih:** 28 Eylül 2026
**Durum:** Dalga B ve Dalga C uygulandı ve test kapılarından geçti (29 Eylül 2026). Dalga A ve Dalga D kapsam dışı bırakıldı; commit henüz atılmadı.
**Dayanak:** V6 raporunun uygulama kısmı (Dalga 1 + Dalga 2) tamamlandı; bu rapor geriye kalanları, sistemde hâlâ elle dönen işleri ve yeni fırsatları tarayarak hazırlandı. Kaynak: kod envanteri, V2–V6 plan belgeleri, kullanım rehberi.

---

## 1. Bugünkü durum — V6 ne tamamlandı, ne kaldı

**Tamamlandı (18–28 Eylül 2026, Dalga 1 + Dalga 2):**

| Madde | Ne kazandırdı |
|---|---|
| B1 ödeme sözü hatırlatmaları | Vade 1 gün öncesi ve gecikmede uygulama içi + e-posta hatırlatması |
| B2 + D3 haftalık özet | Her pazartesi 09:00 (TR) sonrası alacak, onay, veri kalitesi ve hedef özeti |
| E1 lead zaman aşımı | 14 gündür bekleyen satış aşamasına otomatik görev |
| D1 marka sağlık skoru | 0–100 açıklanabilir puan; hangi etken ne kadar etkiledi ekranda |
| D2 sektör karşılaştırması | Aynı sektör markalarının oranları yan yana |
| A1 Meta/Google reklam verisi | Marka ayarlarında bağlantı + dönem harcaması okuma |
| A2 Shopify sipariş çekme | GrandNode deseninde sipariş senkronu ve doğrulama |
| A3 onaylı ciro önerisi | Sipariş toplamı forma yazılır, kullanıcı onayıyla kaydedilir |
| E2 portalda dönem onayı | Müşteri kendi aylık özetini onaylar/reddeder, gerekçeli |

**Doğrulama durumu:** backend 458 test (174 domain + 284 API) yeşil; web tip kontrolü, lint, birim testleri ve üretim derlemesi yeşil; CI'daki iki saat dilimi testi hatası bugün giderildi. Canlı Supabase şeması güncel (35 migration, bekleyen yok), canlı bağlantıyla `/health` 200.

**Sonuç:** V6'nın "ilk kazanım + veri otomasyonu" kısmı bitti. Geriye iki grup iş kaldı: **(a)** V6'dan devralanan açık kalemler, **(b)** bugünkü analizde ortaya çıkan yeni boşluklar.

---

## 2. Devralınan açık işler

| # | İş | Durum | Not |
|---|---|---|---|
| F1 | **Canlı veritabanı otomatik yedek + ayda bir kurtarma provası** | Yapılmadı | Yedek/geri yükleme provası yalnız **yerel** veritabanında yapıldı (V5 Faz C). Canlı Supabase için yedek planı yok. En büyük tek risk. |
| F2 | **Eski sürüme dönüş (rollback) prosedürü** | Yazılmadı | Coolify rehberi var ama "dün yaşanan deploy hatası" gibi durumlarda eski sürüme dönüş yazılı değil (V5 Faz F 2 atlandı). |
| F3 | **Gmail gerçek gönderim pilotu** | Atlandı | Tüm e-posta akışları kodda hazır; küçük bir gerçek alıcıyla hiç denenmedi (V5 Faz F 1). |
| F4 | **GrandNode API şifresi rotasyonu** | Bekliyor | Pilot şifresi sohbete açık yazılmıştı; panelden yenilenmeli (V5 notu). |
| F5 | **Coolify'a ilk canlı yayın** | Netleştirilmedi | V5 Faz F 2 "atlandı" denerek geçildi; sistemin şu an canlıda yayında olup olmadığı doğrulanmalı. |
| G1 | **Sözleşme/ek-protokol PDF üretimi (C1)** | Sağlayıcı gerektirmiyor | V6'da Dalga 3'e atılmıştı ama dış sağlayıcı gerekmez; mevcut PDF altyapısıyla yapılabilir. |
| G2 | **WhatsApp/SMS (B3), ödeme alma (B4), e-imza (C2), e-fatura (C3), AI anlatım taslağı (D4)** | Karar bekliyor | Her biri sağlayıcı hesabı, API anahtarı veya bütçe kararı gerektirir; V6 Dalga 3 sırasıyla bekliyor. |

---

## 3. Boşluk analizi — sistem bugün nerede hâlâ elle dönüyor?

Yeni analizin bulguları; her madde kodda doğrulanmış durumdadır.

| # | Boşluk (sorun) | Öneri | İş değeri | Çaba |
|---|---|---|---|---|
| N1 | Mağaza sipariş senkronu **tamamen manuel**: yönetici tek tek marka + dönem seçip tetikliyor; ay sonunda unutulursa ciro doğrulaması eksik kalır. | **Zamanlanmış senkron**: her ayın ilk gününde bağlı tüm markaların geçen ay siparişleri otomatik çekilsin; sonuç/hata yöneticiye bildirilsin (mevcut worker + kuyruk deseni). | Kapanış öncesi ciro verisi kendiliğinden hazır; manuel işlem kalkar | Küçük–Orta |
| N2 | Reklam harcaması **istek anında okunuyor** ve yalnız sonucu gösteriyor; aylık sonuç formuna kendiliğinden yazmıyor, kapanış kontrolüne de bağlı değil. | **A3 desenini reklama uygula**: "Reklam harcamasını getir" düğmesi formu doldursun (kaydetmez, kullanıcı onaylar); bağlantısı olan markada harcama girilmemişse kapanış hazırlığı uyarısına düşsün. | Aylık kapanışta "reklam gideri nerede" arama biter; eksik gider uyarısı gerçek veriyle çalışır | Küçük–Orta |
| N3 | **Yenileme hatırlatması yok**: yenileme özeti ve görevi var, ancak anlaşma bitiş tarihine yaklaşınca otomatik bildirim/görev açılmıyor. | B1 deseniyle **vade öncesi yenileme hatırlatması** (örn. 30/7 gün kala sorumlu kişiye bildirim + e-posta), tektirlik anahtarıyla. | Zamanında yenileme; kaybedilen anlaşma olmaz | Küçük |
| N4 | **Farklı para birimli portföy toplamı yok** (bilinçli kural: toplanmıyor). USD/EUR markaları olan portföyde yönetim toplamı göremiyor. | Para birimi bazında **alt toplamlar + isteğe bağlı manuel kurla genel toplam** (açıklanabilir, deterministik; kur elle girilir, API'den çekilmez). | Yönetim tek bakışta portföy büyüklüğünü görür | Küçük–Orta |
| N5 | Müşteri, açık bakiyesini ve ödeme sözlerini **portalda göremiyor**; "ne borcum var" sorusu e-posta trafiğiyle soruluyor. | Portalda **açık bakiye + ödeme sözü** görünümü (yalnız müşterinin kendi markası, paylaşıma açık alanlar kuralıyla). | Müşteri sorusu azalır; tahsilat konuşması kolaylaşır | Orta — **finansal veri paylaşımı, sizin onayınız** |
| N6 | Raporlar ekranda; **PDF/çıkıcı şablonu ve toplu indirme yok** (V6'da C1'e atılmıştı). | Marka raporu ve hakediş listesi için **PDF şablonu** (mevcut PDF altyapısıyla). | Müşteriye/ortağa resmi evrak hızlı çıkar | Orta |
| N7 | Tarihsel **kohort/öngörü raporu yok** (V2'den beri açık madde). | Marka bazında 6–12 aylık ciro/marj serisi + basit trend (gerçek veriden, tahmin yok). | Yenileme ve müzakere masasında referans | Orta |
| N8 | Worker/SMTP hataları **yalnız loglarda**; e-posta gönderim sorunu kimseye görünmez. | Haftalık yönetime özete (D3) bir **"sistem sağlığı" bölümü**: gönderilemeyen e-posta ve başarısız senkron sayısı. | Sorun büyümeden görülür | Küçük |

> Bilinçli olarak **atlananlar**: inbound webhook (mağaza olay dinleme), TikTok/LinkedIn reklam API adaptörleri (şu an manuel sütunlar yeterli), ek reklam platformları — talep oluşunca değerlendirilir.

---

## 4. Etki × çaba matrisi (tüm adaylar)

**Hemen değer üreten (küçük çaba, dış sağlayıcı gerekmez):**
1. **F1** canlı yedek + kurtarma provası — en büyük risk
2. **F2** rollback prosedürü
3. **F3** Gmail gerçek gönderim pilotu + **F4** şifre rotasyonu (hijyen)
4. **N3** yenileme hatırlatması
5. **N8** haftalık özete sistem sağlığı bölümü

**Orta vadede (veri otomasyonunun derinleşmesi):**
6. **N1** zamanlanmış sipariş senkronu
7. **N2** reklam harcamasının forma gelmesi + kapanış uyarısı
8. **G1 (C1)** sözleşme/ek-protokol PDF
9. **N4** para birimi bazlı portföy toplamları
10. **N6** PDF rapor şablonları
11. **N7** kohort/trend raporu

**Karar veya onay gerektirenler:**
12. **N5** portalda açık bakiye görünümü (finansal veri paylaşımı — sizin onayınız)
13. **F5** Coolify canlı yayın (yayın kararı/onayı)
14. **G2** WhatsApp/SMS → ödeme alma → e-imza → e-fatura → AI anlatım taslağı (sağlayıcı/bütçe)

---

## 5. Önerilen V7 yol haritası

### Dalga A — Risk kapatma ve hijyen (≈1 hafta, onay gerektirmez)
- F1: canlı Supabase için otomatik yedek (aylık `pg_dump` + saklama) ve ayda bir kurtarma provası
- F2: Coolify eski sürüme dönüş prosedürünün yazılıp ekiple paylaşılması
- F3: Gmail gerçek gönderim pilotu (küçük bir alıcıyla tek deneme)
- F4: GrandNode API şifresinin panelden yenilenmesi
- F5: canlı yayın durumunun netleştirilmesi (yayında değilse yayın, yayında ise doğrulama)

### Dalga B — Elle işi azaltan otomasyon (≈3 hafta)
- N1: zamanlanmış sipariş senkronu + sonuç bildirimi
- N2: reklam harcamasını forma getirme (onaylı) + kapanış uyarısına bağlama
- N3: yenileme hatırlatması
- N8: haftalık özet + sistem sağlığı bölümü

### Dalga C — Rapor ve müşteri tarafı (≈2–3 hafta)
- G1 (C1): sözleşme/ek-protokol PDF üretimi
- N6: PDF rapor şablonu (marka raporu + hakediş)
- N4: para birimi bazlı portföy toplamları
- N7: kohort/trend raporu
- N5 (opsiyonel, **onayınızla**): portalda açık bakiye görünümü

### Dalga D — Dış sağlayıcılar (her madde başlamadan ayrıca onayınıza gelir)
- B3 WhatsApp/SMS → B4 ödeme alma → C2 e-imza → C3 e-fatura → D4 AI anlatım taslağı

> Kural aynı: **her dalga biter, test kapılarından geçer, rehber güncellenir ve size Türkçe raporla bildirilir; sonra sonraki dalga başlar.** Fazlar arasında soru sormadan ilerleme korunur; yalnız dış sağlayıcı, bütçe ve finansal veri paylaşımı kararlarında onayınıza gelinir.

---

## 6. Kapsam dışı kalanlar (bilinçli, değişmedi)

- Gizli/AI tabanlı finansal karar üretimi — tüm para hesapları açıklanabilir ve deterministik kalır.
- Banka entegrasyonu, KDV hesabı, gerçek para iadesi/mahsup — finansal defterin sınırları aşılmaz.
- Sipariş verisinin hakedişe/aylık sonuca kendiliğinden yazılması — yalnız onayla önerilir.
- Çok şirketli SaaS / kurumsal SSO — tek şirket kullanımı için düşük öncelikli.
- Pazarlama kampanyaları — bildirim/ileti pazarlama iletişimi değildir.

---

## 7. Onayınız için

1. **Dalga A başlasın mı?** (öneri: evet — en düşük çaba, en yüksek risk kapatması)
2. **Dalga B sırası doğru mu?** (öneri: senkron → reklam → yenileme; isterseniz yenileme önceliklenebilir)
3. **Dalga C'de N5 (portal açık bakiye) dahil mi?** Finansal veri müşteriye açıldığı için ayrıca onayınız gerekiyor.
4. **Dalga D için hangi sağlayıcıları masaya alalım?** (WhatsApp/SMS, ödeme, e-imza, e-fatura)

Onayınız alınan maddeler iş kalemi hâline getirilip mevcut geliştirme düzeniyle (küçük adımlar, test kapıları, rehber güncellemesi, size Türkçe rapor) uygulanacak.

---

## 8. Uygulama sonucu (29 Eylül 2026)

**Dalga B ve Dalga C tamamlandı** (onay: "sadece dalga b ve dalga c"). Dalga A ve D kapsam dışı bırakıldı.

| Madde | Teslim edilen |
|---|---|
| N1 | Her ayın ilk günü otomatik sipariş senkronu; sonuç **StoreSync** panel bildirimi (özet satırı AuditRecord'da, e-posta yok) |
| N2 | Reklam harcaması alanında **Reklam harcamasını getir** düğmesi (onaylı, tümü/hiçbiri) + bağlantı var harcama yoksa kapanış uyarısı |
| N3 | Anlaşmanın bitişine 30 gün / 7 gün / bitiş gününde **RenewalDue** uygulama içi bildirimi (yönetici + iş ortağı; durum canlı doğrulanır) |
| N8 | Haftalık yönetim özetine **5) Sistem sağlığı** bölümü (son 7 gün: gönderilemeyen e-posta, başarısız otomatik senkron) |
| G1 | Anlaşma ekranında **Sözleşme ve ek protokol belgesi (PDF)** — yazdırılabilir A4 özeti (kapsam, ek protokoller, koşullar; iç maliyet hariç) |
| N6 | Hakediş listesinde **PDF'ye kaydet / yazdır** (A4 yatay, filtreler baskına girmez); marka raporunda zaten vardı |
| N4 | Portföy raporunda **para birimi bazında alt toplamlar** + **manuel kurla genel toplam** (dönüşüm sunucuda; canlı kur çekilmez) |
| N7 | Marka sayfasında 12 aylık ciro/kâr ve verimlilik grafikleri + **basit trend** satırı (son 3 ay vs önceki 3 ay; tahmin değil) |
| N5 | Müşteri portalında **Açık bakiye ve ödeme sözleri** (canlı; yalnız kapanmış dönemler, yalnız kendi markası; söz: kalan tutar/tarih/durum) |

**Doğrulama:** backend 473 test (176 domain + 297 API) normal ve `TZ=UTC` (CI taklidi) modunda yeşil; web typecheck, lint, birim test ve üretim derlemesi yeşil. Kullanım rehberi her madde için güncellendi. Commit/push kullanıcı onayıyla yapılacak.
