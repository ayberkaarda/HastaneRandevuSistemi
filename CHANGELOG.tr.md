# Değişiklik Günlüğü

🇬🇧 [English](CHANGELOG.md) · 🇹🇷 **Türkçe**

Bu projedeki önemli değişiklikler bu dosyada belgelenir.
Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) temel alınarak hazırlanmıştır.

## [Yayımlanmadı]

### Eklenenler

- Girişten sonra yaklaşan randevu hatırlatmaları (pencere içinde ve bir kez odağı almayan bir balon olarak;
  `Appointments:ReminderLeadHours` ve `Appointments:ShowReminderBalloon`), hastanın geçmişi CSV olarak (UTF-8 BOM, `;`,
  formül enjeksiyonu önlenmiş) ve doktorun günlük ajandası yazdırılabilir HTML olarak (betik yok, CSP, tüm değerler
  kodlanmış). Dışa aktarımlar geçici dosya üzerinden yazılır, T.C. Kimlik No'yu maskeler ve içeriklerinden hiçbir şey
  günlüğe yazılmaz.
- Ada ve branşa göre doktor arama, doktor başına boş ve dolu günleri gösteren ay/hafta takvimi ve ilk boş randevu
  bulucu; hepsi iş katmanında hesaplanır (izin günü, dolu gün ve 90 günlük ufuk sınırlarında test edilir).
- Erteleme: hasta kendi randevusunu aynı doktorun boş bir dilimine tek bir koşullu güncellemeyle taşır (iptal edip
  yeniden almadan); başlangıca `Appointments:RescheduleCutoffHours` saat kalana kadar (varsayılan 2, açılışta
  doğrulanır). Yeni dilim için erteleme ve randevu alma yarışları SQL Server'da test edilir.
- Doktor çalışma programı: her doktor kendi çalışma günlerini, saatlerini ve izin günlerini belirler (`DoktorCalismaSaatleri`
  ve `DoktorIzinGunleri` tabloları, `AddDoctorSchedules` migration'ı). Boş dilimler ve randevular buna uyar; programı
  olmayan doktor hafta içi 09:00-17:00 kuralıyla çalışır. Programı yalnızca doktorun kendisi değiştirebilir; mevcut
  randevular korunur, izin gününe veya saatlerin dışına düşenler uyarı olarak listelenir.
- `%LOCALAPPDATA%\HastaneRandevu\logs` altında Serilog ile günlük dönen dosya günlükleri (30 günden eski
  dosyalar silinir, en fazla 30 dosya saklanır; testler bunu tarihli dosyalarla doğrular).
  T.C. kimlik numaraları, adlar, şifreler, randevu ayrıntıları ve istisna mesajları hiçbir zaman günlüğe yazılmaz.
- Giriş, şifre yükseltme, kayıt, randevu alma, iptal ve reddedilen erişim için anonim denetim kayıtları:
  yalnızca sabit bir kod ve rol, örneğin `Audit auth.sign_in.failed patient`.
- Kayıt, hasta ve doktor girişi, randevu alma ve iptal için merkezi FluentValidation doğrulayıcıları.
- Doğrulayıcı sınır testleri; kilidin bitişi, hash yükseltme, 90 günlük ufuk, iptal sınırı ve sahiplik için Moq
  testleri; aynı kurallar için gerçek repository'lerle SQLite testleri.
- Tam bir oturumu çalıştırıp günlük dosyasında kişisel veri arayan bir test.
- UI testlerinin değiştirdiği tek bir ileti kutusu yardımcısı; böylece hiçbir test pencere açmaz. UI testleri
  sırayla çalışır.
- CI'da sürüm işi: `v*` etiketlerinde win-x64 derlemesi yayımlanır ve zip çıktısı olarak yüklenir.
- Mimari diyagramlı İngilizce ve Türkçe README, tehdit modeli (`docs/THREAT_MODEL.md`) ve bu değişiklik günlüğü.

### Değişenler

- Yeni bir veritabanına artık şifresi yayımlanmış hesaplar eklenmez: migration'lar hiçbir satır eklemez; demo
  doktorları ve hastaları yalnızca demo modunda (`HASTANE_DEMO=1` veya `Seed:Demo=true`, varsayılan kapalı)
  oluşturulur. Doktor ekleme ekranı olmadığından doktor hesaplarını işletmeci ekler.
- Servislerde elle yazılmış denetimler doğrulayıcılara taşındı; mesajlar, kırpma ve denetim sırası aynıdır
  (kayıtta şifre 8-64 karakter, girişte en fazla 100).
- Trace çıktısına yazılan hatalar artık günlük dosyasına yazılır.

### Düzeltilenler

- Reddedilen bir girişten sonra şifre alanı, kayıttan sonra ad alanı, form yeniden etkinleştiğinde odağı alır.
- Kilitli bir hesaba yapılan giriş denemesi günlüğe ayrı bir kilit koduyla değil, diğer tüm başarısız girişler gibi
  `auth.sign_in.failed` olarak yazılır; böylece günlük dosyasını okuyabilen yerel kullanıcı hangi hesabın kilitli
  olduğunu öğrenemez.

### Kaldırılanlar

- Devlet logoları ve stok fotoğraflar içeren eski ekran görüntüleri.

## Katmanlı .NET 10 temeli - 2026-11-19 (#1)

### Eklenenler

- Katmanlı çözüm: `src/` altında `Domain`, `Business`, `Data` ve `UI` projeleri.
- PBKDF2 şifre hash'leri, 5 hatalı denemeden sonra kilit ve T.C. kimlik denetimiyle hasta ve doktor girişi.
- Sahipliği oturum açmış hesaba göre denetleyen randevu servisleri: hafta içi 09:00-17:00 arası 30 dakikalık
  dilimler, en fazla 90 gün sonrası, iptal yalnızca başlangıçtan önce.
- Varlık yapılandırmaları, migration'lar ve kurgusal örnek verilerle EF Core veri erişimi; migration'lar açılışta
  uygulanır.
- Eski uygulamanın oluşturduğu veritabanlarının, veriler korunarak yerinde yükseltilmesi; düz metin şifreler
  işaretlenir ve bir sonraki girişte hash'lenir.
- `appsettings.json`, user secrets ve `HASTANE_CONNECTION` ortam değişkeninden yapılandırma; gerçek ayarlar
  yerine `appsettings.example.json` depoya eklenir.
- SVG kaynaklarıyla özgün logo, arka plan ve uygulama simgesi.
- İş kuralları, SQLite üzerinde servisler, SQL Server'da yarış durumları, eski veritabanı yükseltmesi ve UI
  korumaları için test projeleri.
- Her push ve pull request'te derleyip test eden CI iş akışı.

### Değişenler

- Nullable başvuru türleriyle .NET 10 hedefleyen SDK tarzı projelere geçildi.
- Formlar SQL çalıştırmak yerine Business servislerini dependency injection ile çağırır.

### Düzeltilenler

- Yazma düğmelerine tekrar tıklamak artık işlemi iki kez başlatmaz.
- Yanıtlar sırasız geldiğinde yalnızca en son saat ve randevu listeleri gösterilir.
- Kilitli bir hesap, hatalı bilgilerle aynı yanıtı alır.

### Kaldırılanlar

- Depoda izlenen derleme çıktıları, IDE durumu, veritabanı yedeği, devlet logoları ve lisanslı stok görseller.
