# OVO Growth OS — İş Geliştirme Raporu ve Geliştirme Planı (V6)

**Tarih:** 27 Eylül 2026
**Durum:** Onay bekliyor — hiçbir madde başlamadı, sıralama sizin onayınıza bağlı.
**Dayanak:** Ürün envanteri (40 ekran, 26 iş modülü), V2/V4/V5 planlarının açık kalemleri ve kullanım rehberinin "sistem dışında kalan işler" bölümü taranarak hazırlandı.

---

## 1. Bugün elimizde ne var (kısa özet)

Sistemin ana omurgası tam:

- Marka kaydı → değerlendirme → senaryo → anlaşma → aylık hedef ve performans → kapanış kontrolü → hakediş ve tahsilat defteri → rapor ve müşteri portalı zinciri eksiksiz çalışıyor.
- Kararlar sürümlemeilmiş kural setleriyle, para hesapları tek merkezde ve **decimal** ile yapılıyor; her kritik işlemde denetim kaydı var.
- Tek canlı dış bağlantı: **GrandNode sipariş çekme** (salt okunur, Ağustos pilotu doğrulandı). E-posta altyapısı (Gmail) kodda hazır ama gerçek gönderim pilotu yapılmadı.
- Güvenlik ve yayın kapısı olgunlaştı: test kapıları, imaj doğrulama, açılış güvenlik kontrolü, sağlık uçları.

**Özetle: iç mekanik güçlü; eksik olan, sistemin iş hayatına daha fazla dokunması (otomatik veri, otomatik hatırlatma, dış sistemlerle entegrasyon) ve işin kritik altyapı riskinin (yedek/geri alma) kapatılması.**

---

## 2. Fırsat alanları — neler eklenebilir?

Her madde: **sorun → öneri → iş değeri → çaba** biçiminde. Çaba büyüklükleri: **Küçük** (<1 hafta), **Orta** (1–3 hafta), **Büyük** (3+ hafta).

### A. Veri otomasyonu — "veriyi elle girmeyi bırakmak"

| # | Öneri | İş değeri | Çaba |
|---|---|---|---|
| A1 | **Reklam platformu verisi otomatik çekme** (Meta Ads / Google Ads raporlama API'leri). Mevcut `AllowableAdSpend` ve veri kalitesi uyarıları gerçek reklam gideriyle çalışır. (V2 Faz 8B) | Aylık kapanışta "reklam gideri nerede" arama; eksik/şüpheli gider uyarısı kapanır; marka bazlı net kâr daha erken görülür | Orta |
| A2 | **Shopify sipariş çekme** — GrandNode pilotunun birebir aynı desenle genişletilmesi (marka API ayarları + salt okunur senkron). | Shopify kullanan markalarda aynı "mağaza siparişleri" doğrulaması; kapanış öncesi ciro kontrolü | Orta (GrandNode altyapısı hazır, desen tekrar) |
| A3 | **Otomatik ciro girdi önerisi** — mağaza siparişleri toplamı, aylık sonuç girişinde "önerilen brüt ciro" olarak sunulsun; **kullanıcı onayıyla** yazılsın. | Manuel giriş hatası azalır; veri kalitesi uyarıları (aynı Önceki/aynı değer) azalır | Küçük–Orta |

> Kural uyumu: A3'te öneri **kullanıcı onayı olmadan yazmaz**, hakedişe asla otomatik bağlanmaz (mevcut staging/disiplin korunur).

### B. Tahsilat ve nakit hızı — "alacak erken tahsil edilsin"

| # | Öneri | İş değeri | Çaba |
|---|---|---|---|
| B1 | **Ödeme sözü hatırlatma akışı** — ödeme sözü tarihine 1 gün kala ve gecikince, sorumlu kişiye uygulama içi + e-posta hatırlatması (mevcut bildirim ve e-posta kuyruğu üstüne kural). | Tahsilat günleri kısalır; "sözü unutulan" alacak azalır; banka/ödeme entegrasyonu olmadan bile ilk kazanım | Küçük |
| B2 | **Haftalık alacak özeti e-postası** — vadesi gelen/ geçen bakiyeler, sorumlularıyla haftalık tek e-posta (mevcut zamanlanmış rapor iskeletinin haftalık sürümü). | Yönetim nakit akışını panoya girmeden görür | Küçük |
| B3 | **WhatsApp/SMS hatırlatmaları** (Faz F 4 — ertelenmiş) | Tahsilat hatırlatmaları okunma oranını yükseltir; iş ortağı/ekip duyuruları | Orta + **sağlayıcı hesabı, API anahtarı, bütçe kararı** (sizin onayınız) |
| B4 | **Ödeme alma** — panelden link ile ödeme (iyzico/PayTR/Stripe) (Faz F 5 — ertelenmiş) | Tahsilat tek adıma iner; ödeme kaydı sisteme otomatik düşer | Büyük + **ticari sağlayıcı hesabı, komisyon kararı** |

### C. Faturalama ve sözleşme — "kağıt işleri sisteme girsin"

| # | Öneri | İş değeri | Çaba |
|---|---|---|---|
| C1 | **Sözleşme/ek-protokol PDF üretimi** — anlaşma verilerinden otomatik PDF (mevcut rapor PDF altyapısıyla). | Her yenileme/ek için hazır evrak; kopyala-yapıştır hatası biter | Orta |
| C2 | **Elektronik imza** (Paraşüt, inSign, veya sağlayıcı seçimiyle) | Anlaşma 1–2 günde kapanır; "imzaya gönderildi" takibi sisteme girer | Büyük + sağlayıcı kararı |
| C3 | **E-fatura / muhasebe bağlantısı** (Paraşüt, Logo, Netsis vb.) — hakediş faturasını dış sistemde oluşturma. | Muhasebe tarafında çift giriş kalkar | Büyük + muhasebe ekibi kararı |

### D. Raporlama ve karar desteği — "aynı veriden daha çok içgörü"

| # | Öneri | İş değeri | Çaba |
|---|---|---|---|
| D1 | **Marka sağlık skoru** — veri kalitesi + hedef sapması + tahsilat gecikmesi + anlaşma durumundan **açıklanabilir** tek puan (hangi kuralın ne kadar etkisi ekranda yazar). | Portföyde "kim dikkat istiyor" tek bakışta belli olur | Orta |
| D2 | **Portföy içi sektör karşılaştırması** — aynı sektör markalarının ciro/marj/reklam verimliliği yan yana (elimizdeki verilerle, tahmin yok). | Yeni anlaşma müzakeresinde referans | Küçük–Orta |
| D3 | **Haftalık yönetim özeti e-postası** — onay bekleyenler, geciken tahsilat, veri kalitesi uyarısı, hedef sapması tek özet. | Sabah panosu e-postaya gelir | Küçük |
| D4 | Yapay zekâ ile **rapor anlatımı yazımı** (isteğe bağlı, açıkça etiketli; karar/kurallarda AI yok) | Rapor yazma süresi azalır | Orta — **kural riski var**: finansal karar AI ile üretilemez; yalnız "anlatım taslağı" |

### E. Satış hattı ve müşteri tarafı

| # | Öneri | İş değeri | Çaba |
|---|---|---|---|
| E1 | **Lead aşaması zaman aşımı kuralı** — X gündür bekleyen adaya otomatik görev/bildirim (mevcut pipeline + görev sistemi). | Soğuyan fırsatlar kaybolmaz | Küçük |
| E2 | **Müşteri portalında dönem onayı** — marka yetkilisi kendi aylık özetini portalda "onayla/red + gerekçe" ile kapatsın. | Kapanış onayı e-posta trafiği olmadan, kayıtla tamamlanır | Orta |
| E3 | **Portalda sözleşme görüntüleme/kağıt onayı** (C1/C2 ile kombine) | Müşteri tarafında tek ekran | Orta |

### F. Zorunlu altyapı (iş değil, risk kapatma)

| # | Öneri | Neden | Çaba |
|---|---|---|---|
| F1 | **Canlı yedek + geri alma provası** (V2 Faz 2 — ertelenmiş) | Rehberde açıkça "sunucuda yedek bulunduğunu varsaymayın" yazıyor; veri kaybı tek senaryoda her şeyi bitirir | Küçük (aylık otomatik pg_dump + ayda bir kurtarma provası) |
| F2 | **Bakım/geri alma kolaylığı** — Coolify'da tek tık eski sürüme dönüş prosedürünün yazılı olması | Dün yaşanan deploy hatası gibi durumlarda erişim kesilmesin | Küçük |

---

## 3. Öncelik önerisi (etki × çaba)

**Hemen değer üreten (Düşük çaba, sağlayıcı gerektirmez):**
1. **B1** ödeme sözü hatırlatmaları — tahsilata doğrudan dokunur
2. **F1** canlı yedek + kurtarma provası — en büyük tek risk
3. **D3** haftalık yönetim özeti
4. **E1** lead zaman aşımı görevleri
5. **B2** haftalık alacak özeti

**Orta vadede (kapsam genişletme, mevcut altyapı üstünde):**
6. **A2** Shopify sipariş çekme (GrandNode deseni)
7. **A3** sipariş → ciro önerisi (onaylı)
8. **D1** marka sağlık skoru
9. **E2** portalda dönem onayı
10. **D2** sektör karşılaştırması

**Karar/konuşma gerektiren (dış sağlayıcı, bütçe, sizin onayınız):**
11. **A1** reklam API'leri (Meta/Google erişimi)
12. **B3** WhatsApp/SMS (Faz F 4)
13. **B4** ödeme alma (Faz F 5)
14. **C1→C2** sözleşme PDF → e-imza
15. **C3** e-fatura bağlantısı
16. **D4** yapay zekâ anlatım taslağı

---

## 4. Önerilen yol haritası

### Dalga 0 — Güvenlik ve hijyen (1 hafta)
- F1: otomatik yedek + ayda bir geri alma provası
- F2: eski sürüme dönüş (rollback) prosedürünün yazılması
- Açık kalan küçük kapılar: GrandNode API şifresi rotasyonu, Gmail gerçek gönderim pilotunun (Faz F 1) küçük bir alıcıyla denenmesi

### Dalga 1 — "İlk kazanım" (2–3 hafta) — *sizden tek onay*
- B1 ödeme sözü hatırlatmaları
- B2 + D3 haftalık alacak ve yönetim özeti e-postaları
- E1 lead zaman aşımı görevleri
- D1 marka sağlık skoru (açıklanabilir kurallarla)

### Dalga 2 — Veri otomasyonu (3–4 hafta)
- A2 Shopify sipariş çekme
- A3 onaylı ciro önerisi
- E2 portalda dönem onayı
- D2 sektör karşılaştırması

### Dalga 3 — Dış sağlayıcı kararları (sizin kararınıza göre, 4+ hafta)
- A1 reklam API'leri → B3 WhatsApp/SMS → B4 ödeme alma → C1 sözleşme PDF → C2 e-imza → C3 e-fatura
- Her madde başlamadan önce sağlayıcı/bütçe için ayrıca onayınız alınır.

> Sıralama değişebilir; kural: **her dalga biter, doğrulanır, sonra sonraki dalga başlar.** Fazlar arasında soru sormadan ilerleme alışkanlığınız korunur, yalnız dış sağlayıcı/bütçe kararlarında onayınıza gelinir.

---

## 5. Kapsam dışı kalanlar (bilinçli)

- **Gizli/AI tabanlı finansal karar üretimi** — yapılmayacak; tüm para hesapları açıklanabilir ve deterministik kalacak (D4 yalnız açık etiketli anlatım taslağıdır).
- **Banka entegrasyonu, KDV hesabı, gerçek para iadesi/mahsup** — finansal defterin sınırları aşılmayacak.
- **Çok şirketli SaaS / kurumsal SSO** — tek şirket kullanımı için düşük öncelikli.
- **Pazarlama kampanyaları** — bildirim/ileti, pazarlama iletişimi değildir.
- Örnek/sahte veri hiçbir zaman gerçek bağlantı gibi gösterilmeyecek; sipariş verisi hakedişe otomatik yazılmayacaktır.

---

## 6. Onayınız için

Bu raporu onaylarken şu üç noktayı belirtmeniz yeterli:

1. **Dalga 0 + Dalga 1 başlasın mı?** (öneri: evet — en yüksek kazanç/en düşük risk)
2. **Dalga 2 sırası değişsin mi?** (ör. Shopify yerine reklam API'leri önce)
3. **Dalga 3 için hangi dış sağlayıcıları masaya alalım?** (reklam API'leri, WhatsApp/SMS sağlayıcısı, ödeme sağlayıcısı, e-imza, e-fatura)

Onayınız alınan maddeler iş kalemi hâline getirilip mevcut geliştirme düzeniyle (küçük adımlar, test kapıları, rehber güncellemesi, size Türkçe rapor) uygulanacak.

---

## 7. Uygulama — onaylanan kapsam ve kabul koşulları (Dalga 1 + Dalga 2)

**Onay kararı:** Dalga 1 + Dalga 2 uygulanacak; Dalga 0 (yedek/rollback) ve Dalga 3 (WhatsApp/SMS, ödeme, e-imza, e-fatura) bu turda kodlanmayacak. Shopify ve reklam API bağlantı bilgileri marka ayarlarına girilip oradan okunacak; güncel resmi dokümantasyona göre kodlanacak.

### B1 — Ödeme sözü hatırlatmaları
- Söz tarihinden 1 gün önce ve tarih geçtikten sonra, kalan tutar varsa sorumlu çalışana uygulama içi bildirim düşer; SMTP hazırsa ve kullanıcının tercihi açıksa e-posta gider.
- Aynı olay ikinci kez bildirime/e-postaya dönüşmez; iptal edilmiş ya da kalanı 0 olan söz için bildirim oluşmaz.

### B2 + D3 — Haftalık yönetim özeti e-postası
- Her pazartesi 09:00 (TR) sonrasındaki ilk kontrolde Admin ve Partner kullanıcılara hafta başına tek bildirim oluşur; tercih ve SMTP açıksa e-posta gider; hafta içinde tekrarı yoktur.
- E-posta dört bölüm taşır: vadesi geçen/yaklaşan ödeme sözleri, onay bekleyen aylık sonuçlar ve müşteri onayları, veri kalitesi uyarıları, hedefin altındaki markalar. Boş bölüm "yok" olarak açıkça yazılır.

### E1 — Aşama zaman aşımı görevi
- Açık satış aşaması 14 günden uzun süredir değişmemiş ve markanın sorumlusu varsa sorumluya otomatik iş görevi açılır.
- Aynı aşama kaydı için görev tektir; aşama değişince sayaç sıfırlanır; görev silinirse yeniden açılabilir.

### D1 — Marka sağlık skoru
- Marka detayında 0–100 puan ve her etkenin (veri kalitesi, hedef sapması, tahsilat gecikmesi, anlaşma durumu) puan etkisi ayrı ayrı görünür; aynı veri aynı puanı verir; açıklama metni sade Türkçedir, gizli/AI karar yoktur.

### A2 — Shopify sipariş çekme
- Marka API ayarlarında platform seçimi (GrandNode / Shopify) vardır; Shopify'ta mağaza adresi `*.myshopify.com`, şifre alanı Admin API jetonudur; "Bağlantıyı doğrula" sipariş sayımıyla canlı doğrular.
- Shopify senkronu GrandNode ile aynı ekranı, aynı dönemin aynı doğrulamalarını (dönem sınırı, eksi tutar, para birimi, müşteri bilgisi sızmaması, tekrar senkronda mükerrer satır) kullanır.

### A1 — Meta ve Google reklam harcaması çekme
- Marka ayarlarında Meta ve Google için ayrı bağlantı kartları vardır; erişim anahtarları kaydedilirken korunur ve ekranda geri gösterilmez.
- "Bağlantıyı doğrula" canlı ve salt okunur sorgu yapar; harcama ucu seçili ay için tutarı ve para birimini döner; ağ/hatada sade Türkçe mesaj verilir, kimlik bilgisi sızdırılmaz.

### A3 — Onaylı ciro önerisi
- Aylık sonuç girişinde "Mağaza siparişlerinden öneriyi al" düğmesi dönem toplamını **form alanına yazar, kendiliğinden kaydetmez**; kullanıcı normal kaydet akışıyla onaylar, audit kaydı düşer. Kaynak ve son sipariş aktarım tarihi görünür.

### E2 — Müşteri tarafı dönem onayı
- Müşteri portalında dönem listesinden dönem onaylanabilir veya gerekçeyle reddedilebilir; aynı marka+dönem için kayıt tektir, yeniden karar audit'le izlenir.
- Kilitli, faturalanmış veya ödenmiş dönemde onay kapalıdır; onay/ret durumu iç ekip tarafında aylık sonuç listesinde görünür. Müşteri yalnızca kendi markasının dönemini onaylayabilir.

### D2 — Sektör karşılaştırması
- Raporlar sayfasında seçili dönem için sektörler bazında marka sayısı ve ortalama oranlar (brüt kâr marjı, iade oranı, hedef gerçekleşme) görünür; farklı para birimli markalar toplanmaz, yalnız oran ve adet karşılaştırılır.

### Genel kapılar
- Kullanım rehberi aynı işte güncellenir ve `/guide` derlenir.
- `dotnet build` + `dotnet test`, web `typecheck` + `lint` + `test:unit` + `build` yeşil olur.
- Yeni migration önce yerel veritabanında uygulanıp doğrulanır; canlı Supabase'e yazım ayrıca sizin onayınızla olur.
