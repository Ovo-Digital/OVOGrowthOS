# OVO Growth OS — Üçüncü Gelişim Planı

Tarih: 15 Eylül 2026  
Durum: **Sıralı uygulama kullanıcı tarafından onaylandı. Faz 1–5 tamamlandı. Faz 6'nın kodlama ve yerel doğrulaması tamamlandı: hesap e-postaları, kişisel bildirimler ve tercihler, görev/rapor/konuşma e-postaları, isteğe bağlı yönetici iki aşamalı girişi ve kurtarma akışı hazır. Gerçek Gmail teslim pilotu kullanıcı bilgilerini girdikten sonra yapılacak; canlı yayın ve gerçek hesapta güvenlik etkinleştirme yapılmış sayılmaz.**
İncelenen kaynak sürümü: `ebc6c68`

## 1. Kısa değerlendirme

Sistemin güçlü tarafı artık sadece hesap yapması değil: marka değerlendirmesi, anlaşma, aylık kapanış, görev takibi, tahsilat, gerçekleşen gider, yatırım takibi, açıklamalı rapor ve kontrollü müşteri paylaşımı aynı yerde bulunuyor.

Benim önerim, bundan sonraki yatırımı şu soruya yöneltmek: **“Ekip bugün ne yapmalı, hedefin neresindeyiz ve önümüzdeki ay hangi sorunla karşılaşabiliriz?”**

Bu nedenle ilk hedef daha fazla grafik veya yapay zekâ eklemek değil; günlük işteki eksikleri kapatmak, hedef koymak ve mevcut veriden eylem çıkarmak olmalı.

### Analizin sınırları

- Kaynak kodu, mevcut ekranlar, domain modelleri, test dosyaları, kullanım rehberi ve önceki plan incelendi.
- İlk plan analizi sırasında canlı uygulamaya, müşteri hesaplarına veya veritabanına bağlanılmadı. Güncel marka sayısı, gerçek iş hacmi ve canlı sürüm doğrulanmış kabul edilmez.
- İlk plan analizindeki eski test sonuçları tarihsel kanıttır. Uygulama sırasındaki yeni doğrulamalar aşağıdaki ilerleme kaydında ayrıca tutulur; bu çalışma kapsamlı güvenlik denetimi sayılmaz.
- Önceki plandaki tamamlanmış özellikler yeni özellikmiş gibi tekrar önerilmiyor.
- Önceki V2 Faz 2 (yayın/örnek veri/yedekleme işleri) ve Faz 8 (canlı entegrasyonlar) ertelenmiş durumdadır. Bu belge onları yeniden başlatma izni değildir.
- İlk analizde yalnız plan dosyası eklendi. Sonraki kullanıcı onayıyla uygulamaya başlandı; yapılmamış fazlar kullanım rehberine mevcutmuş gibi yazılmaz.

### Uygulama ilerlemesi — 16 Eylül 2026

- **Faz 1 tamamlandı:** Yalnız taslak düzenleme, sunucuda hesap ön izlemesi, gerekçeli taslağa gönderme, onayın kaldırılması, eski ekranla güncelleme reddi ve aynı kişinin yeniden onay engeli kodlandı. Hazırlayanın e-postası değişse de yeni gönderimlerde hesap kimliğiyle ikinci kişi kuralı korunur.
- Rehber tek MD dosyasından, sunucuda iç ekip hesabı doğrulandıktan sonra yüklenir. Girişsiz ve müşteri istekleri için kapalıdır. Rehber yeni iş akışıyla güncellendi.
- Başarılı kontroller: 66 domain + 96 API testi; dört web birim testi; web typecheck, lint ve üretim derlemesi. İzole tarayıcı verisiyle yeni kayıt, 390×844 mobil düzenleme, taslağa dönüş, yeniden gönderme, ikinci kişi onayı, kilit/açma ve analistin işlem düğmelerine erişememesi doğrulandı. Tarayıcı konsolunda hata görülmedi.
- Gerçek PostgreSQL bağlantısında iki bağımsız EF bağlamının eski `xmin` ile yazması engellendi; gerçek kaydetme işleyicisinin zaman damgası veritabanından okunanla aynı kaldı. 50.000,1256 iade → 949.999,8744 net ciro ve 50.000 hakediş aynen saklandı. Eski ekran isteği 409 aldı. İşlem geri alındı; deneme markası, dönem ve işlem geçmişi kaydının kalmadığı doğrulandı. Mevcut veriler güncellenmedi.
- Üretim web derlemesinde rehber API'si girişsiz/geçersiz hesapta 401, müşteri hesabında 403, üç iç ekip rolünde 200 verdi. HTML/RSC yanıtlarında rehber metni bulunmadı. İç ekip yanıtının kökteki MD ile birebir aynı olduğu ve `private, no-store` başlığı doğrulandı. Müşteri `/guide` adresinden portalına yönlendi; güncel rehber normal ekip ekranında açıldı.
- Şema/migration değişikliği yok. Mevcut PostgreSQL `xmin` eşzamanlılık denetimi ve `UpdatedAt` alanı kullanılır. Güncelleme/onay işlemleri artık okunan sürümü `If-Match` ile ister; eski panelle işlem 428/409 ile durabilir. Yayın sorumlusu API ve web'i aynı tamamlanmış sürümden birlikte seçmelidir. Bu not otomatik yayın yetkisi vermez.
- **Faz 2 tamamlandı:** Marka/ay/para birimi için net ciro, reklam bütçesi, katkı marjı ve etkin sorumlu kaydı; zorunlu gerekçeli sürüm geçmişi; aynı kapsamdaki gerçekleşen sonuçla tutar/yüzde/yüzde puan karşılaştırması; sapmadan mevcut görev sistemine izlenebilir takip işi eklendi. Aynı gösterge için hedef değişse veya iş tamamlansa da ikinci görev üretilmez; mevcut iş yeniden açılır.
- Marka sayfasında ve açıklamalı raporda hedef alanına bağlantı var. Analist salt okunur, müşteri kapalı. Müşteri raporu/CSV/PDF'ye hedefler kendiliğinden eklenmez. Rehberde ay/para birimi seçimi, kayıt, değişiklik, eksik/geçici sonuçlar ve görev akışı anlatılır.
- Faz 2 doğrulaması: 71 domain + 100 API testi, beş web birim testi, typecheck, lint ve üretim build'i. Gerçek tarayıcıda ilk kayıt, gerekçeli ikinci sürüm, önce/sonra geçmişi, %45,25 marj girişi, 1.000.000,1234 hedefin dört ondalıkla gösterimi, eksik sonuçta “veri yok”, taslakta geçici sonuç, görev oluşturma/tamamlama ve yeniden üretmeme doğrulandı. 390×844 görünümde yatay taşma yok; konsolda hata görülmedi. Analistte değiştir/görev düğmeleri yok; müşteri doğrudan hedef adresinden portalına yönlendi. Rehberin yeni bölümü uygulamada açıldı.
- `20260916170856_MonthlyTargets` EF migration'ı incelendi, testlerden sonra yapılandırılmış Supabase'e uygulandı; bekleyen migration kalmadı. Yalnız `growth.MonthlyTargets` ve `growth.TargetActions` tabloları, yabancı anahtarları, kontrol kısıtları ve indeksleri eklendi. Mevcut tablo/veri dönüştürülmedi. Para alanları `numeric(18,4)`, hedef sürümü eşzamanlılık belirtecidir.
- Gerçek PostgreSQL'de eski hedef sürümüyle ikinci yazma, yinelenen marka/ay/para birimi ve yinelenen hedef/gösterge engellendi; başarısız görev oluşturma geride görev bırakmadı. Gerçek API üzerinden `/health`, hedef okuma ve takip oluşturma doğrulandı. 1.000.000,1234 hedef ile 850.000,1256 gerçekleşen arasındaki fark −149.999,9978 olarak aynen okundu; kapalı dönemin hakedişi korunmuştu. Bütün deneme kayıtları aynı işlemin geri alınmasıyla kaldırıldı ve kalmadıkları doğrulandı. `growth` şemasına `anon`/`authenticated` rollerinin erişimi olmadığı kontrol edildi; yeni genel erişim yetkisi açılmadı.
- Geri dönüş yolu: gerekirse önceki uygulama sürümüne dönülür, yeni tablolar ve geçmiş korunur. Migration'ın `Down` adımı yeni hedef/geçmiş bağlantılarını sileceğinden veri varken otomatik uygulanmaz; ayrıca açık veri silme izni gerekir. Uygulama yayını yapılmadı; canlı panel bu kaynak değişikliklerini henüz içermeyebilir.
- **Faz 3 tamamlandı — 17 Eylül:** Kullanıcı onaylı iki görev şablonu, mevcut kapanış görevini değiştirmeden kullanma, haftalık kapasite, görev saat planı ve kaydedilmeyen ek iş ön izlemesi eklendi. Tekrar görev üretmeme, eski sürümle güncelleme engeli, eksik/sıfır ayrımı ve çalışan değişince geçmiş haftalar dahil planın yeni sorumluya taşınması test edildi. Kullanım rehberi bütün adımları ve sınırları anlatır. Doğrulanmış kişi/görev bazlı gerçekleşen süre olmadığı için toplam hizmet saatlerinden çalışan süresi veya maliyet türetilmez.
- Faz 3 doğrulaması: **77 domain + 106 API testi**, **7 web birim testi**, backend build, web typecheck/lint/üretim build'i ve `git diff --check` başarılı. Müşteri rolünün yeni iç API'lere erişimi genel yetki testiyle kapalı; analistin yazma istekleri reddediliyor. Mevcut kapanış görevinin tamamlanmış durumunu koruma, eski ön izlemeyi reddetme, hatalı son adımda kısmi görev oluşturmama ve finansal kaydı değiştirmeme testleri geçti.
- `20260916222234_WorkTemplatesAndCapacity` migration'ı incelenip testlerden sonra yapılandırılmış Supabase'e uygulandı. Yalnız `growth.TaskHourPlans`, `WeeklyCapacities`, `WorkTemplateRuns`, `WorkTemplateTasks` ve ilgili indeks/yabancı anahtar/kısıtlar eklendi. Migration listesinde bekleyen kayıt yok. Önceki uygulama sürümü yeni tabloları kullanmadan çalışabilir; veri varken `Down` uygulanmaz, geri dönüşte yeni kayıtlar korunur.
- Gerçek PostgreSQL bağlantısında `/health`, salt okunur şablon ön izlemesi, gerçek API ile üç görev oluşturma, yinelenen şablonu reddetme, 40 − 8 − 20,25 = **11,75 saat** hesabı ve 15 saat ek işte **−3,25 saat** ön izlemesi doğrulandı. İki ayrı EF bağlamında eski kapasite/plan sürümüyle yazma reddedildi. Yinelenen çalışan/hafta, görev/hafta, şablon kapsamı ve pazartesi olmayan tarih veritabanınca engellendi. Tüm deneme kayıtları tek işlemin geri alınmasıyla kaldırıldı; geride deneme markası, görev, kapasite, plan veya işlem geçmişi kalmadığı doğrulandı.
- İzole bellek verisi kullanan tarayıcı testinde her iki şablondan üçer görev oluşturma, başlangıç şablonunu ikinci kez uygulayamama, `20,25` saat kaydı, eksik kapasitenin “Bilgi yok” olması, 40/8 kapasite ve 15 saat ek yük denemesi geçti. Ön izleme seçimi korundu; kapasite düzenlenirken ön izlemeyi temizleyerek formu kaybetme engellendi. Analistte plan/kapasite okunuyor, yazma düğmeleri yok; müşteri doğrudan kapasite adresinden portalına yönlendi. 390×844 görünümde yatay taşma yok, konsolda hata görülmedi. Yeni rehber bölümü bağlantıdan açıldı; rehber API/HTML/RSC erişim kontrolü yeniden geçti. Bu testler canlı uygulamanın yayımlandığı anlamına gelmez.
- **Faz 4 ilk adımı:** Vade yaşlandırması, bugünden ileri 4/8/12 haftalık vade planı ve geriye aynı uzunlukta gerçek ödeme tarihli tahsilat görünümü kodlandı. Her para birimi ayrı; vadesi bilinmeyen/gecikmiş/takvim sonrası alacaklar ayrı tutulur. İnceleme gereken kayıtlar toplamdan çıkarılıp açıkça gösterilir. Bu ilk okuma akışı mevcut tahsilat defterini kullanır, şema veya finansal kayıt değiştirmez. Ödeme sözü ve görev bağlantısı aşağıdaki ikinci adımda tamamlandı.
- Faz 4 ilk adım doğrulaması: **92 domain + 108 API testi**, **7 web testi**, backend build, web typecheck/lint/üretim build'i ve diff kontrolü başarılı. Yaşlandırmanın 0/1/30/31/60/61/90/91 gün sınırları, haftaların uç tarihleri, kısmi ödeme ve iptal, sıfır/eksik veri, inceleme gereken kayıtların ayrılması ve para birimi izolasyonu test edildi. Model ile son migration arasında değişiklik yok; bu adım için yeni migration gerekmiyor.
- Gerçek PostgreSQL/API kontrolünde yalnız işlem içinde oluşturulan deneme kaydında 100,1234 − 40,0001 = **60,1233** kalan alacak ve ilk haftanın beklentisi doğrulandı; 12 ayrı zaman aralığı ve gerçek tarihli 40,0001 ödeme okundu. Hakediş/ciro/hesap snapshot'ı değişmedi; okuma işlem geçmişi yaratmadı. İşlem geri alındı, geride deneme kaydı kalmadı. İzole tarayıcı testinde 4/8/12 hafta, TRY/USD ayrımı, liste filtresinin üst toplamları değiştirmemesi, tarihsiz eski 200 USD'nin haftalara dağıtılmaması, kaynak dönem bağlantıları, 390×844 taşmasız mobil görünüm ve güncel rehber bağlantısı doğrulandı. Müşteri doğrudan yeni adresten portalına yönlendi; konsol hatası görülmedi.
- **Faz 4 tamamlandı — 18 Eylül:** Dönem başına tek güncel ödeme sözü, kaynak marka görüşmesi, etkin takip sorumlusu, gerekçeli önce/sonra geçmişi, sözü takipten kaldırma ve aynı dönemde çoğalmayan görev bağlantısı eklendi. Yeni söz şu andan itibaren beklenen tutardır; kayıt anında mevcut ödemeler tekrar düşülmez. Sonradan eklenen geçerli ödemeler sözün kalanını azaltır, iptali geri getirir. Beklenti her zaman gerçek kalan alacakla sınırlıdır. Hakediş, kâr, vade ve finansal kapanış değiştirilmez; söz gerçek tahsilat sayılmaz. Bağlı görev söz değişince otomatik atanmaz/tamamlanmaz; kullanıcı mevcut işi yönetir.
- Vade planı ile ödeme sözü planı ayrı seçilir. Söz takvimi + tarihi geçmiş sözler + takvim sonrasındaki sözler + sözle karşılanmayan alacak = toplam kalan alacak. Para birimleri karışmaz; eksik söz/tarih uydurulmaz. Rehberde kayıt, değişiklik, kısmi ödeme, iptal, görev ve eski ekran uyarıları anlatıldı.
- Faz 4 ikinci adım doğrulaması: **96 domain + 114 API testi**, **7 web testi**, backend build, web typecheck/lint/üretim build'i başarılı. Yanlış marka notu, kapalı çalışan, uygunsuz dönem, fazla/sıfır/negatif veya hassasiyet dışı tutar, eski söz/tahsilat sürümü, analist yazması ve müşteri iç API erişimi engellendi. Tamamlanmış işi çoğaltmama, sözün sorumlusu değişince mevcut görevi sessizce değiştirmeme ve finansal snapshot koruması test edildi.
- `20260917215440_CollectionPromises` migration'ı incelenip testlerden sonra yapılandırılmış Supabase'e uygulandı. Yalnız `growth.CollectionPromises` ve yabancı anahtar/indeks/kısıtları eklendi; mevcut satırlar dönüştürülmedi. Bekleyen migration/model değişikliği yok. Para alanı `numeric(18,4)`; ödeme kimliği dizisi kayıt anındaki ödeme tabanını saklar. Önceki uygulama sürümü yeni tabloyu kullanmadan çalışabilir. Geri dönüşte tablo/geçmiş korunur; veri silen `Down` ayrıca açık izin olmadan uygulanmaz.
- Gerçek PostgreSQL'de eski söz sürümüyle yazma, aynı döneme ikinci söz ve sıfır tutar engellendi; başarısız yinelenen kayıtta görev artığı kalmadı. Gerçek API okumasında önceki 20,0001 ödemeden bağımsız yeni 60,1233 söz, sonradan eklenen 10,0001 ödemeyle **50,1232** kaldı; ödeme iptalinde **60,1233** oldu. Kalan alacağın sözlü/sözsüz ayrımı ve kapalı finansal snapshot korundu. `/health` ve özel `growth` şeması erişimi kontrol edildi. Bütün deneme satırları aynı işlemin geri alınmasıyla kaldırıldı; geride kalmadıkları doğrulandı. Bu kontrol gerçek API okumalarını ve PostgreSQL yazma kısıtlarını kapsar; API yazma işleyicilerinin tüm senaryoları ayrıca bellek içi entegrasyon testlerinde çalıştırıldı.
- İzole tarayıcıda dört ondalıklı söz kaydı, görev oluşturma, 1.000,0001 kısmi ödeme ve gerekçeli iptal, söz kaldırma/yenileme, önce–sonra kaynak metinli geçmiş, 4/12 hafta ve TRY/USD ayrımı doğrulandı. 390×844 form ve takvimde yatay taşma yok. Analist bilgileri okuyup değiştiremiyor; müşteri doğrudan hakediş adresinden portalına yönleniyor. Rehberin yeni ödeme sözü bölümü uygulamada açıldı.
- **Faz 5 ilk adımı tamamlandı — 18 Eylül:** Müşteri portalına son ay dahil 3/6/12 aylık yayımlanmış rapor görünümü eklendi. Oturumun marka erişimi esas alınır; her ayın paylaşımı açık en yüksek sürümü yalnız bir kez kullanılır. Para birimi seçimi sürüm seçildikten sonra uygulanır; eski para birimli sürüm geri getirilmez. Paylaşılmamış ayın değeri sıfır değil eksik bilgidir. Her satırdan kullanılan kaynak rapor açılır. Toplamlar yalnız yayımlanan anlık görüntülerden domain katmanında `decimal` ile hesaplanır; canlı dönem veya iç maliyet okunmaz. Yeni ödeme otomatik yansımaz. Tek raporun PDF görünümüne karşılaştırma eklenmez. Rehberin müşteri bölümüne adımlar ve sınırlar yazıldı.
- Faz 5 ilk adım doğrulaması: **102 domain + 117 API testi**, **7 web testi**, backend build, web typecheck/lint/üretim build'i başarılı. Başka marka ve paylaşılmamış dönem izolasyonu, aynı ayın sürümleri, geri çekilen sürüm, farklı para birimi, eksik/sıfır/negatif karşılaştırma bazı ve toplam ciro/toplam reklam gideri hesabı test edildi. Yeni migration yok; modelde bekleyen değişiklik yok.
- Gerçek PostgreSQL'e bağlı API okumasında iki paylaşımdan yalnız son sürümün **2.500,1234** cirosu alındı; canlı dönemin 1.000 değeri kullanılmadı. İki paylaşım geri çekilince toplam boş döndü. Bütün doğrulama kayıtları tek işlem içinde tutulup geri alındı; geride kayıt kalmadı. İzole tarayıcıda 3/6/12 ay, bitiş ayı değişimi, **220.000,1234 TL** iki aylık toplam, temmuz sürüm 2'ye kaynak bağlantısı, eksik ağustos, **5.000 USD** ayrı toplam ve boş aralık doğrulandı. 390×844 görünümde yatay taşma ve son kontrol konsolunda hata yok. Güncel rehberin API/HTML/RSC erişim kontrolleri tüm roller için geçti.
- **Faz 5 tamamlandı — 23 Eylül:** Önceden kodlanmış devam yazışmaları, sorumlu/durum takibi, hesap ve rapor sürümüne özel görüntüleme/“İnceledim” kaydı, ekipçe yönetilen bilgi/belge talepleri ve yeni rapor sürümü uyarısının kalan doğrulamaları kapatıldı. Eski soru/yanıt metinleri korunur; yeni mesaj eklenir. Müşteri yalnız kendi konuşmalarını görür; talepler markanın bütün müşteri hesaplarına açıktır. İç sorumlu ve teslim gerekçesi müşteriye verilmez. Otomatik e-posta, müşteri dosya yükleme, finansal onay veya otomatik rapor paylaşımı eklenmedi. Rehber bütün akışları ve günlük kontrol sırasını anlatıyor.
- `e057d26` kaynak sürümünde **102 domain + 125 API testi**, **7 web testi**, backend build, web typecheck/lint/üretim build'i başarılı. Eski sürümle yazma, marka/hesap sınırı, yanlış sorumlu, geri çekilmiş rapora mesaj/inceleme, teslim durumunun gerekçesi, eski yanıtın değişmezliği, kısmi ödeme/iptal ve yalnız müşteriye açık değerlerin karşılaştırılması test edildi. Finansal tutar ve rapor sürümü kendiliğinden değişmedi.
- `20260919041908_PortalCollaboration` yapılandırılmış Supabase'de zaten uygulanmış bulundu; bu turda migration uygulanmadı veya eski migration değiştirilmedi. Bekleyen migration/model değişikliği yok. Gerçek bağlantıda üç yeni tablonun sahibi `postgres`, uygulama hesabı `ovo_growth_app` olduğu için eksik tablo izni saptandı. Yalnız mesajlar için okuma/ekleme, talepler ve incelemeler için okuma/ekleme/güncelleme izinleri tamamlandı. Silme veya genel erişim açılmadı; ayrıntı ve doğrulama sorgusu `docs/DATABASE.md` içinde.
- İzin düzeltmesinden sonra gerçek PostgreSQL'de eski konuşma/talep sürümüyle yazma, yinelenen mesaj sırası, boş mesaj, yanlış markaya inceleme ve yinelenen hesap/sürüm kaydı engellendi. Gerçek API okumalarında eski yanıt ve yeni mesaj birlikte görüldü; iç sorumlu gizli kaldı; teslim ve inceleme kayıtları okundu. **10,0001** kısmi ödeme güncellik uyarısını açtı; **100,1234** hakediş ve rapor sayısı değişmedi. `/health` ve `growth` şemasının `anon`/`authenticated` rollerine kapalı olduğu kontrol edildi. Deneme verilerinin tümü aynı işlemin geri alınmasıyla kaldırıldı ve kalmadıkları doğrulandı. Bu kontrol gerçek API okumaları ve PostgreSQL yazma kısıtlarını kapsar; API yazma işleyicileri bellek içi entegrasyon testlerinde doğrulandı.
- İzole yerel tarayıcıda müşteri devam mesajı, eski yanıtın korunması, ekip yanıtı, Partner sorumlu ataması, çözüldü durumu, görüntüleme/“İnceledim” kaydı, yeni sürüm uyarısı, talep oluşturma ve teslim alma doğrulandı. İkinci sekmede konu değişince eski gönderim engellendi; taslak metni korundu ve güncel sürüm alınınca gönderilebildi. Müşteride iç gerekçe/sorumlu görünmedi. 390×844 mobil görünümde yatay taşma ve konsol hatası görülmedi. Güncel rehber ekranda açıldı; girişsiz/geçersiz rehber API isteği 401, müşteri 403, üç ekip rolü 200 verdi. HTML/RSC yanıtında rehber metni yok; izinli API yanıtı kökteki MD ile birebir aynı ve `private, no-store`.
- API ve web aynı tamamlanmış sürümden yayımlanmalı; eski API yeni yazışma geçmişini tanımadığı için eski/yeni yazıcıları birlikte kullanmak güvenli sayılmaz. Geri dönüşte yeni tablolar ve geçmiş korunur; veri silen `Down` ayrıca açık izin olmadan uygulanmaz. Faz 6'ya geçilmedi. V2'de ertelenen yayın/yedek ve canlı entegrasyon işleri kapsam dışında; e-posta sağlayıcısı/bütçe ayrıca onay gerektirir. Bu turda commit, push veya canlı uygulama yayını yapılmadı; yalnız belirtilen Supabase izin düzeltmesi uygulandı.

### Faz 6 ilerlemesi — 23–24 Eylül 2026

- Kullanıcı Gmail SMTP kullanılmasını, gerçek bilgileri daha sonra kendisinin girmesini istedi. Bu nedenle ilk alt adım hesap daveti ve şifre yenileme olarak uygulandı; aşağıdaki ilk plandaki panel bildirimi önceliği bu istek doğrultusunda değişti. Örnek parola kullanılmadı veya depoya yazılmadı. Gerçek alıcıya e-posta gönderilmedi; varsayılan gönderim kapalı.
- Yönetici ekip çalışanını veya tek markaya bağlı müşteri hesabını süreli, tek kullanımlık bağlantıyla davet edebilir. Davet bekleyen hesap giriş yapamaz. Giriş ekranından yeni bağlantı istenebilir; yeni şifre bütün eski oturumları kapatır. Rol/marka bağlantısı değişmez. Son etkin yönetici koruması davet bekleyen yöneticiyi saymaz.
- Gmail 465/TLS ve 587/zorunlu STARTTLS desteği, kalıcı şifreleme anahtarı volume'u, şifrelenmiş gönderim kuyruğu ve son 100 gönderimin yönetici ekranı eklendi. İleti yalnız ilgili hesap adresine gider; `MAIL_TO` kullanılmaz. Belirsiz SMTP sonucu otomatik tekrar gönderilmez; yeni davet eski bağlantıyı geçersiz kılar. Sunucu kabulü gelen kutusu teslimi olarak sunulmaz.
- **102 domain + 141 API testi** (24 Eylül tekrar çalıştırması dahil), **7 web birim testi**, backend build, web typecheck/lint/üretim build'i başarılı. Devre dışı hizmet, alıcı izolasyonu, şifrelenmiş içerik, tek kullanım, süre/hesap/oturum değişiminde ret, eski oturumun kapanması, hız sınırı, gönderim belirsizliği, yeniden davet ve rol sınırları test edildi.
- `20260923163849_AccountMail` migration'ı 23 Eylül'de testlerden sonra yapılandırılmış Supabase'e uygulandı. Yalnız hesap bekleme bayrağı, `growth.AccountLinks`, `growth.MailDeliveries` ve ilgili indeks/kısıtlar eklendi. Bekleyen migration/model değişikliği yok. Gerçek PostgreSQL'de eski bağlantı/gönderim sürümüyle yazma ve aynı bağlantıya ikinci gönderim engellendi; API sağlık/durum/gönderim okumaları ve hassas alanların yanıtta bulunmaması doğrulandı. Deneme kayıtları işlem geri alınarak kaldırıldı; mevcut kullanıcı verisi temizlenmedi.
- İzole yerel tarayıcıda giriş, davet formu, şifre bağlantısı isteğinin genel yanıtı, bağlantının adres çubuğundan kaldırılması, şifre belirleme formu ve güncel rehber doğrulandı. Tarayıcının onay penceresi denemede gecikmeye yol açtı; ardından gönderim ekranında hem yerel davet hem şifre yenileme için sahte göndericinin kabul sonucu görüldü. Bu SMTP teslim kanıtı değildir. 390×844 şifre belirleme/gönderim ekranlarında yatay taşma yok. Şifreyi tarayıcıda değiştirme yapılmadı; tamamlanma ve yeni şifreyle giriş API testleriyle doğrulandı.
- Kurulum, etkinleştirme, Gmail uygulama şifresi ve gerçek alıcı pilotu adımları `docs/SMTP_KURULUMU.md` içinde; kullanıcı akışı ana rehberde güncel. Commit, push veya canlı uygulama yayını yapılmadı. Geri dönüşte yeni tablolar, davet bayrağı ve anahtar deposu korunmalı; eski/yeni hesap yazıcıları birlikte çalıştırılmamalı. Gönderimi durdurmak için `MAIL_ENABLED=false` yeterli.
- **Bu ilk alt adım sonunda kalanlar:** Bildirim ve iki aşamalı giriş geliştirmeleri aşağıdaki kapanışta tamamlandı. Gerçek Gmail bilgileriyle kullanıcı kontrollü teslim pilotu bekliyor.

### Faz 6 kodlama kapanışı — 24 Eylül 2026

- İç ekip menüsüne ve müşteri portalına **Bildirimler** eklendi. Kişiye özel okundu takibi, günlük görev özeti, yaklaşan/gecikmiş görev, müşteri sorusu/yanıtı ve yayımlanan rapor bildirimleri hazır. Müşteri yalnız kendi konuşmalarını ve markasına açık raporları görür; iç ekip görev tercihleri müşteriye gösterilmez.
- E-posta tercihleri başlangıçta kapalıdır. Eski olaylar tercih açılınca topluca gönderilmez. Gönderim öncesinde hesap, adres/sürüm, tercih, görev sorumlusu ve rapor erişimi tekrar kontrol edilir. Tamamlanmış/başkasına atanmış görev veya geri çekilen rapor için bekleyen gönderim iptal edilir. E-posta özel finansal değer, mesaj metni veya dosya eki içermez. Belirsiz sonuç otomatik tekrar denenmez.
- Yönetici kendi hesabına doğrulama uygulamasıyla ikinci aşama ekleyebilir; zorunlu değildir. Kurulumda on tek kullanımlık kurtarma kodu gösterilir. İkinci aşama tamamlanmadan oturum verilmez; yeniden kullanılan kod, eski/süresi dolmuş giriş isteği ve beş başarısız deneme denetlenir. Şifre yenileme ikinci aşamayı atlamaz. Kurulum, kapatma ve kod yenileme için mevcut şifre ve ilgili doğrulamalar gerekir; diğer oturumlar kapatılır. Telefon/son yönetici kaybı süreci `docs/HESAP_GUVENLIGI.md` içinde açıklanır.
- Başarılı doğrulama: **102 domain + 161 API = 263 backend testi**, **7 web birim testi**, backend build, web typecheck/lint/üretim build'i ve fark biçim kontrolü. Alıcı/marka izolasyonu, tek olay/tek bildirim, eski sürümle yazma, gönderim belirsizliği, kod/istek tek kullanımı, hesap kilidi, kurtarma yenileme ve gerçek şifre yenileme işleyicisinden sonra ikinci aşamanın korunması test edildi.
- `20260923220416_UserNotifications` ve `20260924152241_AccountSecurity` migration'ları testlerden sonra 24 Eylül'de yapılandırılmış Supabase'e uygulandı. Yalnız `growth.NotificationPreferences`, `growth.UserNotifications`, `growth.AccountSecurities` ve ilgili kısıt/indeksler eklendi; mevcut hesaplarda ikinci aşama açılmadı. Bekleyen migration/model değişikliği yok.
- Gerçek PostgreSQL'de yinelenen alıcı/olay engeli, eski bildirim sürümüyle ikinci gönderim talebinin ve eski güvenlik sürümüyle kurtarma kodu tüketiminin reddi doğrulandı. Gerçek API sağlık/bildirim/güvenlik okumalarında izinli alanlar görüldü; `growth` şeması genel rollere kapalı kaldı. Deneme kayıtları tek işlem geri alınarak kaldırıldı ve kalmadıkları kontrol edildi. API yazma senaryoları bellek içi entegrasyon testlerinde doğrulandı; bunlar canlı eşzamanlı HTTP yük testi değildir.
- İzole tarayıcıda bildirim/tercih kaydı ve başarı mesajı, okundu sayacı, boş durum, müşteriye özel iki tercih, önceden hazırlanmış deneme hesabında ikinci giriş adımı ve tek kullanımlık kurtarma koduyla giriş doğrulandı. Güvenlik durum ekranında tüketilmiş kodun sayısı görüldü. Gerçek hesapta kurulum/kapama yapılmadı; bu işlemler API testleriyle sınandı. 390×844 bildirim görünümünde yatay taşma yok; rehberin yeni bölümleri açıldı, konsol hatası görülmedi. Konuşma ekranındaki eski “E-posta gönderilmez” metni tercihli gönderim davranışına göre düzeltildi.
- **Kullanıcıya bağlı kalan:** Gerçek Gmail gönderici bilgilerini girip kontrollü alıcıyla teslim pilotu yapmak, doğrulanmış API/web sürümünü manuel yayımlamak ve isteyen yöneticinin kendi telefonuyla korumayı açması. Bu doğrulamalarda gerçek e-posta gönderilmedi; ajan commit/push veya canlı yayın yapmadı. SMTP kapalıyken de kalıcı anahtar volume'u korunmalı; ikinci aşamayı tanımayan eski API'ye dönüş güvenli kabul edilmez. V2'de ertelenen yedek/yayın ve canlı entegrasyon işleri yeniden kapsama alınmadı.

## 2. Mevcut durumdan çıkan ihtiyaçlar

Aşağıdaki ihtiyaç tablosu ilk analiz anını gösterir; güncel uygulama durumu yukarıdaki ilerleme kaydındadır.

| Mevcut yetenek | Eksik kalan kullanıcı sonucu | Öneri |
| --- | --- | --- |
| Aylık sonuç oluşturma ve API üzerinden güncelleme var | Detay ekranında tam veri düzenleme akışı yok; kontrol sırasında düzeltme için geri gönderme yolu belirgin değil | Faz 1: güvenli düzenleme ve geri gönderme |
| İki kişili onay ve dönem kilidi var | Bazı işlemler basit tarayıcı pencereleriyle yapılıyor; rol, işlem sürüyor ve eski ekranla kayıt gönderme davranışları bütün akışlarda aynı değil | Faz 1: tutarlı işlem güvenliği |
| Gerçekleşen sonuç ve geçmiş ay karşılaştırması var | Marka/ay için kaydedilmiş işletme hedefi ve hedef-gerçekleşen fark takibi yok | Faz 2: hedef ve bütçe |
| Kişiye atanmış işler ve gerçekleşen ekip saat maliyeti var | Gelecek ayın kapasitesi ve tekrarlayan iş planı yok; gerçekleşen saat kaydı çalışan bazlı zaman çizelgesi değil | Faz 3: kapasite ve iş şablonları |
| Kısmi tahsilat ve gecikme hesabı var | Ödeme sözü, gelecek haftalarda beklenen giriş ve alacak yaşlandırma özeti yok | Faz 4: tahsilat planı |
| Markaya rapor/belge paylaşma ve tek soruya tek yanıt var | Ekip için okunmamış/bekleyen takip düzeni, sürdürülebilir yazışma ve güvenli çok aylı müşteri görünümü sınırlı | Faz 5: müşteri iş birliği |
| Panel içi hatırlatmalar ve yönetici eliyle hesap açma var | Paneli açmayan kişiye kontrollü bildirim, davet ve kendi şifresini yenileme akışı yok | Faz 6: bildirim ve hesap kolaylığı |

### Özelliklerden ayrı tutulması gereken kontrol notları

1. **İç rehberin erişimi:** Arayüz müşteri rolünden rehberi saklıyor; ancak rehber dosyası sunucu bileşeninde okunuyor ve arayüzdeki oturum kontrolü içeriğin ağ yanıtına hiç girmediğini tek başına kanıtlamıyor. Rehberin iç ekip içeriği kalması isteniyorsa girişsiz ve müşteri oturumlu HTML/RSC yanıtlarında içerik bulunmadığı ayrıca test edilmeli. Bu, müşteri finansal API'lerinin açık olduğu iddiası değildir. Faz 1'e dar kapsamlı bir kontrol/düzeltme olarak önerilir. Sunucuda yetki kontrolünün önemi [Next.js kimlik doğrulama rehberinde](https://nextjs.org/docs/app/guides/authentication) ve [OWASP yetkilendirme rehberinde](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) de vurgulanır.
2. **Yalnız rehber değişikliğiyle imaj hazırlama:** Mevcut iş akışının dosya filtresinde kökteki kullanım rehberi yok. Bu yüzden yalnız bu dosyanın değiştiği bir gönderim imaj iş akışını tetiklemeyebilir; elle iş akışı çalıştırma seçeneği mevcut. Değişikliği gelecekte otomatik hazırlama kapsamına almak, otomatik canlı yayın açmak değildir. [GitHub dosya filtresi belgesi](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax) bu ayrımı açıklar. Bu iş, ertelenmiş yayın başlığından ayrıca onayla seçilmeli.
3. **API ve panel sürüm eşleşmesi:** Her iki imajda commit etiketi var; buna rağmen `latest` etiketleri ayrı işlerde yayımlanıyor. Bir iş başarısız olduğunda uyumsuz çift riski sürüyor. Aynı başarılı sürüm çiftini seçme ve manuel yayından önce kontrol etme, ertelenen yayın işinin parçasıdır; burada uygulanmadı.
4. **Örnek ve gerçek veri ayrımı:** Başlangıç kodu örnek portföy hazırlama yolunu çağırmaya devam ediyor. Canlıda örnek eklenmesini açık tercihe bağlama, güvenli ayrı deneme ortamı ve geri dönüş hazırlığı yeniden onay gerektirir. Mevcut kayıtları otomatik silmek veya adından örnek olduğuna karar vermek önerilmez.
5. **Operasyon sağlığı:** Mevcut `/health` yanıtı sürecin cevap verdiğini gösterir; veritabanı erişiminin sağlıklı olduğunu tek başına kanıtlamaz. Gerekiyorsa ayrı hazır olma kontrolü ve yayın sonrası giriş/okuma kontrolü, yayın iş paketiyle ele alınmalı.

## 3. Önerilen faz sırası

| Faz | Kullanıcıya sonucu | Öncelik | Yaklaşık büyüklük | Bağımlılık |
| --- | --- | --- | --- | --- |
| 1 | Günlük işlemleri hatasız tamamlayabilmek | Çok yüksek | Orta | Mevcut sistem |
| 2 | Hedefin gerisinde kalınca ne yapılacağını görmek | Yüksek | Orta | Faz 1 |
| 3 | Yeni marka almadan ekip yükünü görmek | Yüksek | Orta–büyük | Faz 1; mevcut görev ve maliyet modülleri |
| 4 | Ne kadar para, ne zaman gelebilir sorusunu cevaplamak | Yüksek | Orta | Faz 1; mevcut tahsilat modülü |
| 5 | Müşteriyle rapor üzerinden düzenli çalışmak | Orta–yüksek | Orta–büyük | Faz 1; hedef paylaşılacaksa Faz 2 |
| 6 | Önemli işi kaçırmamak, hesap işlemlerini kolaylaştırmak | Orta | Orta–büyük | Faz 5; onaylı e-posta hizmeti ve alan adı ayarları |

Büyüklükler süre veya fiyat taahhüdü değildir. Varsayılan öneri **1 → 2 → 3 → 4 → 5 → 6**; bir fazın zorunlu ölçütleri tamamlanmadan sonraki faza geçilmez. Tahsilat günlük en büyük sorun ise 3 ve 4'ün sırası onayla değiştirilebilir.

## 4. Faz 1 — Günlük kullanım ve güvenli düzeltme

**Amaç:** Çalışan hata yaptığında kaydı çoğaltmadan, geçmişi bozmadan ve teknik destek istemeden doğru yoldan düzeltebilsin.

Kapsam:

- Aylık sonuç detayından izin verilen hazırlık durumlarında tam veri düzenleme; yeni kayıt formunun uygun kısımlarını yeniden kullanma.
- Kontrol eden kişinin gerekçe yazarak hazırlayana geri göndermesi; değişiklikten sonra yeniden inceleme ve farklı kişi onayı.
- Kilit, fatura ve ödeme kurallarını koruma. Faturalanmış/ödenmiş aylar bu yolla açılmayacak.
- Düzenlenen verinin önceki onayını geçerli bırakmama; eski ekrandan gelen kayıtla başkasının değişikliğini sessizce ezmeme.
- Tutar ve gerekçe girişlerinde anlaşılır form; kaydetmeden önce işlem özeti, kaydederken tekrar basmayı engelleme ve açık sonuç mesajı.
- Yetkisiz düğmeleri rol ve durumla uyumlu gösterme; API yetki kontrolünü koruma.
- Kritik formlarda kaydedilmemiş değişiklik uyarısı ve ilgili rehber bölümüne yardım bağlantısı.
- İç rehber için girişsiz/müşteri ağ yanıtı kontrolü; iç ekip içeriği olarak kalacaksa sunucuda erişim sınırı.

**Örnek:** Çalışan iade tutarını yanlış yazmış. Kontrol eden kişi “İade raporuyla tutar uyuşmuyor” gerekçesiyle geri gönderir; çalışan düzeltir, tekrar gönderir ve ikinci kişi yeniden onaylar.

**Tamamlanma ölçütleri:** Tam düzeltme–geri gönderme–yeniden onay–kilit akışı çalışır. Hazırlayan kendi kaydını onaylayamaz. Önceden onaylanmış sayı değiştiğinde eski onay geçersiz olur. Aynı kayda iki kişinin değişikliği test edilir. Faturalı/ödenmiş kayıt ve yayımlanmış müşteri sürümü değişmez. Yetkisiz kullanıcı korunan rehber metnini HTML/RSC yanıtından da alamaz. Mobilde ana form akışı tamamlanır.

**Sınır:** Otomatik finansal onay, tüm ekranları yeniden tasarlama, muhasebe düzeltmesi veya eski veriyi silme yok.

**Başlangıç kararı:** Hangi hazırlık durumunda doğrudan düzenleme yapılacağı netleştirilir. Önerim: taslak düzenlenebilir; incelemedeki kayıt gerekçeyle geri gelir; onaylı ama kilitlenmemiş kayıt ancak onay iptal edilerek yeniden incelemeye hazırlanır.

## 5. Faz 2 — Marka hedefleri, bütçe ve takip aksiyonları

**Amaç:** “Geçen ay ne oldu?” yanında “Bu ay ne hedefledik, nerede kaldık, sıradaki iş ne?” sorusunu cevaplamak.

Kapsam:

- Marka, ay ve para birimi bazında net ciro hedefi, reklam bütçesi ve marka katkı marjı hedefi.
- Hedefin sorumlusu, belirlenme tarihi ve gerekçeli değişiklik geçmişi. Geçmiş hedefler başarı görüntüsü yaratmak için sessizce değiştirilemez.
- Hedef ve aynı kapsamlı gerçekleşen rakamın yan yana gösterimi; tutar farkı, uygun olduğunda yüzde farkı, oranlarda yüzde puan farkı.
- Mevcut açıklamalı raporlarla bağlantı; anlaşma simülatöründen ayrı işletme hedefi olarak gösterme.
- Hedef dışı sonuçtan mevcut görev sisteminde sorumlu ve son tarih seçerek takip işi açma; aynı işin tekrar üretilmesini önleme.

**Örnek:** “Ağustos net ciro hedefimiz 1 milyon TL idi; kapanmış sonuç 850 bin TL. Aradaki fark 150 bin TL. İade kaynaklarını inceleme işi Ayşe'de, son gün cuma.” Bu yalnız öğretici örnektir; gerçek marka sonucu değildir.

**Tamamlanma ölçütleri:** Hedefi olmayan ay sıfır hedef sayılmaz. Sıfır hedefe bölünmez; negatif/uygunsuz bazda yanıltıcı oran verilmez. Para birimleri karışmaz. Taslak gerçekleşen sonuç kesinleşmiş gibi sunulmaz. Hedef değişikliği ve açılan iş izlenebilir. Hakediş hesabı değişmez.

**Sınır:** Otomatik reklam bütçesi değiştirme, garanti gelir, kaynak olmadan neden-sonuç iddiası ve yapay zekâ tahmini yok.

## 6. Faz 3 — İş şablonları ve ekip kapasitesi

**Amaç:** Aylık rutinleri yeniden yazmayı azaltmak ve yeni markanın ekibe sığıp sığmayacağını görmek.

Kapsam:

- Önce iki küçük şablon: yeni marka başlangıcı ve aylık kapanış. Genel amaçlı süreç tasarım aracı kurulmaz.
- Şablondan görevleri ön izleyip sorumlularını/tarihlerini kontrol ederek oluşturma; başlangıçta kullanıcı onaylı üretim.
- Aylık kapanış ve yenileme görevlerinin mevcut tekillik kurallarını koruma.
- Çalışan/hafta için elle tanımlanan kullanılabilir saat; görev veya marka işi için planlanan saat.
- İzin ve başka işler nedeniyle kullanılamayan zamanı kapasiteden ayırma; özel izin nedeni veya sağlık bilgisi toplamama.
- Ekip yükü, boş kapasite ve aşım uyarısı. “Bu markayı alırsak hangi ekipte yoğunluk olur?” ön izlemesi.
- Gerçekleşen saatle karşılaştırma ancak çalışan/görev ilişkisi doğrulanmış kayıtlarda yapılır. Mevcut toplam hizmet saatleri kişilere tahmini dağıtılmaz.

**Tamamlanma ölçütleri:** Şablon tekrar çalıştırılınca aynı hedef dönemin işleri çoğalmaz. Kapalı hesaplara yeni iş atanmaz. Eksik kapasite sıfır kapasite olarak yorumlanmaz. Kapasite hesabı bordro veya gider defterine otomatik maliyet yazmaz. Hassas saat maliyetleri yetkisiz role görünmez.

**Sınır:** Çalışan gözetimi, ekran/klavye takibi, otomatik performans puanı ve otomatik işe alım/işten çıkarma önerisi yok.

## 7. Faz 4 — Tahsilat takvimi ve beklenen para girişi

**Amaç:** Bugünkü alacağı görmekten, önümüzdeki haftaların tahsilat işini planlamaya geçmek.

Kapsam:

- Vadesi gelmemiş, 1–30, 31–60, 61–90 ve 90 günden fazla gecikmiş kalan alacak grupları; vadesi bilinmeyenler ayrı.
- Markanın bildirdiği ödeme sözü: tarih, tutar, kaynak görüşme notu ve takip sorumlusu. Vade ile ödeme sözü ayrı kalır.
- Önümüzdeki 4/8/12 haftanın beklenen tahsilatı; vade planı ile bildirilen ödeme sözünü aynı tutarı iki kez saymadan ayrı görünümde sunma.
- Beklenen, gerçekleşen ve geciken tutarı ayırma; para birimi bazında görünüm.
- Takip görevleriyle bağlantı ve geçmiş görüşmenin görünmesi.

**Tamamlanma ölçütleri:** Ödeme sözü tahsilat yaratmaz veya dönemi ödendi yapmaz. Kısmi ödeme beklentide kalan tutarı azaltır. İptal edilen ödeme doğru alacağı geri getirir. Tarihsiz eski ödemelere tarih uydurulmaz. Para birimleri toplanmaz.

**Sınır:** Bu bir banka bakiyesi veya tam nakit akış tablosu değildir; şirketin tüm çıkışları bilinmiyor. Banka entegrasyonu, para gönderme, KDV hesabı, fazla ödeme ve mahsup ayrı kapsamdır.

## 8. Faz 5 — Müşteriyle çalışma alanı

**Amaç:** Portalı rapor indirilen yerden, kontrollü aylık görüşmenin yürütüldüğü alana geliştirmek.

Kapsam:

- Yalnız açıkça yayımlanmış ay ve sürümlerden 3/6/12 aylık müşteri görünümü. Aynı aya ait iki sürüm aynı toplamda iki kez sayılmaz; kullanılan sürümler görünür.
- Soru/yanıtların devam edebildiği konu dizileri; açık, yanıt bekliyor ve çözüldü durumları.
- Ekipte sorumlu ve son yanıt tarihi; gönderilmiş mesajı sessizce değiştirmek yerine açıklama/düzeltme mesajı ekleme.
- “İnceledim” kaydı ve görüntüleme bilgisi; bunun ticari kabul veya dönem onayı olmadığı açıkça yazılır.
- OVO'nun istediği veri/belge için müşteri kontrol listesi. İlk küçük sürümde talep durumunu ekip günceller; müşteri dosya yüklemesi otomatik dahil değildir.
- Paylaşılmış eski rapordan sonra yeni tahsilat olması gibi durumlarda ekibe “Yeni sürüm gerekebilir” uyarısı; otomatik yeniden yayımlama yapılmaz.

**Tamamlanma ölçütleri:** Başka marka verisi, paylaşılmamış ay, iç maliyet ve özel not hiçbir çıktıdan sızmaz. Sorular varsayılan olarak hesap sahibine özel kalır. Geri çekilen rapor yeni karşılaştırmalardan çıkarılır; daha önce indirilmiş kopyaların silinemediği açıklanır. İnceledim işlemi hakedişi, tahsilatı veya ticari onayı değiştirmez.

**Sınır:** E-imza, sözleşme kabulü, müşteriye finansal veri düzenleme yetkisi ve varsayılan herkese açık bağlantı yok. Müşteri yüklemesi istenirse boyut/içerik kontrolü, güvenli indirme, zararlı dosya kontrolü ve depolama ihtiyacı ayrıca kapsamlanır.

## 9. Faz 6 — Kontrollü bildirimler ve hesap kolaylığı

**Amaç:** İnsanlar önemli işi kaçırmasın; yönetici her yeni kişi için şifre taşımak zorunda kalmasın.

Kapsam:

- Önce panelde okunmamış bildirimler ve kişisel tercihler; ardından seçilen tek e-posta sağlayıcısıyla pilot.
- Günlük görev özeti, yaklaşan teslim, müşteri sorusu/yanıtı ve yeni paylaşılan rapor bildirimi.
- Marka, alıcı ve bildirim türünü açıkça kontrol etme; yeniden denemede aynı olayın çok sayıda ileti üretmesini engelleme.
- Süreli, tek kullanımlık hesap daveti ve parola yenileme; düz metin şifre gönderilmemesi.
- Yönetici hesaplarında iki aşamalı girişin ayrı alt adımı; kurtarma ve son yöneticiye erişim senaryosu tasarlanmadan zorunlu hale getirmeme.

**Tamamlanma ölçütleri:** Kapalı hesaba yeni özel içerik bildirimi gitmez. E-postada özel finansal ek yerine giriş gerektiren bağlantı tercih edilir. Soru bildirimi aynı markadaki ilgisiz kişiye gitmez. Davet/yenileme bağlantısı süresi bitince veya kullanıldıktan sonra çalışmaz. Gönderilemeyen ileti görünür; finansal işlem başarısı e-posta gönderimine bağlı değildir.

**Bağımlılık:** Kullanıcı Gmail SMTP'yi seçti; gerçek gönderici hesabı ve uygulama şifresini daha sonra girecek. Hesap ve bildirim e-postalarının kod/test hazırlığı ile alıcı/tercih kontrolleri tamamlandı; gerçek teslim pilotu henüz yapılmadı. Yeni ücretli sağlayıcı veya ek harcama ayrıca onay gerektirir.

**Sınır:** WhatsApp, SMS, pazarlama kampanyaları ve otomatik müşteri raporu yayımlama ilk kapsamda yok.

## 10. Bekleme listesi — Ana fazlara otomatik dahil değil

- **Canlı mağaza/reklam entegrasyonu:** V2 Faz 8 devamı. Erişim sağlanınca tek marka ve tek kaynak pilotu; salt okunur, önce hazırlık ve kaynak tutarlarıyla uzlaştırma. Örnek veri gerçek bağlantı diye sunulmaz.
- **Yayın, örnek ortam ve geri dönüş güvenliği:** V2 Faz 2 devamı. Rehber dosya filtresi, eşleşen sürüm çifti, hazır olma kontrolü, açık örnek veri tercihi ve istenirse yedek/geri yükleme provası. Kullanıcının manuel canlı yayın tercihi korunur; yedekleme kendiliğinden yeniden kapsama alınmaz.
- **Yapay zekâlı rapor anlatımı:** Doğrulanmış ve yetkiye göre süzülmüş sonuçlardan taslak metin. Kaynak dönem/sürüm belirtilir; kullanıcı kontrolünden önce müşteriye gönderilmez. Hesap, onay, hedef veya eksik veri uydurmaz. Sağlayıcıya gönderilecek veri ve harcama limiti ayrıca onaylanır. Mevcut kural tabanlı açıklamalar çalışmaya devam eder.
- **Hizmet paketi ve yenileme analizi:** Marka bazında iş kapsamı, paket dışı işler ve yenilemede planlanan–gerçek emek karşılaştırması. Kapasite ve gider verileri yeterli olduğunda anlamlıdır; otomatik fiyat artışı veya mevcut anlaşmayı değiştirme yok.
- **Kayıp aday analizi:** Aday kaybının gerekçeleri, tekliften anlaşmaya dönüşüm ve aşamada bekleme süresi. Önce olay tarihleri/gerekçeleri toplanır; geçmiş kayıtların süresi tahmin edilmez.
- **Belge depolamasını büyütme:** Dosyalar bugün uygulama veritabanında tutuluyor. Gerçek hacim ve maliyet ölçülmeden başka depolamaya taşıma yapılmaz; gerekirse erişim sınırı ve geri dönüş planıyla ayrı iş olur.
- **Çok şirketli satılabilir SaaS:** Şu an OVO iç işletmesi ve markaya sınırlı portal modeli korunmalı. Başka ajanslara satış somut hedef olursa şirketler arası veri ayrımı, üyelik ve faturalama ayrı ürün kararıdır.

## 11. Başarıyı nasıl ölçeceğiz?

İlk gerçek kullanım haftasında başlangıç değerleri ölçülür; bugün bilinmeyen değerler veya zaman kazancı yüzdeleri uydurulmaz.

- Bir markanın aylık kapanışını hazırlamak ve ikinci kişiye onaylatmak kaç dakika/gün sürüyor?
- Kaç kayıt düzeltme için geri dönüyor; en sık hata hangi alan?
- Açık görevlerin kaçının sorumlusu ve gerçek son tarihi var?
- Aktif markaların kaçında o aya ait hedef ve tamamlanmış maliyet kontrolü var?
- Vadesi geçmiş alacakların kaçında takip sorumlusu ve sonraki adım belli?
- Müşteri sorusuna ilk yanıt süresi nedir?

Ölçüm sonuçları sonraki fazın önceliğini etkiler. Personeli otomatik puanlamak veya gelir garantisi vermek için kullanılmaz.

## 12. Uygulama ve onay kuralları

1. Kullanıcı fazı veya tüm sıralı planı açıkça onaylar. Önceki planın onayı bu yeni plana otomatik taşınmaz.
2. Her faz başında açık iş kararları ve test örnekleri netleştirilir. Özellikle rol, onay, tahmin ve dış paylaşım sınırları yazılır.
3. Domain, API, veri modeli, arayüz ve rehber etkisi yalnız gerekli ölçüde uygulanır; yeni servis veya yeni altyapı varsayılan çözüm değildir.
4. Mevcut finansal hesaplar ve kapalı kayıtlar korunur. Para hesabı domain katmanında `decimal` ile yapılır. Şema değişirse eklemeli/geçmişle uyumlu migration ve geri dönüş etkisi değerlendirilir.
5. İlgili birim/API testleri, PostgreSQL'e özel senaryolar gerekiyorsa ayrı doğrulama, web typecheck/lint/build ve gerçek kullanıcı akışı kontrolü tamamlanır. Test verisi gerçek veriyle karışmaz.
6. Kullanım rehberine ne işe yaradığı, nereden açıldığı, işlem sırası, örnek ve hata halinde yapılacaklar eklenir; rehber sayfası da doğrulanır.
7. Fazın sonuçları ve eksikleri sunulur. Zorunlu ölçüt başarısızsa sonraki faza geçilmez. Ertelenen alt iş açıkça ertelenmiş kalır.
8. Commit/push, canlı yayın, ücretli hizmet, dışarı veri aktarımı, güvenlik yetkisi değişikliği ve veri silme için ilgili kullanıcı yetkisi ayrıca korunur.

**Benim başlangıç önerim:** Önce **Faz 1**, ardından **Faz 2**. Bu ikisi, mevcut güçlü hesap ve rapor altyapısını teknik olmayan ekibin günlük kararlarına en doğrudan bağlayan gelişmelerdir.

## 13. Kaynak kodu dayanakları

- Aylık güncelleme ve onay yolları: `apps/api/src/OvoGrowthOS.Api/Features/WorkflowEndpoints.cs`, özellikle `MapPerformance`, `SubmitPerformance`, `ApprovePerformance`, `LockPerformance`.
- Aylık kullanıcı ekranları: `apps/web/src/app/performance/new/page.tsx`, `apps/web/src/app/performance/[id]/page.tsx`.
- Görev kapsamı ve tarih yaklaşımı: `apps/api/src/OvoGrowthOS.Domain/TeamWork.cs`, `Features/WorkflowEndpoints.Team.cs`.
- Tahsilat ile gerçek ödemeyi ayıran mevcut kurallar: `apps/api/src/OvoGrowthOS.Domain/Collections.cs`.
- Gerçek saat/giderin mevcut kapsamı: `apps/api/src/OvoGrowthOS.Domain/OperatingCosts.cs`.
- Müşteri paylaşım sınırı ve mesaj modeli: `apps/api/src/OvoGrowthOS.Domain/CustomerPortal.cs`, `Features/WorkflowEndpoints.Portal.cs`, `apps/web/src/app/portal-management/page.tsx`.
- Oturum ve rehber: `apps/api/src/OvoGrowthOS.Api/Program.cs`, `apps/web/src/lib/api.ts`, `apps/web/src/components/app-shell.tsx`, `apps/web/src/app/guide/page.tsx`.
- Başlangıç/örnek kayıt akışı: `apps/api/src/OvoGrowthOS.Api/Data/SeedData.cs`.
- Yayın hazırlama: `.github/workflows/container-images.yml`.
- Önceki kararlar ve mevcut kullanıcı iş akışları: `docs/GELISIM_PLANI_V2.md`, `OVO_GROWTH_OS_KULLANIM_REHBERI.md`.

Bu dayanaklar kaynak kodu incelemesini gösterir; canlı ortamda açık bulunduğu veya yeni özelliklerin çalıştığı anlamına gelmez.
