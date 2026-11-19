# Değişiklik Günlüğü

🇬🇧 [English](CHANGELOG.md) · 🇹🇷 **Türkçe**

Bu projedeki önemli değişiklikler bu dosyada belgelenir.
Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) temel alınarak hazırlanmıştır.

## [Yayımlanmadı]

### Eklenenler

- `%LOCALAPPDATA%\HastaneRandevu\logs` altında Serilog ile günlük dönen dosya günlükleri (son 30 dosya saklanır).
  T.C. kimlik numaraları, adlar, şifreler, randevu ayrıntıları ve istisna mesajları hiçbir zaman günlüğe yazılmaz.
- Giriş, hesap kilidi, şifre yükseltme, kayıt, randevu alma, iptal ve reddedilen erişim için anonim denetim
  kayıtları: yalnızca sabit bir kod ve rol, örneğin `Audit auth.account.locked patient`.
- Kayıt, hasta ve doktor girişi, randevu alma ve iptal için merkezi FluentValidation doğrulayıcıları.
- Doğrulayıcı sınır testleri; kilidin bitişi, hash yükseltme, 90 günlük ufuk, iptal sınırı ve sahiplik için Moq
  testleri; aynı kurallar için gerçek repository'lerle SQLite testleri.
- Tam bir oturumu çalıştırıp günlük dosyasında kişisel veri arayan bir test.
- UI testlerinin değiştirdiği tek bir ileti kutusu yardımcısı; böylece hiçbir test pencere açmaz. UI testleri
  sırayla çalışır.
- CI'da sürüm işi: `v*` etiketlerinde win-x64 derlemesi yayımlanır ve zip çıktısı olarak yüklenir.
- Mimari diyagramlı İngilizce ve Türkçe README, tehdit modeli (`docs/THREAT_MODEL.md`) ve bu değişiklik günlüğü.

### Değişenler

- Servislerde elle yazılmış denetimler doğrulayıcılara taşındı; mesajlar, kırpma ve denetim sırası aynıdır
  (kayıtta şifre 8-64 karakter, girişte en fazla 100).
- Trace çıktısına yazılan hatalar artık günlük dosyasına yazılır.

### Düzeltilenler

- Reddedilen bir girişten sonra şifre alanı, kayıttan sonra ad alanı, form yeniden etkinleştiğinde odağı alır.

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
