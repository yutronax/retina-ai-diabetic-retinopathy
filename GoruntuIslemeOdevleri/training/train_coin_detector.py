from __future__ import annotations

import argparse
import json
from pathlib import Path

import torch
from ultralytics import YOLO


def main() -> None:
    parser = argparse.ArgumentParser(description="Turk bozuk paralari icin YOLOv8 egitimi.")
    parser.add_argument("--data", type=Path, default=Path("data/coins/data.local.yaml"))
    parser.add_argument("--model", default="yolov8n.pt")
    parser.add_argument("--epochs", type=int, default=60)
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--batch", type=int, default=16)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--project", type=Path, default=Path("training/runs"))
    parser.add_argument("--name", default="turk_bozuk_para_yolov8n")
    parser.add_argument("--device", default=None)
    args = parser.parse_args()

    data = args.data.resolve()
    if not data.is_file():
        raise FileNotFoundError(f"Veri YAML bulunamadi: {data}")
    device = args.device if args.device is not None else ("0" if torch.cuda.is_available() else "cpu")
    print(f"Egitim aygiti: {device}")
    if torch.cuda.is_available():
        print(f"GPU: {torch.cuda.get_device_name(0)}")

    model = YOLO(args.model)
    results = model.train(
        data=str(data),
        epochs=args.epochs,
        imgsz=args.imgsz,
        batch=args.batch,
        workers=args.workers,
        device=device,
        project=str(args.project.resolve()),
        name=args.name,
        exist_ok=True,
        pretrained=True,
        patience=15,
        seed=42,
        deterministic=True,
        cache=False,
        plots=True,
    )

    best = Path(results.save_dir) / "weights" / "best.pt"
    if not best.is_file():
        raise FileNotFoundError(f"Egitim tamamlandi ancak best.pt yok: {best}")

    best_model = YOLO(str(best))
    metrics = best_model.val(data=str(data), device=device, plots=True)
    onnx_path = Path(
        best_model.export(format="onnx", imgsz=args.imgsz, simplify=True, dynamic=False, opset=12)
    )
    summary = {
        "weights": str(best.resolve()),
        "onnx": str(onnx_path.resolve()),
        "map50": float(metrics.box.map50),
        "map50_95": float(metrics.box.map),
        "precision": float(metrics.box.mp),
        "recall": float(metrics.box.mr),
        "device": device,
    }
    summary_path = Path(results.save_dir) / "training_summary.json"
    summary_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(summary, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()

