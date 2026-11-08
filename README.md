# ChaosXMB

PS3/PSP XMB esintili, tam ekran Windows oyun arayüzü. C# / WPF / .NET 8.

Steam istemcisi gerekmez. Program oyunları sadece Windows üzerinden açar; oyun indirmez, lisans atlatmaz.

## Özellikler

- Yatay kategoriler (Games, Apps, Favorites, Recent, Settings), dikey gezinti, sinüs dalgalı XMB arka planı
- Klavye, fare ve XInput gamepad desteği
- Oyun ekleme, düzenleme, silme ve favorilere alma
- Steam / Epic / GOG'da yüklü oyunları otomatik tarama
- Steam üzerinden isim, açıklama, tür, geliştirici, çıkış tarihi, kapak ve hero görseli alma (istersen kendi görselini seç)
- Oyun detay ekranı: toplam süre, son oturum, en uzun oturum, son oynanma, başlatma sayısı
- Son oynananlar ve favoriler
- Async görsel indirme, RAM ve disk cache
- F3 ile canlı FPS göstergesi

## Kurulum

1. Releases bölümünden son sürümü indir ve zip'ten çıkar.
2. `ChaosXMB.exe` dosyasını çalıştır.
3. Sağ ok → **Settings** → **Oyun Ekle** ya da **Oyunları Tara**.

Kaynak koddan derlemek için: Visual Studio 2022 (.NET desktop development) + .NET 8 SDK, ardından `ChaosXMB.sln` aç ve F5 bas.

## Kontroller

| Eylem | Klavye | Gamepad |
|---|---|---|
| Gezinme | Ok tuşları | D-pad / sol analog |
| Seç / Aç | Enter | A |
| Geri | Esc | B |
| FPS göstergesi | F3 | - |

## Veri ve yedek

Kütüphane `%LOCALAPPDATA%\ChaosXMB\games.json` dosyasında tutulur (`games.json.bak` önceki kayıt yedeğidir). İndirilen görseller `%LOCALAPPDATA%\ChaosXMB\artwork\` klasöründedir. Yedek için uygulama kapalıyken `games.json` dosyasını kopyalaman yeterli. Programı silmek veya taşımak kütüphaneni silmez.

## Bilmen gerekenler

- Steam'in herkese açık endpoint'leri bazen istek reddedebilir; bu durumda bilgi ve görselleri elle girebilirsin.
- Oynama süresi yalnızca bu launcher üzerinden başlatılan oturumlar için ölçülür. Steam/Epic protokolüyle açılan oyunlarda süreç tespit edilemezse süre yazılmaz.
- DualShock/DS4 için Windows'ta XInput emülasyonu gerekebilir.
- Görseller Steam'den alınır; ilgili hizmet koşullarına ve kullanım haklarına uymak sana aittir.
- Bu uygulama Windows shell'inin yerini almaz.
