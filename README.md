# AE - Masaüstü Asistanım

AE, Windows masaüstünde çalışan, yerel yapay zekâ modeliyle sohbet edebilen ve sohbet geçmişini ve kişisel hafıza kayıtlarını SQL Server üzerinde saklayan bir WPF masaüstü asistanı projesidir.

Projenin temel amacı, internet tabanlı yapay zekâ servislerine bağımlılığı azaltarak yerel ortamda çalışan, kişiselleştirilebilir ve geliştirilebilir bir asistan oluşturmaktır.

## Özellikler

- **WPF masaüstü arayüzü:** Windows için masaüstü uygulaması.
- **Yerel yapay zekâ:** Ollama üzerinden yerel dil modeli entegrasyonu.
- **Dil modeli:** Qwen3:8B.
- **Kalıcı sohbet geçmişi:** Mesajları SQL Server veritabanında saklama.
- **Kişisel hafıza yönetimi:** Hafıza kayıtlarını ekleme, düzenleme ve silme.
- **Çalışma modları:** Dengeli ve Performans modları.
- **Kişisel asistan profili:** AE isimli asistan profili.
- **Mesaj kopyalama:** Sohbet mesajlarını kopyalayabilme.
- **Birim testleri:** Uygulamanın test edilebilirliğini destekleyen ayrı bir test projesi.

> Not: Özelliklerin tamamının mevcut sürümde çalıştığı, ilgili kod ve testler üzerinden doğrulanmalıdır.

## Proje Yapısı

```text
AE-Masaustu-Asistani/
├── WpfApp1/
│   └── Ana WPF uygulaması
├── WpfApp1.UnitTests/
│   └── Birim testleri
├── LICENSE
├── README.md
└── WpfApp1.slnx
    └── Visual Studio çözüm dosyası
```

## Kullanılan Teknolojiler

- **Programlama dili:** C#
- **Arayüz teknolojisi:** WPF
- **Platform:** .NET 10 (Windows)
- **Veritabanı:** Microsoft SQL Server / SQL Server Express
- **Veritabanı bağlantısı:** Microsoft.Data.SqlClient
- **Yerel yapay zekâ altyapısı:** Ollama
- **Dil modeli:** Qwen3:8B

## Sistem Gereksinimleri

Projeyi çalıştırmak için aşağıdaki bileşenlere ihtiyaç vardır:

- Windows 10 veya Windows 11
- Projenin hedeflediği .NET sürümüyle uyumlu Visual Studio ve .NET SDK
- Visual Studio için .NET masaüstü geliştirme iş yükü
- Ollama
- SQL Server veya SQL Server Express
- Kullanılan SQL Server örneğine erişim yetkisi

**Önemli:** Projeyi derlemek için uygun .NET 10 SDK kurulu olmalıdır. Visual Studio sürümünüzün de hedef framework'ü desteklediğinden emin olun.

## Ollama Kurulumu

1. Ollama'yı resmî web sitesinden indirip kurun:

   https://ollama.com/

2. Terminal veya Komut İstemi'ni açın ve aşağıdaki komutu çalıştırın:

   ```bash
   ollama pull qwen3:8b
   ```

3. Modelin kurulduğunu doğrulayın:

   ```bash
   ollama list
   ```

4. Ollama servisinin çalıştığından emin olun. Varsayılan yerel API adresi:

   ```text
   http://localhost:11434
   ```

5. Modeli terminal üzerinden denemek için:

   ```bash
   ollama run qwen3:8b
   ```

Ollama servisi veya model hazır değilse uygulamanın yapay zekâ özellikleri çalışmayabilir.

## Veritabanı Kurulumu

Uygulamanın mevcut yapılandırmasında aşağıdaki SQL Server bağlantı dizesi kullanılmaktadır:

```csharp
Server=.\\SQLEXPRESS05;Database=MasaustuAsistanDB;Integrated Security=True;TrustServerCertificate=True;
```

Bu yapılandırmanın çalışması için:

- SQL Server veya SQL Server Express kurulu olmalıdır.
- `SQLEXPRESS05` isimli SQL Server örneği mevcut ve erişilebilir olmalıdır.
- Windows kullanıcısının SQL Server'a erişim yetkisi bulunmalıdır.
- `MasaustuAsistanDB` veritabanının oluşturulması için gerekli izinler sağlanmalıdır.

Uygulamanın ilk çalıştırmada gerekli tabloları otomatik oluşturduğu belirtilmektedir:

- `dbo.ChatMessages` — sohbet mesajları.
- `dbo.AssistantMemories` — kişisel hafıza kayıtları.

Tabloların otomatik oluşturulması, uygulamadaki veritabanı başlatma kodunun gerçekten bu işlemi gerçekleştirmesine bağlıdır.

**Güvenlik notu:** `TrustServerCertificate=True` yerel geliştirme ortamında kullanılabilir. Üretim ortamında sertifika doğrulamasının güvenli biçimde yapılandırılması önerilir.

## Projeyi Çalıştırma

### Visual Studio ile

1. Depoyu bilgisayarınıza indirin veya Git ile klonlayın.
2. `WpfApp1.slnx` çözüm dosyasını Visual Studio'da açın.
3. NuGet bağımlılıklarının geri yüklenmesini bekleyin.
4. SQL Server bağlantısını ve Ollama kurulumunu kontrol edin.
5. `WpfApp1` projesini başlangıç projesi olarak seçin.
6. Çözümü derleyin ve uygulamayı çalıştırın.

### Komut satırı ile

Projenin ana dizininde aşağıdaki komutları çalıştırın:

```bash
dotnet restore WpfApp1.slnx
dotnet build WpfApp1.slnx
```

Uygulamayı çalıştırmak için:

```bash
dotnet run --project WpfApp1
```

Bu komutların çalışması için proje dosyalarının belirtilen dizinlerde bulunması ve gerekli geliştirme araçlarının kurulu olması gerekir.

## Kullanım

1. Uygulamayı başlatın.
2. Veritabanı bağlantısının kurulabildiğini kontrol edin.
3. Arayüzdeki güç düğmesiyle asistanı başlatın.
4. Ollama servisinin ve `qwen3:8b` modelinin hazır olmasını bekleyin.
5. Sohbet alanına mesajınızı yazın ve yanıt alın.
6. Mesaj kopyalama özelliğiyle istediğiniz mesajı kopyalayın.
7. **Hafızayı Yönet** bölümünden kalıcı olarak saklamak istediğiniz bilgileri ekleyin, düzenleyin veya silin.
8. İhtiyacınıza göre Dengeli veya Performans modunu seçin.

Arayüzdeki düğmelerin ve çalışma modlarının davranışı uygulamanın mevcut koduna bağlıdır.

## Sorun Giderme

### Ollama bağlantısı kurulamıyor

- Ollama'nın kurulu olduğunu kontrol edin.
- `ollama list` komutuyla modelin yüklendiğini doğrulayın.
- `ollama run qwen3:8b` komutuyla modeli deneyin.
- `http://localhost:11434` adresindeki yerel servisin erişilebilir olduğunu kontrol edin.
- Model adı ile uygulama yapılandırmasındaki model adının aynı olduğundan emin olun.

### Veritabanına bağlanılamıyor

- SQL Server servisinin çalıştığını kontrol edin.
- `SQLEXPRESS05` örneğinin doğru isimle yapılandırıldığını doğrulayın.
- Bağlantı dizesindeki sunucu ve veritabanı adlarını kontrol edin.
- Windows kullanıcısının gerekli erişim izinlerine sahip olduğundan emin olun.
- Veritabanı veya tablo oluşturma izinlerini kontrol edin.

### Derleme hatası alınıyor

- Uygun .NET 10 SDK sürümünün kurulu olduğunu doğrulayın.
- Visual Studio'nun hedef framework'ü desteklediğini kontrol edin.
- .NET masaüstü geliştirme iş yükünün kurulu olduğundan emin olun.
- NuGet paketlerini geri yükleyin.
- Hata mesajındaki proje ve dosya bilgilerini inceleyin.

### Sohbet geçmişi veya hafıza kayıtları saklanmıyor

- Veritabanı bağlantısını kontrol edin.
- Tabloların oluşturulup oluşturulmadığını doğrulayın.
- Uygulamanın kayıt işlemlerinde hata alıp almadığını inceleyin.
- İlgili SQL tabloları için gerekli izinleri kontrol edin.

## Güvenlik ve Gizlilik

AE, yerel Ollama modeli ve yerel SQL Server kullanacak şekilde tasarlanmıştır. Ancak yerel model kullanılması, uygulamadaki bütün verilerin otomatik olarak güvenli olduğu anlamına gelmez.

- Veritabanına erişimi yetkili kullanıcılarla sınırlandırın.
- Kişisel hafıza kayıtlarında hassas bilgileri gereksiz yere saklamayın.
- Veritabanı yedeklerini güvenli bir yerde tutun.
- Üretim ortamında bağlantı güvenliğini ve hata günlüklerini gözden geçirin.
- Haricî ağ bağlantıları ve veri aktarımı davranışlarını uygulamanın gerçek kodu üzerinden doğrulayın.
- GitHub'a yüklemeden önce bağlantı bilgilerini, parolaları ve diğer gizli verileri kontrol edin.

## Katkıda Bulunma

Projeye katkı sağlamak isteyenler aşağıdaki adımları izleyebilir:

1. Depoyu fork edin.
2. Yeni bir geliştirme dalı oluşturun.
3. Değişikliklerinizi yapın.
4. Uygun testleri çalıştırın.
5. Değişikliklerinizi açıklayan bir Pull Request açın.

Katkılar, projenin geliştirilmesine ve yeni özelliklerin eklenmesine yardımcı olur.

## Lisans

Bu proje **MIT Lisansı** altında yayımlanmaktadır.

MIT Lisansı kapsamında kullanıcılar, lisans ve telif bildirimlerini korumaları koşuluyla yazılımı kullanabilir, kopyalayabilir, değiştirebilir, dağıtabilir ve ticari amaçlarla değerlendirebilir.

Lisansın tam metni için [`LICENSE`](LICENSE) dosyasına bakabilirsiniz.

Copyright (c) 2026 Arif Ergünöz (ergunozarif-dev)

Bu lisans yalnızca lisanslama hakkına sahip olduğunuz proje bileşenleri için geçerlidir. Ollama, Qwen3:8B ve üçüncü taraf kütüphaneler kendi lisans koşullarına tabi olmaya devam eder.

## Proje Bilgileri

- **Proje adı:** AE - Masaüstü Asistanım
- **Proje türü:** Windows masaüstü yapay zekâ asistanı
- **Geliştirici:** Arif Ergünöz
- **GitHub:** [ergunozarif-dev](https://github.com/ergunozarif-dev)
- **Durum:** Geliştirme aşamasında
- **Lisans:** MIT License

