# Netlify Manuel Yayın Rehberi

OVO Growth OS'un Next.js arayüzü Netlify'daki `ovogrowthos` projesinde yayınlanır. GitHub bağlantısı kaynak kodunu göstermek için korunur; Netlify otomatik derlemeleri durdurulduğu için GitHub'a gönderilen her commit kendiliğinden yayın oluşturmaz.

## Mimari sınır

Netlify bu projedeki Next.js arayüzünü çalıştırır. ASP.NET Core API ayrı ve herkese açık bir HTTPS adresinde çalışmalıdır. Arayüzün giriş ve veri ekranlarının çalışması için Netlify ortamındaki `NEXT_PUBLIC_API_URL` değeri bu API adresine ayarlanmalıdır. API tarafındaki `WebOrigin` değeri de `https://ovogrowthos.netlify.app` olmalıdır.

## İlk yerel bağlantı

Proje kökünde bir kez çalıştırın:

```bash
npx --yes netlify-cli@latest login
npx --yes netlify-cli@latest link --name ovogrowthos
```

Netlify bağlantı bilgisi `.netlify/state.json` altında tutulur ve Git'e gönderilmez.

## Ön izleme oluşturma

Güncel yerel kodu önce geçici bir adreste kontrol etmek için proje kökünde çalıştırın:

```bash
npx --yes netlify-cli@latest deploy --build
```

Komutun verdiği ön izleme adresinde giriş, menüler ve temel iş akışları kontrol edilir.

## Canlı yayına çıkarma

Kontroller bittikten sonra aynı güncel kodu canlı adrese göndermek için:

```bash
npx --yes netlify-cli@latest deploy --build --prod
```

Canlı adres: `https://ovogrowthos.netlify.app`

Bu yöntem yalnızca komut çalıştırıldığında yayın yapar. GitHub'a commit veya push yapılması tek başına Netlify yayını oluşturmaz.
