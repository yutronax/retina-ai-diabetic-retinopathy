# Ödevler İçin Veri Kaynakları

Bu liste, kod geliştirme ve ilk deneyler içindir. Ödev 1'de istenen "kendi çektiğiniz en az 5 fotoğraf" şartı internet datasetleriyle karşılanamaz.

## Ödev 1 - Ceviz ve fındık sayma

### Geliştirme verisi

- **NutsBD**: 10 kuruyemiş türünde 1.742 görüntü; hem `hazelnut` hem `walnut` sınıfları var. Lisans: CC BY 4.0.
  - Sayfa: https://data.mendeley.com/datasets/fmrnybgxb9/1
  - Kullanım: Nesne görünümünü incelemek ve tekil örneklerden beyaz zeminli sentetik sahne oluşturmak.
- **Nut classification - Roboflow**: Fındık ve ceviz dâhil çoklu kuruyemiş nesne tespit görüntüleri. Lisans: CC BY 4.0.
  - Sayfa: https://universe.roboflow.com/dataset-r3opz/nut-classification-jmkxg

### Teslim için gerekli gerçek veri

Kendiniz 5 fotoğraf çekin. Her fotoğrafta farklı sayıda ceviz ve fındık olsun. Önerilen dağılım:

| Fotoğraf | Fındık | Ceviz | Toplam |
|---|---:|---:|---:|
| 1 | 4 | 2 | 6 |
| 2 | 6 | 3 | 9 |
| 3 | 8 | 4 | 12 |
| 4 | 5 | 5 | 10 |
| 5 | 10 | 3 | 13 |

Çekim: mat beyaz A4, üstten 90 derece, sabit kamera yüksekliği, gölgesiz ışık, nesneler arasında en az 1 cm boşluk.

## Ödev 2 - Ön planı arka plandan ayırma

- **GrabCut test seti**: 50 görüntü ve karşılık gelen nesne maskeleri; yaklaşık 11 MB.
  - Doğrudan ZIP: https://github.com/saic-vul/fbrs_interactive_segmentation/releases/download/v1.0/GrabCut.zip
  - Kaynak deposu: https://github.com/saic-vul/fbrs_interactive_segmentation
  - Kullanım: Programın ürettiği ön plan maskesini gerçek maske ile karşılaştırmak.
- Teslim için ayrıca düz duvar önünde kendi çektiğiniz bir kişi fotoğrafı kullanın.

## Ödev 3 - Tuz-biber gürültüsü ve morfolojik filtreler

- **USC-SIPI Miscellaneous**: 44 standart görüntü; 16 renkli ve 28 gri görüntü. ZIP yaklaşık 12,4 MB.
  - Doğrudan ZIP: https://sipi.usc.edu/database/misc.zip
  - Liste: https://sipi.usc.edu/database/?volume=misc
  - Önerilen görüntüler: `4.2.07 Peppers`, `4.1.05 House`, `5.2.09 Aerial`.
  - Kullanım şartı: Araştırma/eğitim amaçlı.

Program bu temiz görüntülere kontrollü olarak aynı yoğunlukta tuz-biber gürültüsü ekleyebilir. Böylece filtreleri PSNR ile adil karşılaştırmak mümkün olur.

## Ödev 4 - Bozuk para sayma ve yazı-tura

### Çoklu para sahneleri

- **Bozuk Paralar - Roboflow (v2)**: 1 TL, 50 kuruş, 25 kuruş, 10 kuruş ve 5 kuruş sınıflarını içeren nesne tespit seti. v2 sürümünde 2.753 görüntü vardır; eğitim/doğrulama/test ayrımı hazırdır. Lisans: CC BY 4.0.
  - Veri sayfası: https://universe.roboflow.com/abdulkadir-caglar/bozuk-paralar/dataset/2
  - Doğrudan indirme ekranı: https://universe.roboflow.com/abdulkadir-caglar/bozuk-paralar/dataset/2/download
  - Önerilen indirme biçimi: `COCO JSON` (yalnızca görüntüler kullanılacaksa ZIP içindeki `train`, `valid` ve `test` klasörlerindeki JPG/PNG dosyalarını alın).

Not: Zenodo'daki **Turkish Lira Coin Classification Dataset** daha güncel ve uygundur; ancak dosyaları 31 Aralık 2026'ya kadar ambargoludur. Bu nedenle mevcut ödev için kullanılamaz: https://zenodo.org/records/18890826

### Yazı-tura referansları

- **TurCoins**: 138 sınıfta 11.080 kırpılmış Türk madeni para görüntüsü; 256x256 RGB. Toplam yaklaşık 335 MB. Lisans: CC BY 4.0; indirmek için Hugging Face hesabıyla kullanım koşullarını kabul etmek gerekiyor.
  - Sayfa: https://huggingface.co/datasets/hsyntemiz/turcoins
- **TCMB/Darphane para görselleri ve ölçüleri**: Yazı-tura yüzleri ve resmî çaplar için kullanılabilir.
  - Broşür: https://tcmb.gov.tr/wps/wcm/connect/8e9bfdb9-811d-4aca-ae30-7654437e7c4b/tl_profesyonel_specimen.pdf?MOD=AJPERES

Programdaki çap değerleri:

| Para | Resmî çap |
|---|---:|
| 25 kuruş | 20,50 mm |
| 50 kuruş | 23,85 mm |
| 1 TL | 26,15 mm |

Teslim için en güvenilir yöntem yine beyaz A4 üzerinde kendi paranızı çekmektir. Görüntüde kalibrasyon amacıyla en az bir 1 TL bulunmalıdır.

## Ödev 5 - Uydu görüntüsünden alan ve çevre

- **Lakevanlandsat.jpeg**: NASA/Landsat Van Gölü görüntüsü; 1024x768, yaklaşık 205 KB. Kamu malı.
  - Sayfa ve indirme: https://commons.wikimedia.org/wiki/File:Lakevanlandsat.jpeg
- **Lake Van Sentinel-2 (2022)**: 8170x4733 yüksek çözünürlüklü görüntü; yaklaşık 22 MB. Copernicus atfı gerekir.
  - Sayfa ve indirme: https://commons.wikimedia.org/wiki/File:Lake_Van,_Armenian_Highlands,_Turkey_-_15_November_2022_(52635538305).jpg
- **Copernicus Browser**: Tarih, bulut oranı ve alan seçerek yeni Sentinel-2 görüntüsü indirmek için.
  - Tarayıcı: https://browser.dataspace.copernicus.eu/
  - Dokümantasyon: https://documentation.dataspace.copernicus.eu/Applications/Browser.html

Hızlı test için `Lakevanlandsat.jpeg` yeterlidir. Nihai sonuçta ölçek çizgisini Copernicus ölçüm aracı veya bilinen harita mesafesiyle doğrulayın.

## Minimum indirme paketi

İlk çalıştırma için tamamını indirmeyin. Yeterli paket:

1. GrabCut ZIP - yaklaşık 11 MB
2. USC-SIPI Misc ZIP - yaklaşık 12,4 MB
3. NASA Van Gölü Landsat görüntüsü - yaklaşık 205 KB
4. Roboflow Bozuk Paralar v2 setinden 10-20 örnek görüntü
5. Kendi çektiğiniz 5 ceviz-fındık fotoğrafı ve 5-10 bozuk para fotoğrafı

TurCoins'in 335 MB'lık tamamı bu ödev için gereksizdir; yalnızca uygun 25 kuruş, 50 kuruş ve 1 TL yazı/tura referanslarını seçmek yeterlidir.
