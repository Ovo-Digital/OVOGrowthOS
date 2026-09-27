# OVO Growth OS — Beşinci geliştirme planı (V5)

## 1. Yönetici özeti

Dördüncü plan (`docs/GELISIM_PLANI_V4.md`) Faz 0–6 ile tamamlandı: e-posta ayarları, gönderim politikası, zamanlanmış raporlar, gönderim merkezi, veri kalitesi panosu, iş kapsamı ve aday hattı sahaya çıktı; 371 backend testi geçiyor.

V5, dört analiz turundan çıkan bulguları sıraya dizer: doğrulama kapıları (CI) ve açılış gözetimi yokluğu, giriş hız sınırı ve parola kilidi boşluğu, veri doğruluğu eksikleri, performans/gözlemlenebilirlik ve arayüz tutarlılığı.

**Kullanıcı kararları (27 Eylül 2026):**

- Faz A ile başla; fazlar arasında ara soru sorma, doğrudan devam et.
- **Faz C (PostgreSQL test ortamı + yedek doğrulama)** ertelenmişti; kullanıcı onayıyla 27 Eylül 2026'da yerel ortamda tamamlandı (bkz. 4. Faz).
- **Faz F açık onayla yürütülüyor** ("hepsine onay veriyorum sırayla ilerle"): 1. Gmail pilotu ve 2. canlı yayın provası kullanıcı tarafından atlandı; 3. adım (marka API ayarları + bağlantı doğrulaması) uygulandı; V2 Faz 8A (Ağustos 2026 mağaza siparişleri salt okunur çekme) canlı GrandNode ile uçtan uca doğrulandı; 4. WhatsApp/SMS ve 5. ödeme adımları kullanıcının kararıyla ertelendi (sağlayıcı hesabı/API anahtarı/bütçe kararı bekliyor).

## 2. Faz A — Temel güvenlik, doğrulama kapıları ve açılış gözetimi

**Amaç:** Hatalı yapılandırma, kaba kuvvet ve doğrulanmamış sürüm yayınlamayı daha ilk istekte durdurmak.

Önerilen işler:

1. CI kapısı: her pull request'te backend build + test ve web tip kontrolü/lint/birim testi/üretim derlemesi çalışsın; imaj üretimi bu kapıya bağlanır.
2. Hız sınırı üç ayrı politikaya bölünsün: giriş (IP, 10/dk), oturumlu işlem (kullanıcı, 30/dk), yönetici işlemi (kullanıcı, 20/dk). Güvenilir ağ arkasındaki istekte gerçek istemci IP'si, doğrulanamayan yerde doğrudan bağlantı IP'si kullanılsın; istemcinin kendi yazdığı başlık sınırlamayı etkilemesin.
3. Beş hatalı parola denemesi hesabı beş dakika kilitlesin; kilit süresince doğru parola da reddedilsin. Her deneme ve kilit sonucu denetim kaydına yazılsın; başarılı giriş sayacı sıfırlasın.
4. Üretim/test dışı ortamda repodaki varsayılan JWT anahtarı, boş/çok kısa anahtar ve varsayılan yönetici parola özeti ile açılış durdurulsun. Swagger yalnız geliştirmede açık kalsın.
5. Örnek (demo) marka portföyü yalnız geliştirmede veya açıkça açılan `Seed:DemoData` bayrağıyla yüklensin; canlı ortamda boş veri tabanı boş kalsın.
6. `GET /health/ready` veritabanına ulaşabiliyorsa 200, ulaşamıyorsa 503 dönsün; duran `/health` yalnız sürecin ayakta olduğunu söyler.

**Kabul:** Kötü anahtarla üretim ayağa kalkamaz; kilitli hesap parola ile açılamaz; hız sınırı yapan istek diğer kullanıcıların işini engelleyemez; doğrulanmamış PR üretime imaj üretemez; canlı ortama örnek marka verisi girmez.

### Uygulanan kapsam — 27 Eylül 2026 (Faz A)

- `.github/workflows/ci.yml` (yeni): `pull_request` + `workflow_call` ile tetiklenen iki iş — **API derleme ve test** (`dotnet build` + `dotnet test OvoGrowthOS.sln`) ve **Web tip kontrolü, lint ve test** (`npm ci` → `typecheck` → `lint` → `test:unit` → `build`, `apps/web`). `push` olayı bilinçli eklenmedi; çift çalışmayı önlemek için.
- `.github/workflows/container-images.yml`: `dogrulama` işi `ci.yml`'i çağırır; `api-imaji` ve `web-imaji` artık `needs: [ayarlari-dogrula, dogrulama]` ile yalnız kapılar geçince üretilir; imaj yollarına `*.sln` ve `ci.yml` eklendi. İki dosyanın iş sırası Python `yaml.safe_load` ile doğrulandı.
- `Program.cs`: üç hız sınırı politikası (`login`, `user-action`, `admin-action`) ve `RatePartitionKey` (oturum varsa `user:<uid>`, yoksa `ip:<istemci>`); `ClientIp` yalnız **güvenilir ağ** (`172.16.0.0/12`, `192.168.0.0/16` — Docker/Coolify ağları) bağlantısında `X-Forwarded-For` başlığının **son** değeri alır, güvenilmez kaynakta başlık yok sayılır. Boru hizası `UseAuthentication` → `UseRateLimiter` oldu ki kullanıcı kimliği sınırda görünebilsin. Hız sınırı reddi mesajı ayrıştı: `/api/auth/*` için giriş, diğer uçlar için genel Türkçe uyarı. Tek politika `login` kullanan uçlar taşındı: 2FA yönetimi → `user-action`, davet ve e-posta ayarları → `admin-action`, bildirim tazeleme → `user-action`.
- `StartupGuard.cs` (yeni, `Program.cs`'te servis kayıtlarından önce çağrılır): Development/Testing dışı ortamda boş veya 32 karakterden kısa `Jwt:Key`, repodaki varsayılan anahtar ve repodaki varsayılan `DefaultAdmin:PasswordHash` ile `InvalidOperationException` fırlatır; mesajlar `JWT_KEY` ve `DEFAULT_ADMIN_PASSWORD_HASH` ortam değişkenlerini gösterir. Swagger yalnız `IsDevelopment()`.
- `AccountSecurityService.Login`: parola kontrolünden önce kilit sorgulanır (kilitliyse 401 + “Çok fazla hatalı deneme…”); parola yanlışsa `Failed` + `LoginFailed` denetim kaydı yazılır; parola doğruysa `Success` (sayaç ve kilit sıfırlanır) ve oturum açılışında `Login` denetim kaydı yazılır. Var olan 2FA akışı (`Locked`/`Proof`/`CompleteLogin`) değiştirilmedi.
- `SeedData.InitializeAsync(db, configuration, includeDemoData)`: demo portföy yalnız bayrak açıkken; `MigrateAsync` yalnız ilişkisel sağlayıcıda. `Program.cs` çağrısı: `IsDevelopment() || Seed:DemoData`. `appsettings.json` → `"Seed": { "DemoData": false }`; `docker-compose.coolify.yml` → `Seed__DemoData: ${SEED_DEMO_DATA:-false}`.
- `GET /health/ready`: `CanConnectAsync()` ile 200/503. `docker-compose.coolify.yml` sağlık kontrolü `/health` üzerinde bırakıldı (konteyner yeniden başlatma döngüsü olmasın); yük dengeleyici isteyen kurulum `/health/ready` kullanabilir.
- Web tarafında ek kod gerekmedi: `apps/web/src/lib/api.ts` sunucudan gelen `error`/`title` metnini zaten “ovo:notice” uyarısı olarak gösterir; bu yüzden 429 ve kilit mesajları arayüzde Türkçe görünür.

**Tasarım kararları ve sapmalar:**

- `ForwardedHeaders` ara katmanı **kullanılmadı**: denetimsiz bir eş (TestServer ve doğrulanmamış yerel istek) başlığı güvenilir kabul edip sınır anahtarını değiştiriyordu; bunun yerine başlık güveni `ClientIp` içinde açık ve test edilebilir kuralla verildi. API protokolü zaten HTTP olduğu için şema/yönlendirme başlığına da ihtiyaç yok.
- Kilit durumu **401** döner (403 değil): oturum açma isteği zaten kimlik doğrulaması isteğidir.
- Örnek veri testi, uygulama fabrikası üzerinden değil `SeedData.InitializeAsync` doğrudan çağrılarak yazıldı; test barındırıcısı Program akışını iki aşamada çalıştırdığı için örnek yükleme tek bir sağlayıcıda görünmüyordu. Davranışın kendisi (bayrak → 0 veya 10 marka) aynı doğrulamayı verir.

### Tamamlanan doğrulama — 27 Eylül 2026

382 backend testi (139 domain + 243 API) geçti; bu fazın yeni testleri: `StartupGuardTests` (6: geliştirme/test ortamı varsayılanla çalışır, boş/kısa/varsayılan anahtar ve varsayılan yönetici özeti reddedilir, döndürülmüş yapı kabul edilir), `StartupBehaviorTests` (3: `/health` ve `/health/ready` 200; giriş hız sınırı dolduğunda oturumlu ve yönetici uçları çalışır; `SeedData` bayrağı kapalıyken 0, açıkken 10 örnek marka ve yönetici tohumlanır) ve `AuthenticationTests.Five_failed_logins_lock_password_entry_until_the_lock_expires` (5 hatalı → kilit + 5 `LoginFailed`, kilit sonunda doğru parola 401, süre dolunca giriş açılır, sayaç sıfırlanır, tek `Login` kaydı). Mevcut `Repeated_login_attempts_are_limited_even_with_spoofed_forwarding_header` sahte `X-Forwarded-For` ile 10 denemede 429 beklentisini **değiştirmeden** geçti. Build 0 uyarı/0 hata (yalnız önceden var olan tek `CS8602`); web tip kontrolü, lint (0 uyarı), 7 birim testi ve üretim derlemesi başarılı. Docker compose dosyaları ve iki GitHub Actions dosyası sözdizimi denetiminden geçti. Gerçek e-posta gönderilmedi, canlıya yayınlanmadı, veritabanı migration'ı bu fazda gerekmedi.

## 3. Faz B — Veri doğruluğu ve rapor tutarlılığı

**Amaç:** Ekranda görülen sayı ile kayıttaki gerçeğin çelişmemesi.

1. Analizi yeniden çalıştırma artık `ApprovalConditions`/`ApprovalEvidence` silmiyor; onay kaydı yalnız açıkça kaldırılır.
2. Gönderim merkezi: filtre + sayfalama (ilk 25 kayıta kısıtlı liste).
3. `/leads`: arama, filtre ve sayfalama.
4. `money()` her yerde para birimiyle; `/deals` kuru göstermiyor.
5. `AdminOnly` uçlarda rol yalnız başlıkta değil gerçekten kontrol edilsin (mail ayarları dahil).
6. `performance?search=` ile arama alanı.

**Kabul:** Yeniden çalıştırmadan sonra onay kanıtı kaybolmaz; liste baştan sona gezilebilir; tutarsız para birimi görünmez; yetki başlıktan ibaret kalmaz.

### Uygulanan kapsam — 27 Eylül 2026 (Faz B)

- **B1 — analizi yeniden çalıştırma kanıt silmiyor:** `WorkflowEndpoints.cs` `Analyze` içinde `RemoveRange` + toplu ekleme yerine koda göre eşleştirme geldi. Mevcut koşul bulunursa başlık/açıklama/zorunluluk tazelenir, `Status`, `EvidenceUrl`, `ResolutionReason`, `ResolvedBy`, `ResolvedAt` **korunur**; yeni kod Pending olarak eklenir; artık istenmeyen satırlardan yalnız `Pending` olanlar silinir, çözülmüş (`Satisfied`/`Waived`) satırlar geçmiş olarak kalır.
- **B2 — gönderim merkezi sayfalama:** `mail-deliveries/page.tsx` `page` durumu ve `Pagination` aldı; `page` sorguya eklenir, filtreler değişince sayfa 1'e döner. Filtreler (tür, durum, tarih aralığı, alıcı) zaten sunucu tarafındaydı.
- **B3 — `/leads` arama, filtre, sayfalama:** `/api/lead-follow-ups` `search` (marka adı + iletişim kişisi, `ToLower().Contains`) ve `stage` (takip aşaması) parametrelerini kabul eder; `WorkflowEndpoints.Team.cs`. Arayüzde `ListControls` (arama + aşama) ve ortak `Pagination` var; arama `useDebouncedValue` ile 300 ms geciktirilir, filtre/sayfa değişimi sorgu anahtarına yazılır. Boş sonuç, filtre varsa “Aramaya uyan marka bulunmuyor.” der.
- **B4 — `money()` her yerde para birimiyle:** `/deals`, marka ve değerlendirme listeleri artık kayıt üzerindeki para birimini geçirir; `evaluations/[id]/deals` (eğilim bölümü `e.data.brand.currency`), `evaluations/[id]/scenarios` ve `/data-quality` düzeltildi. `DataQuality.cs` `BrandQuality`/`QualityInput` alanına `Currency` eklendi, `WorkflowEndpoints.DataQuality.cs` marka para birimini üretir. `components/ui/core.tsx` içinde kullanılmayan ikinci `money` tanımı silindi.
- **B5 — yetki gerçekten kontrol ediliyor:** `AuthorizationAuditTests.cs` (yeni) üç kanıt verir: (1) hiçbir uç politika metadata'sı olmadan açık kalmaz (`Every_route_is_policy_gated_or_explicitly_public`), (2) `AllowAnonymous` yalnız bilinen altı genel uçtur (`/health`, `/health/ready`, `complete-account`, `forgot-password`, `login`, `second-factor`), (3) veritabanında rolü `Analyst` yapılan veya pasifleştirilen yöneticinin elindeki geçerli jeton bile `/api/users`'a giremez. Kullanıcıya dönük düzeltme gerekmedi: `Program.cs` `OnTokenValidated` (satır 113–129) her istekte hesabın aktifliğini, davet durumunu, jeton sürümünü **ve rolünü** veritabanıyla karşılaştırır; politika bu doğrulanmış role bakar. Mail uçlarındaki handler içi `CurrentAdmin` deseni (`MailSettings`, `AccountMail`, `MailCenter`, `BrandMailPolicy`) olduğu gibi korundu.
- **B6 — `/performance?search=` derin bağlantısı:** `performance/page.tsx` artık `useSearchParams` + `Suspense` ile `/activity` deseninde açılır, `search` değerini okur ve filtrelerle birlikte sıfırlama kuralını uygular. Sunucu tarafında `search` zaten vardı; `Trim` eklendi.

**Tasarım kararları ve sapmalar:**

- Arama `ToLower().Contains()` ile yazıldı: `EF.Functions.ILike` InMemory test sağlayıcısında çalışmaz; PostgreSQL tarafında da aynı sonucu verir, tek fark sıralama (kullanıcı araması için fark yok).
- B5 kapsamı 23 `AdminOnly` handler'ına tek tek `CurrentAdmin` eklemek yerine, yetkinin **zaten** jeton doğrulama katmanında veritabanına karşı sağlandığını testlerle kanıtlamak olarak uygulandı; bu, aynı korumayı tüm oturumlu uçlar için tek yerde tutar ve politika ile handler arasında ikinci bir doğrulama noktasını çoğaltmaz.
- Yeniden çalıştırmada artık istenmeyen ama çözülmüş bir koşul silinmez: o koşulun kaldırılması ayrı ve bilinçli bir işlem ister; analiz tek başına onay geçmişini temizlemez.

### Tamamlanan doğrulama — 27 Eylül 2026

387 backend testi (139 domain + 248 API) geçti; bu fazın yeni testleri: `WorkflowApiTests.Reanalysis_keeps_resolved_conditions_and_drops_only_stale_pending_ones` (çözülmüş koşul kanıtıyla kalır, bekleyen artık koşul silinir, kod tekrarı oluşmaz — düzeltme geri alındığında testin gerçekten başarısız olduğu ayrıca doğrulandı), `TeamWorkTests.Lead_list_filters_by_search_text_and_stage` (ad, iletişim kişisi ve aşamaya göre süzme), `AuthorizationAuditTests` (3: açık uç yok, anonim uç listesi sabit, değişen rol/pasif hesap jetonu reddedilir). Build 0 uyarı/0 hata (yalnız önceden var olan tek `CS8602`); web tip kontrolü, lint, 7 birim testi ve üretim derlemesi başarılı. Migration gerekmedi; gerçek e-posta gönderilmedi, canlıya yayınlanmadı.

## 4. Faz C — PostgreSQL test ortamı, yedek provası ve göç geri alma

**Amaç:** Göç zincirinin, verinin ve felaket kurtarma yolunun gerçek bir PostgreSQL üzerinde kanıtlanması; test ortamlarının InMemory sağlayıcısı sorgu çevirisini ve yedek/geri adımları doğrulayamaz.

1. Yerel PostgreSQL 17 test ortamında göç zincirinin tamamının uygulanması.
2. Gerçek veritabanında yedek (`pg_dump`) ve geri yükleme (`pg_restore`) provası.
3. Son göcün geri alınması ve yeniden uygulanması (rollback) denemesi.

**Kabul:** Göç zinciri gerçek PostgreSQL'de hatasız uygulanır; yedek yeni veritabanına hatasız geri yüklenir ve yapı/veri özeti birebir eşleşir; geri alma sonrası ileri alma ile şema geri gelir ve API yeniden ayağa kalkar.

**Not:** İlk planlamada ertelenmişti; kullanıcı onayıyla 27 Eylül 2026'da açıldı. Yalnız yerel/test ortamında yapıldı; canlı veritabanına yazma yapılmadı.

### Uygulanan kapsam — 27 Eylül 2026 (Faz C)

- **C1 — yerel test ortamı:** Makineye Homebrew üzerinden PostgreSQL 17.11 kuruldu; geçici veri dizininde servis olarak değil, prova için başlatıldı (bittiğinde durduruldu). Uygulamanın yerel bağlantı dizesi (`Host=localhost`, `Database=ovo_growth_os`) ve modelin `growth` şeması kullanıldı. Repodaki **22 göcün tamamı** gerçek PostgreSQL'e uygulandı — böylece EF Core testlerinin InMemory sağlayıcısında doğrulanamayan sorgu çevirisi de gerçek veritabanında sınanmış oldu.
- **C2 — açılış ve tohum provası:** API Development ortamında yerel veritabanına bağlanarak açıldı; `/health` ve `/health/ready` **200** döndü. Örnek portföy **188 satır** yüklendi; ikinci açılışta satır sayısı değişmedi (tohum idempotent).
- **C3 — yedek/geri yükleme provası:** `pg_dump -Fc` ile **169 850 bayt** yedek alındı; taze bir veritabanına `pg_restore` **0 hata** ile geri yüklendi. Kaynakla geri yükleme karşılaştırması: 52 tablo/kolon yapısı aynı, 22 göç geçmişi aynı, toplam 188 satır aynı, `Brands` içerik özeti (md5) ve kullanıcı listesi birebir aynı.
- **C4 — göç geri alma provası:** `dotnet ef database update 20260926003951_Phase5ScopeTimeTracking` ile son göç (`Phase6PipelineTracking`) geri alındı: göç geçmişi 22 → 21, tablo 52 → 51, `BrandFollowUps` 12 → 7 kolon (eklenen `LostBy`, `LostOn`, `LostReason`, `SourceChannel`, `SourceNote` düştü). Ardından ileri `database update` ile her şey geri geldi; yapı yedekle birebir aynı oldu ve API yeniden ayağa kalkıp `/health/ready` 200 verdi.

**Tasarım kararları ve sapmalar:**

- **Dikkat — canlı bağlantı tuzağı:** `dotnet ef` komutuna `--connection` yazılmazsa bağlantı **kullanıcı sırlarındaki (user-secrets) canlı Supabase** bağlantısını kullanır. Provanın ilk denemesi bu yüzden canlıya gitti ve yalnız salt-okunur çalıştı (“veritabanı zaten güncel”); hiçbir şey yazılmadı. Bu yüzden prova boyunca tüm yerel komutlar açık `--connection` ile verildi. İleride yapılacak her yerel göç işinde bu bağlantı açıkça yazılmalı.
- **Canlı ile yerel aynı şemada:** Canlı göç geçmişinde repoda olmayan eski bir satır var (`20260322220251_Initial_PostgreSQL`); EF bunu yok sayar. Tablo yapısı (52 tablo) repodaki zincirle birebir aynı.
- **Geri alma veri de düşürür:** Geri alınan göçün eklediği kolon ve tablolardaki veri silinir. Canlıda geri alma sırası: önce yedek al, sonra geri alma, doğrula, gerekirse ileri al. Yedek/geri yükleme provası bu yüzden geri alma provasından önce yapıldı.
- Prova geçici veri dizininde yapıldı, bittiğinde yerel PostgreSQL durduruldu; yedek dosyası geçici dizinde kanıt olarak duruyor. Göç kodu değişmedi, testler etkilenmedi.

### Tamamlanan doğrulama — 27 Eylül 2026

Yerel PostgreSQL 17 üzerinde 22/22 göç, API açılışı (`/health` + `/health/ready` 200), idempotent tohum (188 satır), hatasız yedek geri yükleme (yapı/veri özeti birebir) ve geri alma → ileri alma turu tamamlandı; tur sonunda API yeniden ayağa kalktı. Backend testleri bu fazda değişmedi: 392 test (139 domain + 253 API) geçiyor. Canlı Supabase'e yalnız salt-okunur sorguyla bakıldı, yazma yapılmadı.

## 5. Faz D — Performans ve gözlemlenebilirlik

**Amaç:** Büyüyen veride sayfaların ve ayıklamanın yavaşlamaması.

1. Sorgu/katman ölçümü ve gereksiz yüklemelerin kaldırılması (özellikle liste uçları).
2. Kritik uçlar için yapısal log/olay: kim, ne, ne zaman — gizli alanlar hariç.
3. Yavaş istek ve hata uyarıları; `/health/ready`'yi gözlem aracı bağlama.

**Kabul:** Ölçüm öncesi/sonrası sayısal fark raporlanır; loglarda parola/anahtar/İçerik bulunmaz.

### Uygulanan kapsam — 27 Eylül 2026 (Faz D)

- **D1 — liste uçları ölçülüp hafifletildi:** `/api/evaluations`, `/api/deals`, `/api/performance` ve `/api/audit` listeleri artık anlamsal alanları proje ediyor; marka adı, durum, tarih ve para gibi görünen alanlar dururken kayıtların ekranı dolduran `SnapshotJson`/değer JSON blokları liste yanıtına hiç girmiyor (`WorkflowEndpoints.cs`, `WorkflowEndpoints.Performance.cs`). Detay uçları (`/api/evaluations/{id}`, `/api/deals/{id}`) eski zengin içeriklerini koruyor; arayüz liste alanlarını zaten kullanmıyordu.
- **D2 — bildirim listesinde satır başına sorgu kalktı:** `NotificationService` artık toplu çözümleme (`ResolveMany`) sunuyor; günlük görev tarihleri, görev, portal erişimi, soru ve rapor aktifliği sorguları sayfa için **birer kez** çalışır. `Resolve` imzası ve gönderim yollarındaki kilitli okuma davranışı değişmedi; sonuçlar `WorkflowEndpoints.Notifications.cs` listesinde toplu çözümleniyor.
- **D3 — aday listesinde bağlı alt sorgular kalktı:** `/api/lead-follow-ups` son temas ve açık aşama bilgisini satır başına iki ayrı alt sorgu yerine sayfa için iki toplu sorgudan alıyor (`WorkflowEndpoints.Team.cs`). Yanıt şekli ve filtre/sayfalama davranışı aynı.
- **D4 — gönderim merkezinde filtreler sınırdan önce:** tür, durum ve alıcı filtreleri artık `Take(500)` öncesinde sorguya yazılıyor; önce 500 en yeni kayıt çekilip bellekte süzüldüğü için filtre eski kayıtları görmüyordu (`WorkflowEndpoints.MailCenter.cs`). Kayıt/durum özeti ve sayfalama aynı sonuçlarla çalışıyor.
- **D5 — yavaş istek ve hata uyarısı:** `RequestLogMiddleware` yalnız **5xx** ve `RequestLog:SlowMs` (varsayılan 1000 ms) üstü istekleri uyarı seviyesinde, şu alanlarla yazıyor: istek kimliği, yöntem, **yol**, durum kodu, süre, kullanıcı ve rol. Sorgu dizesi, başlık, gövde ve çerez yazılmaz; yapılandırmayla eşiği değiştirilebilir.
- **D6 — log hijyeni:** Serilog istek günlüğü `IncludeQueryInRequestPath = false` ile sorgu dizesiz yazıyor; `Microsoft.AspNetCore.Hosting` kategorisi uyarıya çekildi (barındırma katmanının “istek başladı/bitti” satırları sorgu dizesi içeriyordu). Parola, jeton ve ilet gövdesi hiçbir hatta yazılmıyor; bunu testler doğruluyor.
- **D7 — `/health/ready` gözlem aracı bağlantısı:** `docs/COOLIFY_YAYIN.md` zaten `/health/ready`'yi izleme aracı adresi olarak tanımlıyor (konteyner sağlık kontrolü `/health`'te kalır); faz kapsamında belge bu davranışla tutarlı tutuldu, yeni altyapı gerekmedi.

**Ölçüm — aynı tohum verisiyle öncesi/sonrası (40 değerlendirme + 40 anlaşma + 100 aday + 100 denetim kaydı, her biri 4 KB'lik ekran içeriğiyle):**

| Uç | Önce | Sonra |
| --- | --- | --- |
| `/api/evaluations?pageSize=40` | 709 870 bayt | 15 671 bayt |
| `/api/deals?pageSize=40` | 872 025 bayt | 22 639 bayt |
| `/api/performance?pageSize=40` | 916 670 bayt | 13 334 bayt |
| `/api/audit?pageSize=50` | 422 846 bayt | 11 446 bayt |
| Dört uç toplamı | 2 921 411 bayt | 63 090 bayt (−%97,8) |
| `/api/notifications` (100 satır) | ~204 sorgu, 7 ms | ~6 sorgu, 2 ms |
| `/api/lead-follow-ups?pageSize=50` | 3 alt sorgu × satır (≈150+) | 3 toplu sorgu |

**Tasarım kararları ve sapmalar:**

- EF Core InMemory sağlayıcısı sorgu **icra** sayısını yalnızca farklı sorgu biçimi sayısına indirdiği için (100 satırda 7) doğrudan sayılamıyor; kabul ölçütü bu yüzden **kesin yanıt baytı + medyan süre** alındı, sorgu azaltımı kod yapısından raporlandı ve kalıcı test bayt/anahtar sınırlarını koruyor. Ölçüm amaçlı geçici `ListPerformanceMeasurements`/probe testleri kaldırıldı.
- `NotificationService.Resolve` yerine “toplu yol” eklendi, iç mantık tek yerde kaldı: paylaşılan önbellek hem tekil (`Resolve`) hem toplu (`ResolveMany`) çağrıda kullanılıyor, dolayısıyla iki çözümleme yolu arasında davranış farkı oluşmuyor.
- Yavaş istek kaydı ayrı bir kütüphaneye değil, projede zaten kurulu Serilog ile birlikte çalışan küçük bir middleware'e bağlandı; bilgi seviyesindeki istek günlüğü olduğu gibi kaldı, yalnız gizlenmesi gereken sorgu dizesi kapatıldı.
- Gönderim merkezinde test dalındaki `q` filtresi de sorguya taşındı (denetim kayıtlarında alıcı kullanıcı kimliğidir), böylece filtre ve sınır her kaynakta aynı sırada çalışıyor.

### Tamamlanan doğrulama — 27 Eylül 2026

392 backend testi (139 domain + 253 API) geçti; bu fazın testleri: `ListPayloadTests.List_endpoints_stay_small_and_never_ship_snapshot_or_audit_content` (6 liste ucunun bayt sınırı, `snapshotJson`/`oldValueJson`/`newValueJson` anahtarlarının yanıttan çıkması, medyan süre sınırı, aday ve denetim toplamları), `RequestLoggingTests` (4: 5xx yalnız operasyon alanlarıyla ve sorgu dizesi/başlık olmadan yazılır, fırlatılan istisna 500 olarak kaydedilir, yavaş istek yazılır hızlı/200 yazılmaz, canlı isteklerde parola/jeton/sorgu değeri loglara sızmaz). Mevcut `MailCenterTests`, `TeamWorkTests`, `NotificationTests` ve `AuthorizationAuditTests` değişiklikleri doğruladı. Build 0 uyarı/0 hata (yalnız önceden var olan tek `CS8602`); web tip kontrolü, lint, 7 birim testi ve üretim derlemesi başarılı. Migration gerekmedi; gerçek e-posta gönderilmedi, canlıya yayınlanmadı.

## 6. Faz E — Arayüz tutarlılığı ve erişilebilirlik

**Amaç:** Dağınık `prompt`/`confirm` ve hata durumlarını tek desende toplamak.

1. `Modal` bileşeni; onay ve form diyalonlarını `prompt`/`confirm` yerine ona taşımak.
2. Yükleniyor, boş ve hata durumlarının her listeli ekranda aynı görünmesi.
3. Klavye ile gezinme, odak yönetimi, etiket–alan ilişkisi ve 390 pikselde taşma kontrolü.

**Kabul:** Kritik akışda tarayıcı `prompt`/`confirm` kullanmaz; hata metni okunur ve erişilebilirdir.

### Uygulanan kapsam — 27 Eylül 2026 (Faz E)

- **E1 — `Modal` bileşeni ve diyalog API'si:** `apps/web/src/components/ui/modal.tsx` içinde `Modal` (yerleşim/odak/kapama), `DialogProvider` ve `useDialog()` var; `confirm(...)` ve `prompt(...)` Promise döndürür ve `Providers` üzerinden tüm uygulamaya bağlıdır. Pencere `role="dialog"`, `aria-modal` ve `aria-labelledby` taşır; **Esc** ve dışarı tıklama vazgeçer, odak açılışta giriş kutusuna ya da **Vazgeç** düğmesine gider, kapanınca geri döner, **Tab** odak içeride kalır, gövde kaydırması kilitlenir; giriş alanı ile etiket `htmlFor` ile bağlıdır ve 390 pikselde alttan gelen tam genişlikte yerleşir.
- **E2 — tarayıcı `prompt`/`confirm` kalmadı:** 21 dosyada 33 çağrı (16 uygulama sayfası + 11 bileşen) uygulamanın kendi penceresine taşındı; her çağrı `await confirm(`/`await prompt(` biçiminde, iptal davranışı (false/null) ve Türkçe cümleler aynen korunarak yazıldı. Silme, arşivleme, sonlandırma, geri çekme, yayımlama ve yeniden davet gibi işlemlerde `tone:"danger"` ve işlemin adını taşıyan düğme kullanıldı. Gezinme korumaları da (`use-unsaved-changes`, `app-shell`) dönüştürüldü: tıklama anında navigasyon durdurulur, onaydan sonra `router.push`/`location.assign` ile gidilir. `beforeunload` (sayfadan ayrılmadan önce tarayıcının kendi sorması) platform zorunluluğu olduğu için native kaldı.
- **E3 — liste durumları tek desende:** `core.tsx`'e `LoadingState`, `EmptyState` ve `ErrorState` eklendi; 21 dosyada (10 uygulama sayfası + 11 bileşen) yaklaşık 85 yükleme/boş/hata bloğu bu bileşenlerle değiştirildi. Türkçe cümleler aynen korundu; “Yeniden dene” düğmesi içeren hata bloklarında düğme ayrıca tutuldu.
- **E4 — 390 piksel ve odak denetimi:** Tablo kullanan ve kaydırma sarmalayıcısı olmayan 10 dosyadaki tüm tablolar `overflow-x-auto` içine alındı (dar ekranda sayfa taşmıyor); `globals.css`'e klavye odağı için mavi `:focus-visible` çerçevesi eklendi. Pencere ve liste durumu kodu zaten `role="dialog"`/`role="status"`/`role="alert"` taşıyor.
- **E5 — rehber:** `OVO_GROWTH_OS_KULLANIM_REHBERI.md` 4. bölüme “Onay pencereleri, liste durumları ve klavye kullanımı” alt başlığı eklendi; `apps/web` kopyası birebir senkron.

**Kabul kanıtı:** `apps/web/tests/browser-dialogs.test.mjs` iki kalıcı testi kaynak kodu tarar: (1) hiçbir `.ts`/`.tsx` dosyasında tarayıcının `prompt`/`confirm` çağrısı kalmadığı (`await prompt(`/`await confirm(` biçimleri ayıklanarak doğrudan kodda aranır), (2) 10 listeli ekranın ortak `LoadingState`/`EmptyState`/`ErrorState` bileşenlerini kullandığı.

**Tasarım kararları ve sapmalar:**

- Bekleyen söz (Promise) deseni tercih edildi: kütüphane, durum makinesi veya yeni bir form çerçevesi eklenmedi; mevcut erken-return akışı `if (!(await confirm(...))) return;` ile birebir aynı kaldı.
- Kritik akışta tarayıcı diyalogu kalmadı; yalnız `beforeunload` native çünkü tarayıcı bu sormayı kendi göstermek zorunda. Kaynak tarama testi bu dosyada `confirm(` bulunmadığı için ayrıcalık gerektirmez.
- Hata bloklarındaki “Yeniden dene” düğmeleri ortak `ErrorState` sadece metin aldığı için korundu; standartlık metin/rol/rengi kapsıyor.
- `leads` kayıp nedeni boş kontrolünün mesajı `validate` içine taşındı; kullanıcı aynı Türkçe uyarıyı görüyor ve yine gönderim yapılmıyor.

### Tamamlanan doğrulama — 27 Eylül 2026

Web tarafı: tip kontrolü temiz, lint 0 hata/0 uyarı, **9 birim testi** (7 mevcut + 2 yeni kaynak tarama testi) ve üretim derlemesi başarılı. Backend bu fazda değişmedi; 392 test (139 domain + 253 API) aynı şekilde geçti. Kullanım rehberi güncellendi ve `apps/web` kopyasıyla birebir aynı. Gerçek e-posta gönderilmedi, canlıya yayınlanmadı, migration gerekmedi.

## 7. Faz F — Onay kapısı (kullanıcı onayıyla açıldı)

Kapsam: Gmail pilotu (gerçek gönderim), canlı yayın provası, Shopify/GrandNode/reklam entegrasyonları, WhatsApp/SMS, ödeme alma. Kullanıcı 27 Eylül 2026'da fazları sırayla onayladı; 1. ve 2. adım "bu adımları geç" diyerek atlandı (gerçek gönderim yapılmadı, canlıya publish edilmedi).

### Uygulanan kapsam — 27 Eylül 2026 (Faz F, 3. adım: Mağaza API ayarları)

- Yeni tablo `growth.BrandApiSettings` (marka başına tek satır): `BrandId` PK + Brands FK (silinmeye bağlı), `StoreUrl` (300), `ApiUser` (320), korunan `ProtectedPassword`, `Revision` (eşzamanlılık + `CK_BrandApiSettings_Revision`), `UpdatedAt`, `LastTestAt`. Migration `20260927083033_BrandApiSettings`; yerel PostgreSQL 17'de uygulanıp yapı doğrulandı (yerelde 23 göç).
- Uçlar (AdminOnly + `admin-action` hız sınırı): `GET/PUT /api/brands/{id:guid}/api-settings`, `POST /api/brands/{id:guid}/api-settings/test`. Şifre `IDataProtectionProvider` ile `OVO.BrandApiCredentials.v1` amacıyla korunur; GET yalnız `passwordStored` döner, şifre hiçbir yanıtta/üretim günlüğünde bulunmaz. Boş şifre kayıtlı şifreyi korur; ilk kayıtta şifre zorunlu. Eski `Revision` → 409, `DbUpdateConcurrencyException` → 409.
- `POST .../test`: kayıtlı şifre endpoint'te base64'e çevrilip `IStoreTokenClient` (`StoreTokenClient`, 15 sn aşım, yanıtta düz ayrıntı/HTML reddi) üzerinden `POST /Api/Token/Create` çağırır. Yanıt yalnız genel Türkçe kabul/red mesajı; denetim kayıtları `BrandApiTestRequested/Accepted/Uncertain` içerik içermez. `LastTestAt` 1 dakika tekrar sınırı; istek, mağaza çağrısından önce kalıcı yazılır.
- Arayüz: marka detayında **Mağaza API ayarları** kartı (`brand-api-settings.tsx`; yönetici dışı rolde kart yerine kısa bilgi). Kaydetmek doğrulama yapmaz; doğrulama tek onay kutulu düğmeyle. `Cache-Control: no-store`, `/api-settings` içeren tüm yollara uygulanır.
- Doğrulama düzeltmeleri: `SmtpSettings.IsAddress` artık `@` şartı da koşuyor (MimeKit düz yerel parçayı geçerli sanıyordu); no-store yolu `EndsWith` yerine `Contains` ile `/api-settings/test` yolunu da kapsıyor.

### Uygulanan kapsam — 27 Eylül 2026 (V2 Faz 8A: Mağaza siparişleri çekme)

- Yeni tablo `growth.StoreOrderStaging`: `BrandId` + `SourceOrderId` (marka bazlı teklik), döneme göre `PlacedOnUtc` indeksi, `numeric(18,4)` tutarlar ve `>= 0` kontrolü; alanlar `OrderNumber`, `Currency`, `OrderTotal`, `PaidAmount`, `RefundedAmount`, `OrderStatusId`, `PaymentStatusId`, `ImportedAt` — müşteri adı/e-posta/adres/IP **yok** (istemci yalnız seçili alanları çeker). Migration `20260927093733_StoreOrderStaging`.
- `StoreOrderPeriod`: dönem sınırları Europe/Istanbul (UTC+3, yaz saati yok) `YYYY-MM` olarak parse edilir; `StoreOrderSummary.Summarize` tüm toplamları domain'de `decimal` ile üretir, `ComparePanel` aylık sonuçla yalnız karşılaştırma değerleri döner.
- `StoreOrderClient`: GrandNode OData `GET /odata/Order` salt okunur okuma — `CreatedOnUtc` dönem filtresi + `$count`/`$skip` sayfalama (sayfa 100, en fazla 50 sayfa → taşma bayrağı), 30 saniye aşım; yasaklı değiştirici uçlar (`SetAsImported`, `UpdateOrderShipment`, `AddExternalId`, `AddOrderNote`) kullanılmaz; jeton uçta alınır ve hiçbir yanıtta/günlükte geçmez.
- Uçlar: `GET /api/brands/{id}/store-orders?period=&page=` (Admin/Partner/Analyst) ve `POST .../store-orders/sync` (AdminOnly + `admin-action` hız sınırı). Kaynak kimliğiyle (BrandId + SourceOrderId) tekrar engellenir; negatif tutar / dönem dışı sipariş / geçersiz para birimi / geçersiz dönem biçimi → 400 ve mevcut kayıtlar değişmez. Denetim yalnız `StoreOrdersSynced` + dönem/sayı özeti.
- Arayüz: marka detayında **Mağaza siparişleri** kartı — dönem seçimi, yöneticiye özel "Siparişleri getir", özet (iptal hariç toplam, iptal, iade), aylık sonuçla karşılaştırma notu, kaydırılabilir sipariş tablosu ve sayfalama; dönem/hata/bilgi metinleri Türkçe.
- Canlı doğrulama (27 Eylül 2026): `admin.ovodigi.com` panelinden alınan geçici jetonla Ağustos 2026 (TR sınırları) **8 sipariş** çekildi; ikinci senkronizasyonda "0 yeni, 8 güncellenen" (teklik korundu), dönem dışı Temmuz senkronizasyonu Ağustos kayıtlarına dokunmadı, okuma yanıtında kaynak kimliği/PII/jeton/şifre bulunmadı, denetim kayıtlarında yalnız dönem ve sayı özeti. Şifre veritabanında korunaklı (`CfDJ…`), düz metin yok. Aktarılan siparişler hakedişe, aylık sonuca veya anlaşma kaydına otomatik yazılmaz. Canlı Supabase göçü 27 Eylül 2026'da kullanıcının onayıyla uygulandı (aşağıdaki doğrulama paragrafı). Sohbete açık yazılan API şifresi pilot sonrası yenilenmeli.

**Doğrulama — 27 Eylül 2026:** Backend **428 test** (154 domain + 274 API; bu fazın yeni testleri: 15 `StoreOrderStagingTests` + 9 `StoreOrderSyncTests` — idempotent tekrar, 400 ile yazmama, dönem/saat sınırı, rol ve hız sınırı, hata mesajı sızdırmazlığı, aylık sonuç karşılaştırması) ve 0 hata ile build; web tip kontrolü, lint, 9 birim testi ve üretim derlemesi başarılı. Migration yerel PostgreSQL 17'de uygulandı; kullanım rehberi §6/§27 güncellendi, `apps/web` kopyası senkron. **Canlı Supabase (27 Eylül, kullanıcı onayıyla):** `20260927083033_BrandApiSettings` ve `20260927093733_StoreOrderStaging` canlıya uygulandı, bekleyen migration kalmadı; uygulama canlı bağlantıyla başlatılıp `/health` ve `/health/ready` 200, yeni uçlar 401 (yönlendirme doğru) ile doğrulandı. Canlıya yalnız bu iki şema migration'u yazıldı; uygulama üzerinden veri yazımı (senkron vb.) yapılmadı.

### Engel ve sonraki adım

Erişim engeli çözüldü: panelde hazır API kullanıcısının şifresinin **sonunda nokta** olduğu anlaşıldı (`POST /Api/Token/Create` 200 döndü) ve V2 Faz 8A canlı veriyle tamamlandı. Sohbete açık yazılan API şifresi pilot sonrasında panelden yenilenmeli. Sırada: Faz F'in ertelenen 4–5. adımları (WhatsApp/SMS, ödeme) — kullanıcı kararıyla beklemede; canlı Supabase şeması güncel.

### Tamamlanan doğrulama — 27 Eylül 2026

Backend: **404 test** (139 domain + 265 API; 12'si yeni `BrandApiSettingsTests` — yetki/revizyon/400-404-409, base64 şifre doğrulaması, başarısız testte güvenli mesaj, 1 dakika sınırı) ve 0 hata ile build. Migration yerel PostgreSQL'de uygulandı ve yerel API ile uçtan uca duman testi yapıldı: boş okuma → kayıt → `passwordStored:true` + şifreli kolon (`CfDJ…`) → başarısız doğrulamada genel mesaj → ikinci deneme 409 → denetimde yalnız işlem adı, şifre yok. Web: tip kontrolü, lint, 9 birim testi ve üretim derlemesi başarılı; rehber §6/§27 güncellendi, `apps/web` kopyası senkron. Canlıya yayınlanmadı, canlı veritabanına yazma yapılmadı.

## 8. Önerilen uygulama sırası ve karar kapıları

1. Faz A tamamlandı (bu belge).
2. Faz B tamamlandı: veri doğruluğu.
3. Faz D tamamlandı: performans ve gözlemlenebilirlik.
4. Faz E tamamlandı: arayüz tutarlılığı ve erişilebilirlik.
5. Faz C tamamlandı: yerel PostgreSQL 17'de göç zinciri, yedek/geri yükleme ve göç geri alma provası; canlı için sıra "önce yedek" olarak belgelendi.
6. Faz F açık onayla sürüyor: 1–2 kullanıcı tarafından atlandı, 3. adım (marka API ayarları + bağlantı doğrulaması) tamamlandı ve V2 Faz 8A (Ağustos 2026 mağaza siparişleri salt okunur çekme) canlı GrandNode ile doğrulandı; 4–5 kullanıcı kararıyla beklemede.

## Kaynak kodu dayanakları

- `.github/workflows/ci.yml`, `.github/workflows/container-images.yml`
- `apps/api/src/OvoGrowthOS.Api/Program.cs`, `StartupGuard.cs`, `Auth/AccountSecurityService.cs`, `Data/SeedData.cs`
- `apps/api/src/OvoGrowthOS.Api/Features/WorkflowEndpoints.Notifications.cs`, `WorkflowEndpoints.AccountMail.cs`, `WorkflowEndpoints.MailSettings.cs`
- `apps/api/tests/OvoGrowthOS.Api.Tests/StartupGuardTests.cs`, `StartupBehaviorTests.cs`, `AuthenticationTests.cs`
- `apps/web/src/lib/api.ts` (hata metni gösterimi), `apps/web/src/components/list-controls.tsx` (ListControls/Pagination)
- `apps/api/src/OvoGrowthOS.Api/Features/WorkflowEndpoints.Team.cs`, `WorkflowEndpoints.cs` (Analyze), `WorkflowEndpoints.DataQuality.cs`
- `apps/api/tests/OvoGrowthOS.Api.Tests/AuthorizationAuditTests.cs`, `WorkflowApiTests.cs`, `TeamWorkTests.cs`
- `apps/web/src/app/leads/page.tsx`, `mail-deliveries/page.tsx`, `performance/page.tsx`
- `docker-compose.coolify.yml`, `apps/api/src/OvoGrowthOS.Api/appsettings.json`
- `apps/api/src/OvoGrowthOS.Api/RequestLogMiddleware.cs`, `Notifications/NotificationService.cs`, `Features/WorkflowEndpoints.MailCenter.cs`
- `apps/api/tests/OvoGrowthOS.Api.Tests/ListPayloadTests.cs`, `RequestLoggingTests.cs`, `MailCenterTests.cs`
- `apps/web/src/components/ui/modal.tsx`, `src/components/ui/core.tsx`, `src/app/globals.css`, `src/components/use-unsaved-changes.ts`, `src/components/app-shell.tsx`
- `apps/web/tests/browser-dialogs.test.mjs`
- `apps/api/src/OvoGrowthOS.Domain/BrandApiSettings.cs`, `apps/api/src/OvoGrowthOS.Api/Features/WorkflowEndpoints.BrandApiSettings.cs`, `apps/api/src/OvoGrowthOS.Api/Integration/StoreTokenClient.cs`
- `apps/api/tests/OvoGrowthOS.Api.Tests/BrandApiSettingsTests.cs`, `apps/web/src/components/brand-api-settings.tsx`
