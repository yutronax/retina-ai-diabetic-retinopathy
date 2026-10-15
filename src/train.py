"""Eğitim döngüsü.

Kullanım:
    python -m src.train --data-dir data/splits --config configs/config.yaml
"""

import argparse
import json
from collections import Counter
from pathlib import Path

import torch
import torch.nn as nn
from torch.optim import AdamW
from tqdm import tqdm

from src.dataset import build_dataloaders
from src.model import build_model
from src.utils import ensure_dir, get_device, load_config, set_seed


def compute_class_weights(dataset, num_classes: int) -> torch.Tensor:
    counts = Counter(label for _, label in dataset.samples)
    total = sum(counts.values())
    weights = [total / (num_classes * counts.get(c, 1)) for c in range(num_classes)]
    return torch.tensor(weights, dtype=torch.float32)


def run_epoch(model, loader, criterion, optimizer, device, train: bool) -> tuple[float, float]:
    model.train(mode=train)
    total_loss, correct, total = 0.0, 0, 0

    context = torch.enable_grad() if train else torch.no_grad()
    with context:
        for images, labels in tqdm(loader, leave=False):
            images, labels = images.to(device), labels.to(device)

            if train:
                optimizer.zero_grad()

            outputs = model(images)
            loss = criterion(outputs, labels)

            if train:
                loss.backward()
                optimizer.step()

            total_loss += loss.item() * images.size(0)
            correct += (outputs.argmax(dim=1) == labels).sum().item()
            total += images.size(0)

    return total_loss / total, correct / total


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", required=True)
    parser.add_argument("--config", default="configs/config.yaml")
    args = parser.parse_args()

    cfg = load_config(args.config)
    set_seed(cfg["seed"])
    device = get_device()
    print(f"Cihaz: {device}")

    train_loader, val_loader, _, classes = build_dataloaders(
        args.data_dir,
        cfg["data"]["image_size"],
        cfg["data"]["batch_size"],
        cfg["data"]["num_workers"],
    )
    print(f"Sınıflar (ImageFolder sırasına göre): {classes}")

    model = build_model(
        cfg["model"]["arch"],
        cfg["data"]["num_classes"],
        cfg["model"]["pretrained"],
        cfg["model"]["dropout"],
    ).to(device)

    if cfg["train"]["use_class_weights"]:
        class_weights = compute_class_weights(train_loader.dataset, cfg["data"]["num_classes"]).to(device)
        criterion = nn.CrossEntropyLoss(weight=class_weights)
    else:
        criterion = nn.CrossEntropyLoss()

    optimizer = AdamW(
        model.parameters(), lr=cfg["train"]["lr"], weight_decay=cfg["train"]["weight_decay"]
    )

    ckpt_dir = ensure_dir(cfg["train"]["checkpoint_dir"])
    log_dir = ensure_dir(cfg["train"]["log_dir"])

    best_val_acc = 0.0
    epochs_no_improve = 0
    history = []

    for epoch in range(1, cfg["train"]["epochs"] + 1):
        train_loss, train_acc = run_epoch(model, train_loader, criterion, optimizer, device, train=True)
        val_loss, val_acc = run_epoch(model, val_loader, criterion, optimizer, device, train=False)

        print(
            f"Epoch {epoch}/{cfg['train']['epochs']} "
            f"| train_loss={train_loss:.4f} train_acc={train_acc:.4f} "
            f"| val_loss={val_loss:.4f} val_acc={val_acc:.4f}"
        )
        history.append(
            {"epoch": epoch, "train_loss": train_loss, "train_acc": train_acc, "val_loss": val_loss, "val_acc": val_acc}
        )

        if val_acc > best_val_acc:
            best_val_acc = val_acc
            epochs_no_improve = 0
            torch.save(
                {"model_state": model.state_dict(), "classes": classes, "config": cfg},
                Path(ckpt_dir) / "best_model.pt",
            )
            print(f"  -> yeni en iyi model kaydedildi (val_acc={val_acc:.4f})")
        else:
            epochs_no_improve += 1
            if epochs_no_improve >= cfg["train"]["patience"]:
                print(f"Early stopping: {cfg['train']['patience']} epoch boyunca iyileşme yok.")
                break

    with open(Path(log_dir) / "history.json", "w", encoding="utf-8") as f:
        json.dump(history, f, ensure_ascii=False, indent=2)

    print(f"Eğitim tamamlandı. En iyi val_acc={best_val_acc:.4f}")


if __name__ == "__main__":
    main()
