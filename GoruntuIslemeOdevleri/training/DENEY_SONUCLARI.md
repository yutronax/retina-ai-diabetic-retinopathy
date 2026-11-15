# Bozuk Para Veri Seti ve Eğitim Durumu

## Veri doğrulaması

Roboflow **Bozuk Paralar v2** veri seti YOLOv8 biçiminde indirildi ve yerel
olarak doğrulandı.

| Bölüm | Görüntü | Etiket | Nesne |
|---|---:|---:|---:|
| Eğitim | 2.409 | 2.409 | 23.082 |
| Doğrulama | 229 | 229 | 2.156 |
| Test | 115 | 115 | 1.112 |
| **Toplam** | **2.753** | **2.753** | **26.350** |

Eksik etiket: **0**  
Geçersiz YOLO kutusu: **0**

## Sınıf dağılımı

| Sınıf | Eğitim | Doğrulama | Test | Toplam |
|---|---:|---:|---:|---:|
| 1 lira | 4.887 | 429 | 231 | 5.547 |
| 10 kuruş | 4.107 | 393 | 202 | 4.702 |
| 25 kuruş | 6.288 | 561 | 298 | 7.147 |
| 5 kuruş | 3.462 | 354 | 183 | 3.999 |
| 50 kuruş | 4.338 | 419 | 198 | 4.955 |

## Eğitim ayarları

- Model: YOLOv8n
- Görüntü boyutu: 640 px
- Epoch: 60
- Batch: 16
- Erken durdurma: 15 epoch
- Tohum: 42
- Donanım: NVIDIA GeForce RTX 4060 Laptop GPU, 8 GB VRAM

## Beklenen çıktı

Eğitim tamamlandığında aşağıdaki değerler `training_summary.json` dosyasından
bu bölüme aktarılacaktır:

| Ölçüt | Sonuç |
|---|---:|
| mAP50 | Eğitim bekleniyor |
| mAP50-95 | Eğitim bekleniyor |
| Precision | Eğitim bekleniyor |
| Recall | Eğitim bekleniyor |

Kaynak: https://universe.roboflow.com/abdulkadir-caglar/bozuk-paralar/dataset/2
