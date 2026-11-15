# Görüntü İşleme - 8. Hafta Ödevleri

C# Windows Forms ve OpenCvSharp ile hazırlanmış tek uygulamada beş ödev ve otomatik eşikleme araştırması bulunur.

## İçerik

1. Ceviz/fındık sayma ve büyüklüğe göre sınıflandırma
2. GrabCut ile ön planı ayırıp yeni arka planla birleştirme
3. Tuz-biber gürültüsünde 9'lu kare, 5'li artı ve medyan filtre karşılaştırması
4. 25 kuruş, 50 kuruş ve 1 TL sayma; toplam değer ve referans tabanlı yazı-tura tespiti
5. Uydu görüntüsünde ölçek çizgisiyle alan ve çevre hesabı
6. Gri, RGB ve HSV kanallarında otomatik Otsu eşiği arama deneyi

## Gereksinimler

- Windows 10/11 x64
- Visual Studio 2022 veya .NET 8 SDK
- İnternet bağlantısı (ilk NuGet geri yüklemesinde)

Kullanılan paket: `OpenCvSharp5.Windows` sürüm `5.0.0.20261018`.

## Çalıştırma

Visual Studio'da `GoruntuIslemeOdevleri.sln` dosyasını açıp `F5` tuşuna basın.

Komut satırı:

```powershell
dotnet restore
dotnet run --project .\GoruntuIslemeOdevleri.csproj
```

Uygulama ilk açılışta tüm sekmeler için sentetik örnekler oluşturur; harici veri indirmek gerekmez.

## Gerçek fotoğraflar için çekim kuralları

- Ceviz/fındık ve paraları mat beyaz zemin üzerinde, üstten ve gölgesiz çekin.
- Nesneler birbirine ve görüntü sınırına değmesin.
- Farklı fotoğraflarda kamera-zemin mesafesini değiştirmeyin.
- Para değer kalibrasyonu için görüntüde en az bir 1 TL bulundurun.
- Yazı-tura için aynı ışık ve uzaklıkta çekilmiş tek para referanslarını yükleyin.
- Uydu görüntüsünde ölçülecek alan merkeze yakın olmalı; ölçek çizgisinin iki ucunu görüntü üzerinde seçin.

## Sunum öncesi doğrulama listesi

- [ ] Ödev 1 kendi çektiğiniz en az 5 fotoğrafta test edildi.
- [ ] Her fotoğraf için gerçek ve bulunan ceviz/fındık sayıları kaydedildi.
- [ ] Ödev 2 gerçek kişi fotoğrafında denendi; hatalı sınırlar not edildi.
- [ ] Ödev 3 için üç yöntemin PSNR değerleri ekran görüntüsüne alındı.
- [ ] Ödev 4 değer ve yazı-tura sonucu elle doğrulandı.
- [ ] Ödev 5 için ölçek çizgisi doğru uçlardan seçildi.
- [ ] Araştırma sekmesindeki aday puan tablosu sunuma alındı.

## Bilinen sınırlar

- Ceviz/fındık ayrımı boyut farkına dayanır; perspektif ve üst üste binme sonucu bozar.
- GrabCut nesnenin görüntü kenarına değmediğini varsayar.
- Para değeri en büyük dairenin 1 TL olduğu varsayımıyla ölçeklenir.
- Yazı-tura sınıflandırması, yüklenen iki referansa döndürülmüş şablon eşleme yapar; parlama ve aşırı açı değişimi sonucu bozar.
- Otomatik eşikleme, hedef nesnenin merkeze yakın ve görüntü kenarından ayrık olduğu görüntülerde tasarlanmıştır.

Araştırma gerekçesi ve kaynaklar için [ARASTIRMA_NOTLARI.md](ARASTIRMA_NOTLARI.md) dosyasına bakın.

Ödevlere uygun görüntü ve dataset bağlantıları için [VERI_KAYNAKLARI.md](VERI_KAYNAKLARI.md) dosyasına bakın.
