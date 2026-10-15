# RETINA-AI: Göz Dibi Fotoğraflarından Çok Sınıflı Diyabetik Retinopati Tespiti ve Sınıflandırma

Göz dibi (fundus) fotoğraflarından diyabetik retinopatinin (DR) varlığını ve şiddet derecesini otomatik olarak
sınıflandıran bir derin öğrenme sistemi. Transfer learning tabanlı bir CNN (EfficientNet) ile 5 sınıflı
sınıflandırma yapılır:

| Sınıf | Etiket |
|---|---|
| 0 | No_DR (DR yok) |
| 1 | Mild (Hafif) |
| 2 | Moderate (Orta) |
| 3 | Severe (Şiddetli) |
| 4 | Proliferate_DR (Proliferatif DR) |

## Veri Seti

[Diabetic Retinopathy (Balanced) — Kaggle](https://www.kaggle.com/datasets/kushagratandon12/diabetic-retinopathy-balanced)

Veri seti sınıf başına dengelenmiş fundus görüntülerinden oluşur, `train/<sınıf_adı>/*.jpg` klasör yapısındadır
(PyTorch `ImageFolder` ile doğrudan uyumlu).

### İndirme

```bash
pip install kaggle
# ~/.kaggle/kaggle.json içinde Kaggle API anahtarınız olmalı
kaggle datasets download -d kushagratandon12/diabetic-retinopathy-balanced -p data/raw --unzip
```

İndirilen veriyi şu şekilde düzenleyin (çoğu dağıtımda zaten bu yapıdadır):

```
data/
  raw/
    train/
      No_DR/
      Mild/
      Moderate/
      Severe/
      Proliferate_DR/
    val/        # yoksa src/prepare_data.py otomatik train'den ayırır
      ...
```

Veri seti düzeninizin farklı olması durumunda `src/prepare_data.py` scripti otomatik train/val/test bölmesi
oluşturur.

## Kurulum

```bash
python -m venv .venv
.venv\Scripts\activate   # Windows
pip install -r requirements.txt
```

## Kullanım

```bash
# 1) Veriyi train/val/test olarak böl (raw klasöründen data/splits altına)
python -m src.prepare_data --raw-dir data/raw --out-dir data/splits

# 2) Modeli eğit
python -m src.train --data-dir data/splits --config configs/config.yaml

# 3) Test setinde değerlendir (karışıklık matrisi + metrikler)
python -m src.evaluate --data-dir data/splits --checkpoint outputs/checkpoints/best_model.pt

# 4) Tek bir görüntü üzerinde tahmin
python -m src.predict --image path/to/image.jpg --checkpoint outputs/checkpoints/best_model.pt
```

## Proje Yapısı

```
src/
  dataset.py       # DRDataset (PyTorch Dataset) + transform/augmentation
  model.py          # EfficientNet tabanlı transfer learning modeli
  prepare_data.py   # train/val/test bölme scripti
  train.py          # eğitim döngüsü, checkpoint, early stopping
  evaluate.py        # test seti değerlendirme (accuracy, F1, confusion matrix)
  predict.py         # tek görüntü üzerinde çıkarım
  utils.py            # ortak yardımcı fonksiyonlar (seed, config yükleme, metrikler)
configs/
  config.yaml         # eğitim hiperparametreleri
outputs/
  checkpoints/        # eğitilmiş model ağırlıkları (git'e dahil değil)
  logs/                # eğitim logları / metrikler (git'e dahil değil)
```

## Hızlı Doğrulama (Smoke Test)

Tam veri seti/eğitim öncesi pipeline'ın uçtan uca çalıştığını doğrulamak için küçük bir örneklemle hızlı test:

```bash
# data/raw/.../train|val|test/<sınıf>/ altından her sınıftan birkaç görüntü kopyalayıp
# data/smoke altında küçük bir örneklem oluşturun, sonra:
python -m src.train --data-dir data/smoke --config configs/smoke.yaml
python -m src.evaluate --data-dir data/smoke --checkpoint outputs/checkpoints_smoke/best_model.pt --config configs/smoke.yaml
```

`configs/smoke.yaml`, 2 epoch ve küçük batch ile hızlı doğrulama için ayrı bir config'tir; gerçek eğitimde
`configs/config.yaml` kullanılmalıdır. Bu proje üzerinde 200 görüntülük bir örneklemle pipeline (eğitim →
değerlendirme → tahmin) uçtan uca doğrulanmıştır.

## Notlar

- Model mimarisi `configs/config.yaml` üzerinden `efficientnet_b0` / `resnet50` arasında değiştirilebilir.
- Sağlık verisi ile çalışırken: bu model **klinik karar desteği amaçlı değildir**, araştırma/eğitim amaçlıdır.
- Sınıf dengesizliği durumunda `train.py` otomatik olarak ağırlıklı kayıp fonksiyonu (weighted CrossEntropyLoss)
  kullanır.
