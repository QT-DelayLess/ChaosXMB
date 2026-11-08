# CHAOSXMB — PS3 XMB esintili Windows oyun arayüzü

Windows 10/11 için **C# / WPF / .NET 8** kaynak kodu. Visual Studio 2022 (".NET desktop development" iş yükü) ile açılır. Steam istemcisi şart değildir. Korsan veya orijinal olduğu ayrıştırılmaz; program seçilen dosyayı Windows üzerinden açar ve herhangi bir oyun indirme veya lisans atlatma işlemi yapmaz.

## Kurulum

1. `ChaosXMB.sln` dosyasını Visual Studio 2022 ile aç.
2. Bilgisayarında **.NET 8 SDK** ve Visual Studio **.NET desktop development** iş yükü olsun.
3. `ChaosXMB` başlangıç projesini seçip `F5` bas. Alternatif: proje klasöründe `dotnet run`.
4. İlk açılışta kütüphane boştur. **Sağ ok** ile `Settings` → `Oyun Ekle` → `Enter`.
5. Oyun `.exe`, `.lnk`, `.url`, `.bat` veya `.cmd` yolunu seç. Uygulama dosya adını oyun adı olarak kullanıp Steam'de arar. Sonuçları kontrol ederek doğru oyun adını seç.
6. İlgili Steam bilgileri otomatik gelir; poster/hero gelmezse sağ taraftan kendi `.jpg` / `.png` görsellerini seçebilirsin. `KAYDET` de.
7. `Games` kategorisinde oyunun yanındaki kapak görünür. Seçip `Enter` basınca **tam ekran oyun detay paneli** açılır; `OYNA` ile çalıştırılır.

## Ne var?

- Tam ekran PS3/PSP XMB esintili yatay kategoriler ve dikey gezinti, orijinal sinüs dalga çizimi, sabit odak noktası, yumuşak kamera kayışı
- **Games, Apps, Favorites, Recent, Settings** kategorileri
- Klavye (ok tuşları, Enter, Esc), fare, ilk XInput kolu (D-pad / sol analog / A / B)
- Gerçek oyun ekleme/düzenleme/silme/kütüphane yönetimi; exe yolunu elle değiştirme
- Steam public Store Search + App Details API üzerinden isim, açıklama, tür, geliştirici, tarih, kapak ve hero görseli alma; doğru oyun arama sonucunu manuel seçebilme
- 600×900 Steam library portrait kapağı denenir; yoksa Steam kapsülü veya hero kullanılır; istenirse yerel resim seçilir
- Oyun kartının yanında otomatik **küçük kapak**, Enter sonrası büyük kapak, hero, açıklama, geliştirici, tür, çıkış tarihi, toplam süre, favori, düzenle ve kaldır
- Process.Start ile oyun başlatma, işlem bitince süreyi güncelleme, son oynananlar ve favoriler
- %LOCALAPPDATA%\ChaosXMB\games.json kalıcı veri, %LOCALAPPDATA%\ChaosXMB\artwork\ internetten indirilen resimler
- Async görsel indirme, ram cache sınırı (32 bitmap), disk cache, prefetch, iptal edilen geç arkaplan değişimlerini yoksayma, 30fps hafif wave visual

## Sınırlar / bilmen gerekenler

- **Bu ZIP kaynak kodudur, derlenmiş .exe değildir.** Proje bu ortamda Windows/WPF çalıştırılarak derlenemedi veya gerçek monitörde FPS testi yapılamadı. Windows'ta ilk `F5` ile test etmek gerekir.
- Steam'in herkese açık Store endpoint'leri ağ, bölge veya oran sınırı nedeniyle zaman zaman istek reddedebilir. Bu durumda oyun ve kapak/arka plan bilgisi **manuel girilir**. RAWG/IGDB/SteamGridDB API anahtarı gerektiren entegrasyonlar bu sürüme ekli değildir.
- Steam'in portrait görseli her oyunda bulunmaz; fallback mekanizması vardır. Bazı .exe adları gerçek oyun adıyla uyuşmayacağından arama sonucunu onaylaman gerekir.
- Oturum süresi Process.Start'ın döndürdüğü işlem ömrüne göre hesaplanır. Steam/Epic launcher veya başlangıç loader'ı başka işlem başlatıp erken kapanırsa süre doğru hesaplanmayabilir.
- Oyun başarım yüzdesi, resmi PlayStation achievement entegrasyonu ve trailer oynatma **bu sürümde yoktur**. Playtime sadece bu launcher üzerinden başlayan oturumları izler.
- İlk XInput oyun kolu için temel destek vardır; DualShock/DS4 için Windows'ta XInput emülasyonu gerekebilir.
- Bu uygulama **Windows shell'inin yerini almaz**, explorer.exe'yi kapatmaz veya Windows'u değiştirmez.
- Projedeki görseller kullanıcı tarafından ağ üzerinden alınır; kullanıcıların hizmet koşullarına ve görsellerin kullanım haklarına uyması gerekir.

## Proje dosyaları

- `ChaosXMB/MainWindow.xaml(.cs)` — ana XMB, detay ekranı, giriş, launch
- `ChaosXMB/Views/AddGameWindow.xaml(.cs)` — oyun ekleme / Steam araması / manuel metadata
- `ChaosXMB/Views/LibraryManagerWindow.xaml(.cs)` — kütüphane yöneticisi
- `ChaosXMB/Services/SteamMetadataService.cs` — Steam isteği
- `ChaosXMB/Services/ImageCacheService.cs` — async RAM / disk cache
- `ChaosXMB/Services/LibraryStore.cs` — kalıcı JSON
- `ChaosXMB/Services/GameLaunchService.cs` — uygulama başlatma / süre
- `ChaosXMB/Services/XInputController.cs` — gamepad
- `ChaosXMB/Controls/WaveRenderer.cs` — DrawingVisual waves
- `ChaosXMB/Engine/XmbNavigator.cs` — tek kaynak navigasyon durumu

## Verini yedeklemek

Kütüphanenin yedeği için uygulama kapalıyken `%LOCALAPPDATA%\ChaosXMB\games.json` dosyasını başka yere kopyala. Programın ZIP klasörünü silmek kütüphane JSON'unu silmez.

## Performans kontrolü

Ana ekranda `F3` ile canlı FPS göstergesi açılıp kapatılır. WPF kare hızı donanım, DWM ve monitör yenileme hızına göre değişir; koda sabit 144 FPS iddiası eklenmemiştir.

## 2026-10-31 düzeltmesi — derleme hataları

`Services/GameLaunchService.cs`, `Services/ImageCacheService.cs` ve `Services/LibraryStore.cs` dosyalarının başına açık `using System.IO;` eklendi. Bu, `File`, `Path`, `Directory`, `MemoryStream`, `IOException`, `FileStream` ve diğer `System.IO` sembollerine ilişkin CS0103/CS0246 hatalarını düzeltir. Kod Windows üzerinde derlenemediği için başka ortam-bağımlı hatalar olabileceği unutulmamalıdır.


## 2026-10-31: Kalıcı kütüphane ve fare kaydırma düzeltmesi

- Oyunlar **uygulamanın çalıştırıldığı klasöre değil**, Windows kullanıcısının `%LOCALAPPDATA%\ChaosXMB\games.json` dosyasına kaydedilir. Programı başka bir klasöre taşısan da aynı kullanıcı hesabında aynı liste açılır.
- `Settings` → `Kayıt Klasörünü Aç` seçeneği bu klasörü Explorer'da açar. `games.json` ana kayıt, `games.json.bak` önceki başarılı kayıt yedeğidir.
- Oyun ekleme/düzenleme/silme ve favori değişikliği yapıldığında kayıt hemen yapılır; uygulamayı tekrar açınca oyunlar yüklenir. Kaydetme başarısız olursa ilgili işlem geri alınır.
- Kayıt dosyası bozulursa, varsa `.bak` dosyasından geri yüklenir ve bozuk dosya ayrıca saklanır. Kurtarma durumunda kullanıcı bilgilendirilir. Dosyaları düzenlemeden önce uygulamayı kapat.
- Kategori / oyun listesi hareket ederken fareyi sabit tutmak, eski sürümde `MouseEnter` ile yanlışlıkla yeni satırı seçebiliyordu. Bu otomatik hover seçimi kapatıldı. **Fareyle tek tık** seçer/açar; klavye okları ve gamepad eskisi gibi çalışır.
- Yeni sürümü çıkardığında mevcut `%LOCALAPPDATA%\ChaosXMB\games.json` dosyanı silme. Önceki oyunların kayıtları bu konumdaysa otomatik görünür.


## 2026-10-31 — V1.3: üç özellik, aynı tasarım

**1. Taşma / temiz görünüm:** Ana oyun listesinin görünür alanı artık alt bilgi çubuğu ile çakışmaz; satırlar bu alan içinde kırpılır. 1320 pikselden dar pencerelerde sağ özet bölümü geçici gizlenir. Oyun seçimi ve fareyle tıklama davranışı korunmuştur.

**2. Oyun detayları ve oynama süresi:** Detay ekranında toplam süre, son oturum, en uzun oturum, son oynanma ve başlatma sayısı görünür. Doğrudan EXE açıldığında süreç çalıştığı sürece zaman ölçülür; 25 saniyede bir oyun kütüphanesine kaydedilerek ani kapanışta kayıp azaltılır. Son 15 oturum `games.json` içinde saklanır (eski kayıtlarda bu alan bulunmasa da açılır). Steam/Epic protokolleri üzerinden başlatılan oyunlarda sadece oyun klasöründeki yeni bir proses tespit edilirse süre ölçülür; tespit edilmezse yanlış süre yazılmaz. Launcher üzerinden başlatılmamış önceki Steam/GOG saatlerini çekmez.

**3. Steam / Epic / GOG yüklü oyun keşfi:** **Settings → Oyunları Tara** ile bilgisayardaki yerel Steam `steamapps/appmanifest_*.acf`, Steam `libraryfolders.vdf`, Epic `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item` ve GOG Windows kayıt defteri `SOFTWARE\GOG.com\Games` bilgileri taranır. Steam/Epic mağaza protokolüyle; GOG uygun yerel `.exe` ile açılır. Eklemek istediğin oyunları listede seç, **SEÇİLENLERİ EKLE** butonuna bas. Bu işlem oyun klasörlerini değiştirmez, internetten oyun indirmez veya mağaza oturumu açmaz. Kaydedildikten sonra doğru eşleşen Steam metadata bilgileri / resimler asenkron tamamlanmaya çalışılır. İnternet erişimin yoksa oyunlar yine kalır; görselleri manuel değiştirebilirsin. Steam/Epic/GOG istemcileri veya kayıtları farklı bir düzen kullanıyorsa bazı oyunlar görünmeyebilir; manuel oyun ekleme hâlâ kullanılabilir.

**Uyumluluk:** Aynı `%LOCALAPPDATA%\ChaosXMB\games.json` dosyası korunur; önceki oyun kayıtlarını yeniden eklemek gerekmez. Yeni şema alanları eski kayıtlarda opsiyoneldir. Güvenlik için uygulamayı kapatıp `games.json` dosyasının bir kopyasını alman önerilir.

**Test durumu:** XAML XML denetimi, olay bağlarının eşleşmesi ve ZIP bütünlüğü kontrol edildi. Bu Linux ortamında Windows .NET 8 WPF derleyicisi bulunmadığından gerçek MSBuild derlemesi veya oynama süresi testi yapılamadı. Projeyi Visual Studio'da derlemek gerekir.


## v1.3.1 — Oyun listesi görünürlük düzeltmesi

- v1.3 ile gelen, yalnızca ilk satırı gösteren kırpma regresyonu düzeltildi.
- `ItemsControl` oyun satırları artık `StackPanel` ile gerçek yüksekliğe sahip (88px + 6px = 94px).
- `ItemsViewport` yüksekliği pencere yüksekliğinden hesaplanıyor; seçili oyun ve takip eden satırlar görünür, alt kontrol alanına taşma yok.
- Oyun kayıtları (`%LOCALAPPDATA%\ChaosXMB\games.json`), mağaza tarama, metadata ve süre takibi değiştirilmedi.


## v1.3.2 — Görünmeyen oyun satırları için gerçek kaydırma düzeltmesi

- v1.3.1'deki hata: `ItemsControl`'a uygulanan `RenderTransform` ile **kırpma sınırı da yukarı kayıyordu**. Bu yüzden seçili satırın altında en fazla 1–2 oyun görünüyordu; kalan oyunlar kütüphaneden silinmemişti.
- Artık oyun satırları, **sabit `ScrollViewer` görüntü alanının içinde** piksel tabanlı yumuşak kaydırılır. Transform ve klipleme birbirinden ayrıldı.
- Son oyunun bile aynı üst odak konumuna gelebilmesi için dinamik alt boşluk eklendi. Farklı ekran yüksekliklerinde otomatik hesaplanır.
- Fare tekerleği seçimle senkronizedir; eski kendi kendine hover seçimi geri getirilmedi.
- Oyun verileri, Steam/Epic/GOG taraması, oynama süreleri ve mevcut ekran tasarımı korunmuştur.
- Kayıt dosyası `%LOCALAPPDATA%\ChaosXMB\games.json` olduğu gibi kalır; güncelleme için silmeyin.
