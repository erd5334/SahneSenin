# PROJE TALİMATI: Perşembe Eğlencesi - Müzik Yarışması Uygulaması

Sen kıdemli bir C# WPF geliştiricisisin. Senden, 33 kişilik bir öğretmen grubu için perşembe akşamı düzenlenecek interaktif bir müzik yarışması oyunu kodlamanı istiyorum. Uygulama tamamen lokal çalışacak, internet bağımlılığı olmayacaktır.

## 1. TEKNOLOJİK YIĞIN (STACK) & MİMARİ
- **Dil/Platform:** C# WPF (.NET 8.0 veya üzeri)
- **Arayüz Tasarımı:** XAML (Modern, karanlık tema, neon geçişler, büyük fontlar, yüksek görsellik)
- **Ses Yönetimi:** `System.Windows.Media.MediaPlayer` veya `NAudio` (Lokal MP3 dosyalarını çalmak, saniye kontrolü yapmak için)
- **Veri Saklama:** `data.json` (Gelişmiş yerel JSON yapısı)

## 2. DOSYA VE VERİ YAPISI
Proje kök dizininde bir `MusicPool` klasörü ve `data.json` dosyası bulunacaktır. Şarkılar mükerrer indirmeyi önlemek için sanatçı bazlı tek bir havuzda toplanacaktır.

### `data.json` Tasarımı:
{
  "Artists": {
    "Tarkan": ["Tarkan_Simarik.mp3", "Tarkan_KuzuKuzu.mp3"],
    "Serdar Ortac": ["Serdar Ortac_Karabiberim.mp3", "Serdar Ortac_Dansoz.mp3"]
  },
  "Teachers": [
    {
      "Name": "Ahmet Yılmaz",
      "SelectedArtists": ["Tarkan", "Serdar Ortac"],
      "Score": 0,
      "HasPlayed": false
    }
  ]
}

## 3. EKANLAR VE AKIŞ GEREKSİNİMLERİ

### A. Giriş ve Çarkıfelek Ekranı (Teacher Selection)
- `HasPlayed: false` olan öğretmenlerin isimleri ekranda dinamik bir çarkıfelek (Roulette) veya dikey kayan bir listede listelenmelidir.
- "Öğretmen Seç" butonuna basıldığında bir animasyon dönmeli, heyecanlı bir ses efekti eşliğinde rastgele bir öğretmen seçilmelidir.
- Seçilen öğretmenin `HasPlayed` değeri `true` yapılmalı ve yarışma ekranına geçilmelidir.

### B. Yarışma ve Geri Sayım Ekranı (Game Screen)
- Seçilen öğretmenin `SelectedArtists` listesindeki sanatçılar tespit edilmelidir.
- Bu sanatçıların `data.json` içerisindeki tüm şarkıları birleştirilmeli ve içlerinden **rastgele 1 adet MP3** seçilmelidir.
- Şarkı başladığında, şarkının rastgele bir saniyesinden (örneğin 40. saniyeden/nakarattan) başlaması sağlanmalıdır.
- Ekranda 10 saniyelik büyük, dairesel veya yatay bir geri sayım sayacı (ProgressBar/Timer) akmalıdır. 10 saniye bitince müzik durmalı (Fade-out ile yumuşakça kesilmesi artı puandır).

### C. Skor ve Puanlama Ekranı (Score Screen)
Sunucunun yönetebileceği 3 ana buton bulunmalıdır:
1. **"Bildiyi Seç" (+10 Puan):** Ekranda yeşil renkli başarı teması ve alkış efekti tetiklenir.
2. **"Bonus - Harika Söyledi" (+15 Puan):** WPF Canvas üzerinde yukarıdan aşağıya dökülen **renkli konfeti animasyonları** tetiklenir, coşkulu bir tebrik ekranı gelir.
3. **"Bilemedi" (0 Puan):** Klasik bir hata ses efekti çalar.
- Puanlama sonrası veriler anlık olarak `data.json` dosyasına kaydedilmeli ve ana ekrana (çarkıfeleğe) dönülmelidir.

## 4. SİZDEN BEKLENENLER
- Temiz, MVVM prensiplerine göz kırpan ama hızlı prototiplemeye uygun kod yapısı.
- XAML tarafında görsel efektler, gölgelendirmeler (DropShadow) ve yumuşak animasyon kodları (Storyboard kullanımı).
- İlk aşama olarak bana projenin `data.json` yönetimini ve temel WPF sayfa yapısını kuracak kodları vererek başla.