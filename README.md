# 🏥 Hastane Randevu Sistemi

Bu proje, **C#** ve **SQL Server** kullanılarak geliştirilmiş basit bir *Hastane Randevu Sistemi* uygulamasıdır. Kullanıcılar (hastalar ve doktorlar) için temel randevu alma ve görüntüleme işlevlerini içerir. Kamu hastanelerinin çevrim içi randevu portallerinden esinlenmiştir.

## 📦 Kurulum

1. Projeyi bilgisayarınıza klonlayın veya indirin.
2. [.NET 10 SDK](https://dotnet.microsoft.com/download) ve SQL Server (Express yeterlidir) kurulu olmalıdır.
3. Bağlantı ayarı için `src/HastaneRandevu.UI/appsettings.example.json` dosyasını aynı klasöre `appsettings.json` adıyla kopyalayıp sunucu adını düzenleyin. Bunun yerine `dotnet user-secrets` ya da `HASTANE_CONNECTION` ortam değişkeni de kullanılabilir. `appsettings.json` git'e eklenmez.
4. Uygulamayı `dotnet run --project src/HastaneRandevu.UI` ile başlatın. Veritabanı ilk açılışta EF Core migration'ları ile oluşturulur; eski sürümün oluşturduğu bir veritabanı ise kayıtları korunarak yerinde yükseltilir. Yedek (`.bak`) dosyasına gerek yoktur.
5. Yeni bir veritabanında kurgusal deneme hesapları bulunur: hasta T.C. `10000000450` / şifre `Demo.Hasta1`, doktor `Dr. Ada Örnek` / şifre `Demo.Doktor1`.

## 🧑‍⚕️ Özellikler

### Hasta Paneli

- TC Kimlik No ve şifre ile giriş yapılır.
- Doktorlar ve uzmanlık alanları görüntülenebilir.
- Uygun tarihe randevu alınabilir.
- Alınan randevular listelenebilir veya iptal edilebilir.

### Doktor Paneli

- Doktor giriş yaptıktan sonra **kendi hastaları** ve **randevu tarihleri** listelenir.
- İlgili randevular istenirse iptal edilebilir.

## 🛠️ Kullanılan Teknolojiler

- C# (.NET Framework - Windows Forms)
- SQL Server
- ADO.NET
- WinForms UI

## 📌 Notlar

- Bu proje eğitim amaçlı hazırlanmıştır.
- Gerçek sistemlerdeki güvenlik ve yetkilendirme önlemleri burada basitleştirilmiştir.

## 📄 Lisans

Bu proje kişisel ve eğitimsel kullanım için ücretsizdir. Ticari amaçla kullanılması yasaktır.
