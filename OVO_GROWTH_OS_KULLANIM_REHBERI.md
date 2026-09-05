# OVO Growth OS Kullanım Rehberi

Bu rehber, OVO Growth OS'u ilk kez kullanacak bir ekip arkadaşının sistemi herhangi bir teknik bilgiye ihtiyaç duymadan anlayabilmesi için hazırlanmıştır. Uygulamadaki **Kullanım rehberi** sayfası doğrudan bu dosyayı gösterir. Sisteme yeni bir özellik eklendiğinde veya bir iş akışı değiştiğinde bu dosya da aynı geliştirme kapsamında güncellenmelidir.

Rehberde; uygulamanın ne işe yaradığı, hangi ekranda ne yapılması gerektiği, kararların nasıl oluştuğu ve bir markanın ilk görüşmeden aylık hakediş kapanışına kadar nasıl yönetildiği anlatılır.

### Rehber nasıl güncel tutulur?

Projenin geliştirme kuralları Codex, Claude, Cursor ve benzeri kodlama araçlarına; kullanıcıyı etkileyen her değişiklikte bu rehberi de güncelleme zorunluluğu verir. Yeni bir ekran, düğme, rol, hesaplama veya çalışma sırası eklendiğinde ilgili açıklama aynı geliştirme içinde bu dosyaya işlenmelidir. Uygulamadaki rehber sayfası dosyanın güncel hâlini otomatik gösterir.

---

## 1. OVO Growth OS nedir?

OVO Growth OS, OVO Digital'in marka iş birliklerini değerlendirdiği ve yönettiği iç çalışma sistemidir.

Bir markayla çalışmaya başlamadan önce şu sorulara cevap vermemize yardımcı olur:

- Bu markayla çalışmak OVO için mantıklı mı?
- Marka büyümeye gerçekten hazır mı?
- Markanın kâr marjı, stok yapısı ve reklam bütçesi yeterli mi?
- OVO bu iş birliğinde ne kadar risk alıyor?
- Hangi anlaşma modeli iki taraf için daha sağlıklı olur?
- Aylık sabit ücret gerekli mi?
- Cirodan veya büyümeden ne kadar pay alınmalı?
- Marka reklam harcamasını artırırsa veya ciro düşerse sonuç ne olur?
- Ay sonunda OVO'nun hakedişi ne kadardır?
- Marka, OVO hakedişi ödendikten sonra gerçekten kârlı kalıyor mu?

Kısacası sistem, yalnızca müşteri bilgilerini saklayan bir CRM değildir. OVO Growth OS; iş ortaklığı kararı veren, farklı anlaşma seçeneklerini karşılaştıran, aylık sonuçları hesaplayan ve OVO portföyünün genel sağlığını gösteren bir karar ve operasyon sistemidir.

## 2. Sistem neden kullanılıyor?

Performansa dayalı iş birliklerinde yalnızca “ciro yüksek görünüyor” demek yeterli değildir. Yüksek cirolu bir marka düşük kâr marjı, yüksek iade oranı veya yetersiz stok nedeniyle kötü bir iş ortaklığına dönüşebilir. Daha küçük bir marka ise güçlü marjı ve doğru operasyon yapısıyla çok daha sağlıklı bir büyüme fırsatı sunabilir.

OVO Growth OS şu faydaları sağlar:

- Marka değerlendirmelerini kişisel yoruma bağlı olmaktan çıkarır.
- Her marka için aynı temel soruların sorulmasını sağlar.
- Eksik veya tahmini bilgileri görünür hâle getirir.
- OVO'nun maliyetini ve hedeflediği kârı korur.
- Markanın da iş birliği sonrasında kârlı kalıp kalmadığını gösterir.
- Farklı anlaşma modellerini aynı koşullarda karşılaştırır.
- Aylık hakedişleri kayıtlı anlaşma şartlarına göre hesaplar.
- Geçmiş kararların ve değişikliklerin izlenebilmesini sağlar.
- Yönetimin tüm marka portföyünü tek ekrandan takip etmesine yardımcı olur.

## 3. Sistemin temel çalışma akışı

Bir markanın sistemdeki yolculuğu genel olarak şöyledir:

1. Potansiyel marka sisteme eklenir.
2. Marka hakkında temel bilgiler toplanır.
3. Değerlendirme formu doldurulur.
4. Sistem finansal yapıyı ve veri kalitesini analiz eder.
5. Karar kuralları uygulanır.
6. Sistem kabul, koşullu kabul, ek bilgi gerekli veya ret kararı üretir.
7. Farklı büyüme ve anlaşma senaryoları denenir.
8. Ticari seçenekler karşılaştırılır.
9. Uygun anlaşma kabul edilir ve etkinleştirilir.
10. Her ay gerçekleşen finansal sonuçlar girilir.
11. OVO hakedişi hesaplanır, kontrol edilir ve dönem kapatılır.
12. Sonuçlar ana sayfa ve raporlara yansır.

Bu sıralamanın korunması önemlidir. Örneğin değerlendirme onaylanmadan anlaşma oluşturulmamalı, anlaşma etkinleştirilmeden aylık sonuç girilmemelidir.

---

## 4. Uygulamayı açma ve giriş yapma

### VS Code üzerinden çalıştırma

Proje VS Code ile açıldığında sol taraftaki **Çalıştır ve Hata Ayıkla** bölümüne girin.

Listeden **OVO: Full Stack + Chrome** seçeneğini seçip çalıştırın. Sistem:

- OVO API'sini başlatır,
- web uygulamasını başlatır,
- giriş ekranını Chrome'da otomatik açar.

Uygulama açıldıktan sonra teknik terminal pencerelerine müdahale etmeniz gerekmez. İşiniz bittiğinde VS Code'daki kırmızı durdurma düğmesine basabilirsiniz.

### Canlı yayın ortamı

Uygulamanın canlı sürümü OVO'nun Coolify sunucusunda iki parçayla çalışır: kullanıcıların gördüğü web arayüzü ve verileri yöneten API. Veritabanı Supabase'te kalır. Bu parçalar aynı sürüm olarak hazırlanır ve yalnızca yayın sorumlusu Coolify'dan **Deploy** işlemini başlattığında güncellenir.

GitHub'a kod gönderilmesi tek başına canlı sistemi değiştirmez. Yeni sürüm önce sağlık kontrollerinden geçirilir; web giriş ekranı ve API sağlık adresi çalışıyorsa trafik yeni sürüme yönlendirilir. Yayın sorumlusu için alan adları, güvenli ayarlar ve kontrol sırası `docs/COOLIFY_YAYIN.md` dosyasında anlatılmıştır. Netlify projesi ise otomatik yayın yapmayacak şekilde durdurulmuştur.

### Kullanıcı hesabınızla giriş

Varsayılan yerel giriş bilgileri:

- E-posta: `admin@ovodigital.com`
- Şifre: `ChangeMe123!`

Yönetici, **Ayarlar → Kullanıcılar** alanından her çalışan için kişiye özel hesap oluşturabilir. Ortak şifre kullanmayın. Rolünüz hangi ekranlarda değişiklik yapabileceğinizi belirler.

### Hazır örnek portföyü kullanma

Sistemi ilk açtığınızda boş ekranlarla karşılaşmamanız için farklı iş durumlarını gösteren gerçekçi bir örnek portföy hazırlanmıştır. Supabase veritabanındaki mevcut kayıtlar korunur; eksik örnekler uygulama açılışında yalnızca bir kez eklenir. Güncel çalışma veritabanında toplam 11 marka bulunur.

Hazır örneklerde aktif iş ortakları, değerlendirme aşamasındaki adaylar, görüşmesi süren markalar, duraklatılmış çalışmalar ve reddedilmiş adaylar birlikte görülebilir. Ciro, brüt kâr marjı, reklam bütçesi, sipariş tutarı, iade oranı, stok yeterliliği ve operasyon puanları her marka için farklıdır. Böylece yalnızca başarılı örnekleri değil; düşük marj, yüksek iade, yetersiz stok, eksik veri ve güçlü büyüme fırsatı gibi farklı durumları da inceleyebilirsiniz.

Analizi tamamlanmış örneklerde **Temkinli**, **Beklenen** ve **Büyüme** senaryoları hazırdır. Ayrıca etkin, önerilmiş ve süresi dolmuş anlaşma örnekleri ile ödenmiş, kilitlenmiş ve incelemede olan aylık sonuçlar bulunur. Bu kayıtlar eğitim ve deneme amacı taşır; örnek marka adları ve iletişim bilgileri gerçek bir şirketi veya kişiyi temsil etmez. Gerçek iş akışına geçerken kendi marka kayıtlarınızı ayrıca oluşturabilirsiniz.

---

## 5. Ana sayfa nasıl okunur?

Giriş yaptıktan sonra açılan **Portföy özeti**, OVO'nun tüm aktif marka iş birliklerini üst seviyeden gösterir.

### Üst göstergeler

#### Aktif markalar

Hâlihazırda etkin anlaşması bulunan marka sayısıdır.

#### Portföy net cirosu

Markaların iadeler, iptaller ve ilgili düşüşler sonrasındaki toplam net cirosudur.

#### OVO geliri

Seçili aylık dönemde markalardan hesaplanan toplam OVO hakedişidir.

#### OVO brüt kârı

OVO gelirinden bu markalara hizmet verirken oluşan iç maliyetlerin çıkarılmasıyla bulunan tutardır.

#### OVO brüt kâr marjı

OVO gelirinin ne kadarının maliyetler sonrasında OVO'da kaldığını gösterir.

### Grafikler ve tablolar

- **Ciro ve kâr gelişimi:** Son dönemlerde portföy cirosunun, OVO gelirinin ve OVO brüt kârının nasıl değiştiğini gösterir.
- **Verimlilik gelişimi:** Reklam verimliliği ve OVO marjındaki değişimi gösterir.
- **Aktif anlaşma modelleri:** Portföyde hangi anlaşma modellerinin kaç kez kullanıldığını gösterir.
- **Markaların durumu:** Her markanın cirosunu, OVO hakedişini, MER değerini ve katkı marjını yan yana gösterir.
- **Portföy riski:** Gelirin birkaç markada aşırı yoğunlaşıp yoğunlaşmadığını gösterir.
- **Operasyon göstergeleri:** Devam eden değerlendirme, açık dönem veya işlem bekleyen kayıtları özetler.

Ana sayfadaki rakamlar elle yazılmış sabit değerler değildir. Kaydedilmiş marka, anlaşma ve aylık sonuçlardan hesaplanır.

---

## 6. Potansiyel marka ve marka kaydı

### Potansiyel Markalar ekranı

Henüz değerlendirme aşamasına alınmamış fırsatlar burada görülür.

Bu ekranı şu amaçlarla kullanın:

- Yeni görüşülen markaları takip etmek,
- sektör ve iletişim bilgilerini görmek,
- uygun olduğunda değerlendirmeyi başlatmak.

### Yeni marka ekleme

**Markalar** ekranındaki **Marka ekle** düğmesine basın.

Girilen temel bilgiler şunlardır:

- Marka adı
- Ticari unvan
- İnternet sitesi
- Sektör
- Yetkili adı
- Yetkili e-posta adresi
- Kullanılan e-ticaret altyapısı

Bilgileri kaydettikten sonra değerlendirme sürecine geçebilirsiniz.

### Marka detay ekranı

Marka adına tıklandığında markanın özet sayfası açılır. Burada:

- aylık ciro,
- brüt kâr marjı,
- reklam harcaması,
- iş ortaklığı puanı,
- son değerlendirme,
- mevcut anlaşma,
- markanın güncel durumu

görülebilir.

Bu ekran, marka hakkında hızlı bir yönetim özeti almak için kullanılır.

---

## 7. Marka değerlendirmesi nasıl yapılır?

Değerlendirme, sistemin en önemli bölümüdür. Burada amaç mümkün olduğunca çok sayı girmek değil; karar için gerekli bilgileri doğru ve dürüst biçimde kaydetmektir.

Değerlendirme 10 adımdan oluşur. Çalışma sırasında **Kaydet ve çık** seçeneğiyle taslağı saklayabilir, daha sonra devam edebilirsiniz.

### 1. Firma

Değerlendirilecek kayıtlı markayı seçin.

Doğru marka seçildiğinden emin olun. Değerlendirme kaydedildikten sonra başka bir markaya taşınmamalıdır.

### 2. Ciro ve satış

Bu adımda markanın satış hacmi girilir:

- Aylık ciro
- Ciro bilgisinin güven düzeyi
- Ortalama sepet tutarı
- Sepet tutarının güven düzeyi
- Aylık sipariş sayısı
- Aylık site ziyareti

Mümkünse bu bilgileri markanın e-ticaret paneli, muhasebe raporu veya analiz aracından doğrulayın.

### 3. Ürün kârlılığı

Bu adım, markanın satış yaptıktan sonra elinde ne kadar ekonomik alan kaldığını anlamamızı sağlar:

- Brüt kâr marjı
- Marj bilgisinin güven düzeyi
- Ürün maliyeti oranı
- Ürün maliyetinin güven düzeyi
- İade oranı
- İade bilgisinin güven düzeyi
- Değişken gider oranı

Brüt kâr marjı, anlaşma modelini etkileyen en önemli bilgilerden biridir. Marj çok düşükse yüksek reklam harcaması ve OVO payı markayı zarara sokabilir.

### 4. Pazarlama

Markanın mevcut pazarlama yapısı değerlendirilir:

- Aylık reklam harcaması
- Reklam harcamasının güven düzeyi
- Yeni müşteri edinme maliyeti, yani CAC
- CAC bilgisinin güven düzeyi
- Müşteri yaşam boyu değeri, yani LTV
- LTV bilgisinin güven düzeyi

### 5. Operasyon

Markanın büyümeyi karşılayıp karşılayamayacağı değerlendirilir:

- Stok yeterlilik günü
- Stok bilgisinin güven düzeyi
- Operasyonel hazırlık puanı

Satışları büyütmek tek başına başarı değildir. Marka siparişleri hazırlayamıyor, stok sağlayamıyor veya müşteri hizmetlerini yönetemiyorsa büyüme yeni sorunlar oluşturabilir.

### 6. Ürün ve marka

Bu adımda 1 ile 5 arasında puan verilir:

- Ürün-pazar uyumu
- İçerik üretme kapasitesi
- Kurucu veya yönetim ekibinin iş birliği seviyesi

Puan verirken kişisel yakınlıktan çok gözlemlenebilir davranışlara bakılmalıdır. Örneğin gerekli içeriklerin zamanında hazırlanması ve verilerin şeffaf paylaşılması önemlidir.

### 7. Büyüme

Markanın büyüme potansiyeli ve veriyle çalışma yetkinliği değerlendirilir:

- Büyüme potansiyeli
- Veri yeterliliği
- Yeni müşteri sayısı
- Tekrar alışveriş yapan müşteri sayısı

### 8. OVO maliyetleri

OVO'nun bu iş ortaklığı için ayıracağı kaynak kaydedilir:

- OVO aylık iç maliyeti
- Kurulum yatırımı

İç maliyet; ekip zamanı, geliştirme, strateji, reklam yönetimi, operasyon ve benzeri hizmet yüklerinin OVO açısından karşılığıdır.

Kurulum yatırımı ise işin başlangıcında OVO tarafından üstlenilen e-ticaret geliştirme, entegrasyon, tasarım veya benzeri ilk yatırım yükünü ifade eder.

### 9. Analiz

Girilen bilgileri son kez kontrol edin ve **Analizi çalıştır** düğmesine basın.

Sistem bu aşamada:

- veri güven puanını hesaplar,
- iş ortaklığı puanını hesaplar,
- geçerli karar kurallarını uygular,
- olumlu göstergeleri ve riskleri çıkarır,
- eksik bilgileri belirler,
- önerilen anlaşma modelini hesaplar,
- asgari aylık ücreti belirler,
- başa baş reklam verimliliğini hesaplar.

Analiz sunucuda hesaplanır ve kaydedilir. Tarayıcı ekranı tek başına nihai karar üretmez.

### 10. Öneri

Son adımda sistemin kararı gösterilir.

Olası kararlar:

- **Kabul:** Mevcut bilgiler ve riskler iş ortaklığı için uygundur.
- **Koşullu kabul:** İş birliği yapılabilir ancak belirli şartların yerine getirilmesi gerekir.
- **Ek bilgi gerekli:** Kritik bilgiler eksik veya yeterince güvenilir değildir. Karar vermeden önce veri tamamlanmalıdır.
- **Ret:** Finansal veya operasyonel riskler kabul edilebilir seviyenin üzerindedir.

Ekranda ayrıca olumlu göstergeler, risk göstergeleri, eksik bilgiler ve gerekli koşullar yer alır.

### Veri güven düzeyleri

Her kritik bilginin yanında kaynağını belirten bir güven düzeyi seçilir:

- **Doğrulandı:** Rapor, panel veya belge üzerinden kontrol edildi.
- **Marka beyanı:** Marka tarafından verildi ancak OVO tarafından doğrudan doğrulanmadı.
- **Tahmini:** Yaklaşık hesap veya varsayım kullanıldı.
- **Bilinmiyor:** Bilgi mevcut değil.

“Bilinmiyor” seçmek kötü bir şey değildir. Bilinmeyen bir değeri kesinmiş gibi girmekten daha güvenlidir. Sistem kritik eksikleri açıkça gösterir.

---

## 8. Yüzde değerleri nasıl girilir?

Sistemde yüzde alanları günlük hayatta kullandığımız biçimde girilir:

- `%5` için `5`
- `%18` için `18`
- `%25` için `25`
- `%50` için `50`
- `%65` için `65`

Örneğin ekranda “Gelir payı (%)” yazıyorsa `5` girmek yüzde beş anlamına gelir. Sistem bu değeri hesaplama sırasında güvenli biçimde dönüştürür.

---

## 9. Senaryo simülatörü nasıl kullanılır?

Değerlendirme sonucundan sonra **Senaryolar** ekranına geçilir.

Senaryo simülatörü “şu değer değişirse ne olur?” sorusuna cevap verir. Örneğin:

- Ciro artarsa OVO hakedişi ne olur?
- Reklam bütçesi iki katına çıkarsa marka kârlı kalır mı?
- İade oranı yükselirse katkı marjı ne kadar düşer?
- Aylık sabit ücret yerine ciro payı kullanılırsa sonuç nasıl değişir?
- OVO'nun kurulum yatırımı kaç ayda geri döner?

### Senaryoda değiştirilebilen başlıca bilgiler

- Aylık ciro
- Brüt kâr marjı
- Reklam harcaması
- İade oranı
- Ortalama sepet tutarı
- Yeni müşteri sayısı
- Değişken gider oranı
- OVO aylık iç maliyeti
- Asgari aylık ücret
- Ciro payı oranı
- Sabit aylık ücret
- Başlangıç cirosu
- Büyüme farkı payı
- Kâr paylaşım oranı
- Kurulum yatırımı
- Sözleşme süresi
- Hakediş modeli

Değerler değiştirildikçe anlık sonuçlar yeniden hesaplanır.

### Sonuçlarda nelere bakılmalı?

- OVO hakedişi
- OVO brüt kârı ve marjı
- Markanın katkı kârı ve katkı marjı
- MER
- Başa baş MER
- CAC
- Kurulum yatırımının geri dönüş süresi
- Karşılanabilir reklam bütçesi
- Risk seviyesi
- Senaryo kararı

Senaryolar kaydedilebilir, kopyalanabilir, yeniden adlandırılabilir ve biri tercih edilen senaryo olarak işaretlenebilir.

Senaryo çalışması ilk değerlendirme sonucunu değiştirmez. Yalnızca farklı ticari ihtimalleri güvenli şekilde karşılaştırmaya yarar.

---

## 10. Anlaşma seçenekleri ve karşılaştırma

Anlaşma seçenekleri oluşturulmadan önce değerlendirme onaylanmalıdır.

Onay sonrasında sistem üç temel ticari seçenek oluşturabilir:

- Asgari aylık ücret ve kademeli ciro payı
- Aylık sabit hizmet bedeli ve sabit ciro payı
- Aylık hizmet bedeli ve mevcut cironun üzerindeki büyümeden pay

**Seçenekleri karşılaştır** düğmesi tüm modelleri aynı marka değerleriyle hesaplar.

Karşılaştırmada şu başlıklar değerlendirilir:

- OVO hakedişi
- Gerçekleşen hakediş oranı
- OVO brüt kârı
- OVO brüt kâr marjı
- Markanın katkı marjı
- Kurulum yatırımının geri dönüşü
- Başa baş MER
- Risk seviyesi
- Genel seçenek puanı

Sistem en uygun seçeneği işaretler ve nedenlerini açıklar. Ancak sistemin önerisi anlaşmayı kendiliğinden kabul etmez. Nihai tercih yetkili kullanıcı tarafından **Seçeneği kabul et** düğmesiyle yapılır.

Kabul edilen anlaşma daha sonra **Etkinleştir** işlemiyle aktif hâle getirilir. Anlaşma etkinleştiğinde marka da aktif marka durumuna geçer.

---

## 11. Başlıca anlaşma modelleri

### Sabit ciro payı

OVO, hesaplamaya esas cironun sabit bir yüzdesini alır.

### Kademeli ciro payı

Cironun farklı dilimlerine farklı oranlar uygulanır. Tüm ciroya tek oran uygulamak yerine her dilim kendi oranıyla hesaplanır.

### Aylık hizmet bedeli ve ciro payı

OVO sabit bir aylık hizmet bedeline ek olarak cironun belirli bir yüzdesini alır.

### Asgari ücret ve ciro payı

Ciro payı hesaplanır; sonuç belirlenen asgari aylık ücretin altında kalırsa asgari ücret uygulanır.

### Büyüme farkı üzerinden pay

Önceden belirlenen başlangıç cirosunun üzerindeki büyümeden pay alınır.

### Aylık hizmet bedeli ve büyüme farkı payı

Sabit aylık ücret ile başlangıç cirosunun üzerindeki büyümeden alınan pay birlikte uygulanır.

### Katkı kârı paylaşımı

Pay, ciro yerine markanın gerçek katkı kârı üzerinden hesaplanır.

### Sabit aylık hizmet bedeli

Performans payı olmadan yalnızca sabit aylık ücret uygulanır.

---

## 12. Aylık sonuç girişi

Etkin bir anlaşması bulunan marka için her ay **Aylık sonuç gir** işlemi yapılır.

Bu ekranda muhasebe, e-ticaret ve reklam platformlarındaki gerçekleşen değerler girilir.

### Dönem bilgileri

- Aktif anlaşma
- Yıl
- Ay

Aynı marka için aynı yıl ve aya ikinci bir kayıt açılmamalıdır. Sistem mükerrer dönemleri engeller.

### Satış ve kesintiler

- Brüt satış
- KDV
- İadeler
- İptaller
- Ters ibrazlar
- Müşterinin ödediği kargo
- Hediye kartı yüklemeleri

### Sipariş ve müşteri bilgileri

- Sipariş sayısı
- Site ziyareti
- Yeni müşteri sayısı
- Tekrar alışveriş yapan müşteri sayısı

### Ürün ve operasyon maliyetleri

- Ürün maliyeti
- Ödeme sistemi giderleri
- Sipariş hazırlama giderleri
- Kargo desteği
- Diğer değişken giderler

### Reklam harcamaları

- Meta harcaması
- Google harcaması
- TikTok harcaması
- Influencer harcaması
- Diğer reklam harcamaları

Sistem bu temel değerlerden net ciroyu, hesaplamaya esas ciroyu, katkı kârını, MER'i, CAC'yi, iade oranını ve OVO hakedişini otomatik hesaplar.

---

## 13. Aylık dönem kapatma süreci

Bir aylık sonuç kaydı aşağıdaki sırayla ilerler:

1. **Taslak:** Bilgiler giriliyor veya kontrol ediliyor.
2. **İncelemede:** Kayıt kontrol için yetkili kişiye gönderildi.
3. **Onaylandı:** Rakamların doğru olduğu teyit edildi.
4. **Kilitlendi:** Dönem kapatıldı ve normal kullanıcılar tarafından değiştirilemez.
5. **Faturalandı:** OVO hakedişi için fatura süreci tamamlandı.
6. **Ödendi:** İlgili hakedişin tahsilatı tamamlandı.

### Düzeltmeler

İstisnai bir ekleme veya düşüş gerekiyorsa **Elle yapılan düzeltmeler** bölümünden tutar ve açıklama girilir.

Düzeltme nedeni mutlaka açık yazılmalıdır. Örnek:

- “Önceki aydan devreden 5.000 TL eksik hakediş”
- “Mutabakat sonucunda 2.500 TL iade düzeltmesi”

“Düzeltme”, “fark” veya “manuel” gibi belirsiz açıklamalar kullanılmamalıdır.

### Kilit açma

Kilitlenmiş bir dönem yalnızca yönetici işlemiyle ve gerekçe yazılarak yeniden açılabilir. Yapılan işlem sistem geçmişinde saklanır.

---

## 14. Hakedişler ekranı

**Hakedişler** ekranı, aylık sonuçlara göre hesaplanan OVO kazançlarını gösterir.

Bir hakediş kaydı açıldığında şu bilgiler görülebilir:

- Hesaplamaya esas ciro
- Kademeli oranların ayrı ayrı hesapları
- Sabit aylık ücret
- Hesaplanan ciro payı
- Asgari ücret
- Elle yapılan düzeltmeler
- Son OVO hakedişi
- Gerçekleşen hakediş oranı

Bu döküm, markayla yapılacak mutabakat sırasında hesabın nasıl oluştuğunu açıklamak için kullanılabilir.

---

## 15. Raporlar ekranı

**Portföy raporu**, kaydedilmiş aylık sonuçlardan güncel bir yönetim özeti üretir.

Bu ekran özellikle şu sorular için kullanılır:

- Toplam OVO geliri nasıl gelişiyor?
- Portföyün kârlılığı yeterli mi?
- Gelirin büyük kısmı tek bir markaya mı bağlı?
- Hangi markalar portföye daha fazla katkı sağlıyor?
- Riskli yoğunlaşma var mı?

Raporların anlamlı olabilmesi için aylık sonuçların düzenli ve doğru biçimde kapatılması gerekir.

---

## 16. Karar kuralları

**Karar kuralları**, sistemin değerlendirme sırasında hangi eşiklere göre uyarı veya öneri üreteceğini belirler.

Örnek kurallar:

- Brüt kâr marjı %25'in altındaysa yalnızca ciro payına dayalı model önerilmez.
- İade oranı %25'in üzerindeyse yüksek risk işareti oluşur.
- Stok 30 günün altındaysa yüksek risk oluşur.
- Kurucu iş birliği zayıfsa yüksek risk oluşur.
- Brüt kâr marjı yükseldikçe performansa dayalı modellere daha fazla alan açılır.

### Neden kurallar doğrudan değiştirilmez?

Yayımlanmış bir kural seti geçmiş değerlendirmelerde kullanılmış olabilir. Geçmiş kararların neden değişmediğini açıklayabilmek için yayımlanmış kurallar kilitlidir.

Bir değişiklik gerektiğinde:

1. Mevcut kural setinden yeni sürüm oluşturulur.
2. Yeni taslak üzerinde değişiklik yapılır.
3. Kurallar ekip içinde kontrol edilir.
4. Yeni sürüm yayımlanır.
5. Önceki sürüm arşivlenir.

Karar kuralları yalnızca etkisi anlaşılarak ve yetkili kişilerce değiştirilmelidir.

---

## 17. Genel ayarlar

**Ayarlar** ekranındaki değerler birçok hesaplamayı etkiler.

Başlıca ayarlar:

- Varsayılan para birimi
- Varsayılan KDV oranı
- Varsayılan sözleşme süresi
- Varsayılan kurulum yatırımı
- Hedef OVO brüt kâr marjı
- Hedef marka katkı marjı
- Asgari ücret çarpanı
- Mevcut ciro eşiği
- Müşteri yoğunlaşma riski eşiği

Bu alanlarda yapılacak değişiklikler yeni değerlendirme ve hesapları etkileyebilir. Bu nedenle ayarlar günlük operasyon sırasında gelişigüzel değiştirilmemelidir.

---

## 18. İşlem geçmişi

**İşlem geçmişi**, sistemde yapılan önemli değişiklikleri kayıt altında tutar.

Örneğin:

- marka oluşturulması,
- değerlendirme kaydı ve onayı,
- kural değişiklikleri,
- anlaşma oluşturma veya etkinleştirme,
- aylık sonuç değişiklikleri,
- dönem kilitleme ve kilit açma,
- hakediş düzeltmeleri,
- faturalama ve ödeme işlemleri

buradan takip edilebilir.

Bir rakam veya durum beklenmedik görünüyorsa ilk kontrol edilmesi gereken yerlerden biri işlem geçmişidir.

### Üst arama ve yapılacak işler

Ekranın üstündeki arama alanından marka, değerlendirme veya anlaşma adı aranabilir. Klavyede `Ctrl + K` veya Mac'te `⌘ + K` kısayolu aramayı hızlıca açar.

Bildirim simgesi, işlem bekleyen kayıtları gösterir. Burada onay bekleyen değerlendirmeler, etkinleştirilecek anlaşmalar, kontrol bekleyen aylık dönemler ve eksik aylık girişler görülebilir.

---

## 19. Anlaşma koşulları, belgeler ve yaşam döngüsü

### Koşul kontrol listesi

Koşullu kabul edilen bir değerlendirmedeki şartlar, anlaşma kaydına taşınır. Her koşul için:

- **Tamamlandı** işlemiyle şartın yerine getirildiğini kaydedin.
- **Gerekçeyle feragat et** işlemini yalnızca yetkili kararla kullanın ve açık bir neden yazın.
- Varsa rapor, onay veya başka bir kanıtın bağlantısını ekleyin.

Sistem işlemi yapan kişiyi ve tarihi otomatik saklar. Bu kayıtlar sessizce değişmez ve işlem geçmişinden izlenebilir.

### Belgeler ve notlar

Anlaşma ekranındaki **Belgeler ve notlar** bölümünden PDF, PNG, JPG, CSV veya Excel dosyası yüklenebilir. Dosya başına üst sınır 10 MB'dır. Dosyanın ne olduğunu açıklayan kısa bir not yazın; örneğin “Mayıs mutabakat raporu” veya “İmzalı ticari şartlar”.

### Anlaşmanın ilerleyişi

Anlaşma; taslak, iç inceleme, markaya önerildi, görüşme, kabul edildi ve etkin durumlarından geçer. Etkin bir anlaşma gerekçesi kaydedilerek yenilenebilir, sonlandırılabilir veya süresi doldu olarak kapatılabilir. Kabul edilmiş ya da kapanmış ticari şartlar geriye dönük değiştirilemez.

### Anlaşma şablonları

Yöneticiler **Ayarlar → Anlaşma şablonları** sayfasından standart ticari seçenekleri yönetir. Şablondaki ücret, oran, süre ve model; değerlendirme sonrası oluşturulan seçeneklerin başlangıç değeridir. Şablon değişikliği eski anlaşmaları değiştirmez.

---

## 20. Kullanıcılar ve roller

Yöneticiler **Ayarlar → Kullanıcılar** sayfasından kişiye özel hesap oluşturabilir.

- **Analist:** Marka ve değerlendirme çalışmalarını görür ve değerlendirme hazırlayabilir.
- **Partner:** Operasyon, anlaşma ve aylık sonuç işlemlerini yürütebilir.
- **Yönetici:** Tüm alanlara erişir; kullanıcıları, kuralları, şablonları ve kritik kilit işlemlerini yönetir.

Yeni kullanıcıya en az 10 karakterli geçici şifre verin ve şifreyi güvenli bir kanaldan iletin. Kullanılmayan hesaplar pasife alınmalıdır.

### İki kişili aylık onay

Aylık sonucu hazırlayıp incelemeye gönderen kişi aynı kaydı onaylayamaz. İkinci bir Partner veya Yönetici hesabı kontrol edip onaylamalıdır. Bu ayrım, rakamların tek kişinin kontrolünde sessizce kapanmasını engeller.

---

## 21. Sade finansal terimler sözlüğü

### Brüt satış

İade, iptal, KDV ve diğer düşüşler yapılmadan önceki toplam satış tutarıdır.

### Net ciro

Brüt satıştan KDV, iade, iptal ve ters ibraz gibi kalemler çıkarıldıktan sonra kalan gerçek satış tutarıdır.

### Brüt kâr marjı

Ürün maliyeti çıktıktan sonra cironun ne kadarının kaldığını gösterir. Reklam, operasyon ve OVO hakedişi henüz bu tutardan düşülmemiştir.

### Katkı kârı

Ürün maliyeti, değişken giderler, reklam harcaması ve ilgili hakedişler çıktıktan sonra markada kalan tutardır.

### Katkı marjı

Katkı kârının net ciroya oranıdır. Markanın büyürken gerçekten para kazanıp kazanmadığını anlamaya yardımcı olur.

### AOV — Ortalama sepet tutarı

Bir siparişin ortalama değeridir.

### CAC — Yeni müşteri edinme maliyeti

Bir yeni müşteri kazanmak için ortalama ne kadar reklam harcandığını gösterir.

### LTV — Müşteri yaşam boyu değeri

Bir müşterinin markayla ilişkisi boyunca oluşturması beklenen toplam değerdir.

### MER — Toplam reklam verimliliği

Net cironun toplam reklam harcamasına oranıdır. Örneğin `4x MER`, her 1 TL reklam harcamasına karşılık 4 TL net ciro oluştuğunu ifade eder.

### Başa baş MER

Markanın hedeflenen katkı marjını koruyabilmesi için altına düşmemesi gereken reklam verimliliğidir.

### OVO hakedişi

Aktif anlaşmanın şartlarına ve o ayın gerçekleşen sonuçlarına göre OVO'nun kazandığı tutardır.

### OVO iç maliyeti

OVO ekibinin markaya hizmet verirken kullandığı kaynakların aylık maliyetidir.

### OVO brüt kârı

OVO hakedişinden OVO iç maliyetinin çıkarılmasıyla bulunan tutardır.

### Sabit aylık hizmet bedeli

Cirodan bağımsız olarak her ay alınan sabit tutardır. İngilizce kaynaklarda “retainer” olarak da geçebilir.

### Ciro payı

Hesaplamaya esas cironun anlaşmada belirlenen yüzdesinin OVO hakedişi olmasıdır.

### Büyüme farkı

Belirlenmiş başlangıç cirosunun üzerinde oluşan ek cirodur. Artımlı gelir olarak da ifade edilebilir.

### Kurulum yatırımının geri dönüşü

OVO'nun başlangıçta yaptığı yatırımın, aylık OVO brüt kârıyla kaç ayda karşılanacağını gösterir.

---

## 22. Örnek bir marka yolculuğu

Yeni görüşülen bir moda markası olduğunu düşünelim.

1. Marka, **Marka ekle** ekranından kaydedilir.
2. Marka için yeni değerlendirme başlatılır.
3. Aylık ciro, marj, reklam harcaması, iade oranı ve stok bilgileri girilir.
4. Belgeli bilgiler “Doğrulandı”, yalnızca markadan alınan bilgiler “Marka beyanı” olarak işaretlenir.
5. Operasyon, ürün-pazar uyumu ve ekip iş birliği puanlanır.
6. OVO'nun aylık iç maliyeti ve başlangıç yatırımı girilir.
7. Analiz çalıştırılır.
8. Sistem “Koşullu kabul” kararı verir ve en az 60 günlük stok şartı gösterir.
9. Stok şartı markayla görüşülür ve değerlendirme yetkili kişi tarafından onaylanır.
10. Üç anlaşma seçeneği oluşturulur.
11. Seçenekler aynı ciro ve maliyet değerleriyle karşılaştırılır.
12. Marka ve OVO için en dengeli seçenek kabul edilip etkinleştirilir.
13. Ay sonunda Shopify, muhasebe ve reklam hesaplarından gerçekleşen bilgiler alınır.
14. Aylık sonuç kaydı oluşturulur ve OVO hakedişi hesaplanır.
15. Kayıt incelemeye gönderilir, onaylanır ve kilitlenir.
16. Fatura kesildikten sonra “Faturalandı”, ödeme alındıktan sonra “Ödendi” durumuna geçirilir.
17. Sonuçlar ana sayfa ve portföy raporuna otomatik yansır.

---

## 23. Önerilen çalışma düzeni

### Yeni marka görüşmesinde

- Marka kaydını açın.
- Kritik finansal bilgileri isteyin.
- Bilgilerin kaynağını not edin.
- Değerlendirmeyi tamamlayın.
- Eksik bilgiler varsa kesin karar vermeyin.
- Senaryoları görüşme öncesinde hazırlayın.

### Anlaşma öncesinde

- Değerlendirme kararını ekip içinde kontrol edin.
- Gerekli koşulları markayla netleştirin.
- En az üç ticari seçeneği karşılaştırın.
- Yalnızca OVO gelirine değil, markanın katkı marjına da bakın.
- Kabul edilen seçeneğin anlaşmadaki gerçek şartlarla aynı olduğundan emin olun.

### Her ay

- Muhasebe ve reklam verilerini aynı dönem için toplayın.
- İade, iptal, KDV ve reklam harcamalarını eksiksiz girin.
- Hesaplanan sonuçları kaynak raporlarla karşılaştırın.
- Gerekli düzeltmeleri açıklamasıyla kaydedin.
- Dönemi sırasıyla incelemeye gönderin, onaylayın ve kilitleyin.
- Fatura ve ödeme durumlarını güncel tutun.

### Yönetim kontrolünde

- Portföy gelirini ve OVO brüt kâr marjını izleyin.
- Tek markaya bağımlılık riskini kontrol edin.
- Riskli veya katkı marjı düşen markaları inceleyin.
- Uzun süredir kapanmamış aylık dönemleri takip edin.

---

## 24. Sık yapılan hatalar

### Yüzdeyi yanlış girmek

Yüzde işaretli bir alanda `%5` için `0,05` yazmak değeri yüz kat küçültür. Doğru giriş `5` olmalıdır.

### Tahmini bilgiyi doğrulanmış göstermek

Veri güven puanını yapay olarak yükseltir ve yanlış ticari karar üretebilir.

### KDV veya iadeleri atlamak

Net ciroyu ve dolayısıyla hakedişi olduğundan yüksek gösterebilir.

### Etkin anlaşmadaki oranla sistemdeki oranı farklı bırakmak

Sistemin hesapladığı tutarla gerçek sözleşme arasında fark oluşmasına neden olur.

### Aylık dönemi kontrol etmeden kilitlemek

Kilitlenen dönem normal şekilde değiştirilemez. Yeniden açmak için yönetici gerekçesi gerekir.

### Yalnızca OVO hakedişine bakmak

OVO için yüksek gelir üreten bir model markayı zarara sokuyorsa sürdürülebilir değildir. Marka katkı marjı mutlaka kontrol edilmelidir.

### Eksik veri varken kesin karar vermek

Sistem “Ek bilgi gerekli” diyorsa önce eksik alanlar tamamlanmalıdır.

---

## 25. Bir sorun olduğunda ne yapılmalı?

### Uygulama açılmıyor

- VS Code'da **OVO: Full Stack + Chrome** seçeneğinin çalıştırıldığını kontrol edin.
- Açık terminal pencerelerinde kırmızı bir hata mesajı olup olmadığına bakın.
- Uygulamayı durdurup yeniden çalıştırın.

### Giriş yapılamıyor

- E-posta ve şifrede boşluk olmadığını kontrol edin.
- API'nin VS Code içinde çalışıyor olduğundan emin olun.

### Sayılar beklenenden farklı

- Yüzde alanlarında `%5` için `5` girildiğini kontrol edin.
- KDV, iade, iptal ve reklam harcamalarını tekrar gözden geçirin.
- Doğru ayın ve doğru anlaşmanın seçildiğini kontrol edin.
- Hakediş dökümündeki her kalemi ayrı ayrı inceleyin.

### Bir kayıt değiştirilemiyor

Kayıt analiz edilmiş, onaylanmış, arşivlenmiş veya dönem kilitlenmiş olabilir. Sistem geçmiş kararların güvenilirliğini korumak için bu kayıtları kilitler.

### Bir işlemi kimin yaptığı bilinmiyor

**İşlem geçmişi** ekranından kayıt türünü, tarihi ve işlemi yapan kullanıcıyı kontrol edin.

---

## 26. Şu anda sistemin dışında kalan işler

OVO Growth OS mevcut hâliyle temel iş ortaklığı ve aylık kapanış akışını çalıştırır. Ancak aşağıdaki işler henüz tam otomatik değildir:

- Shopify ve reklam platformlarından otomatik veri çekme
- Muhasebe veya fatura sistemi entegrasyonu
- Sözleşme belgesi oluşturma ve elektronik imza
- Kurumsal tek oturum açma, şifre sıfırlama ve çok şirketli kullanım
- Gelişmiş PDF ve yazdırılabilir yönetim raporları

Bu nedenle aylık veriler ilgili kaynaklardan kontrol edilerek sisteme girilmeli; fatura ve sözleşme işlemleri mevcut şirket süreçleriyle birlikte yürütülmelidir.

---

## 27. Son kontrol listesi

### Yeni bir anlaşma öncesinde

- [ ] Marka bilgileri doğru ve güncel mi?
- [ ] Ciro ve marj bilgileri güvenilir bir kaynaktan mı?
- [ ] İade oranı ve stok bilgisi girildi mi?
- [ ] Reklam bütçesi ve müşteri edinme maliyeti kontrol edildi mi?
- [ ] OVO iç maliyeti gerçekçi mi?
- [ ] Veri güven düzeyleri doğru seçildi mi?
- [ ] Sistem kararı ve risk göstergeleri incelendi mi?
- [ ] Gerekli koşullar markayla görüşüldü mü?
- [ ] En az üç anlaşma seçeneği karşılaştırıldı mı?
- [ ] Kabul edilen seçenek gerçek ticari mutabakatla aynı mı?

### Aylık kapanış öncesinde

- [ ] Doğru marka, anlaşma, ay ve yıl seçildi mi?
- [ ] Brüt satış ve KDV kontrol edildi mi?
- [ ] İade, iptal ve ters ibrazlar girildi mi?
- [ ] Ürün ve değişken maliyetler eksiksiz mi?
- [ ] Tüm reklam kanalları girildi mi?
- [ ] Sipariş ve müşteri adetleri kontrol edildi mi?
- [ ] OVO hakediş dökümü incelendi mi?
- [ ] Elle yapılan düzeltmeler açıklamalı mı?
- [ ] Dönem onaylandı ve kilitlendi mi?
- [ ] Fatura ve ödeme durumu güncellendi mi?

---

## Kısa özet

OVO Growth OS'un temel amacı “Bu marka ne kadar ciro yapıyor?” sorusundan daha kapsamlı bir cevap üretmektir.

Sistem aynı anda şunları korumaya çalışır:

- OVO'nun hizmet maliyeti ve kârlılığı,
- markanın sürdürülebilir katkı kârı,
- verilen kararın dayandığı verilerin güvenilirliği,
- anlaşma şartlarının şeffaflığı,
- aylık hakediş hesabının izlenebilirliği.

Doğru kullanımın en önemli üç kuralı şudur:

1. Bilinmeyen veriyi tahmin ederek kesinmiş gibi girmeyin.
2. Anlaşma modelini seçerken hem OVO'nun hem markanın kârlılığına bakın.
3. Aylık dönemleri kaynak verilerle kontrol etmeden kapatmayın.
