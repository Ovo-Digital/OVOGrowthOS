# OVO Growth OS — Gelişim raporu ve dördüncü faz planı

Tarih: 25 Eylül 2026

**Onay kapsamı:** Faz 0 tamamlandıktan sonra kullanıcı önerilen sırayla kodlamaya başlanmasını onayladı ve fazlar arasında soru sormadan doğrudan devam edilmesini istedi. Faz 0–6'nın kodlama ve doğrulaması tamamlandı. Her fazın doğrulaması tamamlanmadan sonraki faza geçilmez. Gerçek Gmail pilotu, canlı yayın ve otomatik gönderimi etkinleştirme bu kodlama onayından ayrı tutulur. V2'de ertelenen yedekleme/yayın ve canlı entegrasyon işleri kendiliğinden yeniden başlatılmaz.

## 1. Yönetici özeti

Ürün artık bir markanın değerlendirilmesi, anlaşması, aylık kapanışı, tahsilatı, giderleri, hedefleri, ekip görevleri ve müşteriyle kontrollü rapor paylaşımını aynı yerde yönetiyor. Bir sonraki değer, daha fazla ekran eklemekten çok **işin doğru zamanda, doğru kişiye, eksiksiz bilgiyle ulaşmasını sağlamak**.

Önerilen sıra: göndericiyi panelden yönet → gönderim kurallarını netleştir → aylık raporu kontrollü planla → bütün gönderimleri görünür kıl → verinin kalitesini artır → emek/kapsam ve satış sürecini geliştirmeye geç.

Bu rapor kaynak kodu, kullanım rehberi ve V2/V3 planlarının incelemesine dayanır. Canlı müşteri kullanım istatistikleri, gerçek e-posta teslim oranı veya gelir etkisi ölçülmedi. Aşağıdaki faydalar beklentidir; garantili zaman/gelir kazancı olarak sunulmaz.

## 2. Mevcut güçlü alanlar ve gerçek boşluklar

| Mevcut özellik | Hâlâ ihtiyaç duyulan sonuç | Öneri |
| --- | --- | --- |
| Gmail göndericisi, kuyruk, hesap daveti ve şifre yenileme | Yönetici sunucuya girmeden göndericiyi hazırlayabilsin | Faz 0 |
| Kişisel görev/konuşma/rapor e-posta tercihleri | Marka düzeyinde hangi iletilerin açık olduğu ve alıcı ön izlemesi net olsun | Faz 1 |
| Kapalı dönem raporunu elle portalda yayımlama ve yeni rapor bildirimi | Ayın belirli gününde doğru dönemin onaylı raporu güvenli biçimde hatırlatılsın | Faz 2 |
| Hesap gönderim listesi ve kişisel bildirim durumu | Yönetici bütün ileti türlerinde neyin neden gönderilmediğini görebilsin | Faz 3 |
| Kaynak güven düzeyi, dosya aktarımı ve eksik dönem uyarıları | Aylık kaynağın nereden geldiği ve tutarsızlıklar kapanıştan önce görünür olsun | Faz 4 |
| Haftalık planlanan kapasite ve ayrı gerçek hizmet gideri | Planlanan iş kapsamıyla gerçekleşen emek karşılaştırılabilsin | Faz 5 |
| Aday/görüşme takibi ve anlaşmalar | Kaybedilen adayların nedeni, aşamada bekleme ve yenileme ihtiyacı ölçülebilsin | Faz 6 |

## 3. Faz 0 — E-posta ayarları ve güvenli gönderici yönetimi

Bu turda uygulanan kapsam:

- Yalnız yöneticinin açabildiği **Ayarlar → E-posta ayarları** sayfası.
- Gmail SMTP sunucusu, 465/TLS veya 587/STARTTLS, kullanıcı adresi, gönderici adresi/adı ve uygulama şifresi.
- Şifreyi şifreli saklama, geri göstermeme, boş bırakınca koruma, açık kaldırma ve hesap değişiminde yeni şifre isteme.
- Genel gönderimi açma/kapatma. Panel kaydı yokken mevcut sunucu ayarlarını koruma; panel kaydından sonra eski şifreye sessizce dönmeme.
- Yönetici açık onay verdiğinde yalnız kendi adresine sade deneme iletisi; müşterilere veya serbest alıcı listesine gönderim yok.
- Eski ekranla ayar ezme ve dakikada birden fazla deneme engeli. Deneme sonucu belirsizse otomatik tekrar yok.
- Mevcut görev/konuşma/rapor bildirimlerinde alıcının kişisel tercihi ve erişim denetimleri korunur. Hesap daveti/şifre yenileme istenen hesap işlemidir; bu kişisel bildirim tercihleriyle karıştırılmaz.
- Gerçek Gmail bilgileri kullanıcı tarafından girilecek. Testlerde sahte gönderici kullanılır; gerçek teslim pilotu ayrı adımdır.

**Kabul ölçütleri:** Yönetici olmayanlar okuyamaz/yazamaz/test edemez; şifre yanıtta/geçmişte yoktur; kapalı hizmet normal gönderim yapmaz; tercih kapalıysa bildirim gönderilmez; eski sunucu ayarları yalnız panel kaydı yokken geçerlidir; test alıcısı değiştirilemez; gerçek PostgreSQL ve tarayıcı kontrolleri tamamlanır.

**Bilinçli sınırlar:** Gmail dışında sağlayıcı, marka başına ayrı SMTP, takvimli aylık rapor, PDF eki, toplu pazarlama, marka bazlı izin anahtarı bu faza dahil değildir. Ayrı bir e-posta servisi/iş kuyruğu kurulmaz; mevcut yapı kullanılır.

**25 Eylül 2026 doğrulaması:** 278 backend testi ve 7 web testi geçti. Web tip kontrolü, lint ve üretim derlemesi başarılı. Migration Supabase'e uygulandı; gerçek PostgreSQL kısıt/erişim kontrollerinin deneme kayıtları geri alındı. Yerel tarayıcıda yönetici bağlantısı, şifreyi değiştirmeden kayıt ve yeniden yükleme, sahte göndericiyle açık onaylı deneme, yönetici dışı erişim engeli, 390 piksel mobil görünüm ve rehberdeki yeni bölüm doğrulandı. Tarayıcı kontrolünde uyarı/hata kaydı görülmedi. Gerçek e-posta gönderilmedi; Gmail bilgileriyle teslim pilotu ve canlı yayın henüz yapılmadı.

## 4. Faz 1 — Marka ve ileti türü bazında gönderim politikası

**Amaç:** “Bu markaya hangi tür ileti, hangi yetkiliye gider?” sorusunu göndermeden önce cevaplamak.

Önerilen işler:

1. Portal yönetiminde marka düzeyinde rapor bildirimlerine izin/durdurma anahtarı.
2. Genel izin, marka izni ve kişinin kendi tercihini ayrı gösterme. Marka izni kişisel tercihi zorla açmaz.
3. Alıcı listesi ön izlemesi: alacak kişiler ve alınmama nedenleri — hesap kapalı, tercih kapalı, marka erişimi yok gibi.
4. Konu/mesaj şablonları için güvenli, sınırlı değişkenler: marka adı, dönem ve portal bağlantısı. Ham HTML veya rastgele finansal alan ekleme yok.
5. İzin değişikliklerinin yapan kişi/zaman/gerekçeyle kaydı. Tüm ileti türleri varsayılan olarak açık yapılmaz.

**Örnek:** Lale'nin iki yetkilisinden yalnız biri rapor e-postasına izin vermiştir. Ön izlemede bir alıcı görünür. Yönetici marka gönderimini durdurursa ikisine de rapor iletisi gitmez; kişisel tercihler silinmez.

**Kabul:** Kapalı izinlerden herhangi biri gönderimi engeller; müşteri başka markaya taşınamaz; alıcı olmayanın nedeni açıklanır; izinler gönderim anında yeniden kontrol edilir.

**Uygulama kararı:** İlk politika yalnız yeni paylaşılan rapor e-postalarıdır. Marka anahtarı mevcut ve yeni markalarda başlangıçta kapalıdır; kişisel tercihler değiştirilmez. Görev, konuşma, davet ve şifre yenileme etkilenmez. Yönetici gerekçeyle açar/kapatır. Ayar ve ön izleme uçları yalnız yöneticiye açıktır. Şablon düz metindir; izinli üç değişken dışında veri alanı yoktur. Ön izleme, o markaya bağlı hesapları ve güncel dışlanma nedenlerini gösterir; ileti göndermez. Eski bildirimleri sonradan izin açarak geriye dönük e-postalama yoktur.

**Tamamlanan doğrulama — 25 Eylül 2026:** 302 backend testi (102 domain + 200 API) ve 7 web testi geçti. Backend build, web typecheck/lint/üretim derlemesi başarılı. `20260925183737_BrandMailPolicy` Supabase'e uygulandı; model/migration farkı kalmadı. Gerçek PostgreSQL'de marka ilişkisi, tek kural ve eski sürümle yazma engeli ile API ayar okuması doğrulandı; deneme kayıtlarının tamamı geri alındı. Alıcı ön izlemesi, farklı marka sınırı, rol engelleri, kişisel tercih, erişim/paylaşım geri çekme, süresi geçmiş ve daha önce işlenmiş bildirimler otomatik testlerde kontrol edildi. Tarayıcıda iki örnek alıcıyla açıklamalar, gerekçeli kayıt, marka izni–kişisel tercih ayrımı, yönetici dışına panelin kapalı olması, 390 pikselde taşmasız form ve rehber bölümü doğrulandı. Temiz deneme oturumunda tarayıcı hatası yoktu. Gerçek e-posta veya canlı yayın yapılmadı.

**Sıradaki sınır:** Faz 2 aylık rapor zamanlamasıdır. Üretimde açılmadan önce gerçek Gmail pilotu ve tek marka/dönem kabulü gerekir. Bu fazın tamamlanması bütün planın tamamlandığı veya otomatik aylık iletimin başladığı anlamına gelmez.

## 5. Faz 2 — Aylık marka raporunun zamanlanmış iletimi

**Amaç:** Örneğin ayın 5'inde, bir önceki ayın müşteriyle paylaşılmış raporu ilgili kişiye ulaştırmak.

Önerilen güvenli ilk sürüm:

- Marka için ayın günü ve Türkiye saati seçilir. İlk kapsam önceki takvim ayıdır; eksikse daha eski ay sessizce gönderilmez.
- İlk pilot: tek marka, tek dönem, kontrollü alıcı; önce elle ön izleme/onay, sonra açık kararla zamanlama.
- Yalnız kapalı ve ayrıca müşteri portalında yayımlanmış rapor kullanılır. Zamanlayıcı finansal onay veya rapor yayımlama yapmaz.
- E-posta rapor dosyası yerine giriş gerektiren bağlantı içerir. PDF eki ve finansal rakam içeren e-posta ayrı karar gerektirir; gönderilen ek sonradan geri çekilemez.
- Ayın son günü gibi takvim sınırları, API kapalıyken kaçırılan saat ve geç yayımlanan rapor için davranış önceden kararlaştırılır. Öneri: sınırlı telafi penceresi, daha eski bekleyenlere otomatik toplu gönderim yok.
- Aynı marka/dönem/alıcı için tekrar planlama ikinci ileti üretmez. Rapor sürümü değiştiğinde “düzeltme iletisi” ayrıca kullanıcı kararı olur.
- Var olan “yeni rapor paylaşıldı” bildirimiyle takvimli ileti çakışırsa iki iletiyi önleme kuralı belirlenir.

**Örnek:** 5 Kasım'da Ekim raporu paylaşılmamışsa müşteriye taslak veya Eylül raporu gönderilmez. Yönetici “Ekim raporu henüz paylaşılmadı” gerekçesini görür.

**Kabul:** Yanlış marka/ay/sürüm gönderilmez; izin ve paylaşım geri çekilince bekleyen ileti durur; aynı anda çalışan iki işleyici yinelenen ileti üretmez; sunucu yeniden başlayınca geçmiş aylar topluca yollanmaz.

**Onayda netleşecek:** Gönderim günü/saati, otomatik mi her ay onaylı mı, bağlantı mı ileride PDF mi, kaçırılan günün telafi süresi. Bu seçimler yapılmadan zamanlama açılmaz.

**Onaylanan kararlar — 25 Eylül 2026:**

- Gönderim ayın 5'i 09:00 (Europe/Istanbul). Gün ve saat marka bazında değiştirilebilir; varsayılan 5 / 09:00'dur.
- Marka bazında anahtar, varsayılan kapalı. Anahtar açıkken gönderim otomatiktir; kapatmak bekleyen iletiyi durdurur.
- E-posta yalnız giriş gerektiren portal bağlantısı içerir; PDF eki yoktur.
- Telafi penceresi 3 gündür. Süresi geçen dönemler geriye dönük olarak toplu gönderilmez.

**Çakışma kuralı:** Aynı rapor aynı kişiye en fazla bir kez e-posta ile gider. Zamanlama açıkken raporun e-postası takvim gününde gider ve paylaşıldığı anda ayrı bir e-posta üretmez; uygulama içi bildirim yine anında oluşur. Zamanlama kapalıyken önceki fazın davranışı (paylaşımdan itibaren 24 saat) değişmez.

**Uygulanan kapsam:**

- `BrandMailPolicy` üzerine `ScheduledReportEnabled`, `ScheduledSendDay` ve `ScheduledSendHour` alanları; gün 1–31, saat 0–23 veritabanı kısıtıyla korunur.
- `ReportMailSchedule` domain hesabı: Türkiye saatiyle gönderim anı, 3 günlük telafi penceresi, raporun sahiplik penceresi (günden 7 gün önce başlar).
- `ScheduledReportQueue`, mevcut bildirim döngüsünün başında pencere açıksa hedef dönemin alıcılarını hazırlar; gönderim, şablon ve durum makinesi mevcut bildirim kuyruğunu kullanır.
- Yalnız `RevokedAt` olmayan, kaynak dönemi kapanmış ve en yüksek sürümdeki rapor alınır; izin, tercih, erişim ve sürüm gönderim anında yeniden kontrol edilir.
- Yönetici panelinde gün/saat seçimi, sıradaki gönderim tarihi, hedef dönemin raporu yayımlanmış mı bilgisi ve ertelenmiş alıcıların ön izlemesi görünür.

**Tamamlanan doğrulama — 25 Eylül 2026:** 328 backend testi (114 domain + 214 API) geçti. Web tip kontrolü, lint ve üretim derlemesi başarılı. `20260925231801_BrandReportSchedule` Supabase'e uygulandı; model/migration farkı kalmadı. Zamanlama açıkken anlık e-postanın ertelendiği, takvim penceresinde tek seferlik gönderim, daha eski dönem/sürüm için sessizlik, kişisel tercih ve marka izni yeniden kontrolü, üç günlük pencere dışı denemede gönderim yapılmaması ve yönetici dışına erişim engeli otomatik testlerle doğrulandı. Gerçek e-posta gönderilmedi; Gmail bilgileriyle teslim pilotu ve canlı yayın yapılmadı.

## 6. Faz 3 — Birleşik gönderim merkezi ve sorun çözme

**Amaç:** Yönetici için tek yerde “ne gönderildi, ne gönderilmedi, ne yapmalıyım?” görünümü.

- Hesap iletileri, görev/konuşma/rapor bildirimleri ve denemeleri tür/tarih/durum/alıcı ile filtreleme.
- Bekliyor, sunucu kabul etti, iptal edildi ve sonucu belirsiz ayrımı. Sunucu kabulünü “teslim edildi” veya “okundu” diye göstermeme.
- Gönderilmeme nedenlerini anlaşılır Türkçe açıklama; şifre, hesap bağlantısı tokenı, mesaj gövdesi veya iç finansal veriyi göstermeme.
- Güvenli yeniden gönderme ancak ileti türüne uygun açık kararla. Davette eski bağlantı iptali; raporda güncel erişim/sürüm kontrolü; belirsiz sonucu körlemesine tekrar etmeme.
- Gönderim hacmi/sınırı ve saklama süresi gerçek kullanım verisiyle belirlenir; ücretsiz Gmail sınırsız toplu gönderici kabul edilmez.

**Kabul:** Kayıtlar yalnız yetkiliye açık; tekrar düğmesi çift gönderim yaratmaz; kim/ne zaman/niçin izi korunur. Dış sağlayıcıdan kanıt yoksa teslim veya açılma oranı uydurulmaz.

**Uygulanan kapsam — 25 Eylül 2026:**

- `GET /api/mail-center/messages`: hesap (davet/şifre yenileme), bildirim (görev/konuşma/rapor) ve deneme iletilerini tek listede birleştirir. `from`/`to`/`type`/`status`/`q`/`page` filtreleri ve durum özeti döner. Yalnız yöneticiye açıktır (`AdminOnly`) ve yanıt `no-store` ile önbelleğe alınmaz.
- Tür ayrımı: hesap ileti türleri `MailDeliveries` + `AccountLinks` + `UserAccounts` üzerinden; bildirimler `UserNotifications.EmailStatus != null` üzerinden (e-posta istenmemiş satırlar listelenmez); denemeler `AuditRecords` içindeki `MailTestRequested/Accepted/Uncertain` kayıtlarıdır.
- Gönderilmeme nedenleri tek yerde üretilir: bekliyor/sunan/sunucu kabul etti/doğrulanamadı/iptal edildi ve kesinleşmemiş hesap nedenleri (alıcı hesabı değişmiş, bağlantı süresi dolmuş, koruma anahtarı yok, iptal). Açıklamalar alıcının hesabına göre ne yapması gerektiğini söyler; şifre, bağlantı tokenı, ileti gövdesi veya iç finansal veri döndürülmez.
- `POST /api/mail-center/notifications/{id}/resend`: yalnız `Cancelled` ve `Uncertain` iletiler için ve zorunlu gerekçeyle. Kilitli hesap satırı üzerinde (aynı hesap için eşzamanlı ikinci gönderim engellenir), gönderimden hemen önce alıcının hesabı, e-posta adresi, oturum sürümü, davet durumu ve aktifliği yeniden kontrol edilir. Uygun değilse `409` ile gönderilmez. İşlem `NotificationMailResent` adıyla işlem geçmişine yazılır; `Sent` iletisi ikinci kez gönderilmez.
- Davet yeniden gönderimi mevcut `POST /api/account-mail/invitations/{id}/resend` ile korunur (eski bağlantıyı geçersiz kılar). Şifre yenileme yeniden gönderilemez; rehberde giriş ekranındaki akışa yönlendirilir.
- `apps/web/src/app/mail-deliveries/page.tsx` artık **Gönderim merkezi**dir: tür/durum/tarih/alıcı filtreleri, durum özeti, açıklama satırı ve iletisine uygun yeniden gönderim düğmesi. Başlık ve rota değişmedi; sol menüde Admin'e **Gönderim merkezi** bağlantısı eklendi.
- Liste 500 kayıtla sınırlıdır; saklama süresi not metninde “gerçek kullanım verisiyle belirlenecek” olarak açık bırakılır. Dış sağlayıcı kanıtı olmadığı için teslim/açılma oranı üretilmez.

**Tamamlanan doğrulama — 25 Eylül 2026:** 331 backend testi (114 domain + 217 API) geçti; bunlardan 3'ü bu faza özel `MailCenterTests` testidir. Testler üç kaynağın tek listede birleştiğini, tür/durum/tarih/alıcı filtrelerinin çalıştığını, yanıtta korumalı gövde/anahtar/sözde korunmaz alan adlarının hiç bulunmadığını, yalnız yöneticinin okuyabildiğini ve yeniden gönderimin gerekçesiz, ikinci kez veya hesabı artık uygun olmayan alıcıya yapılamadığını doğrular. Genel `/api/` kapsam testi yeni uçları da marka yöneticisi ve müşteri için 403 ile kapsar. Web tip kontrolü, lint, 7 web testi ve üretim derlemesi başarılı. Gerçek e-posta gönderilmedi; canlı yayın yapılmadı.

## 7. Faz 4 — Aylık veri kalitesi ve kapanış hazırlık panosu

**Amaç:** Yanlış veya eksik bilgiyi müşteri raporu paylaşılmadan fark etmek.

- Marka/ay için kaynak listesi: satış, iade, reklam ve gider raporlarının durumu, kaynağı ve sorumlusu.
- Önceki aya göre sıra dışı değişiklikleri inceleme uyarısı olarak gösterme; otomatik hata/başarısızlık kararı vermeme.
- İade, KDV ve farklı tarih kapsamının iki kez düşülmesi gibi kontrolleri kaynak açıklamasıyla birlikte sunma.
- Eksik kaynaktan mevcut görev sistemine tek takip işi açma; ayrı görev sistemi kurmama.
- Kapanış öncesi “hangi bilgi eksik, hangi ay hazır?” özeti. Mevcut iki kişi onayı ve kilit korunur.

**Kabul:** Bilinmeyen sıfıra çevrilmez; uyarı sonuçları kendiliğinden değiştirmez; kapalı dönem ve geçmiş müşteri sürümü korunur. Zorunlu kaynak listesi kullanıcı onayıyla belirlenir.

**Uygulanan kapsam — 26 Eylül 2026:**

- `Domain/DataQuality.cs`: kaynaklar `sales/returns/ads/costs`, kaynak durumları `entered/zero/missing/notApplicable`, hazırlık durumları `ready/attention/missing/notApplicable`. Uyarı kodları `missing_record`, `unknown_start`, `all_zero`, `returns_exceed_sales`, `returns_share` (%15), `vat_share`/`vat_zero` (kayıtlı ayarlamaya göre yaklaşık %5 sapma), `negative_net`, `repeat_returns`, `same_as_previous`, `change_*` (önceki aya göre hem %50 hem 5.000 birim), `missing_orders`, `missing_ads`, `missing_costs`. Formül `NetRevenue = Brüt satış − KDV − İade − İptal − chargeback` olarak mevcut `WorkflowEngine` ile aynıdır; bilinmeyen alan asla 0'a çevrilmez (`zero` ile `missing` ayrıdır) ve marka ayda anlaşma kapsamı dışındaysa `notApplicable` döner.
- `GET /api/data-quality` (`ReadAccess`): dönem (yıl/ay, varsayılan içindeki ay), kayıt durumu, kaynak kökeni (`manual`/`import`, köken `MonthlyPerformanceCreated` kaydıyla, dosya aktarımında `fileName`), hazırlayan/onaylayan (audit `UserId` üzerinden, bulunamazsa marka sorumlusu), uyarılar ve varsa açık kapanış görevi döner. Yanıt **yalnız veri durumunu anlatır**; onay, kilit, tahsilat ve hakediş tutarı içermez.
- `POST /api/data-quality/track-task` (`OperationsWrite`): mevcut `WorkTask` sisteminden `WorkKind.MonthlyClose` ile **tek** takip işi açar (aynı marka+dönem için ikinci istek `TaskTargetError` ile reddedilir), açıklama sistemce üretilir ve son tarih takip eden ayın 5'idir. Ayrı bir görev tablosu veya servis eklenmedi.
- `apps/web/src/app/data-quality/page.tsx`: **Kapanış hazırlığı** panosu — dönem seçimi (URL `?year=&month=` destekli), özet rozetleri, marka kartlarında dört kaynak durumu, uyarı/inceleme önerileri (bulundu–neden önemli–ne yapılacak), kaynak kökeni ve sorumlu, durum + iki kişi onayı satırı, **Tek takip işi aç** düğmesi (Admin/Partner, hazır ve kapsam dışı kartlarda görünmez) ve döneme ait aylık sonuca bağlantı. Sol menüde **Aylık sonuçlar** sonrasına **Kapanış hazırlığı** bağlantısı eklendi.
- Kullanıcıya dönük metinler `Kullanım rehberi` §13 “Kapanış öncesi: hangi bilgi eksik?” ve hızlı başlangıç maddesi olarak yazıldı.

**Tamamlanan doğrulama — 26 Eylül 2026:** 348 backend testi (127 domain + 214+7 API = 221 API) geçti; bunlardan 4'ü bu faza özel `Api.Tests/DataQualityTests`, 13'ü `Domain.Tests/DataQualityTests` testidir. Testler bilinmeyen değerin 0'a çevrilmediğini, `zero`/`missing` ayrımını, kapsam dışı markanın `notApplicable` olduğunu, yalnız yetkilinin panoyu okuyabildiğini, tek takip işinin ikinci kez açılamadığını ve uyarının onay/kilit durumunu değiştirmediğini doğrular. Genel `/api/` kapsam testi yeni uçları marka yöneticisi ve müşteri için 403 ile kapsar. Web tip kontrolü, lint, 7 web testi ve üretim derlemesi (`/data-quality` dâhil) başarılı. Şema değişikliği yok; migrasyon yapılmadı. Gerçek e-posta gönderilmedi; canlı yayın yapılmadı.

## 8. Faz 5 — İş kapsamı, gerçek emek ve yenileme hazırlığı

**Amaç:** “Bu markaya verdiğimiz hizmet, anlaştığımız iş yükünü aşıyor mu?” sorusunu kayıtla cevaplamak.

- Anlaşmaya bağlı hizmet kapsamı ve paket dışı taleplerin onaylı takibi.
- Çalışan/görev bazlı gerçek süre girişi ve yönetici kontrolü; mevcut haftalık planlanan saatlerden gerçek süre uydurmama.
- Gerçek süreyi mevcut hizmet maliyeti defterine aktarmada çift maliyet engeli ve açık onay.
- Yenileme toplantısı için hedef, kapsam, tahsilat ve gerçek gider özeti. Otomatik ücret artışı veya eski anlaşma değişikliği yok.

**Kabul:** Aynı çalışma iki kez maliyet olmaz; saat ücretleri yetkisiz çalışan/müşteriyle paylaşılmaz; planlanan–gerçekleşen açık ayrılır. Çalışanları otomatik puanlama/bordro sistemi bu kapsamda yok.

**Uygulanan kapsam — 26 Eylül 2026:**

- `Domain/DealScope.cs`: `DealScopeItem` (kalem), `DealScopeRequest` (paket dışı talep, `ScopeRequestStatus`), `DealScope.Editable/EditError/DecisionError/Approve/StatusLabel`. `Deal.RenewalOfDealId` üzerinden devam anlaşması zinciri okunur; mevcut `Deal` alanına dokunulmadı.
- `Domain/TimeTracking.cs`: `TaskTimeEntry` (hafta, saat, not, iptal), `TimeEntrySummary` ve `TimeTracking.Summary` — planlanan ile gerçekleşen **yan yana** durur, biri diğerinden türetilmez; `MaxWeekHours = 168`.
- `WorkflowEndpoints.Scope.cs` uçları: `GET /api/deals/{id}/scope` (`ReadAccess`) ile `POST .../scope/items`, `POST .../items/{itemId}/remove`, `POST .../scope/requests`, `POST .../requests/{requestId}/decision` (hepsi `OperationsWrite` + `LockInvestmentDeal` ile serileşme). Onaylanan talep kapsam kalemine dönüşür; çıkarma silme değil, gerekçeyle `RemovedAt` işaretlemesidir. Her işlem `DealScope*` adıyla işlem geçmişine yazılır.
- `WorkflowEndpoints.Time.cs` uçları: `GET/POST /api/work-tasks/{id}/time-entries` (`ReadAccess`; ekleme `MayLogTime` ile Admin/Partner veya görev sorumlusuna açıktır), `POST .../void` (`OperationsWrite`, ayrıca kendi girdisini iptal edebilir) ve `POST /api/performance/{id}/costs/entries/from-time` (`OperationsWrite`). Yanıt özeti `plannedHours/actualHours/voidedHours/remainingHours/complete`, kapanmış dönem listesi `revision` değeriyle döner.
- Çift maliyet engeli: `ServiceCostEntries.SourceTimeEntryId` eklendi; `AddServiceCostCore` aynı `SourceTimeEntryId` için canlı kayıt varsa `409` döner ve `IX_ServiceCostEntries_SourceTimeEntryId` (yalnız `SourceTimeEntryId IS NOT NULL AND VoidedAt IS NULL`) bu kuralı veritabanında da korur. Aktarma yalnız kapanmış dönemde, gider tarihi dönem içi ve bugün/önce olacak biçimde, açık onay kutusuyla yapılır; `TimeToCostValidator` onay kutusunu zorunlu tutar.
- `WorkflowEndpoints.Renewal.cs`: `GET /api/deals/{id}/renewal-summary` (`OperationsWrite`) deal, yenileme görevi/devam anlaşması, kapsam sayıları, son 6 ay (hedef, gerçekleşen, alacak, tahsilat, gecikme), hizmet maliyeti ve planlanan–gerçekleşen saati tek yanıtta toplar. Yanıtta saat ücreti veya çalışan başına maliyet yoktur; `notes` ile özetin otomatik ücret artışı yapmadığı, yenileme kararı üretmediği ve açık dönemin alacak sayılmadığı açıkça yazılır.
- Doğrulayıcılar: `Validation/ScopeValidators.cs` (dört validator), `TeamWorkValidators.TimeEntryValidator`, `OperatingCostValidators.TimeToCostValidator`.
- Migration `20260926003951_Phase5ScopeTimeTracking`: `DealScopeItems`, `DealScopeRequests`, `TaskTimeEntries` tabloları, check constraint'leri, indeksleri ve `growth."ServiceCostEntries"."SourceTimeEntryId"` sütunu + unique filtreli indeks. Üç tablo da `Down` ile geri alınabilir; mevcut tablolara yalnız **nullable sütun** eklendi. Migration gerçek Supabase bağlantısında uygulandı ve migrasyon listesinde onaysız kalem kalmadı.
- Web: `apps/web/src/components/deal-scope.tsx` — anlaşma sayfasına **Hizmet kapsamı** kartı (kalem ekleme, gerekçeli çıkarma, çıkarılanların ayrı listesi, paket dışı talep açma ve karar notuyla onay/red) ve **Yenileme toplantısı özeti** kartı (yönetici/ortak dışına özet bilgi notu döner). `apps/web/src/components/work-planning.tsx` — `TaskHourPanel` altına **Gerçekleşen saatler** alanı: dözet (planlanan/gerçekleşen/iptal/kalan), saat ekleme, gerekçeli iptal ve kapanmış döneme aktarma formu (dönem, gider tarihi, saat ücreti, referans, açıklama, onay kutusu).
- Kullanım rehberi: hızlı başlangıçta üç yeni madde ve **Hizmet kapsamı, gerçekleşen saat ve yenileme özeti** başlıklı bölüm eklendi; `apps/web` kopyası `cp` ile senkronlandı.

**Tamamlanan doğrulama — 26 Eylül 2026:** 360 backend testi (133 domain + 227 API) geçti; bunlardan 6'sı bu faza özel `Api.Tests` (2 `ScopeTests`, 3 `TimeTrackingTests`, 1 `RenewalSummaryTests`) ve 6'sı `Domain.Tests` (`DealScopeTests`, `TimeTrackingTests`) testidir. Testler kapsamın yalnız açık anlaşmada değiştirilebildiğini, çıkarmada gerekçe ve geçmişin korunduğunu, onaylanan paket dışı talebin kaleme dönüştüğünü, aynı çalışmanın iki kez maliyet olamadığını, planlanan–gerçekleşen ayrımının bozulmadığını, saat ücretinin yetkisiz kullanıcıya ve müşteriye dönmediğini, özetin anlaşmayı değiştirmediğini ve yalnız yetkilinin okuyabildiğini doğrular. Genel `/api/` kapsam testi yeni uçları marka yöneticisi ve müşteri için 403 ile kapsar. Web tip kontrolü, lint, 7 web testi ve üretim derlemesi (`/deals/[id]` altındaki iki yeni kart dâhil) başarılı. Migration gerçek Supabase'de uygulandı; build 0 uyarı/0 hata. Gerçek e-posta gönderilmedi; canlı yayın yapılmadı.

## 9. Faz 6 — Aday dönüşümü ve kayıp nedenleri

**Amaç:** Hangi adayların nerede beklediğini, neden anlaşmaya dönüşmediğini görmek.

- Aşama giriş/çıkış tarihleri ve aday kayıp gerekçeleri kaydı.
- Kaynak kanal, tekliften anlaşmaya dönüşüm ve aşamada bekleme görünümü.
- Yenileme öncesi görüşme hazırlığına mevcut görev bağlantısı.
- Eski kayıtlarda bilinmeyen tarih/süreyi tahmin etmeden “ölçüm başlangıcı öncesi” diye ayırma.

**Kabul:** Geçmiş süreler uydurulmaz; takip aşaması ticari kabul yerine geçmez; aynı aday mükerrer sayılmaz. Bu ölçümler satış garantisi veya çalışan performans puanı değildir.

**Uygulanan kapsam — 26 Eylül 2026:**

- `Domain/Pipeline.cs`: `LeadSource` (kaynak kanal), `BrandStageHistory` (aşama satırı; `EntryKnown = false` markanın **ölçüm başlangıcını** gösterir ve süresi tahmin edilmez), `StageWait` ve `Pipeline` (`SourceLabel`, `StageDays` — negatif gün 0'a sabitlenir, `LossError`, `CancelLossError`, `ConversionRate`, `Wait`).
- `Domain/TeamWork.cs`: `BrandFollowUp` alanları `SourceChannel`, `SourceNote`, `LostOn`, `LostReason`, `LostBy`. Marka formu ve `BrandUpdateRequest` değiştirilmedi; kaynak ve kayıp bilgisi yalnız takip kaydında tutulur.
- `Data/AppDbContext.cs`: `DbSet<BrandStageHistory>`; `BrandFollowUps` için beş yeni sütun (uzunluk sınırı + mevcut tabloya güvenle eklensin diye `HasDefaultValue`), `LostOn` indeksi; `BrandStageHistory` için FK (`Brands`, `Restrict`), `EnteredBy/ExitedBy/Note` uzunlukları ve `{BrandId, EnteredAt}` / `{Stage, ExitedAt}` indeksleri.
- `Features/WorkflowEndpoints.Pipeline.cs`: `GET /api/pipeline/summary` (`ReadAccess`), `GET /api/brands/{id}/stage-history` (`ReadAccess`), `POST /api/brands/{id}/pipeline/loss` (`OperationsWrite` + `ValidationFilter`) ve `POST .../loss/cancel` (`OperationsWrite`). Özet yanıt: açık/kayıp sayısı, aşama bekleme (açık, **ölçülen**, **bilinmeyen**, ortalama ve en uzun gün), kaynak dağılımı, dönüşüm (`reached/converted/rate/beforeMeasurement/notMeasured`), kayıp nedenleri, ilk 10 yenileme görevi ve altı Türkçe not. Kayıp kaydı yalnız gerekçeyle, anlaşması olan markada ve ikinci kez `409` ile reddedilir; marka `BrandStatus`'u **değişmez**, kayıp `LostOn` ile işaretlenir ve geri alınabilir.
- `Features/WorkflowEndpoints.Team.cs`: `FollowUpRequest`'e `SourceChannel`/`SourceNote` eklendi (null gelirse mevcut değer korunur); `SaveFollowUp` artık `RecordStageChange` çağırır. `RecordStageChange`: ilk satır `EntryKnown = rows.Count > 0` yani markanın önceki kaydı yoksa **false**; aşama değişince açık satırlar `ExitedAt/ExitedBy` ile kapanır ve `EntryKnown = true` yeni satır açılır. `/api/lead-follow-ups` projeksiyonuna `stageEnteredAt` ve `stageEntryKnown` alt sorguları eklendi.
- Validatorler: yeni `Validation/PipelineLossValidator` (`Reason` boş olamaz, en fazla 1000 karakter) ve `FollowUpValidator`'a `SourceChannel.IsInEnum()` + `SourceNote` en fazla 200 karakter kuralları.
- Migration `20260926005919_Phase6PipelineTracking`: `growth."BrandStageHistories"` tablosu (PK, FK `Restrict`, iki indeks), `growth."BrandFollowUps"` beş sütunu (`NOT NULL` sütunlar `DEFAULT` ile eklendi) ve `IX_BrandFollowUps_LostOn`. `Down` ile geri alınabilir; gerçek Supabase bağlantısında uygulandı ve migrasyon listesinde onaysız kalem kalmadı.
- Web: `apps/web/src/app/leads/page.tsx` — **Aday hattı özeti** kartı (açık aday, kayıp, dönüşüm oranı, ölçülmeyen aday; **Aşamalarda bekleme süreleri**, **Kaynak dağılımı**, **Kayıp nedenleri**, **Yenileme öncesi görüşmeler** ve **Önemli notlar** katlanır bölümleri) ve tabloya **Aşamada kalma**, **Kaynak kanal** ve **Kayıp işlemi** sütunları (`prompt()` ile gerekçe, boş gerekçe engellenir; düğmeler `session-user` rolüne göre Admin/Partner ile sınırlı). `apps/web/src/components/brand-team.tsx` — `leadSources` etiketleri, takip formuna **Kaynak kanalı** ve **Kaynak notu** alanları, bilgi listesine kaynak/kayıp satırları ve **Aşama geçmişi** katlanır listesi.
- Kullanım rehberi: hızlı başlangıçta iki yeni madde ve **Aday hattı: aşama geçmişi, kaynak ve kayıp kaydı** başlıklı bölüm (`Aday hattı özeti`, `Aşama geçmişi ve kaynak`, `Kayıp kaydı`) eklendi; `apps/web` kopyası `cp` ile senkronlandı.

**Tamamlanan doğrulama — 26 Eylül 2026:** 371 backend testi (139 domain + 232 API) geçti; bunlardan 5'i bu faza özel `Api.Tests/PipelineTests` ve 6'sı `Domain.Tests/PipelineTests` testidir. Testler ilk satırın ölçüm başlangıcı olup bekleme süresi üretmediğini, aşama değişiminin `BrandStatus`'u ve anlaşmayı değiştirmediğini, aynı adayın dönüşüm oranına tek kez girip ölçüm başlangıcı öncesi/ölçülmeyen adayların ayrı sayıldığını, kayıp kaydının gerekçesiz, ikinci kez ve anlaşması olan markada reddedildiğini, yetkisiz kullanıcının özet okuyabildiğini ancak kayıp işlemi yapamadığını, kaynak kanalının sonraki kayıtlarda silinmediğini ve özet yanıtında saat ücreti bulunmadığını doğrular. Genel `/api/` kapsam testi yeni üç marka ucu için marka yöneticisi ve müşteri için 403 ile kapsar. Web tip kontrolü, lint (0 uyarı), 7 web birim testi ve üretim derlemesi (`/leads` yeni özet kartı ve sütunlar dâhil) başarılı. Build 0 uyarı/0 hata; migration gerçek Supabase'de uygulandı. Gerçek e-posta gönderilmedi; canlı yayın yapılmadı; Gmail pilotu yapılmadı.

## 10. Şimdilik kapsam dışında tutalım

- Shopify/GrandNode/reklam entegrasyonları: Kullanıcı erişim bilgileri ve tek kaynak pilot onayı gelince V2 Faz 8'e dönülür.
- Yedekleme ve canlı yayın işleri: Önceki erteleme korunur; uygulamada menü eklenmesi sunucuda yedek varlığını kanıtlamaz.
- Yapay zekâ anlatımı: Önce mevcut kurallı rapor ve veri kalitesi. Harcama/veri paylaşımı için ayrıca onay gerekir.
- WhatsApp/SMS, pazarlama kampanyaları, e-imza, ödeme alma, çok şirketli SaaS: Her biri ayrı ürün/izin/bütçe kararıdır.
- Yeni e-posta sağlayıcıları: Gmail pilotunda gerçek hacim ve ihtiyaç ölçülmeden sağlayıcı seçimi veya ücretli abonelik yapılmaz.

## 11. Önerilen uygulama sırası ve karar kapıları

1. Faz 0'ın gerçek Gmail pilotunu kullanıcı bilgileriyle tamamla.
2. Faz 1'de alıcı ve içerik politikasını netleştir.
3. Faz 2'yi yalnız tek marka/ay pilotundan sonra genişlet.
4. Faz 3'te yönetim görünürlüğünü tamamla; gönderim sorunları pilotta belirginse bu fazı Faz 2'nin önüne al.
5. Faz 4–6'yı gerçek ekip kullanımındaki darboğaza göre sırala; ayrı faz onayı al.

Her fazda: dar kapsam → güvenlik/rol kontrolü → otomatik test → gerekli gerçek PostgreSQL kontrolü → tarayıcı doğrulaması → kullanım rehberi → kullanıcı kabulü. Bir faz başarısızken sonraki faza geçilmez. Commit/push, canlı yayın, gerçek alıcıya gönderim ve dış sistem erişimi kendi yetki sınırlarında kalır.

İlk pilotta ölçülecekler: rapor hazırlığından paylaşıma geçen süre, eksik rapor nedeniyle gönderilemeyen dönemler, tercih nedeniyle dışlanan alıcılar, belirsiz gönderimler, mükerrer gönderim sayısı ve sorumlusuz işler. Bugün bu sayılar bilinmiyor; raporda uydurulmadı.

## Kaynak kodu dayanakları

- `apps/api/src/OvoGrowthOS.Api/Mail/`: SMTP taşıması ve hesap kuyruğu.
- `Features/WorkflowEndpoints.MailSettings.cs`: yönetici ayarları ve deneme gönderimi.
- `Features/WorkflowEndpoints.MailCenter.cs`, `apps/web/src/app/mail-deliveries/page.tsx`: Faz 3'ün birleşik gönderim merkezi.
- `Notifications/NotificationService.cs`, `NotificationWorker.cs`: güncel alıcı/erişim/tercih kontrolü.
- `Features/WorkflowEndpoints.Portal.cs`, `apps/web/src/app/portal-management/page.tsx`: açık paylaşım ve rapor sürümleri.
- `apps/web/src/app/settings/email/page.tsx`: bu turdaki ayar ekranı.
- `OVO_GROWTH_OS_KULLANIM_REHBERI.md`, `docs/GELISIM_PLANI_V2.md`, `docs/GELISIM_PLANI_V3.md`: mevcut kapsam ve ertelenen işler.

Gmail uygulama şifresi koşulları: [Google resmi yardım](https://support.google.com/accounts/answer/185833?hl=tr). Kaynakları incelemek canlı ortamda bu özelliklerin yayımlandığı anlamına gelmez.
