"""Kaggle'dan indirilen ham veri setini (sınıf-başına klasör yapısı) train/val/test
olarak böler ve data/splits/{train,val,test}/<sınıf>/ altına kopyalar/sembolik link yapar.

Kullanım:
    python -m src.prepare_data --raw-dir data/raw --out-dir data/splits
"""

import argparse
import random
import shutil
from pathlib import Path

IMAGE_EXTS = {".jpg", ".jpeg", ".png"}


def find_class_dirs(raw_dir: Path) -> list[Path]:
    # Hem "raw_dir/train/<sınıf>" hem de doğrudan "raw_dir/<sınıf>" yapısını destekler.
    candidate = raw_dir / "train"
    base = candidate if candidate.is_dir() else raw_dir
    class_dirs = [d for d in sorted(base.iterdir()) if d.is_dir()]
    if not class_dirs:
        raise FileNotFoundError(
            f"{base} altında sınıf klasörü bulunamadı. Beklenen yapı: <raw_dir>/train/<sınıf_adı>/*.jpg"
        )
    return class_dirs


def split_files(files: list[Path], val_ratio: float, test_ratio: float, seed: int):
    rng = random.Random(seed)
    files = files[:]
    rng.shuffle(files)
    n = len(files)
    n_val = int(n * val_ratio)
    n_test = int(n * test_ratio)
    val_files = files[:n_val]
    test_files = files[n_val : n_val + n_test]
    train_files = files[n_val + n_test :]
    return train_files, val_files, test_files


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--raw-dir", required=True)
    parser.add_argument("--out-dir", required=True)
    parser.add_argument("--val-ratio", type=float, default=0.15)
    parser.add_argument("--test-ratio", type=float, default=0.15)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--copy", action="store_true", help="Sembolik link yerine dosyaları kopyala")
    args = parser.parse_args()

    raw_dir = Path(args.raw_dir)
    out_dir = Path(args.out_dir)
    class_dirs = find_class_dirs(raw_dir)

    for split in ("train", "val", "test"):
        for cls_dir in class_dirs:
            (out_dir / split / cls_dir.name).mkdir(parents=True, exist_ok=True)

    summary = {}
    for cls_dir in class_dirs:
        files = [p for p in cls_dir.iterdir() if p.suffix.lower() in IMAGE_EXTS]
        train_files, val_files, test_files = split_files(files, args.val_ratio, args.test_ratio, args.seed)

        for split, split_files_ in (("train", train_files), ("val", val_files), ("test", test_files)):
            dest_dir = out_dir / split / cls_dir.name
            for src in split_files_:
                dest = dest_dir / src.name
                if dest.exists():
                    continue
                if args.copy:
                    shutil.copy2(src, dest)
                else:
                    try:
                        dest.symlink_to(src.resolve())
                    except OSError:
                        # Windows'ta sembolik link izni yoksa kopyalamaya düş
                        shutil.copy2(src, dest)

        summary[cls_dir.name] = (len(train_files), len(val_files), len(test_files))

    print(f"{'Sınıf':<20} {'train':>8} {'val':>8} {'test':>8}")
    for cls_name, (n_train, n_val, n_test) in summary.items():
        print(f"{cls_name:<20} {n_train:>8} {n_val:>8} {n_test:>8}")


if __name__ == "__main__":
    main()
