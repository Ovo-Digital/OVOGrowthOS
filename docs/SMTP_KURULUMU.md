# Gmail SMTP — panel ayarları ve kontrollü gönderim

Bu sürüm hesap daveti, şifre yenileme ve kişisel tercihe bağlı görev/rapor/konuşma bildirimlerini hazırlar. Gerçek Gmail bilgileri girilmedi; dışarıya test iletisi gönderilmedi. Varsayılan gönderim **kapalıdır**. Günlük bildirimlerin kişisel e-posta tercihleri de başlangıçta kapalıdır.

Rapor e-postaları için ayrıca **Müşteri portalı → marka seçimi → Rapor e-postası izni ve alıcı kontrolü** bölümündeki marka izni gerekir. Yeni ve mevcut markalarda bu yeni izin başlangıçta kapalıdır; kişisel tercihler korunur. Yalnız yönetici gerekçeyle değiştirebilir. Ön izleme e-posta üretmez; geçerli rapor/erişim/tercih gönderim anında yeniden kontrol edilir. Eski bildirime sonradan e-posta oluşturulmaz. Konuşma, görev ve hesap iletileri marka rapor anahtarından etkilenmez.

**Sürüm geçişi:** Eski API bu marka iznini tanımaz. Yayına geçerken eski kopyalarda gönderimi durdurun ve hepsini yeni sürüme geçirin; karışık sürümlerde göndermeyin. Gerekirse tüm kopyalarda `MAIL_FORCE_DISABLED=true` kullanın; bu anahtarı tanımayan daha eski sürümlerde süreçleri durdurun. Geri dönüşte tabloyu silmeyin; eski sürüme dönerken gönderimi kapalı tutun. Gönderime başlamış ileti geri alınamaz.

## Panelden yönetim — 25 Eylül 2026

Önerilen yol **Ayarlar → E-posta ayarları**. Yalnız Admin okuyup yazabilir. İlk sürüm yalnız `smtp.gmail.com`, 465/TLS veya 587/zorunlu STARTTLS kabul eder; serbest sunucu/IP ve şifresiz taşıma açılmaz. Google uygulama şifresi, ayrı Data Protection amacıyla şifrelenerek `growth.MailConfigurations` içinde tutulur. GET yanıtı yalnız şifre var/yok bilgisini verir. Şifre, istek gövdesi veya şifreli içerik işlem geçmişine yazılmaz. Harici HTTP gövde kaydında `/api/account-mail/settings` mutlaka hariç tutulmalıdır.

Panel kaydı yoksa önceki ortam değişkenleri çalışır. İlk panel kaydından sonra **panel bütünüyle önceliklidir**; boş/çözülemeyen şifre veya kapalı panel ayarı eski sunucu şifresine geri düşmez. İlk kayıtta ortam şifresi otomatik içeri alınmaz. Boş şifre mevcut panel şifresini korur; açık kaldırma işlemi gönderimi kapatmayı gerektirir. Kullanıcı adresi değişirse yeni şifre veya açık kaldırma gerekir. Eski form 409 alır; kaydetmeden güncel sürüm kontrol edilir.

`MAIL_FORCE_DISABLED=true`, hem ortam hem panel yapılandırmasındaki normal ve deneme gönderimlerini o API kopyasında durdurur. Coolify compose bu yeni değişkeni geçirir. **Panel kaydı varken `MAIL_ENABLED=false` tek başına kapatma anahtarı değildir.** Normal kullanımda panelden kapatın; acil durumda bütün API kopyalarında `MAIL_FORCE_DISABLED=true` uygulayıp yeniden başlatın. Aynı DB'ye bağlı yerel geliştirmede bu bayrağı true tutun; geliştirme sürecinin üretim kuyruğunu sahiplenmesine izin vermeyin. Bu bayrak bildirim panelini veya iki aşamalı girişi kapatmaz.

Yalnız kaydedilmiş ayarlarla, oturumdaki yöneticinin kendi adresine deneme yapılır. İstekten farklı alıcı alınmaz. Onay zorunlu, IP sınırı ve ayar kaydında dakikalık deneme sınırı vardır. Deneme önce sürüm artırılarak kaydedilir; eşzamanlı eski istek ikinci gönderim yapamaz. Normal gönderim kapalıyken açıkça onaylanan deneme yapılabilir; acil durdurma bunu da engeller. SMTP kabulü teslim değildir; hata metni dışarı verilmez, belirsiz sonuç otomatik tekrarlanmaz. Deneme API'si bu açık kullanıcı işlemi için en fazla 30 saniye SMTP bekler; normal hesap/bildirim gönderimleri kuyrukta kalır.

Migration `20260925133126_MailConfiguration` yalnız yeni tabloyu oluşturur; kayıt veya şifre eklemez, gönderimi kendiliğinden açmaz. Anahtar volume'u SMTP kapalıyken de korunur. API kopyaları ortak kalıcı Data Protection anahtar deposu ve uygulama kimliğini kullanmalıdır. Yerel anahtarlarla kaydedilmiş şifrenin farklı anahtar deposundaki sunucuda çözülemeyeceğini unutmayın; canlı ayarları canlı panelden hazırlayın.

Geri dönüş: Eski API panel ayarlarını ve yeni acil durdurma bayrağını tanımaz. Panelden durdurmayı boşa düşürmemek için eski/yeni göndericileri birlikte çalıştırmayın. Eski sürüme dönüş zorunluysa eski kopyaların kendi `MAIL_ENABLED=false` ayarını da açıkça uygulayın. Yeni tabloyu/anahtarları koruyun; veri silen `Down` ayrıca onay olmadan çalıştırılmaz. İki aşamalı girişe ilişkin daha sıkı geri dönüş sınırları geçerlidir.

## Eski ortam değişkenleri — panel kaydı yokken

Yerelde API ortam değişkenleri veya `dotnet user-secrets`, Coolify'da yalnız **api** servisinin gizli ortam değişkenleri kullanılır. Web'e veya `NEXT_PUBLIC_*` alanlarına parola koymayın. Örnek değerleri gerçek hesap bilgilerinizle değiştirin; `.env` dosyasını Git'e eklemeyin.

```dotenv
MAIL_ENABLED=false
MAIL_FORCE_DISABLED=false
SMTP_HOST=smtp.gmail.com
SMTP_PORT=465
SMTP_SECURE=true
SMTP_USER=hesabiniz@gmail.com
SMTP_PASS=BURAYA_GOOGLE_UYGULAMA_SIFRESI
MAIL_FROM="OVO Growth OS <hesabiniz@gmail.com>"
WEB_ORIGIN=https://ovogrowth.ovodigi.com
```

Coolify compose, `WEB_ORIGIN` değerini API'nin `WebOrigin` ayarına bağlar. Yerelde ayar adı `WebOrigin`, örneğin `http://localhost:3000` olmalıdır. Bağlantılar yalnız bu sabit kök adresten üretilir; istek başlığındaki alan adı veya kullanıcıdan gelen yönlendirme kullanılmaz. Üretimde HTTPS gerekir; düz HTTP yalnız loopback yerel adreslerde kabul edilir.

Google'ın [uygulama şifresi açıklamasına](https://support.google.com/accounts/answer/185833) göre uygun hesapta iki adımlı doğrulama açılmalı ve ayrı uygulama şifresi oluşturulmalıdır; normal Gmail şifresini kullanmayın. Bazı kurumsal hesap politikaları uygulama şifresine izin vermez. Sohbette veya depoda paylaşılmış gerçek bir uygulama şifresi varsa kullanmadan önce Google'dan iptal edip yenisini üretin. `MAIL_FROM` için aynı hesabı veya Gmail'de onaylanmış gönderici adresini kullanın.

465 + `SMTP_SECURE=true` bağlantı başından TLS kullanır. Alternatif 587 + `SMTP_SECURE=false` **zorunlu STARTTLS** kullanır; bu ayar şifresiz bağlantı anlamına gelmez. Diğer port/eşleşmeler veya farklı SMTP sağlayıcıları bu Gmail sürümünde kabul edilmez. Sertifika denetimi kapatılmaz. [MailKit bağlantı davranışı](https://mimekit.net/docs/html/M_MailKit_Net_Smtp_SmtpClient_ConnectAsync_2.htm).

**`MAIL_TO` kullanılmaz.** Davet/şifre bağlantısı, ilgili hesabın veritabanındaki adresine gönderilir. Ortak alıcıya yönlendirmek başka kişilerin hesap bağlantılarını açığa çıkaracağı için bu değişken bağlanmadı.

## Gönderimi açmadan önce

1. API ve paneli aynı kaynak sürümünden hazırlayın. `20260923163849_AccountMail` migration'ını yetkili veritabanı hesabıyla uygulayın; uygulama hesabının yeni tablolardaki okuma/ekleme/güncelleme yetkilerini doğrulayın. `growth` genel erişime açılmaz.
2. Güncel migration'ları uygulayın. Panelden SMTP ve gönderici bilgilerini genel gönderim kapalıyken kaydedin; normal gönderimler başlamaz. Deneme ancak ayrı onayla yapılır.
3. Coolify compose içindeki `mail-keys` kalıcı volume'unu koruyun. `MAIL_KEY_PATH=/app/mail-keys` yalnız API içinde kullanılır. Birden fazla API kopyası aynı anahtar deposunu ve aynı uygulama kimliğini kullanmalıdır. Anahtarlar hassastır; dosya erişimini yalnız API ve yetkili işletmeciyle sınırlayın. Volume şifreli gönderim içerikleriyle birlikte korunmalıdır; veritabanındaki şifreleme tek başına anahtara erişen işletmeciye karşı koruma değildir.
4. Panelde açık onayla kendi adresinize deneme gönderip gelen kutusunu kontrol edin. Ardından genel gönderimi açıp kaydedin; yeniden başlatma gerekmez. “Hizmet açık” yalnız ayarların biçimsel olarak tam olduğunu gösterir; Gmail teslim doğrulaması değildir.
5. Kontrollü bir hesap daveti pilotunda doğru site adresi, tek kullanım, şifre belirleme ve gönderim durumu kontrollerini kullanıcıyla yapın. Gerçek SMTP bilgileri girilmeden bu pilot tamamlanmış sayılmaz.

## Günlük bildirimler

`20260923220416_UserNotifications` eklemeli migration'ı bildirim tercihi ve alıcı/olay kayıtlarını `growth` içinde tutar. API'nin mevcut sürecindeki işleyici yaklaşık dakikada bir kontrol eder; yeni sunucu, zamanlayıcı servisi veya dış kuyruk gerektirmez. Kapalı API'de zamanında gönderim beklenmemelidir. Türkiye saatiyle 09.00 sonrası açık görevler için günlük özet, yaklaşan/geçmiş son tarih için görev başına tarih bazlı hatırlatma oluşturulur. Rapor ve konuşmalarda ilk takip tarihinden itibaren son 30 gün okunur; bir günden eski olaylara e-posta hazırlanmaz. Eski arşiv geriye dönük topluca e-postalanmaz.

`growth.UserNotifications` üzerindeki alıcı/olay benzersizliği ve sürüm denetimi tekrar üretimi/gönderim sahiplenmesini korur. SMTP denemesi başlamadan önce sahiplenme kaydedilir; belirsiz gönderim otomatik tekrarlanmaz. Gönderim sırasında hesap ve ilgili kaynak satırları kontrol edilip kilitlenir. Hesap, adres, oturum, tercih, görev sorumlusu veya rapor paylaşımı değiştiyse bekleyen ileti iptal edilir. E-postada özel mali değer, belge veya mesaj gövdesi yoktur. Bildirim listesi de kaynak erişimini yeniden denetler. Bu kuyruğun durumları kişinin **Bildirimler** ekranındadır; yöneticinin **Gönderim merkezi** ekranı davet, şifre yenileme, bildirim, rapor ve deneme iletilerini birlikte listeler.

Geri dönüşte bu tablolar korunur; veri silen `Down` kendiliğinden uygulanmaz. Bildirim tercihini sonradan açmak eski kayıtları göndermeye çevirmez. Gönderim panelden veya `MAIL_FORCE_DISABLED=true` ile durdurulur; başlamış/gönderilmiş ileti geri alınmaz. Yeniden açıldığında hâlâ geçerli bekleyenler işlenebilir.

Anahtar kaybolursa eski kuyruktaki iletiler çözülemez; görünür bir hata durumuna alınır. Önceden gönderilmiş bağlantının doğrulanması anahtara bağlı değildir: veritabanında rastgele 256 bit tokenın SHA-256 özeti bulunur. Kullanılmış/süresi dolmuş bağlantı geçersizdir. Veri varken migration `Down` veya volume silme yapmayın.

## Teslim güvencesi ve hata davranışı

İleti, hesap bağlantısıyla aynı veritabanı kaydında sıraya alınır; SMTP HTTP isteğinin içinde çağrılmaz. Birden fazla API kopyasında sürüm denetimi tek işleyicinin gönderimi sahiplenmesini sağlar. Gönderim öncesi hesabın etkinliği, e-posta adresi, oturum sürümü, müşteri marka erişimi ve bağlantının geçerliliği yeniden kontrol edilir. PostgreSQL hesap satırı gönderim boyunca kilitlenir; hesap kapatma işlemi en fazla gönderim zaman aşımı kadar bekleyebilir.

Gönderim başlatıldığı kalıcı olarak işaretlendikten sonra aynı ileti otomatik tekrarlanmaz. SMTP kabulünden hemen sonra süreç durursa teslim sonucu kesin bilinemez: yaklaşık beş dakika sonra “Gönderim doğrulanamadı” olur. Bu, **tam olarak bir kez teslim** garantisi değildir; yinelenen iletiyi önlemek için belirsizlikte otomatik yeniden gönderim yapılmaması tercihidir. Yeni davet istenirken eski bağlantı geçersiz olur. Sunucu kabulü gelen kutusu teslimi veya okunma kanıtı değildir.

Kuyruk içeriği ASP.NET Data Protection ile şifrelenir; gönderim sonrası veya kapatıldığında temizlenir. SMTP istisna metinleri, tokenlar, parola ve ileti gövdesi log/API/işlem geçmişine yazılmaz. Erişim bağlantısı URL fragment'ındadır (`#token=...`); web sunucusunun istek URL'sine gönderilmez ve sayfa açılınca adres çubuğundan kaldırılır. HTTP gövdesi kaydeden harici izleme araçlarında `/api/auth/complete-account` gövdesini kesinlikle kaydetmeyin.

Daveti kabul etmek rolü/markayı değiştirmez. Yeni şifreyi kaydetmek bütün eski oturumları geçersiz kılar. Şifre yenileme yanıtları hesabın varlığını açıklamaz; IP hız sınırı ve hesap başına dakika/saat sınırı uygulanır. Davet bekleyen yönetici son etkin yönetici yerine sayılmaz.

Geri dönüşte yeni tabloları ve bekleyen hesap bayrağını koruyun. Eski API yeni davet/panel durumunu tanımadığından eski/yeni sürümleri birlikte çalıştırmayın. Güncel sürümde panelden veya `MAIL_FORCE_DISABLED=true` ile gönderimi durdurun; kayıt veya anahtar silmeyin.
