from __future__ import annotations

import argparse
import shutil
import zipfile
from collections import Counter
from pathlib import Path

import yaml


IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}


def safe_extract(archive: Path, destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    root = destination.resolve()
    with zipfile.ZipFile(archive) as zf:
        for member in zf.infolist():
            target = (destination / member.filename).resolve()
            if root != target and root not in target.parents:
                raise ValueError(f"Guvenli olmayan ZIP yolu: {member.filename}")
        zf.extractall(destination)


def find_data_yaml(root: Path) -> Path:
    candidates = sorted(root.rglob("data.yaml"), key=lambda p: len(p.parts))
    if not candidates:
        candidates = sorted(root.rglob("*.yaml"), key=lambda p: len(p.parts))
    for candidate in candidates:
        try:
            config = yaml.safe_load(candidate.read_text(encoding="utf-8")) or {}
        except (UnicodeDecodeError, yaml.YAMLError):
            continue
        if "names" in config and ("train" in config or "val" in config):
            return candidate
    raise FileNotFoundError("ZIP icinde YOLO data.yaml dosyasi bulunamadi.")


def normalize_names(names: object) -> dict[int, str]:
    if isinstance(names, list):
        pairs = enumerate(str(name) for name in names)
    elif isinstance(names, dict):
        pairs = ((int(key), str(value)) for key, value in names.items())
    else:
        raise ValueError("data.yaml icindeki names alani gecersiz.")
    return {index: name.strip() for index, name in pairs}


def resolve_split(dataset_root: Path, value: object) -> Path | None:
    if not isinstance(value, str):
        return None
    path = Path(value)
    if not path.is_absolute():
        path = (dataset_root / path).resolve()
    return path


def count_split(images_path: Path | None) -> tuple[int, Counter[int], int]:
    if images_path is None or not images_path.exists():
        return 0, Counter(), 0
    images = [p for p in images_path.rglob("*") if p.suffix.lower() in IMAGE_SUFFIXES]
    labels_path = Path(str(images_path).replace("images", "labels"))
    class_counts: Counter[int] = Counter()
    missing_labels = 0
    for image in images:
        relative = image.relative_to(images_path).with_suffix(".txt")
        label = labels_path / relative
        if not label.exists():
            missing_labels += 1
            continue
        for line in label.read_text(encoding="utf-8", errors="replace").splitlines():
            parts = line.split()
            if not parts:
                continue
            try:
                class_counts[int(parts[0])] += 1
            except ValueError as exc:
                raise ValueError(f"Gecersiz etiket satiri: {label}: {line}") from exc
    return len(images), class_counts, missing_labels


def main() -> None:
    parser = argparse.ArgumentParser(description="Roboflow YOLO veri setini acar ve dogrular.")
    parser.add_argument("--zip", type=Path, required=True, help="Indirilen Roboflow ZIP dosyasi")
    parser.add_argument("--output", type=Path, default=Path("data/coins"))
    parser.add_argument("--force", action="store_true", help="Mevcut cikti klasorunu yeniden olustur")
    args = parser.parse_args()

    archive = args.zip.resolve()
    output = args.output.resolve()
    if not archive.is_file():
        raise FileNotFoundError(archive)
    if not zipfile.is_zipfile(archive):
        raise ValueError(f"Gecerli ZIP degil: {archive}")
    if output.exists() and args.force:
        shutil.rmtree(output)
    if not output.exists():
        safe_extract(archive, output)

    source_yaml = find_data_yaml(output)
    config = yaml.safe_load(source_yaml.read_text(encoding="utf-8")) or {}
    names = normalize_names(config.get("names"))
    dataset_root = source_yaml.parent.resolve()

    normalized = dict(config)
    normalized["path"] = str(dataset_root)
    for split in ("train", "val", "test"):
        split_path = resolve_split(dataset_root, config.get(split))
        if split_path is not None:
            normalized[split] = str(split_path)

    output_yaml = output / "data.local.yaml"
    output_yaml.write_text(
        yaml.safe_dump(normalized, allow_unicode=True, sort_keys=False), encoding="utf-8"
    )

    print(f"Veri YAML : {output_yaml}")
    print(f"Siniflar  : {names}")
    total_images = 0
    total_instances: Counter[int] = Counter()
    for split in ("train", "val", "test"):
        path = Path(normalized[split]) if split in normalized else None
        image_count, instances, missing = count_split(path)
        total_images += image_count
        total_instances.update(instances)
        print(
            f"{split:5}: {image_count:4} goruntu, {sum(instances.values()):5} nesne, "
            f"etiketsiz={missing}"
        )

    unknown = sorted(set(total_instances) - set(names))
    if unknown:
        raise ValueError(f"data.yaml'da tanimsiz sinif kimlikleri var: {unknown}")
    if total_images == 0:
        raise ValueError("Veri setinde goruntu bulunamadi.")
    for class_id, name in names.items():
        print(f"  {class_id}: {name}: {total_instances[class_id]} ornek")


if __name__ == "__main__":
    main()

