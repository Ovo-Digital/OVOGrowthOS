# İki aşamalı giriş — işletim ve kurtarma

Bu özellik mevcut JWT/UserAccounts akışını kullanır; Supabase Auth'a geçiş yoktur. Yeni kurulum yalnız yönetici tarafından **kendi hesabı** için başlatılır. Zorunlu açılmaz; başka yönetici panelden kişinin ikinci aşamasını kapatamaz. Şifre yenileme ikinci aşamayı atlamaz. Rol değişse bile etkin ikinci aşama girişte aranır.

## Yayın öncesi

- API ve web aynı doğrulanmış sürümden yayımlanmalı; bütün eski API kopyaları durduktan sonra kullanıcı iki aşamalı girişi etkinleştirmelidir. Eski API bu özelliği bilmediğinden yan yana çalıştırmak korumayı atlatır.
- `AccountSecurity` eklemeli EF migration'ı `growth.AccountSecurities` tablosunu oluşturur. Mevcut hesaplarda koruma açılmaz, veri dönüştürülmez. Tablo özel `growth` şemasında kalır; Data API erişimi açılmaz.
- Daha önce SMTP için eklenmiş `MAIL_KEY_PATH=/app/mail-keys` ve kalıcı `mail-keys` volume'u **SMTP kapalı olsa da** korunmalıdır. TOTP anahtarları ayrı Data Protection amacıyla şifrelenir. Bütün API kopyaları aynı anahtar deposu/uygulama kimliğini kullanmalıdır. Anahtara erişim yalnız yetkili sunucu sorumlusunda olmalıdır.
- Sunucu ve telefon saatleri doğru olmalı. Kodlar standart 30 saniyelik TOTP, 6 hane, SHA-1 ile doğrulanır; bir önceki/sonraki zaman aralığı toleransı vardır. Son kabul edilen zaman aralığı kaydedilir; aynı veya eski kod tekrar kullanılamaz. [Otp.NET resmi dokümanı](https://github.com/kspearrin/Otp.NET) ve [RFC 6238](https://datatracker.ietf.org/doc/html/rfc6238).
- Önce deneme ortamında kurulum, doğru/yanlış kod, ikinci giriş, kurtarma kodu, diğer oturumların kapanması ve şifre yenilemeden sonra kodun hâlâ gerekmesi kontrol edilmelidir. Gerçek hesapta etkinleştirmeyi kullanıcı kendi doğrulama uygulamasıyla yapar.

## Saklanan ve saklanmayan bilgiler

TOTP anahtarı şifreli tutulur. On adet 128 bit rastgele kurtarma kodunun yalnız SHA-256 özeti saklanır. Kod listesi kurulumda veya yeniden üretmede bir kez yanıtlanır; tarayıcı belleği dışında saklanmaz. Giriş isteğinin 256 bit rastgele doğrulama anahtarının da yalnız özeti saklanır; 5 dakika geçerlidir, her yeni giriş önceki bekleyen isteği geçersiz kılar. Hesap sürümüyle bağlanır. Kurulum 10 dakika geçerlidir.

İkinci aşama tamamlanmadan JWT verilmez. Beş yanlış denemede hesap bazında beş dakika kilit uygulanır; yeni giriş bu sayacı temizlemez. IP sınırı ayrıca korunur. PostgreSQL hesap satırı kilidi ve durum sürümü, eşzamanlı kurtarma kodu/kod/giriş tüketimini korur. Açma, kapatma ve kurtarma kodu yenileme eski JWT oturumlarını geçersiz kılar.

İşlem geçmişinde yalnız güvenlik eylemi ve hesap kimliği vardır; kod/anahtar/parola yoktur. `/api/auth/*` gövdelerini dış izleme araçlarına kaydetmeyin. API yanıtları `no-store`; kurulum bilgileri URL'ye veya localStorage'a yazılmaz. Bu özellik kapsamlı bağımsız güvenlik denetimi yerine geçmez.

## Telefon veya anahtar deposu kaybı

1. Kullanıcı mevcut şifre + kullanılmamış kurtarma koduyla giriş yapar. Kurtarma kodu doğrulaması TOTP şifreleme anahtarına bağlı değildir; anahtar deposu kaybında da bu yol çalışır.
2. Girişten sonra mevcut şifre + başka kullanılmamış kurtarma koduyla ikinci aşama kapatılır; yeni telefonda tekrar kurulur. Yeni kurulumdan önce anahtar deposu sorunu çözülmelidir.
3. Yalnız kurtarma kodlarını yenilemek telefonun TOTP anahtarını değiştirmez. Telefon kaybında anahtarı da değiştirmek için kapatıp yeniden kurun.
4. Tek yönetici dahil herkes kurtarma kodlarını telefondan ayrı tutmalıdır. Sistem son yönetici için e-postayla ikinci aşamayı atlama veya otomatik devre dışı bırakma sağlamaz.

## Bütün kurtarma yolları kaybolursa

Bu, rutin panel işlemi değil manuel güvenlik olayıdır. Sunucu sorumlusu hesabın sahibini bağımsız ve güvenilir kanaldan doğrulamalı; tam hesap kimliğini, gerekçeyi ve açık onayı kaydetmelidir. Otomatik destek bağlantısı veya ortak kurtarma parolası kullanılmaz.

Yalnız doğrulanmış hesabın güvenlik kaydını, oturum sürümünü ve işlem geçmişini aynı veritabanı işlemi içinde ele alan dar kapsamlı müdahale gerekir: ikinci aşama anahtarı/bekleyen giriş/kurtarma özetleri kapatılır, `UserAccounts.TokenVersion` artırılarak eski oturumlar iptal edilir, gerekçeli işlem geçmişi eklenir. Rol, e-posta, parola ve başka hesaplar değiştirilmez. Bu müdahale için hazır toplu SQL veya herkese açık API **yoktur**; hedef ve izin doğrulanmadan yapılmaz. Sonrasında kullanıcı yeni telefon ve yeni kurtarma kodlarıyla yeniden kurulum yapar.

## Geri dönüş

Tabloyu/volume'u silmeyin ve migration `Down` çalıştırmayın. İkinci aşama açılmış hesap varken eski API sürümüne dönmek güvenlik açığı doğurur. Sorun halinde önce erişimi kısıtlayıp bu korumayı tanıyan doğrulanmış sürümü düzeltmek tercih edilir; eski sürüme dönüş ayrı, açık bir güvenlik kararı ve hesap bazlı plan gerektirir. E-postayı panelden kapatmak veya bütün API kopyalarında `MAIL_FORCE_DISABLED=true` uygulayıp yeniden başlatmak iki aşamalı girişi etkilemez. Eski `MAIL_ENABLED` yalnız panelde henüz e-posta ayarı kaydedilmemişse geçerlidir.
