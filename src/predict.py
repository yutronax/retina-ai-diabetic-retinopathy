"""Tek bir göz dibi görüntüsü üzerinde tahmin.

Kullanım:
    python -m src.predict --image path/to/image.jpg --checkpoint outputs/checkpoints/best_model.pt
"""

import argparse

import torch
from PIL import Image

from src.dataset import build_transforms
from src.model import build_model
from src.utils import get_device, load_config


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--config", default="configs/config.yaml")
    args = parser.parse_args()

    cfg = load_config(args.config)
    device = get_device()

    checkpoint = torch.load(args.checkpoint, map_location=device)
    classes = checkpoint["classes"]

    model = build_model(cfg["model"]["arch"], cfg["data"]["num_classes"], pretrained=False)
    model.load_state_dict(checkpoint["model_state"])
    model.to(device)
    model.eval()

    transform = build_transforms(cfg["data"]["image_size"], train=False)
    image = Image.open(args.image).convert("RGB")
    tensor = transform(image).unsqueeze(0).to(device)

    with torch.no_grad():
        logits = model(tensor)
        probs = torch.softmax(logits, dim=1).squeeze(0).cpu()

    pred_idx = int(probs.argmax())
    print(f"Tahmin: {classes[pred_idx]} (güven: {probs[pred_idx]:.4f})")
    print("Tüm sınıf olasılıkları:")
    for cls_name, prob in sorted(zip(classes, probs.tolist()), key=lambda x: -x[1]):
        print(f"  {cls_name:<20} {prob:.4f}")


if __name__ == "__main__":
    main()
