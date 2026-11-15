from __future__ import annotations

import argparse
from pathlib import Path

from ultralytics import YOLO


def main() -> None:
    parser = argparse.ArgumentParser(description="Egitilmis para modelini goruntulerde test eder.")
    parser.add_argument("--weights", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path("training/predictions"))
    parser.add_argument("--confidence", type=float, default=0.35)
    args = parser.parse_args()

    if not args.weights.is_file():
        raise FileNotFoundError(args.weights)
    if not args.source.exists():
        raise FileNotFoundError(args.source)
    model = YOLO(str(args.weights.resolve()))
    model.predict(
        source=str(args.source.resolve()),
        conf=args.confidence,
        save=True,
        project=str(args.output.resolve()),
        name="results",
        exist_ok=True,
    )


if __name__ == "__main__":
    main()

