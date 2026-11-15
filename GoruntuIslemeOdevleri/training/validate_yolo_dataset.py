from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path


IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
CLASS_NAMES = {
    0: "1 lira",
    1: "10 kurus",
    2: "25 kurus",
    3: "5 kurus",
    4: "50 kurus",
}


def validate_split(root: Path, split: str) -> dict[str, object]:
    images_dir = root / split / "images"
    labels_dir = root / split / "labels"
    images = {
        path.stem: path
        for path in images_dir.iterdir()
        if path.is_file() and path.suffix.lower() in IMAGE_SUFFIXES
    }
    labels = {path.stem: path for path in labels_dir.glob("*.txt")}
    class_counts: Counter[int] = Counter()
    invalid_rows: list[str] = []
    empty_labels = 0

    for stem, label_path in labels.items():
        lines = [line.strip() for line in label_path.read_text(encoding="utf-8").splitlines() if line.strip()]
        if not lines:
            empty_labels += 1
        for line_number, line in enumerate(lines, start=1):
            parts = line.split()
            if len(parts) != 5:
                invalid_rows.append(f"{label_path.name}:{line_number}: alan_sayisi={len(parts)}")
                continue
            try:
                class_id = int(parts[0])
                x, y, width, height = (float(value) for value in parts[1:])
            except ValueError:
                invalid_rows.append(f"{label_path.name}:{line_number}: sayisal_degil")
                continue
            if class_id not in CLASS_NAMES:
                invalid_rows.append(f"{label_path.name}:{line_number}: sinif={class_id}")
                continue
            if not all(0.0 <= value <= 1.0 for value in (x, y, width, height)):
                invalid_rows.append(f"{label_path.name}:{line_number}: koordinat_aralik_disinda")
                continue
            if width <= 0.0 or height <= 0.0:
                invalid_rows.append(f"{label_path.name}:{line_number}: sifir_boyut")
                continue
            class_counts[class_id] += 1

    return {
        "images": len(images),
        "labels": len(labels),
        "objects": sum(class_counts.values()),
        "class_counts": {CLASS_NAMES[key]: class_counts[key] for key in CLASS_NAMES},
        "missing_labels": sorted(set(images) - set(labels)),
        "orphan_labels": sorted(set(labels) - set(images)),
        "empty_labels": empty_labels,
        "invalid_rows": invalid_rows,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="YOLO veri seti etiketlerini bagimlilik olmadan dogrular.")
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    root = args.root.resolve()
    report = {
        "dataset_root": str(root),
        "class_names": CLASS_NAMES,
        "splits": {split: validate_split(root, split) for split in ("train", "valid", "test")},
    }
    report["totals"] = {
        "images": sum(data["images"] for data in report["splits"].values()),
        "labels": sum(data["labels"] for data in report["splits"].values()),
        "objects": sum(data["objects"] for data in report["splits"].values()),
        "invalid_rows": sum(len(data["invalid_rows"]) for data in report["splits"].values()),
        "missing_labels": sum(len(data["missing_labels"]) for data in report["splits"].values()),
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report["totals"], ensure_ascii=False, indent=2))
    for split, data in report["splits"].items():
        print(f"{split}: {data['class_counts']}")
    if report["totals"]["invalid_rows"] or report["totals"]["missing_labels"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()

