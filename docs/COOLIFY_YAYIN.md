# Coolify Yayın Rehberi

OVO Growth OS, Coolify'da tek GitHub deposundan iki servis olarak yayınlanır:

- `web`: Next.js kullanıcı arayüzü, içeride `3000` portunu dinler.
- `api`: ASP.NET Core API, içeride `8080` portunu dinler.

PostgreSQL servisi oluşturulmaz. API doğrudan mevcut Supabase PostgreSQL veritabanına bağlanır. Uygulama dosyaları kalıcı veri tutmadığı için volume gerekmez.

## 1. Coolify kaynağını oluşturma

1. Coolify'da yeni bir proje ve `production` ortamı oluşturun.
2. Yeni kaynak olarak **Docker Compose** uygulamasını seçin.
3. GitHub deposu olarak `Ovo-Digital/OVOGrowthOS`, dal olarak `main` seçin.
4. Compose dosya yolu olarak `/docker-compose.coolify.yml` girin.
5. Otomatik deploy seçeneğini kapalı bırakın. Yeni sürümleri yalnızca Coolify'daki **Deploy** düğmesiyle yayınlayın.

Coolify için tek doğruluk kaynağı `docker-compose.coolify.yml` dosyasıdır. Yerel geliştirmede kullanılan `docker-compose.yml` bu yayın için seçilmemelidir.

## 2. Alan adlarını bağlama

Compose yüklendikten sonra her servise ayrı HTTPS alan adı verin:

- `web` servisi → örnek: `https://growth.example.com:3000`
- `api` servisi → örnek: `https://api-growth.example.com:8080`

Alan adındaki `:3000` ve `:8080`, dışarıya bu portları açmaz; Coolify proxy'sine konteyner içinde hangi portun dinlendiğini söyler. DNS kayıtları Coolify sunucusunu göstermeli ve sertifikalar etkin olmalıdır.

## 3. Ortam değişkenleri

Coolify'ın **Environment Variables** alanında aşağıdaki değerleri girin. Gerçek değerleri GitHub'a, compose dosyasına veya ekran görüntüsüne koymayın.

| Değişken | Kullanım | Ayar |
|---|---|---|
| `SUPABASE_DATABASE_CONNECTION_STRING` | Supabase PostgreSQL bağlantısı | Zorunlu, gizli, yalnız runtime |
| `DEFAULT_ADMIN_EMAIL` | İlk yönetici hesabı | Runtime; varsayılan `admin@ovodigital.com` |
| `DEFAULT_ADMIN_PASSWORD_HASH` | İlk yönetici parola özeti | Zorunlu, gizli, yalnız runtime |
| `PUBLIC_API_URL` | Tarayıcının ulaşacağı API adresi | Zorunlu, build ve runtime; sonda `/` olmamalı |
| `WEB_ORIGIN` | API'nin izin vereceği web adresi | Zorunlu, runtime; web adresiyle birebir aynı |
| `JWT_ISSUER` | Oturum belirteci yayıncısı | Varsayılan bırakılabilir |
| `JWT_AUDIENCE` | Oturum belirteci hedefi | Varsayılan bırakılabilir |

`SERVICE_BASE64_64_JWT` Coolify tarafından otomatik üretilen kalıcı JWT anahtarıdır. İlk yayın sonrasında değiştirilirse açık oturumlar kapanır.

`PUBLIC_API_URL` ve `WEB_ORIGIN` örnekleri:

```text
PUBLIC_API_URL=https://api-growth.example.com
WEB_ORIGIN=https://growth.example.com
```

Supabase bağlantı değerinde `$` gibi özel karakterler varsa Coolify'da **Literal** seçeneğini etkinleştirin. Veritabanı bağlantısını build aşamasına göndermeyin.

## 4. Kaynak sınırları ve sağlık kontrolü

Başlangıç sınırları compose dosyasında tanımlıdır:

- API: 1 CPU, 1 GB bellek.
- Web: 0,5 CPU, 768 MB bellek.

Coolify arayüzünde ayrıca CPU veya bellek sınırı girilirse compose değerleriyle aynı tutulmalıdır. Her iki imajda sağlık kontrolü için `curl` bulunur:

- API: `/health`
- Web: `/login`

Yeni konteyner bu kontrolleri geçmeden sağlıklı kabul edilmez.

## 5. İlk manuel yayın

1. Ortam değişkenlerinin eksiksiz olduğunu kontrol edin.
2. `api` ve `web` alan adlarının HTTPS olarak hazır olduğunu doğrulayın.
3. Coolify'da **Deploy** düğmesine basın.
4. API sağlık adresini açın: `https://api-adresi/health`.
5. Web giriş sayfasını açın ve yönetici hesabıyla giriş yapın.
6. Marka listesi, portföy özeti ve kullanım rehberi ekranlarını kontrol edin.

API açılırken bekleyen EF Core migration'larını Supabase'e uygular ve eksik örnek verileri mükerrer kayıt oluşturmadan tamamlar.

## 6. Sonraki sürümler

GitHub'a gönderilen commitler canlı sistemi kendiliğinden değiştirmez. Yeni sürüm yayınlanacağı zaman:

1. Yerel test ve derlemeleri tamamlayın.
2. Değişiklikleri `main` dalına gönderin.
3. Coolify'da doğru commit'in göründüğünü kontrol edin.
4. **Deploy** düğmesine basın.
5. Sağlık kontrollerini ve temel kullanıcı akışını yeniden doğrulayın.

Sorun olursa Coolify'ın **Rollback** alanından son sağlıklı sürüme dönün. Veritabanı migration'ı geri alınması gereken bir değişiklik içeriyorsa yalnızca konteyner rollback'i yeterli olmayabilir; migration geri dönüş planını ayrıca uygulayın.

## Netlify durumu

`ovogrowthos.netlify.app` projesinin build'leri durdurulmuştur. GitHub commitleri Netlify'da otomatik build veya yayın oluşturmaz. Coolify canlı ortamı doğrulanana kadar bu ayar değiştirilmemelidir.
