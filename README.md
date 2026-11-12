# HMail

ASP.NET Core 8 MVC ile tarayıcı üzerinden çalışan bir e-posta uygulaması. Kullanıcılar kayıt olup giriş yaptıktan sonra sistemdeki diğer üyelere mesaj gönderir; admin panelinde üye, istatistik ve şikayet yönetimi vardır.

## Teknolojiler

- **Backend:** C# • ASP.NET Core 8 MVC
- **Veritabanı:** MS SQL Server • Entity Framework Core • LINQ
- **Kimlik:** ASP.NET Core Identity • Cookie Authentication • Role-Based Authorization (User / Admin)
- **Frontend:** Razor Views • HTML • CSS • Tailwind CSS • JavaScript

## Özellikler

### Kullanıcı

- Kayıt, giriş, çıkış, benzersiz e-posta
- Profil (ad, soyad, fotoğraf) ve şifre değiştirme
- Yalnızca sistemde kayıtlı adrese mesaj gönderme
- Overlay ile yeni mesaj; taslak kaydetme, düzenleme, gönderme
- Mail açılınca okundu; kenarda okunmamış sayısı
- Yanıt ve konuşma zinciri (`ThreadId`)
- Gönderilenlerde alıcının okuyup okumadığı
- Yıldız, çöp, geri yükle, kalıcı sil
- Kullanıcıya özel kategori; mesaja kategori atama
- Gelen kutusunda okunmuş / okunmamış / yıldızlı filtre
- Arama, tarih, sıralama, sayfalama
- Mesaj şikayet etme ve **Şikayet ettiklerim** listesi

### Admin

- Dashboard: toplam / aktif üye, toplam mesaj, bugün gönderilen, okunmamış, çöp
- En çok mesaj gönderenler ve en çok kullanılan kategoriler (`GroupBy`, `Count`, `OrderBy`)
- Üye listeleme, hesabı pasif / aktif yapma, rol atama
- Şikayet edilen mesajı açıp inceleme; işlem yok veya göndereni pasif bırakma

## Kurulum

1. SQL Server (Express yeterli) ve .NET 8 SDK kurulu olsun.
2. `IdentityMail.Web/appsettings.json` içindeki bağlantı cümlesini **kendi SQL sunucuna** göre düzenle:

```json
"DefaultConnection": "server=SUNUCU_ADI\\SQLEXPRESS;database=IdentityMailDb;integrated security=true; trustServerCertificate=true"
```

`SUNUCU_ADI` yerine SSMS’te gördüğün sunucu adını yaz (örnek: `.\\SQLEXPRESS`).

3. Visual Studio’da **Package Manager Console** hedef projesi `IdentityMail.Web` iken:

```
Update-Database
```

4. Projeyi çalıştır. İlk açılışta `Admin` ve `User` rolleri ile seed admin oluşur.

## Demo hesap

| Alan | Değer |
|------|--------|
| E-posta | `admin@hmail.com` |
| Şifre | `Admin123!` |
| Kullanıcı adı | `admin` |

Bu hesap hem mail kutusu hem admin paneli kullanır. Yeni kayıtlar varsayılan olarak **User** rolündedir.

## Proje yapısı

```
MyAcademy_IdentityMailProject.sln
└── IdentityMail.Web
    ├── Controllers
    ├── DTOs
    ├── Entities
    ├── Context
    ├── Migrations
    └── Views
```

Veri taşıma DTO’larla yapılır. Sender–Receiver ilişkileri EF Core `Include` ile yüklenir.

## Geliştirme notu

Veritabanı lokal SQL Server üzerindedir; `.mdf` dosyası repoda yoktur. Klonladıktan sonra connection string’i kendi makinenize göre değiştirip `Update-Database` çalıştırın.
