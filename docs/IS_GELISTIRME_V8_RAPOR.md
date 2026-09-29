# OVO Growth OS — İş Geliştirme Raporu ve Planı (V8)

**Tarih:** 29 Eylül 2026
**Durum:** Analiz tamamlandı; plan onayınıza sunuluyor. Hiçbir madde başlamadı.
**Dayanak:** V7 uygulaması (Dalga B + Dalga C) tamamlandı ve `c0f272a` ile main'e gönderildi; backend 473 test ve tüm web kapıları yeşil. Bu rapor; V7'den devralınan açık işleri, kullanım rehberi §27'deki "sistem dışında kalan işler" listesini ve bu analizde **kodda doğrulanmış yeni boşlukları** birlikte değerlendirir.

---

## 1. Değerlendirme çerçevesi — bu projeye değer ne katar?

OVO Growth OS bir büyüme ajansının iç SaaS'ıdır: marka değerlendirmesi → senaryo → anlaşma → aylık performans/hakediş → tahsilat → müşteri portalı. Değer üreten işler beş başlıkta toplanır:

1. **Elle dönen işi azaltmak** — veri kaynağından kendiliğinden gelsin, insan yalnız onaylasın.
2. **Nakit ve tahsilatı görünür kılmak** — kim, ne zaman, ne kadar ödeyecek; gecikme erken görünsün.
3. **Müşteri güvenini artırmak** — müşteri kendi verisini portalda şeffaf görsün, sorusunu portalda sorsun.
4. **Resmi evrakı kolaylaştırmak** — sözleşme, fatura, rapor çıktısı tek tıkla çıksın.
5. **Operasyonel dayanıklılık** — yedek, geri dönüş, e-posta/senkron sağlığı; sistem çökünce fark edilsin.

Aşağıdaki tüm adaylar bu beş başlığın dışına çıkmaz; gizli/AI finansal karar, banka entegrasyonu ve çok şirketli SaaS gibi bilinçli kapsam dışılar değişmez (§7).

---

## 2. Bugün sistemde ne var? (kısa envanter)

| Alan | Durum |
|---|---|
| Değerlendirme, senaryo, kural motoru, anlaşma motoru (7 model), şablonlar | Tam |
| Aylık kapanış, kilit, iki kişili onay, hakediş, fatura/parçalı ödeme, tahsilat defteri | Tam |
| Otomasyon | Aylık sipariş senkronu (N1), reklam harcaması getirme (N2), vade/haftalık özet/bildirim altyapısı, yenileme hatırlatması (N3), sistem sağlığı özeti (N8) |
| Rapor/analitik | Marka raporu + CSV/PDF yazdırma, sektör karşılaştırması, para birimi alt toplamları + manuel kur (N4), grafikler + trend (N7), alacak yaşı + vade takvimi, hedef/bütçe, kapasite planı, kohort grafiği |
| Müşteri portalı | Dönem onayı, paylaşılan raporlar/belgeler, konuşma + soru, okuma takibi, açık bakiye + ödeme sözü (N5), aylık otomatik rapor e-postası, çoklu portal hesabı |
| Operasyon | Serilog, `/health`, e-posta merkezi + yeniden gönderim, işlem geçmişi, rate limit + 5 başarısız girişte 5 dk kilit, 2FA, yedek yalnızca yerel provada |

---

## 3. Devralınan açık işler (V7'den)

| # | İş | Durum | Not |
|---|---|---|---|
| F1 | **Canlı Supabase otomatik yedek + ayda bir kurtarma provası** | Yapılmadı | En büyük tek risk; kurtarma provası yalnız yerelde (V5 Faz C). |
| F2 | **Eski sürüme dönüş (rollback) yazılımı** | Yazılmadı | Coolify rehberi var, yazılı dönüş adımları yok. |
| F3 | **Gmail gerçek gönderim pilotu** | Atlandı | Tüm e-posta akışları kodda; gerçek alıcıyla hiç denenmedi. |
| F4 | **GrandNode API şifresi rotasyonu** | Bekliyor | Pilot şifre açık yazılmıştı; panelden yenilenmeli. |
| F5 | **Coolify canlı yayın durumu** | Netleştirilmedi | Yayında mı, değil mi; doğrulanmalı. |
| G2 | **Dış sağlayıcılar**: WhatsApp/SMS, ödeme alma, e-imza, e-fatura, AI anlatım taslağı | Karar bekliyor | Her biri sağlayıcı hesabı/API anahtarı/bütçe gerektirir. Rehber §27'de de açıkça "dışında" listeleniyor. |

---

## 4. Yeni boşluk analizi — sistem bugün nerede hâlâ elde kalıyor?

Her madde bu analizde kodda doğrulanmıştır.

| # | Boşluk (kanıt) | Öneri | Değer | Çaba |
|---|---|---|---|---|
| **O1** | Otomatik sipariş senkronu **dönem başına tek deneme**: `StoreOrderSyncQueue` önceki ay için bir kez koşar, dönem kaydı (`StoreOrdersAutoSync`) yazıldığı anda tekrar denenmez — bir marka başarısız olursa o ay sessizce eksik kalır. Bağlantısı **sonradan** kurulan markanın eski ayları otomatik hiç çekilmez (yalnız manuel tekil çekme var). | **Senkronu marka-dönem kırılımına taşı**: başarısız markalar günde birkaç kez otomatik tekrar denensin; yeni bağlantıda "son N ayı geriye dönük çek" seçeneği; sonuç bildirimi devam eder. | Kapanışta eksik ciro verisi riski kalkar | Orta |
| **O2** | Reklam harcaması **yalnız istek anında** okunuyor (N2 butonu); hiçbir yerde aylık zamanlanmış reklam senkronu yok (`AdSpendSync` benzeri kuyruk yok). | **N1 desenini reklama uygula**: her ayın başında bağlı markaların geçen ay harcaması çekilip denetim kaydına yazılsın; kapanış hazırlığı uyarısı hazır veriyle çalışsın. | Kapanışta "reklam gideri nerede" araması tamamen biter | Küçük–Orta |
| **O3** | Yenileme hatırlatması (N3) **yalnız bildirim** veriyor; `ContractRenewal` görev türü var ama görev elle açılıyor (`CreateWorkTask`; tekillik kuralı hazır). | 30 günlük bildirimle birlikte **otomatik yenileme görevi** açılsın (aynı anlaşma için tekillik korunur, sorumlu = anlaşma sahibi/yonetici). | Hatırlatırdan işe dönüşür; yenileme kaçmaz | Küçük |
| **M1** | Müşteri, kendisinden istenen bilgi/belge talebini (`PortalDataRequests`) **yalnız okuyabiliyor** — portalda talebe yanıt veya dosya yükleme ucu yok (`portal.MapGet("/requests")`; durumu personel elle değiştiriyor). | Portalda **talep yanıtı + dosya yükleme** (mevcut belge saklama deseni, yalnız personel görür, boyut/tür sınırı, işlem geçmişi). Soru konuşma zaten var; döngü belgede tamamlanır. | "Belgeyi e-posta ile gönder" trafiği biter; müşteri deneyimi tamamlanır | Orta |
| **M2** | Excel **içe aktarma var** (`PerformanceImportFile` xlsx/csv) ama **dışa aktarma yok** — çıktılar CSV ve tarayıcı yazdırması. | Hakediş listesi ve marka raporu için **.xlsx dışa aktarma** (UTF-8 CSV zaten var; Excel açınca bozulmayan resmi dosya). *Not: yeni bir kitaplık bağımlılığı gerektirir — MIT lisanslı bir kütüphane seçimi sizin kararınız.* | Muhasebe/ortağa resmi dosya hızlı gider | Küçük–Orta (bağımlılık kararı) |
| **Ö1** | Sistemde **alacak yaşı** var ama **tahsilat performansı göstergesi yok**: vade içinde tahsil oranı ve ortalama tahsil günü hiçbir ekranda hesaplanmıyor (kodda doğrulanmış: eşleşen gösterge yok). | Marka ve portföy bazında **"vade içinde tahsil oranı" + "ortalama tahsil günü"** (kapanmış tahsilatlardan deterministik; `decimal`, açıklanabilir). Yenileme/müzakere masasında güçlü argüman. | Tahsilat kültürü ölçülebilir hâle gelir | Orta |
| **Ö2** *(isteğe bağlı)* | Trend ve grafikler geçmişe bakıyor; **hedef bazlı gelecek görünümü yok** (hedefler giriliyor ama "hedef tutarsa ay sonu nasıl görünür" ekranı yok). | **Hedef senaryosu projeksiyonu**: girilen hedef + mevcut anlaşma oranlarıyla sonraki ayın *açıklanabilir* hesabı — tahmin değil, "eğer hedef tutarsa" senaryosu; rehberde açık etiketle. | Yönetime "hedef gerçekçi mi" sorusu cevaplanır | Orta — **kavramsal risk, sizin onayınız** |

> **Bilinçli olarak aday yapılmayanlar:** e-fatura/e-imza/WhatsApp/ödeme alma (→ G2, sağlayıcı kararı), bankadan otomatik ödeme okuma (finansal sınır), TikTok/LinkedIn reklam adaptörleri (talep yok), inbound webhook (talep yok), rate limit/2FA/zaman dilimi testleri (zaten var), Excel'e tam rapor motoru (yazdırma zaten A4).

---

## 5. Etki × çaba matrisi

**Hemen değer üreten (küçük çaba, dış sağlayıcı gerekmez):**
1. **F1–F5** risk ve hijyen grubu (devralınan; en yüksek değer/çaba oranı)
2. **O3** otomatik yenileme görevi
3. **O1** senkron dayanıklılığı (sessiz eksik dönem riskini kapatır)

**Orta vadede (bir–iki haftalık işler):**
4. **O2** zamanlanmış reklam senkronu
5. **M1** portalda talep yanıtı + dosya yükleme
6. **M2** Excel dışa aktarma (kitaplık kararıyla)
7. **Ö1** tahsilat performansı göstergesi

**Karar veya onay gerektirenler:**
8. **Ö2** hedef senaryosu projeksiyonu (kavramsal onay)
9. **G2** dış sağlayıcılar (her biri ayrı: sağlayıcı + bütçe)

---

## 6. Önerilen V8 yol haritası

### Dalga A — Risk kapatma ve hijyen (≈1 hafta, onay gerektirmez, **öneri: önce bu**)
- F1: canlı Supabase için otomatik yedek (pg_dump + saklama) ve ayda bir kurtarma provası
- F2: Coolify rollback prosedürünün yazılıp ekiple paylaşılması
- F3: Gmail gerçek gönderim pilotu (küçük bir alıcıyla tek deneme)
- F4: GrandNode API şifresinin panelden yenilenmesi
- F5: canlı yayın durumunun netleştirilmesi

### Dalga B — Otomasyonun derinleşmesi (≈1–2 hafta)
- O1: sipariş senkronunda marka-bazlı tekrar deneme + geriye dönük çekme
- O2: aylık zamanlanmış reklam harcaması senkronu
- O3: 30 günlük yenileme bildiriminin otomatik yenileme görevine bağlanması

### Dalga C — Müşteri tarafı ve evrak (≈1–2 hafta)
- M1: müşteri portalında bilgi talebine yanıt ve dosya yükleme
- M2: .xlsx dışa aktarma (kitaplık seçimi onayınızla)

### Dalga D — Ölçüm ve müzakere gücü (≈1 hafta)
- Ö1: tahsilat performansı göstergeleri (vade içinde tahsil oranı, ortalama tahsil günü)
- Ö2 *(isteğe bağlı, kavramsal onayınızla)*: hedef senaryosu projeksiyonu

### Dalga E — Dış sağlayıcılar (her madde başlamadan ayrıca onayınıza gelir)
- B3 WhatsApp/SMS → B4 ödeme alma → C2 e-imza → C3 e-fatura → D4 AI anlatım taslağı

> Kural değişmez: **her dalga biter, test kapılarından geçer, rehber güncellenir ve size Türkçe raporla bildirilir; sonra sonraki dalga başlar.** Fazlar arasında soru sormadan ilerleme korunur; yalnız dış sağlayıcı, bütçe, portal finansal veri paylaşımı ve kavramsal kararlarda onayınıza gelinir.

---

## 7. Kapsam dışı kalanlar (bilinçli, değişmedi)

- Gizli/AI tabanlı finansal karar üretimi — tüm para hesapları açıklanabilir ve deterministik kalır.
- Bankadan otomatik ödeme okuma, gerçek para gönderme/iade, mahsup — finansal defterin sınırları aşılmaz.
- Sipariş/harcama verisinin hakedişe kendiliğinden yazılması — yalnız onaylı öneri olarak kalır.
- Çok şirketli SaaS / kurumsal SSO, pazarlama kampanyaları.

---

## 8. Onayınız için

1. **Dalga A başlasın mı?** (öneri: evet — en düşük çaba, en yüksek risk kapatması)
2. **Dalga B sırası doğru mu?** (öneri: O1 → O2 → O3; isterseniz O2 önceliklenebilir)
3. **M1 (portal dosya yükleme) dahil mi?** Müşteri tarafına dosya alma özelliği olduğu için ayrıca haber veriyorum; boyut/tür sınırı ve yalnız personel erişimiyle uygulanır.
4. **M2 için Excel kitaplığı onayı**: .xlsx üretimi için MIT lisanslı harici bir bağımlılık gerekiyor (örneğin ClosedXML) — onaylarsanız eklenir, istemezseniz güçlü CSV ile devam edilir.
5. **Ö2 (hedef senaryosu projeksiyonu) dahil mi?** "Tahmin yok" ürün ilkesiyle gerilim yaratmaması için yalnız hedef bazlı ve açık etiketli tasarlanır.
6. **G2'de öncelik**: dış sağlayıcı grubunda ilk hangi maddeyle başlamak istersiniz (e-fatura muhtemelen en yüksek tekrar eden iş)?

---

## 9. Uygulama sonucu (Dalga B + C + D)

Onaylanan yedi iş kodlandı, testlerden geçti ve kullanıcı rehberi güncellendi.

| İş | Sonuç | Doğrulama |
|---|---|---|
| **O1** sipariş senkronu dayanıklılığı | Marka bazlı tekrar deneme sırası, geriye dönük çekme ve geri bildirim özetleyen `StoreOrderSyncQueue`; ileri/geri yükleme düğmesi ve “Otomatik sipariş senkronu” bildirimi | StoreOrderSyncQueue testleri yeşil; rehber §6 |
| **O2** reklam harcaması senkronu | `AdSpendSyncQueue` + haftalık digeste sistem sağlığı satırı; otomatik okuma bilgilendirmesi (“ads_auto_read”) inceleme gerektirmez olarak | AdSpend + Digest testleri; domain 187/187 |
| **O3** otomatik yenileme görevi | 30 günlük yenileme bildirimi artık varsa açık görev açmaz, yoksa ilk etkin yöneticiye `ContractRenewal` görevi üretir (tamamlanan görev yeniden açılmaz) | RenewalReminder 5/5; rehber yenileme özeti |
| **M1** portal talep yanıtı + dosya | Müşteri talebine metinle yanıt, PDF/PNG/JPG/CSV/XLSX dosya yükleme ve personel indirmesi (10 MB sınırı, marka izolasyonu, denetim kaydı) | PortalCollaboration 9/9; rehber talep akışı |
| **M2** .xlsx dışa aktarma | ClosedXML 0.105.1 (MIT) ile hakediş listesi ve açıklamalı marka raporu .xlsx dışa aktarımı | Reporting + BrandReport 12/12; rehber |
| **Ö1** tahsilat performansı | `CollectionPerformance`: vadesinde tahsil oranı (0–1, 4 onalık) ve ortalama vade farkı (gün); yalnız vade tarihi bilinen, kapanmış, iptalsiz ödemeler; tek para birimi. Alacak yaşı ekranında kart + yenileme özetinde marka bazlı | Domain 5/5 + CollectionPlanning/RenewalSummary testleri; rehber §Tahsilat performansı |
| **Ö2** hedef senaryosu projeksiyonu | `TargetProjectionEngine`: hedef net cirosu + etkin anlaşma oranlarından “eğer hedef tutarsa” hakedişi, etkin oran, OVO katkısı ve marj hedefinden marka katkısı; katkı payı anlaşmaları kapalı formülle, hesaplanamıyorsa Türkçe gerekçe (uydurulan sayı yok). Hedef ekranında açık etiketli kart | Domain 5/5 + MonthlyTargetTests 4/4; rehber §Hedef senaryosu projeksiyonu |

**Geçen kapılar:** `dotnet build` 0 hata · `dotnet test` Domain **187/187**, Api **306/306** (normal ve `TZ=UTC`) · web `typecheck`, `lint`, birim test 9/9 ve `next build` temiz.

**Bekleyenler:**
1. **Canlı Supabase migration onayınız**: `V8AdSpendSync` (bildirim değeri 0..9) ve `V8PortalRequestResponse` (portal talep yanıtı sütunları).
2. **Commit**: yalnız siz istediğinizde.
3. Dalga A (F1–F5) ve Dalga E (G2) başlamadı; G2 öncelik onayınız bekliyor.
