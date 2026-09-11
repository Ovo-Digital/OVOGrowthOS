# OVO Growth OS — Yeni Gelişim Planı

Tarih: 6 Eylül 2026  
Durum: **Faz 1, Faz 3, Faz 4, Faz 5 (5A + 5B), Faz 6, Faz 7 ve Faz 9 tamamlandı. Faz 2 ve Faz 8 kullanıcı isteğiyle ertelendi. Geliştirme ve doğrulamalar yerelde tamamlandı; commit, push ve canlı yayın yapılmadı.**
İncelenen sürüm: `5212a78`

Bu belge, mevcut sistemi geliştirmek için hazırlanmış ikinci yol haritasıdır. `ROADMAP.md` içindeki önceki fazların devamı niteliğindedir; buradaki “Faz 1” yeni geliştirme döneminin ilk fazıdır.

İlk analiz mevcut kaynak kodu, iş akışları, test dosyaları ve yayınlama ayarları üzerinden yapılmıştır. Uygulama sırasında tamamlanan kontroller aşağıdaki doğrulama kayıtlarında belirtilir; diğer fazların tamamlanma ölçütleri henüz gerçekleştirilmiş işlem değildir.

## 1. Bugün elimizde ne var?

OVO Growth OS; bir markayla çalışıp çalışmamaya karar verme, uygun anlaşmayı hazırlama, aylık sonuçları değerlendirme ve OVO hakedişini takip etme konusunda güçlü bir temel sunuyor.

Mevcut yapı içinde marka kayıtları, değerlendirmeler, finansal senaryolar, anlaşma şablonları, teklif ve anlaşma süreçleri, aylık kapanış, hakediş durumları, temel raporlar, işlem geçmişi ve kullanım rehberi bulunuyor. Arama, filtreler, temel eğilim grafikleri ve yapılacak işleri hatırlatan bir görev merkezi de mevcut. Bunları yeniden yapmaya gerek yok.

Bir sonraki hedef: sistemi yalnızca hesap yapan bir panel olmaktan çıkarıp ekibin günlük işini, kararlarını ve para takibini güvenle yürüttüğü bir çalışma alanına dönüştürmek.

### İncelemede görülen önemli eksikler

| Alan | Mevcut durum | Kullanıcı açısından etkisi |
| --- | --- | --- |
| Giriş güvenliği | Aktif kullanıcı bulunamadığında varsayılan yönetici bilgilerine dönüş yapılıyor. Bu yol varsayılan yönetici parolasını gerektiriyor; parolasız giriş değildir. | Olmayan veya kapatılmış bir kullanıcı adıyla girişin kesin olarak reddedilmesi sağlanmalı. |
| Oturumların kapatılması | Kullanıcı güncellemesinde oturum sürümü artırılıyor, fakat giriş belgesi doğrulanırken bu sürüm kullanılmıyor. | Hesabı kapatılan veya yetkisi değişen kişinin mevcut oturumu hemen geçersiz olmayabilir. |
| Kullanıcı yönetimi | Hesap oluşturma ekranı var; düzenleme ve kapatma işlemleri ekran tarafında tamamlanmamış. | Ekip değişikliklerini panelden yönetmek zorlaşıyor. |
| Canlı yayınlama | API ve panel imajları ayrı ayrı `latest` etiketiyle yayımlanıyor. | Biri başarılı, diğeri başarısız olduğunda farklı sürümler birlikte kullanılabilir. |
| Örnek veriler | Başlangıçta örnek portföy oluşturma işlemi canlı ortam için ayrı bir onaya bağlı değil. | Eksilen örnek kayıtlar yeniden oluşabilir; örnek ve gerçek veriler karışabilir. |
| Finansal özet | Son dönemdeki kayıtlar durumlarına göre ayrılmadan toplanıyor; ödenmemiş tutar yalnızca o dönemden hesaplanıyor. | Hazırlık aşamasındaki sonuçlar kesinleşmiş gibi algılanabilir; eski borçlar genel toplam dışında kalabilir. |
| Başlangıç yatırımı | “Kalan yatırım” alanında aktif anlaşmaların toplam başlangıç yatırımı kullanılıyor. | Gerçekte ne kadarının geri kazanıldığı anlaşılmıyor. |
| Günlük işler | Görev merkezi mevcut kayıtlardan hatırlatma çıkarıyor; kişiye atanmış kalıcı görev takibi değil. | İşin kimin sorumluluğunda olduğu ve ne zamana kadar yapılacağı belirsiz kalabiliyor. |
| Tahsilat ve rapor | Ödendi/faturalandı durumları ve temel rapor mevcut; parçalı ödeme takibi ve dönem seçmeli paylaşılabilir rapor eksik. | “Ne kadar kazandık, ne kadarını aldık, ne kadarı bekliyor?” sorusu tek yerden cevaplanamıyor. |

## 2. Ayrı gelişim planları ve öncelikler

| Plan | Amaç | Fazlar | Önerilen öncelik |
| --- | --- | --- | --- |
| A — Güvenilir canlı kullanım | Güvenli erişim, sorunsuz yayın, doğru finansal görünüm | 1–3 | Önce tamamlanmalı |
| B — Ekip ve işletme yönetimi | İş takibi, tahsilat, kârlılık ve anlaşılır raporlar | 4–6 | Günlük kullanım için yüksek değer |
| C — Veri aktarımı ve dış erişim | Elle girişi azaltma ve markalarla kontrollü paylaşım | 7–9 | Temel düzen oturduktan sonra |

Kullanıcı tüm fazların sırayla geliştirilmesini onayladı; 7 Eylül 2026 tarihinde Faz 2'yi, yedekleme dahil, şimdilik erteleyip Faz 3'ten devam edilmesini istedi. 11 Eylül'de erişim bilgileri bulunmadığı için Faz 8'in de ertelenmesini onayladı. Güncel sıra **1 → 3 → 4 → 5 → 6 → 7 → 9**. Ertelenen Faz 2 ve Faz 8 tamamlandı sayılmayacak. Diğer fazların zorunlu kapsamı ve kontrolleri tamamlanmadan sıradakine geçilmeyecek. Yeni ücretli hizmet, veri silme, canlı yayın ve belirsiz ticari hesaplama kararları ayrıca netleştirilecek.

### Faz 1 doğrulama kaydı

- Girişte varsayılan yöneticiye dönüş kaldırıldı; kullanıcı kimliği ve oturum sürümü her korumalı istekte doğrulanıyor.
- Kullanıcı oluşturma/düzenleme, kapatma, parola yenileme, rol doğrulama ve son etkin yönetici koruması tamamlandı.
- Backend build: 0 hata, 0 uyarı. 37 domain + 30 API testi başarılı.
- Web: tür kontrolü, kod kalite kontrolü ve üretim derlemesi başarılı.
- Canlı veriden ayrı bellek içi test API'siyle tarayıcıda giriş, ad düzenleme, çıkış, analistin yönetim ekranından engellenmesi ve rehber açılışı doğrulandı. 390 piksel telefon genişliğinde kullanıcı ekranı kontrol edildi; incelenen akışlarda tarayıcı hata kaydı yoktu.
- Kullanım rehberi güncellendi. Canlı yayın ve veritabanı değişikliği yapılmadı. PostgreSQL'e özgü eşzamanlılık/yük doğrulaması bu bellek içi testlerin kapsamı değildir.

## Plan A — Güvenilir canlı kullanım

### Faz 4 doğrulama kaydı — 8 Eylül 2026

- Marka sorumlusu, ayrı aday takip aşaması, bekleme nedeni, sonraki görüşme/adım ve tarihli görüşme notları eklendi. Marka sayfasında görevler ve mevcut belge alanı birlikte sunuluyor.
- İşlerim, tüm açık işler, gecikenler, tamamlananlar ve aylık onay bekleyenler görünümleri eklendi. Analist kendi görevini tamamlayabilir; görev atama/düzenleme yönetici ve iş ortağı rolündedir. Marka sorumluluğu finansal yetkiyi değiştirmez.
- Tarihler Türkiye takvimine göre değerlendirilir. Henüz bitmemiş ay için otomatik yüksek öncelikli “eksik dönem” hatırlatması kaldırıldı; kapanış ve yenileme görevleri kullanıcı tarafından belirlenmiş gerçek son tarihe dayanır.
- Aynı marka/ay kapanış görevi ve aynı anlaşma yenileme görevi hem API hem veritabanı benzersizlik kuralıyla korunur. Görev sürümü eşzamanlı değişikliğin sessizce ezilmesini engeller. Notlar ve görev geçmişi korunur.
- Backend: 0 hata / 0 uyarı; 48 domain + 47 API testi başarılı. Web tür kontrolü, lint ve üretim derlemesi başarılı.
- Canlı veriden ayrı tarayıcı testinde yönetici görev atadı, marka sorumlusunu değiştirdi ve görüşme notu ekledi. Analist kendi görevini tamamladı; açık listeden çıktı ve tamamlananlarda korundu. Göreve özel işlem geçmişi, 390 piksel telefon görünümü, analistin belge yükleme düğmesini görmemesi ve güncel rehber doğrulandı; incelenen akışlarda konsol hatası yoktu.
- `20260908004239_TeamWorkAndFollowUps` EF migration'ı Supabase `growth` şemasına uygulandı; migration listesi doğrulandı. Yalnız üç yeni tablo eklendi. Sahip uygulama hesabıdır; RLS açık, `anon` ve `authenticated` rollerine doğrudan okuma izni yoktur.
- Gerçek Supabase bağlantısında, başlangıç örnek verisini çalıştırmayan test hostuyla `/health`, görevler, onaylar, aday takibi ve görüşme okuma uçları 200 döndü. Transaction içinde geçici kayıtla birincil anahtar, ilişki, hedef kısıtı ve eski sürümle güncelleme reddi kontrol edildi; transaction geri alındı.
- Son kontrol: 11 mevcut marka; yeni görev/takip/not tablolarında 0 kayıt. Tarayıcı test verileri canlı veritabanına yazılmadı. Canlı panel/API yayını, commit ve push yapılmadı.
- Geri dönüş: Eski uygulama sürümü yeni tabloları kullanmaz; uygulama geri alınırsa bu tablolar yerinde bırakılır. Migration'ın `Down` işlemi yeni görev/not verilerini siler; veri oluştuğunda ayrıca açık izin olmadan çalıştırılmaz. Önceki migration geçmişi korunmuştur.

### Faz 5 için onaylanan iş kararları

### Faz 5A doğrulama kaydı — 9 Eylül 2026

- KDV hariç fatura referansı/tarihi/vadesi, parçalı ödeme, kalan alacak, gecikme ve yönetici gerekçeli hatalı kayıt iptali tamamlandı. Hakediş/kâr/ciro snapshot'ları değiştirilmez; iptal finansal dönem kilidini açmaz.
- Eski ödenmiş hakedişler tutar olarak korunur; bilinmeyen ödeme tarihleri uydurulmaz. Takvim ayındaki para girişi, hakediş ayına bağlı ödenen toplamından ayrıdır. Banka entegrasyonu, KDV hesabı, fazla ödeme ve gerçek para iadesi/mahsup kapsam dışıdır.
- 56 domain + 51 API = 107 test başarılı; web tür kontrolü, lint ve üretim derlemesi geçti. 100.000 TL → 40.000 TL ödeme → 60.000 TL kalan → ikinci ödeme ile kapanış → gerekçeli iptal akışı hem testte hem izole tarayıcıda doğrulandı. Rapor/kaynak bakiye eşitliği, güncel rehber, 390 px yatay taşmasız görünüm ve konsol hatasızlığı kontrol edildi.
- `20260908221022_CollectionLedger` Supabase'e uygulandı. Yalnız iki yeni tablo; uygulama sahipliği, RLS ve dış rollerin erişimsizliği doğrulandı. 11 marka, 4 dönem ve toplam 379.447,64 kayıtlı hakediş korundu. Yeni tablolarda kalıcı deneme verisi yok.
- Gerçek bağlantıda `/health`, hakediş listesi, portföy raporu ve tahsilat okuması 200. İki ayrı PostgreSQL bağlantısıyla aynı dönem için ikinci yazıcının kilitte reddi doğrulandı. Geçici transaction içinde pozitif tutar, ilişki, benzersizlik ve gerekçeli iptal kısıtları ile eski sürüm güncellemesinin reddi kontrol edildi; tüm deneme kayıtları geri alındı. Bekleyen model farkı yok.
- Yayın sırası: Eski API'yi durdurup yeni API ve paneli birlikte manuel yayımlayın. Yeni panel eski API ile çalıştırılmamalı. Tahsilat kaydı oluştuğunda eski API'ye ödeme yazma yetkisiyle geri dönmeyin; eski kör “ödendi” uçları yeni defteri tanımaz. Geri dönüşte defter tablolarını koruyun ve finansal yazmayı kapatın; `Down` ödeme geçmişini siler ve ayrıca açık izin olmadan çalıştırılmaz. Commit/push/canlı uygulama yayını yapılmadı.
- Kapsam dışı mevcut güvenlik bulgusu: Supabase danışmanı `public` şemasındaki 46 eski tabloda RLS kapalı ve `anon`/`authenticated` görünürlüğü bildiriyor. `growth` tabloları için böyle bir erişim açılmadı. Başka uygulamaların erişimini bozma riski nedeniyle bu tablolara dokunulmadı; ayrı yetki incelemesi gerekli. Yeni iç tabloların “RLS açık, policy yok” bilgi notu bilinçli API-only erişim desenidir.

### Onay kapsamı

### Faz 5B doğrulama kaydı — 9 Eylül 2026

- Kapalı döneme ayrı doğrudan gider ve beyana dayalı ekip saati/saat maliyeti kaydı eklendi. Gerçek gider, tahmini gidere eklenerek ikinci kez düşülmez. Kontrol tamamlanmadan katkı/kârlılık gösterilmez; sıfır gider de açıkça teyit edilir. Yönetici gerekçeli yeniden açma/iptal ve özel kontrol geçmişi korundu.
- Anlaşmada planlanan bütçe, gerçek yatırım harcaması ve açıkça kaydedilen geri kazanım ayrı gösterilir. Geri kazanım gerçek yatırımı aşamaz; tahsilat veya hakedişten otomatik pay alınmaz, yeni gelir oluşturulmaz. Aynı harcama referansı hizmet ve yatırım defterine birlikte eklenemez; PostgreSQL'de iki defterin eklemeleri ortak kilitle korunur.
- Yeni hassas maliyet/yatırım API'leri yalnız Admin/Partner için açıktır; iptal/yeniden açma yalnız Admin. Genel dönem/anlaşma cevapları ve analistin okuyabildiği audit ayrıntılarına saat maliyeti, gerçek gider veya özel notlar eklenmedi.
- 62 domain + 55 API = 117 test geçti. Web tür kontrolü, lint ve üretim derlemesi geçti. İzole tarayıcıda 10.000 TL gider + 20 saat × 1.000 TL ekip maliyeti = 30.000 TL maliyet / 70.000 TL katkı doğrulandı; kontrol tamamlanana kadar katkı gösterilmedi. 100.000 TL gerçek yatırım – 40.000 TL açık geri kazanım = 60.000 TL kalan doğrulandı. Analist hesabında iki özel alan görünmedi; rehber güncel, konsol hatasız, 390 px genişlikte yatay taşma yok.
- `20260908223117_OperatingCostsAndInvestment` Supabase'e uygulandı: yalnız beş yeni tablo, uygulama sahipliği, RLS açık ve `anon`/`authenticated` okuma izni yok. Eski finansal veri ve migration geçmişi korundu. Model/migration farkı yok.
- Gerçek bağlantıda sağlık, maliyet ve yatırım okumaları 200. Geri alınan transaction içinde ekip tutarı hesap kısıtı, pozitif tutar, gerekçeli iptal, benzersizlik, ilişki ve eski sürüm güncelleme reddi test edildi. İki bağlantıyla defterler arası yazma kilidi doğrulandı; deneme kayıtları kalıcı bırakılmadı.
- Geri dönüşte yeni tablolar korunur; `Down` maliyet/yatırım geçmişini siler ve ayrıca açık izin olmadan çalıştırılmaz. Tahsilat için Faz 5A'nın eski API ile finansal yazma yasağı geçerlidir. Commit, push ve canlı uygulama yayını yapılmadı.

### Finansal kararlar

Kullanıcı aşağıdaki iki öneriyi “evet ilerle” yanıtıyla onayladı:

1. Hakediş ve ödeme takibinde KDV hariç mi, KDV dahil tutar mı esas alınacak? Öneri mevcut hakedişle karşılaştırılabilir KDV hariç tutardır.
2. İlk maliyet görünümü yalnız markaya açıkça girilen doğrudan giderler ve yatırım geri kazanım kayıtlarını mı kullanacak? Öneri ortak giderleri veya hakedişleri kendiliğinden dağıtmamaktır.

İade/mahsup ve otomatik ortak gider dağıtımı kapsam dışıdır. Faz 5A tamamlanmadan 5B'ye, Faz 5 tamamlanmadan Faz 6'ya geçilmez.

### Faz 3 doğrulama kaydı — 8 Eylül 2026

- Özet, marka detayı ve raporlar ortak dönem/kapsam/para birimi sorgusunu kullanıyor. Hakediş listesi aynı kapsam tanımından tüm sayfaların toplamını alıyor.
- Varsayılan görünüm açıkça “Kapanmış dönemler” olarak adlandırıldı: kilitli, faturalanmış ve ödenmiş. Bu, yeni bir ticari kesinleşme kuralı değildir; onaylı ama kilitlenmemiş kayıtların ayrı görünümü vardır.
- Geçmiş aylardan alacak korunuyor; eksik kayıt sıfır gelir sayılmıyor. Para birimleri karışmıyor. Başlangıç yatırımı geri kazanılmış gibi gösterilmiyor.
- Backend: 0 hata / 0 uyarı; 46 domain + 37 API testi başarılı. Web tür kontrolü, lint ve üretim derlemesi başarılı.
- İzole test API'siyle tarayıcıda Eylül 2026 kapanmış toplamı özet, rapor ve hakediş ekranında 30.000 TL olarak eşleşti. 40.000 TL onaylı ve 900.000 TL taslak ayrı gösterildi; tüm kapsam 970.000 TL oldu. Ağustos'tan 20.000 TL alacak korundu; USD görünümü ayrı 1.000 dolar gösterdi.
- Son ay yalnız taslak olduğunda kapanmış toplam “veri yok” olarak göründü. Rapor 390 piksel telefon genişliğinde ve rehber sayfası kontrol edildi; incelenen akışlarda konsol hatası yoktu.
- Kayıtlı finansal sonuçlar yeniden hesaplanmadı. Şema değişikliği, canlı veri değişikliği ve canlı yayın yapılmadı.

### Faz 1 — Güvenli giriş ve eksiksiz kullanıcı yönetimi

**Kullanıcıya faydası:** Sisteme yalnızca izin verdiğin kişiler girer. Bir çalışan ayrıldığında erişimi gerçekten kapanır.

Kapsam:

- Aktif hesabı bulunmayan kişinin girişini reddetme; yönetici varsayılanına dönüşü kaldırma.
- İlk yönetici oluşturmayı günlük girişten ayrı, kontrollü bir başlangıç işlemi olarak ele alma.
- Hesap kapatıldığında, parola veya rol değiştiğinde eski oturumları geçersiz kılma.
- Kullanıcı düzenleme, rol değiştirme, hesabı kapatma ve yönetici tarafından güvenli parola yenileme akışlarını tamamlama.
- Son aktif yöneticinin yanlışlıkla kapatılması gibi erişim kaybı durumlarına karşı koruma.
- Giriş ekranındaki hazır parola değerini kaldırma; başarısız girişlere deneme sınırı koyma.
- Kullanıcıya görünen rol adlarını anlaşılır Türkçe sunma ve ilgili işlemleri geçmişe kaydetme.

**Tamamlanma ölçütleri:** Olmayan ve kapalı hesapla giriş reddedilir. Rolü değişen veya hesabı kapatılan kullanıcının eski oturumu bir sonraki korumalı istekte reddedilir. Yetkisiz kişi kullanıcı yönetemez. Son yönetici korunur. Bu durumlar otomatik testlerle doğrulanır ve rehber güncellenir.

**Bağımlılık / büyüklük:** Bağımsız başlangıç fazı; orta. E-posta daveti, kendi kendine şifre sıfırlama ve iki aşamalı doğrulama ayrıca kapsamlandırılabilir.

### Faz 2 — Güvenilir yayınlama ve veri koruma

**Kullanıcıya faydası:** Yeni sürüm çıktığında panel ve API uyumlu kalır; sorun yaşanırsa hangi sürüme ve nasıl dönüleceği bellidir.

Kapsam:

- GitHub Actions içinde ilgili otomatik testleri ve panel kontrollerini yayın öncesi zorunlu hale getirme.
- API ve panel için aynı başarılı sürüme ait sabit imaj etiketini kullanma; yalnızca iki imaj da hazırsa sürümü kurulabilir olarak işaretleme.
- Coolify'da manuel yayın tercihini koruma; otomatik canlı yayın açmama.
- Rehber gibi imajı etkileyen kök dosyaları da imaj hazırlama tetikleyicilerine dahil etme.
- Uygulama ayakta mı ve veritabanına erişebiliyor mu kontrollerini ayırma; hassas bilgi göstermeyen hata ve takip kayıtları.
- Örnek veri oluşturmayı açık tercihe bağlama; mevcut örnekleri onaylı bir liste üzerinden ayırt etme. Mevcut kayıtları silmeme.
- Yerel geliştirme, test ve canlı ortamın yanlışlıkla aynı gerçek veriyi değiştirmediğini doğrulama; gerekli ayrımı kurma.
- Yedekleme, geri yükleme ve veritabanı güncelleme prosedürünü belirleme; izole ortamda geri yükleme provası.
- Gerçek PostgreSQL üzerinde test ve migration doğrulaması ekleme. Mevcut bellek içi testler tek başına bunun yerine geçmez.
- Daha önce paylaşılmış gizli bilgilerin yenilenmesini, erişim ve kullanıcı onayıyla yayın kontrol listesine alma.

**Tamamlanma ölçütleri:** İmajlardan biri başarısızsa sürüm kuruluma hazır sayılmaz. İki bileşen aynı sürümle başlar. Veritabanı erişilemezken sistem yanlış bir “hazır” sonucu vermez. Normal yeniden başlatma örnek marka eklemez. Yedek izole ortamda açılır. Geri dönüşte veritabanı uyumluluğu da kontrol edilir; yalnızca eski imajı seçmek yeterli kabul edilmez.

**Bağımlılık / büyüklük:** Faz 1 önerilir; büyük. Canlı panel erişimi, yedek hedefi ve saklama süresi bu fazın başında netleştirilir. Ücretli servis veya kaynak açılması ayrıca onay gerektirir.

### Faz 3 — Finansal ekranların aynı doğruyu söylemesi

**Kullanıcıya faydası:** Hazırlanan sonuç, kesinleşen hakediş ve hesaba geçen para birbirine karışmaz.

Kapsam:

- Taslak, kontroldeki, onaylı ve kilitli sonuçların raporlarda nasıl gösterileceğini açıkça belirleme.
- Seçili dönemin sonucu ile tüm dönemlerden kalan alacağı ayrı gösterme.
- Eksik marka/dönem verisini görünür kılma; eksik kaydı sıfır gelir gibi sunmama.
- “Ciro”, “markaya kalan katkı”, “OVO hakedişi”, “OVO brüt kârı” ve “tahsil edilen tutar” için anlaşılır açıklamalar.
- Geri kazanım kaydı bulunmayan başlangıç yatırımını “kalan yatırım” diye göstermeme; uygun adlandırma ve sonraki hesap için veri ihtiyacını belirleme.
- Özet, marka detayı, hakediş ve rapor ekranları arasında aynı kapsam ve toplamları kullanma.

**Tamamlanma ölçütleri:** Aynı dönem ve aynı durum filtresinde bütün ekranların toplamları eşleşir. Taslak kayıt kesinleşmiş toplamı değiştirmez. Eski dönemdeki ödenmemiş hakediş genel alacaktan kaybolmaz. Eksik veri açıkça belirtilir. Önceden kilitlenmiş hesaplar değişmez.

**Bağımlılık / büyüklük:** Faz 1–2 önerilir; orta. “Kesinleşmiş hakediş hangi aşamada sayılır?” kararı başlamadan onaylanır. Bu faz mevcut ticari oranları değiştirmez ve geriye dönük finansal hesapları yeniden yazmaz.

## Plan B — Ekip ve işletme yönetimi

### Faz 4 — Marka sorumluları, görevler ve görüşme takibi

**Kullanıcıya faydası:** Herkes sabah açtığında kendi işlerini, gecikenleri ve hangi markada hangi adımın beklendiğini görür.

Kapsam:

- Markaya sorumlu kişi; göreve sorumlu, son tarih, öncelik ve tamamlanma durumu atama.
- “Benim işlerim”, “Gecikenler” ve “Onay bekleyenler” görünümleri.
- Marka özelinde görüşme notları, sonraki görüşme tarihi ve takip edilecek adım.
- Mevcut aday marka akışına aşama, bekleme nedeni ve son görüşme bilgisi ekleme.
- Marka detayında anlaşma, dönem sonuçları, belgeler, notlar ve görevleri birlikte sunma.
- Aylık kapanış ve anlaşma yenileme için gerçek teslim tarihine bağlı hatırlatmalar; henüz zamanı gelmemiş işi gecikmiş saymama.

**Tamamlanma ölçütleri:** Bir görev belirli çalışana atanır, yalnızca bir kez listelenir, tamamlandığında açık işlerden çıkar ve geçmişi korunur. Tarih değişikliği gecikme durumuna yansır. Marka sorumlusu ile finansal onay yetkisi birbirine karıştırılmaz; hazırlayan–onaylayan ayrımı korunur.

**Bağımlılık / büyüklük:** Faz 1; orta. İlk sürümde panel içi hatırlatma yeterlidir. E-posta/mesaj gönderimi ayrıca onay ve bağlantı kurulmasını gerektirir.

### Faz 5 — Tahsilat ve gerçek çalışma maliyeti

**Kullanıcıya faydası:** Hangi markanın borcu var, ne zaman ödeme bekleniyor ve OVO bu işe harcadığı emek karşılığında ne kazanıyor soruları cevaplanır.

Bu faz iki ayrı onaylanabilir parçaya ayrılır; 5A tamamlanmadan 5B başlamaz.

**5A — Tahsilat takibi:** Fatura referansı, vade, ödeme tarihi, parçalı ödemeler, kalan alacak, gecikme günleri ve gerekçeli düzeltmeler. Bu bir fatura/tahsilat takip akışıdır; resmi e-fatura düzenleme veya banka entegrasyonu değildir.

**5B — İşin gerçek maliyeti:** Marka bazında gerçekleşen hizmet giderleri ve ekip çalışma süresi/maliyetleri; planlanan ve gerçekleşen kârlılığın karşılaştırılması. Başlangıç yatırımının geri kazanımı ancak kabul edilmiş açık bir dağıtım kuralıyla hesaplanır. Personel maliyeti gibi hassas alanlar uygun rolle sınırlandırılır.

**Tamamlanma ölçütleri:** 100.000 TL hakedişe 40.000 TL ödeme girildiğinde kalan alacak 60.000 TL olur; aynı ödeme yanlışlıkla ikinci kez sayılmaz. Düzeltme geçmişi saklanır. Marka bazındaki gider iki kez düşülmez. Maliyeti eksik marka “kesin net kâr” etiketiyle sunulmaz. Kilitli dönemlerin ticari hesapları değişmez.

**Bağımlılık / büyüklük:** Faz 3; büyük. 5B için Faz 4 yararlıdır. Vergi dahil/hariç tutar, iade/mahsup, ortak gider dağıtımı ve yatırım geri kazanım tanımları iş sahibiyle netleştirilir; bu tanımlar onaysız varsayılmaz.

### Faz 6 — Anlaşılır yönetim ve marka raporları

**Kullanıcıya faydası:** Teknik olmayan bir çalışan, marka iyi mi gidiyor ve hangi konuda aksiyon gerekiyor sorularını okuyarak anlayabilir.

Kapsam:

- Marka ve dönem seçimi; önceki dönemle karşılaştırma; mevcut eğilim görünümünü bu seçimlerle uyumlu hale getirme.
- “Ne oldu?”, “Neden dikkat istiyor?” ve “Sonraki adım ne?” bölümlerinden oluşan marka özeti.
- Gelir yoğunlaşması, iade, reklam verimliliği, kârlılık ve eksik veri uyarılarını gerekçeleriyle sunma.
- İç yönetim raporu ve markayla paylaşılabilecek rapor için ayrı içerik kapsamları.
- PDF ve tablo olarak dışa aktarma; dönem, veri kapsamı, hazırlanma zamanı ve taslak/kesinleşmiş ayrımını çıktıya taşıma.
- Doğrulanmış sayılara dayalı açıklama şablonları; rapor yazmak için ilk aşamada yapay zekâ zorunlu değildir.

**Tamamlanma ölçütleri:** İndirilen rapor ve ekrandaki aynı filtreli sonuç eşleşir. Başka markanın veya OVO'nun iç maliyetleri paylaşılabilir rapora sızmaz. Uyarının hangi veriye dayandığı açıklanır. Veri yokken değişim oranı veya kesin yorum uydurulmaz.

**Bağımlılık / büyüklük:** Faz 3; orta. Ayrıntılı tahsilat ve gerçekleşen maliyet bölümleri ancak Faz 5 tamamlandıysa dahil edilir; Faz 4 şart değildir.

**Faz 6 doğrulama — 9 Eylül 2026:**

- Raporlar → marka seçimi → açıklamalı rapor akışı tamamlandı. Ay, kapsam ve para birimi aynı filtrelerle önceki takvim ayına uygulanır. Eski dönem hesapları değiştirilmez. Veri yokken sıfır veya büyüme oranı uydurulmaz.
- İç yönetim görünümü yalnız yönetici/ortak içindir; analist isteği API'de 403 alır. Paylaşılabilir rapor açık alan listesiyle hazırlanır; özel maliyetler, ödeme açıklamaları ve başka markalar JSON/CSV çıktısına alınmaz.
- CSV aynı rapor nesnesinden hazırlanır; formül başlatabilecek metinler etkisizleştirilir, negatif sayılar korunur. Kapsam, durum, tarih, para birimi ve kesinleşme uyarısı taşınır.
- PDF ayrı bir sunucu hizmeti değil, aynı raporun tarayıcıdaki **PDF olarak kaydet** çıktısıdır. Chrome'dan iki sayfalık gerçek test PDF'si kaydedilip her sayfa görsel olarak incelendi: menü yok, tablo taşmıyor, açıklama kartları bölünmüyor. CSV indirildi; 1.000.000 / 1.250.000 TL ciro, -%20 değişim ve diğer değerler ekran/PDF/tablo arasında eşleşti.
- İç yönetimde kontrol edilmemiş maliyetin katkısı gizlendi; yatırım kaydı olmayan durum geri kazanılmış gibi sunulmadı. 390 px mobil görünüm ve para birimi eksik giriş uyarısı kontrol edildi. Analist ekranında iç yönetim seçeneği yok. Tarayıcı hata günlüğü boş.
- 123 test geçti (65 domain + 58 API); typecheck/lint/üretim derlemesi ve migration-model uyumu doğrulandı. Gerçek Supabase'e bağlı yerel API'de paylaşılabilir ve iç rapor HTTP 200 verdi, kaynak ciro eşleşti; bu fazda veritabanına yazılmadı veya migration eklenmedi.
- Kullanım rehberi yeni akışı, paylaşım sınırlarını ve CSV/PDF adımlarını anlatıyor; `/guide` üzerinde yeni bölüm doğrulandı. Commit, push veya canlı uygulama yayını yapılmadı.

## Plan C — Veri aktarımı ve dış erişim

### Faz 7 — Excel/CSV ile güvenli toplu veri girişi

**Kullanıcıya faydası:** Her ay aynı bilgileri tek tek yazmak yerine bir dosya yükleyerek kontrollü biçimde içeri alabilirsin.

Kapsam:

- İlk aşamada aylık performans için tek bir açık dosya şablonu.
- Sütun eşleştirme, Türkçe sayı/tarih biçimleri ve yüklemeden önce ön izleme.
- Hatalı satırları nedenleriyle gösterme; kullanıcı onayı olmadan kayıt oluşturmama.
- Marka, anlaşma ve dönem eşleşmesi; tekrar yüklemede aynı veriyi çoğaltmama.
- Kilitli dönemlere yazmayı engelleme; dosyanın kim tarafından ne zaman aktarıldığını izleme.
- Mevcut dosya ekleme özelliğinden ayrı bir “veri içeri aktarma” akışı. Dosya eki yüklemek tek başına veri aktarımı değildir.

**Tamamlanma ölçütleri:** Hatalı dosya fark edilmeden kısmi kayıt bırakmaz. Aynı dosyanın tekrar yüklenmesi çift dönem oluşturmaz. Ön izleme ve kaydedilen toplam eşleşir. Yanlış marka ve kilitli dönem güncellemeleri engellenir.

**Bağımlılık / büyüklük:** Faz 2–3; orta. Başlamadan örnek bir gerçek çalışma dosyası kişisel bilgilerden arındırılarak seçilir.

**Güncellenen kapsam onayı — 10 Eylül 2026:** Kullanıcı örnek dosya yerine mevcut aylık sonuç alanlarına uygun standart Türkçe Excel/CSV şablonunu bizim hazırlamamızı onayladı. İlk sürüm yalnız yeni taslak dönem oluşturur; mevcut dönemlerin üzerine yazmaz. Dosya ve satır limitleri, ön izleme sonrası kullanıcı onayı, atomik kayıt, tekrarları engelleme ve işlem geçmişi korunur. Faz 2'nin ertelenmiş olması yedek/deploy çalışmalarına başlama izni değildir.

**Tamamlandı — 10 Eylül 2026:** Aylık sonuçlar → Dosyadan aktar ekranı, 25 alanlı Türkçe Excel/CSV şablonları, sütun eşleştirme, satır bazlı hata açıklamaları, para birimine göre toplamlar, 15 dakikalık kişiye/dosyaya/anlaşmaya bağlı ön izleme onayı ve son 30 başarılı aktarım geçmişi hazır. Tüm satırlar birlikte yeni taslak olarak kaydedilir. Boş tutarlar sıfıra çevrilmez; mevcut taslak dahil hiçbir dönemin üzerine yazılmaz. Otomatik onay, kilitleme, tahsilat ve geçmiş dönem güncellemesi yapılmaz. Yeni tablo veya migration gerekmedi.

Doğrulama kaydı:

- Backend derlemesi ve toplam 144 test (65 domain + 79 API): başarılı. Aktarım testleri; CSV ve Excel, dört ondalık hassasiyet, farklı para birimleri, tek hatalı satırda tüm dosyanın reddi, mükerrer dönem, kilitli kayıt, yanlış marka/anlaşma/tarih, yetkisiz rol, dosya sınırları, formül/XXE reddi, süresi dolmuş/değiştirilmiş onay ve ön izleme–kayıt toplam eşitliğini kapsar.
- Web typecheck, lint ve üretim derlemesi: başarılı. Son derleme geçici test API adresi kullanılmadan alındı.
- Gerçek tarayıcıda Excel şablonunun 25 sütunu otomatik eşleşti; örnek kodlar değiştirilmeden kayıt yapılamadı. Geçerli CSV ön izlendi, açık onaydan sonra taslak ve aktarım geçmişi oluştu; aynı dosya yeniden seçildiğinde mevcut dönem reddedildi. 390 piksel genişlikte sayfa taşmadı; tablo kendi alanında kaydırıldı. Analist aktarım ekranında yetki uyarısı gördü. Rehberde yeni bölüm açıldı; tarayıcı hata kaydı yoktu.
- Gerçek Supabase PostgreSQL bağlantısında iki satırlık aktarım, API üzerinden ön izlendi ve tek işlem içinde yazıldı. Kalıcı kayıt öncesi test kesicisi iki taslağı, üç işlem geçmişi kaydını ve tutar hassasiyetini doğruladı; ikinci bağlantının anlaşma yazma kilidini alamadığını kontrol etti. Ardından bilinçli hata ile işlem tamamen geri alındı. Mevcut dört dönem, işlem geçmişi sayısı ve hakediş toplamı değişmedi; kalıcı test verisi bırakılmadı.
- Kullanım rehberi güncellendi. Commit, push veya canlı yayın yapılmadı. Genel Supabase güvenliği için önceki fazlarda belirtilen, bu projeye ait olmayan `public` şeması bulgusu bu kontrolle çözülmüş sayılmaz.

### Faz 8 — Mağaza ve reklam verilerinin otomatik alınması

**Kullanıcıya faydası:** Düzenli veri girişi azalır; rakamın nereden geldiği ve en son ne zaman güncellendiği görünür.

Önerilen sıra:

1. **8A — Tek mağaza pilotu:** Kullanılan platform doğrulanır. Shopify kullanılıyorsa bir mağazanın sipariş ve iadelerini salt okunur bağlantıyla alma.
2. **8B — Tek reklam kaynağı pilotu:** Kullanılan Meta veya Google hesabından reklam giderini alma; ilk kaynak bitmeden ikinciye geçmeme.
3. **8C — Yaygınlaştırma:** Pilotta doğrulanan eşleşmeleri diğer markalara uygulama; bağlantı kopması ve tekrar deneme takibi.

Ortak kapsam: Kaynak kimliğiyle tekrarları önleme, para birimi ve saat dilimi eşleştirme, iade/iptal kuralları, bağlantı yetkileri ve aktarım geçmişi. Gelen veri önce hazırlık alanına alınır; aylık onay ve kilitleme süreci atlanmaz.

**Tamamlanma ölçütleri:** Seçilen pilot dönem için kaynak ve panel tutarları uzlaştırılır; farklılıkların nedeni görünür. Tekrar aktarım çift kayıt yaratmaz. Bağlantı kesilince eski veri yeniymiş gibi gösterilmez. Reklam bütçesine veya mağaza siparişlerine yazma yapılmaz.

**Bağımlılık / büyüklük:** Faz 7; büyük. Her alt faz ayrı onaylanır. Kullanılan platformlar, erişimler ve güncel sağlayıcı koşulları o alt fazın başında doğrulanır; henüz sağlayıcı uyumluluğu veya maliyet taahhüdü yoktur.

**Pilot seçimi — 11 Eylül 2026:** Kullanıcı seçimi bize bıraktı. Supabase `growth` şemasındaki 11 marka salt okunur sorguyla karşılaştırıldı. Kozabiat (`ba070688-3a6f-44c7-992e-16315418bf46`), kayıtlı adresi `https://www.kozabiat.com/` ve altyapısı GrandNode olduğu için seçildi. Etkin anlaşması ve aylık dönem kaydı yok; bu kayıtları otomatik oluşturma veya markayı etkinleştirme kararı alınmadı. Etkin anlaşma ve dönem verisi bulunan Arven Home, Lodos Coffee, Luna Jewelry ve Minoa Skin kaynak kodundaki örnek portföyde yer alıyor; boş/`.example` adresler gerçek Shopify bağlantısı için kanıt değildir.

Hazırlık bulguları ve devam kapısı:

- Yerel `OVO.V4.Base` projesindeki `src/API/Grand.Api/Controllers/OData/OrderController.cs`, `Controllers/TokenController.cs`, `Controllers/BaseODataController.cs`, `Filters/AuthorizeApiAdminAttribute.cs` ve `DTOs/Order/OrderDto.cs` salt okunur incelendi; diğer projede değişiklik yapılmadı. Yerel kaynakta sipariş okuma yolu `GET /odata/Order`, kimlik doğrulama yolu `POST /Api/Token/Create`. Bu yolların Kozabiat'ın canlı sürümünde etkin olduğu henüz doğrulanmadı.
- Sipariş okuma ve bazı değiştirme işlemleri aynı sipariş iznini kullanıyor. Üstelik `SetAsImported` adlı bir GET işlemi kayıt değiştiriyor. Bu yüzden yalnız HTTP yöntemine göre “salt okunur” sayılmayacak; yalnız doğrulanmış okuma yollarına izin verilecek. Mağazada sınırlı erişim sağlanmadan mevcut yönetici kimliği yeniden kullanılmayacak, güvenlik ayarı değiştirilmeyecek.
- Sipariş tutarı, vergi, kargo, para birimi, tarih, sipariş/ödeme durumu ve toplam iade alanları mevcut. Toplam iade alanı tek başına iadenin hangi ayda gerçekleştiğini kanıtlamıyor. Pilot dönem uzlaştırılmadan bu değer aylık iade veya net ciroya otomatik yazılmayacak. Kaynak sayıları `decimal` olarak okunacak; müşteri adı, adresi, e-postası gibi gereksiz kişisel alanlar alınmayacak.
- Growth veritabanında mağaza bağlantısı/erişim yapılandırması bulunmadı. Devam için canlı API ana adresi, doğru mağazayı belirleyen kimlik ve sınırlandırılmış API erişimi gerekiyor. Sırlar sohbet metnine veya repoya değil yerel güvenli ayarlara/ortam değişkenlerine konacak.
- Kayıtlı web adresinde `https://www.kozabiat.com/odata/$metadata` kimlik bilgisi gönderilmeden HEAD isteğiyle kontrol edildi ve HTTP 404 döndü. Bu sonuç başka bir adreste API bulunmadığını göstermez; kayıtlı web adresinin API adresi olarak kullanılamayacağını ve canlı adresin ayrıca doğrulanması gerektiğini gösterir.
- İlk doğrulama önceki tamamlanmış takvim ayının (Ağustos 2026) sipariş ve iade verileriyle, yalnız hazırlık ve uzlaştırma düzeyinde yapılacak. Eski dönem erişimi ve saat dilimi kaynakta doğrulanacak. Etkin anlaşma olmadan aylık hakediş kaydı yaratılmayacak; reklam gideri ve bilinmeyen maliyetler sıfır varsayılmayacak.
- Bu çalışma pilot seçimi ve kaynak sözleşmesi incelemesidir; çalışan canlı entegrasyon veya tamamlanmış Faz 8A olarak sunulmaz.

**Erteleme onayı — 11 Eylül 2026:** Kullanıcı erişim bilgilerinin bulunmadığını belirtti ve fazın atlanmasına izin verdi. Faz 8 ertelendi; sahte bağlantı veya gerçekmiş gibi gösterilen mağaza verisi eklenmedi. Sıradaki onaylı Faz 9'a geçildi. Erişimler sağlandığında Faz 8'in doğrulama kapıları aynen korunarak dönülecek.

### Faz 9 — Markaya özel güvenli müşteri portalı

**Kullanıcıya faydası:** Marka yetkilisi kendi raporunu ve kendisiyle paylaşılan belgeleri görür; her şeyi ayrı mesajla göndermek gerekmez.

Kapsam:

- İç ekip rollerinden ayrı, sadece atanmış markaya erişebilen müşteri hesabı.
- Markaya özel özet, yayımlanmış dönem raporu ve paylaşılması seçilmiş belgeler.
- İlk sürümde görüntüleme ve açıklama isteme; ticari onay yetkileri ayrıca kararlaştırılır.
- Marka erişimini liste, detay, arama, dosya indirme ve dışa aktarma dahil her yerde uygulama.
- Paylaşılan raporların sürümünü koruma ve erişimi iptal etme.

**Tamamlanma ölçütleri:** A markasının hesabı, bağlantı veya kayıt kimliğini değiştirerek B markasının tek bir kaydına, belgesine ya da raporuna ulaşamaz. OVO'nun iç maliyetleri ve iç notları görünmez. İptal edilen hesap mevcut oturumuyla da erişemez.

**Bağımlılık / büyüklük:** Faz 1–3 ve 6; büyük. Mevcut iç ekip yetkileri tek başına dış müşteri erişimi için yeterli değildir. Kullanıcı ihtiyacı kesinleşmeden genel amaçlı çok şirketli bir SaaS dönüşümü yapılmaz.

**Faz 9 tamamlandı — 11 Eylül 2026.** Doğrulama kaydı:

- Müşteri rolü iç ekip rollerinden ayrıldı. Hesap yalnız tek markaya bağlı; yayımlanmayan rapor ve belgeler kapalı. Sorular hesap sahibine özel, yanıtlar açık onayla yayımlanıyor. Rapor sürümleri değiştirilmiyor; geri çekme geçmişi silmiyor.
- Backend derlemesi ve 155 test (65 domain + 90 API) geçti. İç ekip API yollarının müşteri rolüne kapalı olması, diğer markanın rapor/CSV/belge/sorusuna erişememe, hesap değişikliğinde eski oturumun geçersizliği, iç verilerin paylaşılmaması ve sürüm/geri çekme test edildi.
- Gerçek Supabase bağlantısında sağlık ve portal yönetiminin beş okuma yolu HTTP 200 verdi. Gerçek rapor yayımlama işlemi commit öncesi kontrollü hata ile geri alındı; kaydedilen snapshot'ın kaynak tutarıyla eşleştiği ve ikinci bağlantının yayımlama yazma kilidini alamadığı doğrulandı. Ayrı işlemde marka ilişkisi, benzersiz sürüm, geçersiz ay ve eski yanıt kontrolü denendi; tamamı rollback ile temizlendi.
- Son veritabanı kontrolü: 11 marka, 4 dönem, toplam 379.447,6400 hakediş korundu; dört portal tablosu boş. Gerçek müşteri hesabı, rapor yayını veya belge paylaşımı oluşturulmadı.
- `20260910212239_CustomerPortal` migration'ı uygulandı. Dört yeni tablo `growth` şemasında, sahibi `ovo_growth_app`, RLS açık; `anon` ve `authenticated` okuma izinleri kapalı. Doğrudan uygulama rolüyle kullanılan bu özel tabloların politikasız RLS bilgi uyarısı bilinçlidir; [açıklama](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy). Projeye ait olmayan `public` şemasındaki önceden bilinen 46 RLS bulgusu çözülmedi; [ayrı güvenlik kontrolü](https://supabase.com/docs/guides/database/database-linter?lint=0013_rls_disabled_in_public) gerektirir.
- İzole tarayıcı testinde yönetici girişi, dönem ön izlemesi, iki rapor sürümü, belge paylaşımı, müşteri girişi, soru gönderme, ekip yanıtı ve rapor sürümünü geri çekme çalıştı. Müşteri listesinde geri çekilen sürüm kayboldu; `/brands` adresi portalına yönlendirdi. 390 piksel mobil görünümde yatay taşma veya iç ekip menüsü yoktu; müşteri konsolunda hata görülmedi.
- Yerel SDK'nın üretilen `bin` dosyalarını web içeriği olarak tekrar kopyaladığı doğrulandı. API projesinde yalnız `bin/**` ve `obj/**` içerikten çıkarıldı; içerik listesinde 431 üretilen dosya sıfıra indi, tekrar derleme ve test geçti.
- Portalın yalnız seçili ayı kapsaması, önceki ay kaydının bulunmadığı şeklinde yorumlanmıyor; açıklama ve API testi düzeltildi. Geçmiş paylaşımların içeriği değiştirilmedi, gerçek veritabanında yayımlanmış rapor bulunmuyordu.
- Chrome'da müşteri CSV'si (1.353 bayt) ve paylaşılan deneme belgesi (51 bayt) indirildi. Yazdır/PDF ön izlemesinde yalnız raporun yer aldığı tek sayfalık çıktı, tutarlar, sürüm ve uyarılar görüldü; fiziksel yazdırma yapılmadı. Uygulama içi tarayıcının onay/indirme aracı sınırlamaları nedeniyle bu işlemler gerçek Chrome penceresinde doğrulandı.
- Web typecheck, lint ve varsayılan yerel ayarlarla son üretim derlemesi geçti; `/guide`, `/portal` ve `/portal-management` derlendi. Rehberin portal eğitim bölümü üretilen HTML'de mevcut. Son istemci dosyalarında geçici `localhost:18081` adresi yok. Test sunucuları ve test sekmeleri kapatıldı; `git diff --check` geçti.
- Commit, push veya canlı yayın yapılmadı. Geri dönüşte önceki uygulama sürümüne dönüp eklenen tabloları korumak tercih edilir; veri oluştuğunda tablo silen migration geri dönüşü ayrıca onay gerektirir.

## 3. Daha sonra değerlendirilebilecek fikirler

Bunlar yukarıdaki fazlara dahil değildir; ayrı ihtiyaç ve kapsam onayı ister:

- **Yapay zekâ destekli rapor anlatımı:** Doğrulanmış sonuçları sadeleştirir; hakediş hesaplamaz, anlaşma onaylamaz ve eksik veriyi tahmin ederek kesin bilgi gibi sunmaz. Kullanım maliyeti ve veri paylaşımı önceden onaylanır.
- **Hedef ve bütçe takibi:** Marka için dönem hedefi, gerçekleşen sonuç ve farkın nedenleri. Hedef gelir garanti edilmiş gelir gibi gösterilmez.
- **Ekip kapasite planı:** Yeni marka alındığında hangi ekipte ne kadar iş oluşacağı; Faz 4 ve 5B verileri olgunlaştıktan sonra.
- **Belge depolamasını ölçekleme:** Mevcut dosya hacmi gerektirirse dosyaları ayrı depolama alanına taşıma; erişim izinleri ve yedekleme korunarak.
- **Davet ve iki aşamalı giriş:** Özellikle ekip ve dış kullanıcı sayısı büyüdüğünde hesap güvenliğini güçlendirme.

## 4. Fazlarla nasıl ilerleyeceğiz?

1. Tüm fazlar için alınmış geliştirme onayıyla belirtilen sırada ilerlenir.
2. Başlamadan kapsam, gerekli iş kararları ve kabul örnekleri netleştirilir. Yeni ücret, dış erişim veya veri silme gerekiyorsa ayrıca onay alınır.
3. Yalnızca o kapsam uygulanır. Mevcut kayıtlar, geçmiş anlaşma koşulları ve kilitli dönemler korunur.
4. İlgili otomatik testler, derleme ve kullanıcı akışı kontrolleri yapılır. Veri değişikliği varsa migration ve geri dönüş etkileri doğrulanır.
5. Kullanıcıya görünen değişiklikler aynı çalışmada `OVO_GROWTH_OS_KULLANIM_REHBERI.md` dosyasına anlaşılır örneklerle eklenir. Henüz yapılmamış özellikler rehberde varmış gibi anlatılmaz.
6. Tamamlananlar, test kanıtları ve kalan sorunlar sunulur. Başarısız kontrol veya zorunlu eksik varsa faz tamamlandı sayılmaz.
7. Canlı yayın senin manuel yayın tercihinle yürütülür. Tamamlanan fazın kanıtları sunulduktan sonra sıradaki faza geçilir; kapsam dışı işlemler için ayrıca onay alınır.

**Güncel sıra:** Faz 2 ve Faz 8 kullanıcı onayıyla ertelendi. Faz 1, 3, 4, 5A, 5B, 6, 7 ve 9 tamamlandı. Bu plandaki ertelenmeyen fazların geliştirme ve doğrulamaları bitti; yeni fikirler ayrıca kapsam onayı ister. Faz 8 için canlı API adresi, mağaza kimliği ve sınırlandırılmış erişim sağlandığında Kozabiat / GrandNode pilotuna dönülecek; erteleme tamamlanma değildir.

## 5. İnceleme dayanakları

Bu bölüm geliştirme sırasında bulguların izlenebilmesi içindir; planı anlamak için teknik dosyaları okuman gerekmez.

- Giriş ve oturum doğrulama: [Program.cs](../apps/api/src/OvoGrowthOS.Api/Program.cs), [JwtTokenService.cs](../apps/api/src/OvoGrowthOS.Api/Auth/JwtTokenService.cs).
- Kullanıcı yönetimi, özet, görev ve tahsilat akışları: [WorkflowEndpoints.cs](../apps/api/src/OvoGrowthOS.Api/Features/WorkflowEndpoints.cs).
- Kullanıcı ekranı ve rapor kapsamı: [users/page.tsx](../apps/web/src/app/users/page.tsx), [reports/page.tsx](../apps/web/src/app/reports/page.tsx).
- Başlangıç ve örnek veri davranışı: [SeedData.cs](../apps/api/src/OvoGrowthOS.Api/Data/SeedData.cs).
- İmaj hazırlama ve yayın akışı: [container-images.yml](../.github/workflows/container-images.yml).
- Mevcut API doğrulamaları: [WorkflowApiTests.cs](../apps/api/tests/OvoGrowthOS.Api.Tests/WorkflowApiTests.cs).
- Korunacak iş kuralları: [AI_GELISTIRME_KURALLARI.md](AI_GELISTIRME_KURALLARI.md), [mevcut kullanım rehberi](../OVO_GROWTH_OS_KULLANIM_REHBERI.md), [önceki yol haritası](ROADMAP.md).
