# AE - Masaüstü Asistanım

Bu proje, Windows masaüstünde çalışan, yerel Ollama modeli ile sohbet eden ve SQL Server üzerinde sohbet hafızası / kişisel bilgileri saklayan bir WPF asistan uygulamasıdır.

## Özellikler

- WPF tabanlı masaüstü arayüzü
- Yerel Ollama entegrasyonu
- qwen3:8b modeli ile sohbet
- Sohbet geçmişini SQL Server üzerinde saklama
- Kişisel hafıza yönetimi (ekle, düzenle, sil)
- Dengeli / Performans çalışma modu
- "AE" adında kişisel asistan profili
- Sohbet mesajlarını kopyalama desteği

## Proje yapısı

- `WpfApp1/` : Ana WPF uygulaması
- `WpfApp1.UnitTests/` : Test projesi
- `WpfApp1.slnx` : Çözüm dosyası

## Teknolojiler

- C# / WPF
- .NET 10 (Windows hedefi)
- SQL Server / SQL Server Express
- Ollama API (`http://localhost:11434`)
- `Microsoft.Data.SqlClient`

## Gereksinimler

Aşağıdakiler sisteminizde kurulu olmalıdır:

- Windows 10/11
- Visual Studio 2022 veya daha yeni
- .NET 10 SDK
- .NET desktop geliştirme iş yükü
- Ollama
- lokal SQL Server örneği (uygulama, varsayılan olarak `SQLEXPRESS05` sunucusunu kullanır)

## Ollama kurulumu

1. Ollama' yı indirin ve kurun: https://ollama.com/
2. Aşağıdaki komutu çalıştırarak modeli indirin:

```bash
ollama pull qwen3:8b
```

3. Ollama servisinin çalıştığından emin olun.

## Veritabanı kurulumu

Uygulama, aşağıdaki bağlantı dizesini kullanmaktadır:

```csharp
Server=.\SQLEXPRESS05;Database=MasaustuAsistanDB;Integrated Security=True;TrustServerCertificate=True;
```

Aşağıdakileri sağlamanız gerekir:

- SQL Server Express veya SQL Server kurulumu
- `SQLEXPRESS05` sunucusu erişilebilir olmalı
- `MasaustuAsistanDB` veritabanı oluşturulabilir olmalı
- uygulamanın çalıştığı kullanıcı, SQL Server'a erişim sağlamalıdır

Uygulama ilk çalıştırmada gerekli tabloları otomatik olarak oluşturur:

- `dbo.ChatMessages`
- `dbo.AssistantMemories`

## Çalıştırma

### Visual Studio ile

1. `WpfApp1.slnx` dosyasını açın.
2. Çözümü derleyin.
3. `WpfApp1` proje başlatıcısı ile uygulamayı çalıştırın.

### Komut satırı ile

```bash
dotnet restore

dotnet build WpfApp1.slnx
```

Ardından proje çalıştırılabilir uygulamayı başlatın.

## Kullanım

1. Uygulamayı başlatın.
2. Veritabanı bağlantısının hazır olduğundan emin olun.
3. Güç düğmesine basarak asistanı başlatın.
4. Ollama ve qwen3:8b modelinin hazır olmasını bekleyin.
5. Sohbet penceresine mesaj yazıp yanıt alın.
6. "Hafızayı Yönet" ile kalıcı hatırlanacak bilgileri ekleyin.

## Notlar

- Bu uygulama Windows üzerinde çalışacak şekilde tasarlanmıştır.
- WPF ve .NET 10 Windows hedefi nedeniyle Linux/macOS'ta doğrudan çalışmaz.
- Ollama çalışmıyorsa veya model kurulu değilse asistan başlatılamaz.
- Veritabanı bağlantısı kurulamıyorsa uygulama arayüzünde hata mesajı gösterir.

## Lisans

Bu proje için özel lisans bilgisi eklenmemiştir. Gerekirse repo sahibi tarafından lisans dosyası eklenebilir.

## Katkı

Katkı yapmak isteyenler için şu adımlar uygundur:

1. Depoyu fork edin.
2. Yeni bir branch oluşturun.
3. Değişikliklerinizi yapın.
4. Pull request açın.

## Sorun Giderme

### Ollama başlatılamıyor

- Ollama kurulumunun tamamlandığını kontrol edin.
- `ollama pull qwen3:8b` komutunun çalıştığını doğrulayın.
- `http://localhost:11434` adresine erişimi kontrol edin.

### Veritabanı bağlantısı başarısız

- SQL Server Express çalışıyor mu kontrol edin.
- `SQLEXPRESS05` sunucusu erişilebilir mi bakın.
- Kullanıcının SQL Server erişimi olduğundan emin olun.

### Derleme hatası

- .NET 10 SDK kurulu olduğundan emin olun.
- Visual Studio için .NET desktop geliştirme iş yükünü yükleyin.

---

Projeyi geliştiren ve yöneten kişi: repo sahibi / katkıda bulunanlar.
