# OVO Growth OS — Gelişim raporu ve dördüncü faz planı

Tarih: 25 Eylül 2026

**Onay kapsamı:** Kullanıcı bu turda e-posta altyapısının panelden yönetilmesini istedi. Aşağıdaki Faz 0 bu kapsamda uygulanır. Faz 1–6 yalnız öneridir; ayrıca onay verilmeden kodlanmaz, otomatik gönderim veya yeni dış bağlantı açılmaz. V2'de ertelenen yedekleme/yayın ve canlı entegrasyon işleri kendiliğinden yeniden başlatılmaz.

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

**Onayda netleşecek:** İlk politika yalnız rapor mu, konuşma da dahil mi? Marka anahtarının ilk açılış değeri ve mevcut tercihlerin korunma davranışı.

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

## 6. Faz 3 — Birleşik gönderim merkezi ve sorun çözme

**Amaç:** Yönetici için tek yerde “ne gönderildi, ne gönderilmedi, ne yapmalıyım?” görünümü.

- Hesap iletileri, görev/konuşma/rapor bildirimleri ve denemeleri tür/tarih/durum/alıcı ile filtreleme.
- Bekliyor, sunucu kabul etti, iptal edildi ve sonucu belirsiz ayrımı. Sunucu kabulünü “teslim edildi” veya “okundu” diye göstermeme.
- Gönderilmeme nedenlerini anlaşılır Türkçe açıklama; şifre, hesap bağlantısı tokenı, mesaj gövdesi veya iç finansal veriyi göstermeme.
- Güvenli yeniden gönderme ancak ileti türüne uygun açık kararla. Davette eski bağlantı iptali; raporda güncel erişim/sürüm kontrolü; belirsiz sonucu körlemesine tekrar etmeme.
- Gönderim hacmi/sınırı ve saklama süresi gerçek kullanım verisiyle belirlenir; ücretsiz Gmail sınırsız toplu gönderici kabul edilmez.

**Kabul:** Kayıtlar yalnız yetkiliye açık; tekrar düğmesi çift gönderim yaratmaz; kim/ne zaman/niçin izi korunur. Dış sağlayıcıdan kanıt yoksa teslim veya açılma oranı uydurulmaz.

## 7. Faz 4 — Aylık veri kalitesi ve kapanış hazırlık panosu

**Amaç:** Yanlış veya eksik bilgiyi müşteri raporu paylaşılmadan fark etmek.

- Marka/ay için kaynak listesi: satış, iade, reklam ve gider raporlarının durumu, kaynağı ve sorumlusu.
- Önceki aya göre sıra dışı değişiklikleri inceleme uyarısı olarak gösterme; otomatik hata/başarısızlık kararı vermeme.
- İade, KDV ve farklı tarih kapsamının iki kez düşülmesi gibi kontrolleri kaynak açıklamasıyla birlikte sunma.
- Eksik kaynaktan mevcut görev sistemine tek takip işi açma; ayrı görev sistemi kurmama.
- Kapanış öncesi “hangi bilgi eksik, hangi ay hazır?” özeti. Mevcut iki kişi onayı ve kilit korunur.

**Kabul:** Bilinmeyen sıfıra çevrilmez; uyarı sonuçları kendiliğinden değiştirmez; kapalı dönem ve geçmiş müşteri sürümü korunur. Zorunlu kaynak listesi kullanıcı onayıyla belirlenir.

## 8. Faz 5 — İş kapsamı, gerçek emek ve yenileme hazırlığı

**Amaç:** “Bu markaya verdiğimiz hizmet, anlaştığımız iş yükünü aşıyor mu?” sorusunu kayıtla cevaplamak.

- Anlaşmaya bağlı hizmet kapsamı ve paket dışı taleplerin onaylı takibi.
- Çalışan/görev bazlı gerçek süre girişi ve yönetici kontrolü; mevcut haftalık planlanan saatlerden gerçek süre uydurmama.
- Gerçek süreyi mevcut hizmet maliyeti defterine aktarmada çift maliyet engeli ve açık onay.
- Yenileme toplantısı için hedef, kapsam, tahsilat ve gerçek gider özeti. Otomatik ücret artışı veya eski anlaşma değişikliği yok.

**Kabul:** Aynı çalışma iki kez maliyet olmaz; saat ücretleri yetkisiz çalışan/müşteriyle paylaşılmaz; planlanan–gerçekleşen açık ayrılır. Çalışanları otomatik puanlama/bordro sistemi bu kapsamda yok.

## 9. Faz 6 — Aday dönüşümü ve kayıp nedenleri

**Amaç:** Hangi adayların nerede beklediğini, neden anlaşmaya dönüşmediğini görmek.

- Aşama giriş/çıkış tarihleri ve aday kayıp gerekçeleri kaydı.
- Kaynak kanal, tekliften anlaşmaya dönüşüm ve aşamada bekleme görünümü.
- Yenileme öncesi görüşme hazırlığına mevcut görev bağlantısı.
- Eski kayıtlarda bilinmeyen tarih/süreyi tahmin etmeden “ölçüm başlangıcı öncesi” diye ayırma.

**Kabul:** Geçmiş süreler uydurulmaz; takip aşaması ticari kabul yerine geçmez; aynı aday mükerrer sayılmaz. Bu ölçümler satış garantisi veya çalışan performans puanı değildir.

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
- `Notifications/NotificationService.cs`, `NotificationWorker.cs`: güncel alıcı/erişim/tercih kontrolü.
- `Features/WorkflowEndpoints.Portal.cs`, `apps/web/src/app/portal-management/page.tsx`: açık paylaşım ve rapor sürümleri.
- `apps/web/src/app/settings/email/page.tsx`: bu turdaki ayar ekranı.
- `OVO_GROWTH_OS_KULLANIM_REHBERI.md`, `docs/GELISIM_PLANI_V2.md`, `docs/GELISIM_PLANI_V3.md`: mevcut kapsam ve ertelenen işler.

Gmail uygulama şifresi koşulları: [Google resmi yardım](https://support.google.com/accounts/answer/185833?hl=tr). Kaynakları incelemek canlı ortamda bu özelliklerin yayımlandığı anlamına gelmez.
