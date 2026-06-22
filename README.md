# Sahne Senin - Perşembe Eğlencesi Müzik Yarışması Uygulaması

Bu proje, 33+ kişilik bir öğretmen grubu için interaktif, eğlenceli ve çift ekran destekli bir müzik yarışması oyunudur. Tamamen yerel (offline) çalışacak şekilde C# WPF ve .NET 8.0 mimarisiyle geliştirilmiştir.

---

## 🌟 Önemli Özellikler

1. **Çift Ekran (Dual-Screen) Yönetimi:**
   * **Sunucu Kontrol Paneli (`MainWindow`):** Sunucu bilgisayarında açılır. Yarışmacı listesini CSV'den yükleme, çarkı döndürme, doğru cevapları (kopya) görme, tahmin aşamasına geçiş ve manuel puanlama yeteneklerine sahiptir.
   * **TV / Sahne Ekranı (`DisplayWindow`):** İkinci ekrana (TV, projeksiyon) tam ekran yansıtılır. Katılımcıların göreceği neon temalı çark animasyonlarını, 10 saniyelik geri sayımı, 4 şıklı tahmin kartlarını ve bonus puanlarda patlayan fizik tabanlı konfeti yağmurunu barındırır.
2. **Çevrimdışı Türkçe Ses Tanıma (Speech Recognition):**
   * İnternete veya bulut servislerine bağımlı olmadan, Windows'un yerel SAPI (`System.Speech`) kütüphanesini kullanır.
   * **Dinamik Dil Dilbilgisi (Dynamic Grammar):** Her tur başında, ses tanıma motoruna sadece **A, B, C, D** seçenekleri, Türkçe kodlamaları (**Adana, Bursa, Ceyhan, Denizli**) ve o tura özel ekrandaki **4 şarkı adı** komut olarak yüklenir. Bu sayede çevrimdışı ses tanıma performansı %100'e yakın doğrulukla çalışır.
3. **Seçenek Filtreleme ve Jenerik Dolgular:**
   * Tahmin aşamasındaki 4 seçenek, **yalnızca çalınan şarkının sanatçısına ait** diğer şarkılardan rastgele seçilir.
   * Eğer sanatçının klasöründe 4 adet şarkı yoksa, sistem otomatik olarak "[Sanatçı Adı] - Diğer Şarkı 2" gibi jenerik şıklar üreterek listeyi tamamlar.
4. **3 Hak Oyunu Modeli:**
   * Her öğretmenin 3 yarışma hakkı bulunur. Sırayla 3 farklı şarkıyı tahmin eder ve toplam puanları birikir.
   * Aynı öğretmene aynı tur içinde mükerrer (aynı) şarkı çaldırılmaz.

---

## 📂 Dosya ve Klasör Yapısı

* `MusicPool/` : Yarışmada kullanılacak şarkıların ve ses efektlerinin barındırıldığı ana klasör.
* `data.json` : Uygulamanın öğretmen listesini, seçilen sanatçıları ve güncel skor durumlarını sakladığı yerel veritabanı.
* `SahneSenin.csproj` : Proje yapılandırma ve NuGet paket tanımları.

---

## 🎵 Müzik Klasörü (`MusicPool`) Nasıl Hazırlanmalı?

Uygulama, müzikleri iki farklı yerleşim düzeninde de otomatik olarak tarayabilir:

### Yöntem A: Sanatçı Klasör Yapısı (Önerilen)
`MusicPool` klasörü altında her sanatçı için ayrı bir alt klasör oluşturup şarkıları içine atabilirsiniz:
* `MusicPool/Tarkan/Simarik.mp3`
* `MusicPool/Serdar Ortaç/Karabiberim.mp3`
* `MusicPool/Sezen Aksu/Sen Aglama.mp3`

### Yöntem B: Düz Dosya Yapısı
Tüm şarkıları tek bir klasörde `SanatçıAdı_ŞarkıAdı.mp3` formatında tutabilirsiniz:
* `MusicPool/Tarkan_Simarik.mp3`
* `MusicPool/Serdar Ortac_Karabiberim.mp3`

> [!TIP]
> **Özel Ses Efektleri:** Yarışmada çalacak alkış ve hata sesleri için `applause.mp3`, `correct.mp3` ve `wrong.mp3` dosyalarını doğrudan `MusicPool` kök dizinine kopyalayabilirsiniz. Dosyalar bulunamazsa sistem varsayılan uyarı tonlarını çalacaktır.

---

## 📊 Örnek CSV Dosyası Formatı

Yarışmacı listesini sunucu paneline aktarmak için Excel veya Not Defteri ile `.csv` formatında bir dosya hazırlamalısınız. Sistem hem noktalı virgül (`;`) hem de virgül (`,`) ayraçlarını destekler.

**Örnek İçerik (`ogretmenler.csv`):**
```csv
Ad Soyad;Sanatçı1;Sanatçı2;Sanatçı3
Ahmet Yılmaz;Tarkan;Serdar Ortaç;Sezen Aksu
Ayşe Demir;Duman;Barış Manço;Şebnem Ferah
Mehmet Kaya;Kenan Doğulu;Mustafa Sandal
```

* **İlk satır:** Başlık satırıdır (`Ad Soyad;Sanatçı1...`), uygulama tarafından otomatik olarak atlanır.
* **Sonraki satırlar:** Öğretmenin adı ve yarışmak istediği en fazla 3 sanatçının ismi yer alır.

---

## 🎙️ Ses Tanıma Motoru Hataları ve Çözümü

Eğer sunucu panelinde **"Hata: Windows Ses Tanıma yüklü değil"** veya **"0 MB"** uyarısı görüyorsanız, Windows konuşma paketi eksik kurulmuş demektir. Aşağıdaki adımları uygulayarak düzeltebilirsiniz:

### 1. PowerShell ile Ses Paketini Zorla Yükleme
1. Başlat menüsüne sağ tıklayıp **Terminal (Yönetici)** veya **PowerShell (Yönetici)** seçeneğini açın.
2. Aşağıdaki komutu yapıştırıp **Enter**'a basın:
   ```powershell
   Add-WindowsCapability -Online -Name "Language.Speech~~~tr-TR~0.0.1.0"
   ```
3. İndirme %100 tamamlandıktan sonra bilgisayarınızı yeniden başlatın.

### 2. Tarifeli Bağlantı Engelini Kaldırma
Eğer internetiniz "Tarifeli bağlantı" olarak ayarlandıysa dil paketi indirilemez ve 0 MB'ta takılı kalır:
1. **Ayarlar > Ağ ve İnternet > Wi-Fi (veya Ethernet)** kısmına gidin.
2. Bağlı olduğunuz ağın detaylarına tıklayın.
3. **Tarifeli bağlantı** seçeneğini **Kapalı** konuma getirin.

---

## 🚀 Uygulamayı Çalıştırma

1. İkinci ekranı (TV/Projeksiyon) bilgisayarınıza bağlayın ve ekran modunu **Genişlet (Extend)** olarak ayarlayın.
2. Proje dizininde terminali açıp şu komutla derleyin ve çalıştırın:
   ```powershell
   dotnet run
   ```
3. Sunucu panelinden **CSV'den Yükle** butonuna tıklayarak öğretmen listenizi yükleyin ve yarışmayı başlatın!
