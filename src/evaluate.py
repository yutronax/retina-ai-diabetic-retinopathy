"""Test seti üzerinde değerlendirme: accuracy, precision/recall/F1 ve karışıklık matrisi.

Kullanım:
    python -m src.evaluate --data-dir data/splits --checkpoint outputs/checkpoints/best_model.pt
"""

import argparse

import matplotlib.pyplot as plt
import seaborn as sns
import torch
from sklearn.metrics import classification_report, confusion_matrix

from src.dataset import build_dataloaders
from src.model import build_model
from src.utils import get_device, load_config


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--config", default="configs/config.yaml")
    parser.add_argument("--out-fig", default="outputs/logs/confusion_matrix.png")
    args = parser.parse_args()

    cfg = load_config(args.config)
    device = get_device()

    checkpoint = torch.load(args.checkpoint, map_location=device)
    classes = checkpoint["classes"]

    _, _, test_loader, _ = build_dataloaders(
        args.data_dir,
        cfg["data"]["image_size"],
        cfg["data"]["batch_size"],
        cfg["data"]["num_workers"],
    )

    model = build_model(cfg["model"]["arch"], cfg["data"]["num_classes"], pretrained=False)
    model.load_state_dict(checkpoint["model_state"])
    model.to(device)
    model.eval()

    all_preds, all_labels = [], []
    with torch.no_grad():
        for images, labels in test_loader:
            images = images.to(device)
            outputs = model(images)
            preds = outputs.argmax(dim=1).cpu()
            all_preds.extend(preds.tolist())
            all_labels.extend(labels.tolist())

    print(classification_report(all_labels, all_preds, target_names=classes, digits=4))

    cm = confusion_matrix(all_labels, all_preds)
    plt.figure(figsize=(8, 6))
    sns.heatmap(cm, annot=True, fmt="d", cmap="Blues", xticklabels=classes, yticklabels=classes)
    plt.xlabel("Tahmin")
    plt.ylabel("Gerçek")
    plt.title("Karışıklık Matrisi")
    plt.tight_layout()
    plt.savefig(args.out_fig)
    print(f"Karışıklık matrisi kaydedildi: {args.out_fig}")


if __name__ == "__main__":
    main()
