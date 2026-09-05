# Coolify Yayın Rehberi

OVO Growth OS'un üretim imajları GitHub Actions tarafından hazırlanır ve GitHub Container Registry'ye (GHCR) gönderilir. Coolify kaynak kodu derlemez; hazır imajları indirir ve yalnızca yayın sorumlusu **Deploy** düğmesine bastığında canlı sistemi günceller.

- `ghcr.io/ovo-digital/ovogrowthos-web`: Next.js kullanıcı arayüzü, içeride `3000` portunu dinler.
- `ghcr.io/ovo-digital/ovogrowthos-api`: ASP.NET Core API, içeride `8080` portunu dinler.

PostgreSQL servisi oluşturulmaz. API doğrudan mevcut Supabase PostgreSQL veritabanına bağlanır. Uygulama dosyaları kalıcı veri tutmadığı için volume gerekmez.

## 1. GitHub Actions ayarı

GitHub deposunda **Settings → Secrets and variables → Actions → Variables** bölümüne şu repository variable eklenmelidir:

| Değişken | Örnek | Açıklama |
|---|---|---|
| `PUBLIC_API_URL` | `https://growth-api.ovodigi.com` | Tarayıcının ulaşacağı API adresi; `https://` ile başlamalı ve sonunda `/` olmamalıdır. |

Bu değer parola değildir; web imajının derlenmesi sırasında tarayıcı koduna yazılır. Supabase bağlantısı, JWT anahtarı veya yönetici parola özeti GitHub Actions'a verilmez.

`.github/workflows/container-images.yml` workflow'u:

1. `main` dalında API, web veya workflow dosyası değiştiğinde otomatik çalışır.
2. API ve web imajlarını birbirinden bağımsız hazırlar.
3. Her imajı hem `latest` hem de `sha-<tam-commit-kodu>` etiketiyle GHCR'a gönderir.
4. `PUBLIC_API_URL` eksik veya hatalıysa localhost adresli bozuk bir üretim imajı göndermek yerine işlemi durdurur.

Gerektiğinde GitHub'daki **Actions → Üretim imajlarını hazırla → Run workflow** yoluyla aynı işlem elle de başlatılabilir. Bir build'in başarılı olması canlıya deploy yapmaz.

## 2. GHCR erişimi

Yeni GHCR paketleri varsayılan olarak özel görünüyorsa Coolify'ın bu imajları indirebilmesi için GitHub Container Registry kimlik bilgisi tanımlayın:

1. GitHub'da yalnızca paket okuma yetkili bir erişim anahtarı oluşturun (`read:packages`).
2. Coolify'da container registry kimlik bilgisi olarak `ghcr.io`, GitHub kullanıcı adı ve bu anahtarı kaydedin.
3. Bu kimlik bilgisini OVO Growth OS kaynağına bağlayın.

Paketler herkese açık yapılırsa Coolify anonim olarak da indirebilir. Erişim anahtarını compose dosyasına, ortam değişkeni örneğine veya GitHub deposuna yazmayın.

## 3. Coolify kaynağını oluşturma

1. Coolify'da yeni bir proje ve `production` ortamı oluşturun.
2. Yeni kaynak olarak **Docker Compose** uygulamasını seçin.
3. GitHub deposu olarak `Ovo-Digital/OVOGrowthOS`, dal olarak `main` seçin.
4. Compose dosya yolu olarak `/docker-compose.coolify.yml` girin.
5. Otomatik deploy seçeneğini kapalı bırakın.
6. Özel GHCR paketleri kullanılıyorsa bir önceki bölümdeki registry kimlik bilgisini bağlayın.

Coolify için tek doğruluk kaynağı `docker-compose.coolify.yml` dosyasıdır. Yerel geliştirmede kullanılan `docker-compose.yml` bu yayın için seçilmemelidir. Compose içindeki `pull_policy: always`, her manuel deploy sırasında seçili etiketteki güncel imajın indirilmesini sağlar.

## 4. Alan adlarını bağlama

Compose yüklendikten sonra her servise ayrı HTTPS alan adı verin:

- `web` servisi → `https://ovogrowth.ovodigi.com:3000`
- `api` servisi → `https://growth-api.ovodigi.com:8080`

Alan adındaki `:3000` ve `:8080`, dışarıya bu portları açmaz; Coolify proxy'sine konteyner içinde hangi portun dinlendiğini söyler. DNS kayıtları Coolify sunucusunu göstermeli ve sertifikalar etkin olmalıdır.

GitHub'daki `PUBLIC_API_URL`, API servisinin burada belirlediğiniz HTTPS adresiyle aynı olmalıdır. API adresi daha sonra değişirse repository variable değerini güncelleyin ve web imajını yeniden hazırlayın.

## 5. Coolify ortam değişkenleri

Coolify'ın **Environment Variables** alanında aşağıdaki değerleri girin. Gerçek değerleri GitHub'a, compose dosyasına veya ekran görüntüsüne koymayın.

| Değişken | Kullanım | Ayar |
|---|---|---|
| `IMAGE_REGISTRY` | İmaj deposu | Varsayılan `ghcr.io/ovo-digital` |
| `IMAGE_TAG` | Yayınlanacak sürüm | En yeni sürüm için `latest`; sabit sürüm için `sha-<tam-commit-kodu>` |
| `SUPABASE_DATABASE_CONNECTION_STRING` | Supabase PostgreSQL bağlantısı | Zorunlu, gizli, yalnız runtime |
| `DEFAULT_ADMIN_EMAIL` | İlk yönetici hesabı | Runtime; varsayılan `admin@ovodigital.com` |
| `DEFAULT_ADMIN_PASSWORD_HASH` | İlk yönetici parola özeti | Zorunlu, gizli, yalnız runtime |
| `WEB_ORIGIN` | API'nin izin vereceği web adresi | Zorunlu, runtime; web adresiyle birebir aynı |
| `JWT_ISSUER` | Oturum belirteci yayıncısı | Varsayılan bırakılabilir |
| `JWT_AUDIENCE` | Oturum belirteci hedefi | Varsayılan bırakılabilir |

`SERVICE_BASE64_64_JWT` Coolify tarafından otomatik üretilen kalıcı JWT anahtarıdır. İlk yayın sonrasında değiştirilirse açık oturumlar kapanır.

`PUBLIC_API_URL` artık Coolify ortam değişkeni değildir; GitHub repository variable olarak imaj hazırlanırken kullanılır. `WEB_ORIGIN` örneği:

```text
WEB_ORIGIN=https://ovogrowth.ovodigi.com
```

Supabase bağlantı değerinde `$` gibi özel karakterler varsa Coolify'da **Literal** seçeneğini etkinleştirin. Veritabanı bağlantısını build aşamasına göndermeyin.

## 6. Kaynak sınırları ve sağlık kontrolü

Başlangıç sınırları compose dosyasında tanımlıdır:

- API: 1 CPU, 1 GB bellek.
- Web: 0,5 CPU, 768 MB bellek.

Coolify arayüzünde ayrıca CPU veya bellek sınırı girilirse compose değerleriyle aynı tutulmalıdır. Her iki imajda sağlık kontrolü için `curl` bulunur:

- API: `/health`
- Web: `/login`

Yeni konteyner bu kontrolleri geçmeden sağlıklı kabul edilmez.

## 7. İlk manuel yayın

1. GitHub'daki `PUBLIC_API_URL` değerinin doğru API alan adı olduğunu kontrol edin.
2. GitHub Actions'taki **Üretim imajlarını hazırla** işleminin yeşil tamamlandığını doğrulayın.
3. Coolify ortam değişkenlerinin ve GHCR erişiminin eksiksiz olduğunu kontrol edin.
4. `api` ve `web` alan adlarının HTTPS olarak hazır olduğunu doğrulayın.
5. Coolify'da **Deploy** düğmesine basın.
6. API sağlık adresini açın: `https://api-adresi/health`.
7. Web giriş sayfasını açın ve yönetici hesabıyla giriş yapın.
8. Marka listesi, portföy özeti ve kullanım rehberi ekranlarını kontrol edin.

API açılırken bekleyen EF Core migration'larını Supabase'e uygular ve eksik örnek verileri mükerrer kayıt oluşturmadan tamamlar.

## 8. Sonraki sürümler ve geri dönüş

İlgili değişiklikler `main` dalına gönderildiğinde GitHub Actions yeni `latest` ve `sha-*` imajlarını hazırlar. Bu işlem canlı sistemi kendiliğinden değiştirmez.

Yeni sürüm yayınlanacağı zaman:

1. GitHub Actions build'inin başarılı olduğunu doğrulayın.
2. Coolify'da `IMAGE_TAG=latest` değerini koruyun.
3. **Deploy** düğmesine basın.
4. Sağlık kontrollerini ve temel kullanıcı akışını yeniden doğrulayın.

Belirli bir sürümü sabitlemek veya geri dönmek için ilgili başarılı GitHub Actions çalışmasının commit kodunu alın, Coolify'da iki servisin ortak `IMAGE_TAG` değerini `sha-<tam-commit-kodu>` yapın ve yeniden deploy edin. Veritabanı migration'ı geri alınması gereken bir değişiklik içeriyorsa yalnızca eski konteyner imajına dönmek yeterli olmayabilir; migration geri dönüş planını ayrıca uygulayın.

## Netlify durumu

`ovogrowthos.netlify.app` projesinin build'leri durdurulmuştur. GitHub commitleri Netlify'da otomatik build veya yayın oluşturmaz. Coolify canlı ortamı doğrulanana kadar bu ayar değiştirilmemelidir.
