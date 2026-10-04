# OVO Growth OS — Geliştirme Planı (V9)

**Plan tarihi:** 3 Ekim 2026
**Durum:** Dalga 9-1, 9-2, 9-3 ve 9-4 tamamlandı (test kapıları yeşil, migration uygulandı, rehber güncellendi); kalan üç iş (**Dalga B1–B3**) de 4 Ekim 2026'da tamamlandı; backlog açık.
**Onay:** Tüm gruplar onaylandı. Sıralamayı ajans belirleyecek. Dalga A (F1–F5 risk/hijyen) bilinçli olarak **atlandı**; dış sağlayıcı işleri (Dalga E) ayrıca onaya tabi.

---

## 0. Önceden alınan kararlar

- Bekleyen 2 migration (`V8AdSpendSync`, `V8PortalRequestResponse`) canlı Supabase'de zaten uygulanmış — doğrulandı.
- Commit alındı: `dc86204 docs: reklam ve pazarlama kapsamlı rehberi`.
- **Dalga A (F1–F5) atlandı** (yedek, rollback, Gmail pilotu, şifre rotasyonu, canlı durum).
- Kampanya bazlı reklam okuma (Meta/Google derinleşme) backlog'a alınmıştı; **4 Ekim'de tamamlandı** (aşağıda Dalga B1).

---

## 1. Onaylanan fikir envanteri

| # | Fikir | Grup | Değer | Çaba |
|---|---|---|---|---|
| **A1** | Erken uyarı merkezi (kurallı alarmlar) | Karar/risk | ⭐⭐⭐ | Orta |
| **A2** | Portföy stres testi ("en büyük marka düşerse") | Karar/risk | ⭐⭐⭐ | Orta |
| **B1** | 60 saniyelik hızlı ön eleme (sniff test) | Satış hattı | ⭐⭐⭐ | Küçük–Orta |
| **B2** | Otomatik görüşme brief'i | Satış hattı | ⭐⭐ | Küçük |
| **B3** | Kayıp nedeni + huni dönüşüm analizi | Satış hattı | ⭐⭐ | Küçük |
| **C1** | Nakit giriş projeksiyonu (13 hafta) | Finans/tahsilat | ⭐⭐⭐ | Orta |
| **C2** | Yenileme müzakere simülatörü | Finans/tahsilat | ⭐⭐⭐ | Orta |
| **D1** | Müşteri memnuniyet puanı (portal) | Müşteri tarafı | ⭐⭐ | Küçük–Orta |
| **D2** | Müşteriden "özet okudum" onayı | Müşteri tarafı | ⭐⭐ | Küçük |
| **E1** | Eksik veri → otomatik sorumlu görev | Operasyon | ⭐⭐ | Küçük |
| **E2** | Planlanan vs gerçekleşen saat sapması | Operasyon | ⭐⭐ | Küçük–Orta |
| **F1** | Marka reklam verimlilik paneli (otomatik MER) | Reklam verisi | ⭐⭐⭐ | Orta |

**Zorunlu kurallar (değişmez):** finansal hesaplar domain'de `decimal`; arayüz finansal karar vermez; deterministik ve açıklanabilir; yeni abstract/repository/service yok; kullanıcıya dönük metin açık Türkçe; her dalga sonunda test kapıları geçer, `OVO_GROWTH_OS_KULLANIM_REHBERI.md` güncellenir ve Türkçe rapor sunulur.

---

## 2. Dalga 9-1 — "Görünür sistem" ✅ TAMAM

| İş | Sonuç | Doğrulama |
|---|---|---|
| **A1** Erken uyarı merkezi | `Domain/Alerts.cs` (`AlertEngine.Build`); kodlar: `missing_close`, `overdue_receivable` (>30 gün kritik), `overdue_promise`, `mer_below_breakeven` (<%90 kritik), `stage_timeout` (30 gün), `overdue_task`. API `GET /api/alerts`; ana sayfada **Dikkat gerekenler** paneli | `AlertsTests` + `AlertsApiTests` |
| **E1** Otomatik takip görevi | `Notifications/QualityAutoTaskQueue.cs` — ayın ilk 7 günü, önceki ay için eksik kayıtlı anlaşmalı markalara kapanış görevi; `QualityAutoTaskSummary` audit tekilliği; sorumlu veri sorumlusu/yönetici, son tarih takip eden ayın 5'i. (Tek tık görev zaten vardı: `data-quality` ekranı) | kuyruk + tekillik testleri |
| **F1** Reklam verimlilik paneli | `Domain/AdEfficiency.cs` (`Mer`, bant `strong/watch/risk/unknown`, `WatchRatio=.90m`); yalnız kilitlenmiş dönemler; başa baş hedefi en güncel değerlendirmedeki `RecommendedTargetMer`. API `GET /api/brands/{id}/ad-efficiency`; marka sayfasında **Reklam verimliliği (MER)** kartı | `AdEfficiencyTests` |

Rehber: "Dikkat gerekenler" ve "Reklam verimliliği (MER)" bölümleri eklendi.

---

## 3. Dalga 9-2 — "Satış hattı" (sıra: B3 → B2 → B1) ✅ TAMAMLANDI

| İş | Sonuç |
|---|---|
| **B3** Kayıp/huni analizi | `Domain/PipelineAnalysis.cs` + `GET /api/pipeline/loss-analysis` (3/6/12 ay) → `components/loss-funnel-analysis.tsx`, leads sayfasına bağlı |
| **B2** Görüşme brief'i | `GET /api/brands/{id}/meeting-brief` → `components/meeting-brief.tsx`, marka sayfasına bağlı |
| **B1** Hızlı ön eleme | `Domain/PreScreening.cs` (`ready/attention`, issue kodları, 30 gün durağanlık) + `GET /api/leads/pre-screening` → `components/pre-screening.tsx`, leads sayfasına bağlı |

**Geçen kapılar:** build 0 hata · domain **209/209** · API **314/314** (toplam 523) · web `typecheck` temiz.

**Kapanış:** *(hepsi tamamlandı)* — lint temiz, `test:unit` 9/9, `build` başarılı, rehbere **B1 hızlı ön eleme, B2 görüşme brief’i, B3 kayıp/huni analizi** eklendi ve `apps/web` kopyası sync edildi.

---

## 4. Dalga 9-3 — "Para ve yenileme" ✅ TAMAMLANDI

- **C1 Nakit giriş projeksiyonu:** önümüzdeki 13 hafta için haftalık beklenen para girişi (`CollectionPlanning` + ödeme sözleri ayrı görünüm, `decimal`, tek para birimi; **"beklenen, garanti değil"** etiketi).
  - `Domain/CashProjection.cs` · `GET /api/cash-projection?currency=` (ReadAccess) · `components/cash-projection.tsx` → `/commissions/planning`.
- **C2 Yenileme müzakere simülatörü:** "OVO payı %X artarsa / sabit ücret +Y₺ olursa" senaryosu; `Deal`/`FinancialEngine` + `Scenario` bileşimi; **kaydetmez, yalnız bilgi verir**.
  - `Domain/RenewalSimulation.cs` · `GET /api/deals/{id}/renewal-simulation` (OperationsWrite) · `RenewalSummaryPanel` içinde üç alanlı simülasyon kutusu.
- **A2 Portföy stres testi:** "en büyük marka cirosunun %30'unu kaybederse" portföy etkisi; `PortfolioReporting` + senaryo motorlarının portföy bazlı yeniden kullanımı; **"Eğer X olursa"** açık etiketiyle.
  - `Domain/PortfolioStress.cs` · `GET /api/dashboard/stress?shockRate=` (ReadAccess) · `portfolio-report.tsx` → **En büyük marka stres testi** kartı.

**Geçen kapılar:** `dotnet build` 0 hata · domain **236/236** · API **321/321** (toplam 557) · web `lint` temiz · `typecheck` temiz · `test:unit` 9/9 · `next build` başarılı · rehber 3 yeni bölüm + `apps/web` sync.

## 5. Dalga 9-4 — "Müşteri ve ilişki" ✅ TAMAMLANDI

- **D1 Müşteri memnuniyet puanı:** marka bazında dönem puanı (1–5 + yorum, aynı dönem upsert); sağlık skoruna, hakedişe veya portalı yazılmaz.
  - `Domain/Satisfaction.cs` · `GET/POST /api/brands/{id}/satisfaction` (ReadAccess / OperationsWrite, Türkçe doğrulama + audit) · `components/brand-satisfaction.tsx` → marka sayfasında **Görüşme brief’i** altında.
  - Migration `V9SatisfactionAndReadingIp`: `growth.SatisfactionRatings` tablosu (unique `BrandId+Year+Month`, check constraint, cascade FK).
- **D2 "Özet okudum" onayı:** inceleme onayında tarih + kişi + **IP adresi** + sürüm; imza/tebligat iddiası taşımaz, tekrar tıklama ilk kaydı değiştirmez.
  - `PortalReportReading.ReviewedIpAddress` · ortak `Http/ClientIp.cs` (yalnız güvenilir özel ağ proxy'si XFF kabul eder) · `RecordPortalReading` audit'e `ipAddress` yazar · staff `readings` yanıtı `reviewedIpAddress` döner.
  - `portal-cooperation.tsx`: müşteri metninde IP uyarısı, ekip satırında IP gösterimi ve "imza yerine geçmez" ifadesi.
- **E2 Saat sapması raporu:** seçili hafta aralığı ve marka için planlanan/gerçekleşen/iptal saatler, marka bazlı sapma ve farkı en büyük ilk 100 görev; **finansal hesaba yazmaz**.
  - `Domain/HourDeviation.cs` · `GET /api/reports/hour-deviation?weekFrom&weekTo&brandId` (ReadAccess, 52 hafta sınırı, Türkçe hatalar) · `components/hour-deviation.tsx` → `/work/planning` altına kart.

**Geçen kapılar:** `dotnet build` 0 hata 0 uyarı · domain **246/246** · API **332/332** (toplam 578) · migration Supabase'e uygulandı · web `lint` temiz · `typecheck` temiz · `test:unit` 9/9 · `next build` başarılı · rehber **memnuniyet puanı**, **İnceledim/IP** ve **saat sapması** bölümleri + `apps/web` sync.

## 6. Dalga B — Onaylanan kalan üç iş ✅ TAMAMLANDI (4 Ekim 2026)

Kullanıcıyla soru aracıyla netleştirilip onaylanan üç iş, `ovo-feature-development` akışıyla sırayla bitirildi.

| Dalga | İş | Sonuç |
|---|---|---|
| **B1** | Kampanya bazlı reklam okuma | `Domain/AdCampaigns.cs` + `Integration/AdSpendClient` kampanya okuma (Meta `insights`, Google Ads query) · `GET/POST /api/brands/{id}/ad-campaigns` (salt okunur, hakedişe/bütçeye yazmaz) · `AdCampaignSpends` tablosu · `brand-ad-settings.tsx` **Kampanya kırılımı** alanı. Migration `V9AdCampaignSpends` uygulandı. |
| **B2** | Sunucuda PDF + e-postaya ek | PDFsharp 6.2.4 (MIT) + gömülü Noto Sans ile `Features/PortalReportPdf.cs`; portal **PDF indir** ve portal yönetiminde **Bu raporun PDF’ini indir** endpoint'leri; `MailAttachment` + SMTP eki; `BrandMailPolicy.PdfAttachmentEnabled` (varsayılan kapalı) ile bildirim/zamanlanmış rapor e-postasına ek; `brand-mail-policy.tsx` onay kutusu. Migration `V9ReportPdfAttachment` uygulandı. |
| **B3** | Portalda müşteri bildirimi + onaylı ön doldurma | `Domain/PortalSubmissions.cs` (`PortalPeriodSubmission`: brüt satış, iade, Meta/Google harcaması + not, marka+dönem tekil) · `PUT/GET /api/portal/submissions` (müşteri) · `GET/POST .../submissions/{id}/apply` (gerekçeli onay, kilitli dönemde 409) · `portal-submission.tsx` (müşteri kartı + ekip listesi) · `performance-form.tsx` **Müşteri bildirimini kullan** (onay + audit, kayıt kendiliğinden yapılmaz). Migration `V9PortalPeriodSubmissions` uygulandı. |

**Geçen kapılar:** `dotnet build` 0 hata 0 uyarı · domain **250/250** · API **349/349** (toplam 599) · migrationların üçü de Supabase'e uygulandı · web `lint` temiz · `typecheck` temiz · `test:unit` 9/9 · `next build` başarılı · rehber **Kampanya kırılımı**, **PDF indir/e-posta eki**, **Dönem bilgisi bildirin/Müşteri bildirimleri** bölümleri + `apps/web` sync.

---

## 7. Backlog ve ertelenenler

- **Dalga A (F1–F5):** bilinçli olarak atlandı — yedek + kurtarma provası, rollback yazımı, Gmail pilotu, GrandNode şifre rotasyonu, canlı yayın durumu. *Tekrar değerlendirme önerilir; yedek en yüksek tek risktir.*
- **Dalga E (dış sağlayıcılar):** e-imza, e-fatura, WhatsApp/SMS, ödeme alma, AI anlatım taslağı → her biri ayrı bütçe/sağlayıcı onayı.
- ~~Kampanya bazlı reklam okuma (Meta/Google)~~ — tamamlandı (Dalga B1).
