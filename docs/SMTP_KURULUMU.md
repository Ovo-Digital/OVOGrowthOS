# Gmail SMTP — hesap daveti ve şifre yenileme

Bu sürüm hesap daveti, şifre yenileme ve kişisel tercihe bağlı görev/rapor/konuşma bildirimlerini hazırlar. Gerçek Gmail bilgileri girilmedi; dışarıya test iletisi gönderilmedi. Varsayılan gönderim **kapalıdır**. Günlük bildirimlerin kişisel e-posta tercihleri de başlangıçta kapalıdır.

## Sonradan girilecek ayarlar

Yerelde API ortam değişkenleri veya `dotnet user-secrets`, Coolify'da yalnız **api** servisinin gizli ortam değişkenleri kullanılır. Web'e veya `NEXT_PUBLIC_*` alanlarına parola koymayın. Örnek değerleri gerçek hesap bilgilerinizle değiştirin; `.env` dosyasını Git'e eklemeyin.

```dotenv
MAIL_ENABLED=false
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
2. SMTP ve gönderici bilgilerini gizli ayarlara girin. `MAIL_ENABLED=false` olarak bırakın. Ayarlar ekranında hizmetin kapalı olması beklenen davranıştır; program hiçbir SMTP bağlantısı kurmaz.
3. Coolify compose içindeki `mail-keys` kalıcı volume'unu koruyun. `MAIL_KEY_PATH=/app/mail-keys` yalnız API içinde kullanılır. Birden fazla API kopyası aynı anahtar deposunu ve aynı uygulama kimliğini kullanmalıdır. Anahtarlar hassastır; dosya erişimini yalnız API ve yetkili işletmeciyle sınırlayın. Volume şifreli gönderim içerikleriyle birlikte korunmalıdır; veritabanındaki şifreleme tek başına anahtara erişen işletmeciye karşı koruma değildir.
4. `MAIL_ENABLED=true` yapıp API'yi yeniden başlatın. **Hesap e-postaları** ekranında “hizmet açık” yalnız ayarların biçimsel olarak tam olduğunu gösterir; Gmail kimlik doğrulama veya teslim doğrulaması değildir.
5. Yalnız kontrol ettiğiniz bir deneme alıcısına yeni hesap daveti oluşturun. Gelen kutusu/istenmeyen posta, doğru site adresi, tek kullanım, yeni şifreyle giriş ve gönderim durumu kontrollerini yapın. Bu canlı pilot kullanıcı SMTP bilgilerini girdikten sonra yapılmalıdır.

## Günlük bildirimler

`20260923220416_UserNotifications` eklemeli migration'ı bildirim tercihi ve alıcı/olay kayıtlarını `growth` içinde tutar. API'nin mevcut sürecindeki işleyici yaklaşık dakikada bir kontrol eder; yeni sunucu, zamanlayıcı servisi veya dış kuyruk gerektirmez. Kapalı API'de zamanında gönderim beklenmemelidir. Türkiye saatiyle 09.00 sonrası açık görevler için günlük özet, yaklaşan/geçmiş son tarih için görev başına tarih bazlı hatırlatma oluşturulur. Rapor ve konuşmalarda ilk takip tarihinden itibaren son 30 gün okunur; bir günden eski olaylara e-posta hazırlanmaz. Eski arşiv geriye dönük topluca e-postalanmaz.

`growth.UserNotifications` üzerindeki alıcı/olay benzersizliği ve sürüm denetimi tekrar üretimi/gönderim sahiplenmesini korur. SMTP denemesi başlamadan önce sahiplenme kaydedilir; belirsiz gönderim otomatik tekrarlanmaz. Gönderim sırasında hesap ve ilgili kaynak satırları kontrol edilip kilitlenir. Hesap, adres, oturum, tercih, görev sorumlusu veya rapor paylaşımı değiştiyse bekleyen ileti iptal edilir. E-postada özel mali değer, belge veya mesaj gövdesi yoktur. Bildirim listesi de kaynak erişimini yeniden denetler. Bu kuyruğun durumları kişinin **Bildirimler** ekranındadır; **Hesap e-postaları** yalnız davet/şifre akışını listeler.

Geri dönüşte bu tablolar korunabilir; önceki uygulama bunları kullanmaz. Veri silen `Down` kendiliğinden uygulanmaz. Bildirim tercihini sonradan açmak eski kayıtları göndermeye çevirmez; `MAIL_ENABLED=false` bütün SMTP işleyicilerini durdurur. E-postanın fiziksel olarak alıcının kutusundan geri alınamayacağı unutulmamalıdır.

Anahtar kaybolursa eski kuyruktaki iletiler çözülemez; görünür bir hata durumuna alınır. Önceden gönderilmiş bağlantının doğrulanması anahtara bağlı değildir: veritabanında rastgele 256 bit tokenın SHA-256 özeti bulunur. Kullanılmış/süresi dolmuş bağlantı geçersizdir. Veri varken migration `Down` veya volume silme yapmayın.

## Teslim güvencesi ve hata davranışı

İleti, hesap bağlantısıyla aynı veritabanı kaydında sıraya alınır; SMTP HTTP isteğinin içinde çağrılmaz. Birden fazla API kopyasında sürüm denetimi tek işleyicinin gönderimi sahiplenmesini sağlar. Gönderim öncesi hesabın etkinliği, e-posta adresi, oturum sürümü, müşteri marka erişimi ve bağlantının geçerliliği yeniden kontrol edilir. PostgreSQL hesap satırı gönderim boyunca kilitlenir; hesap kapatma işlemi en fazla gönderim zaman aşımı kadar bekleyebilir.

Gönderim başlatıldığı kalıcı olarak işaretlendikten sonra aynı ileti otomatik tekrarlanmaz. SMTP kabulünden hemen sonra süreç durursa teslim sonucu kesin bilinemez: yaklaşık beş dakika sonra “Gönderim doğrulanamadı” olur. Bu, **tam olarak bir kez teslim** garantisi değildir; yinelenen iletiyi önlemek için belirsizlikte otomatik yeniden gönderim yapılmaması tercihidir. Yeni davet istenirken eski bağlantı geçersiz olur. Sunucu kabulü gelen kutusu teslimi veya okunma kanıtı değildir.

Kuyruk içeriği ASP.NET Data Protection ile şifrelenir; gönderim sonrası veya kapatıldığında temizlenir. SMTP istisna metinleri, tokenlar, parola ve ileti gövdesi log/API/işlem geçmişine yazılmaz. Erişim bağlantısı URL fragment'ındadır (`#token=...`); web sunucusunun istek URL'sine gönderilmez ve sayfa açılınca adres çubuğundan kaldırılır. HTTP gövdesi kaydeden harici izleme araçlarında `/api/auth/complete-account` gövdesini kesinlikle kaydetmeyin.

Daveti kabul etmek rolü/markayı değiştirmez. Yeni şifreyi kaydetmek bütün eski oturumları geçersiz kılar. Şifre yenileme yanıtları hesabın varlığını açıklamaz; IP hız sınırı ve hesap başına dakika/saat sınırı uygulanır. Davet bekleyen yönetici son etkin yönetici yerine sayılmaz.

Geri dönüşte yeni tabloları ve bekleyen hesap bayrağını koruyun. Eski API yeni davet durumunu tanımadığından hesap yönetimi yapan eski/yeni sürümleri birlikte çalıştırmayın. Gönderimi durdurmak için `MAIL_ENABLED=false` yeterlidir; kayıt veya anahtar silmeyin.
