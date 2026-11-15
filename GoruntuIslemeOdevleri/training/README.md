# Bozuk Para Model Eğitimi

Roboflow'dan indirilen **YOLOv8** ZIP dosyasıyla 1 TL, 50 kuruş, 25 kuruş,
10 kuruş ve 5 kuruş nesne tespit modeli eğitilir.

## Tek komut

PowerShell'de proje klasöründeyken:

```powershell
.\training\run_training.ps1 -DatasetZip "$HOME\Downloads\roboflow.zip" -Epochs 60
```

`data/coins_v2/data.local.yaml` varsa mevcut veri doğrudan kullanılır. Yoksa
`-DatasetZip` ile verilen ZIP açılır. ZIP yolu verilmezse İndirilenler
klasöründeki en yeni ZIP seçilir.

## Çıktılar

- `data/coins_v2/data.local.yaml`: Yerel yolları düzeltilmiş veri yapılandırması
- `training/dataset_report.json`: Görüntü, etiket, sınıf ve kutu doğrulama raporu
- `training/runs/turk_bozuk_para_yolov8n/weights/best.pt`: En iyi PyTorch ağırlığı
- `training/runs/turk_bozuk_para_yolov8n/weights/best.onnx`: C# için ONNX model
- `training/runs/turk_bozuk_para_yolov8n/training_summary.json`: mAP, precision ve recall
- Aynı klasörde confusion matrix, PR eğrisi ve örnek tahmin görselleri

60 epoch, RTX 4060 Laptop GPU için başlangıç ayarıdır. Erken durdurma 15 epoch
boyunca iyileşme olmazsa eğitimi sonlandırır.
