# Araştırma: Uygun Eşikleme Değerinin Otomatik Tespiti

## Problem

Tek bir sabit eşik bütün görüntülerde çalışmaz. Aydınlatma, gölge, kamera beyaz dengesi ve nesne rengi histogramı değiştirir. Yalnızca gri tona geçmek de renk bakımından farklı fakat parlaklık bakımından yakın bölgeleri birbirine karıştırabilir.

## Uygulanan yöntem

Program aynı görüntüden yedi tek kanallı aday üretir:

- Gri ton
- B, G ve R kanalları
- HSV uzayındaki H, S ve V kanalları

Her kanal için şu işlem iki ayrı kutupta uygulanır: normal ikili görüntü ve ters ikili görüntü.

1. 5x5 Gauss filtresiyle küçük gürültüyü azalt.
2. Otsu yöntemiyle global eşiği otomatik hesapla.
3. 5x5 elips çekirdekle açma ve kapama uygula.
4. Bağlı dış bölgeleri çıkar.
5. Her bölgeyi merkeze yakınlık, alan oranı, kompaktlık ve görüntü kenarına temas ölçütleriyle puanla.
6. En yüksek puanlı kanal-kutup-bölge birleşimini seç.

Puanlama özeti:

```text
puan = 2,4 × merkez_yakınlığı
      + 1,1 × kullanılabilir_alan_puanı
      + 0,45 × kompaktlık
      - 1,8 × kenara_temas
```

Bu puan evrensel değildir. Ödev 5'teki "göl görüntünün ortasındadır" varsayımını kodlar. Hedef nesne kenarda ise yanlış bölge seçebilir.

## Üç renk üzerinden eşikleme daha iyi mi?

Bazen evet, fakat doğrudan üç bağımsız eşik kullanmak her zaman daha iyi değildir.

- Nesne ile zemin gri tonda yakınsa tek bir RGB veya HSV kanalı ayrımı belirginleştirebilir.
- R, G ve B için katı üçlü aralık kullanmak aydınlatma ve beyaz dengesi değişince kırılgandır.
- HSV-S kanalı renk doygunluğu farkını yakalar; göl/bitki gibi renkli uydu görüntülerinde gri tondan daha iyi olabilir.
- HSV-H düşük doygunlukta kararsızdır. Beyaz, gri ve siyah alanlarda H kanalına güvenilmemelidir.

Bu nedenle program "üç eşiği aynı anda zorunlu tutmak" yerine kanalları aday olarak sınar ve görüntü geometrisine göre puanlar. Renk aralığı biliniyorsa 3. ödev sekmesindeki R/G/B alt-üst sınırları daha kontrollü bir seçenektir.

## Ölçülebilir değerlendirme

Kendi fotoğraflarında araştırmayı savunabilmek için her görüntüde şu değerleri kaydet:

- Seçilen kanal ve eşik
- Gerçek nesne alanı elle işaretlenebiliyorsa IoU
- Sayma görevinde gerçek ve bulunan nesne sayısı
- Yanlış pozitif ve yanlış negatif bölge sayısı
- İşlem süresi

Yalnızca "görüntü güzel görünüyor" demek ölçüm değildir. Sunumda en az bir başarısız örnek gösterip nedenini açıklamak gerekir.

## Kaynaklar

1. N. Otsu, "A Threshold Selection Method from Gray-Level Histograms," IEEE Transactions on Systems, Man, and Cybernetics, 1979. DOI: https://doi.org/10.1109/TSMC.1979.4310076
2. OpenCV, Image Thresholding: https://docs.opencv.org/5.0/py_tutorials/py_imgproc/py_thresholding/py_thresholding.html
3. OpenCV, Morphological Transformations: https://docs.opencv.org/5.0/py_tutorials/py_imgproc/py_morphological_ops/py_morphological_ops.html
4. OpenCV, Connected Components and Shape Descriptors: https://docs.opencv.org/5.0/main_modules/imgproc_shape.html
5. C. Rother, V. Kolmogorov, A. Blake, "GrabCut: Interactive Foreground Extraction using Iterated Graph Cuts," ACM TOG/SIGGRAPH, 2004: https://www.microsoft.com/en-us/research/publication/grabcut-interactive-foreground-extraction-using-iterated-graph-cuts/
6. OpenCV, Hough Circle Transform: https://docs.opencv.org/5.0/tutorials/imgproc/imgtrans/hough_circle/hough_circle.html
7. OpenCV, Image Filtering / Median Blur: https://docs.opencv.org/5.0/main_modules/imgproc_filter.html
8. OpenCvSharp official repository and package selection: https://github.com/shimat/opencvsharp
