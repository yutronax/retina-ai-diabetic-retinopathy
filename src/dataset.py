from pathlib import Path

from torchvision import datasets, transforms
from torch.utils.data import DataLoader


def build_transforms(image_size: int, train: bool) -> transforms.Compose:
    normalize = transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225])

    if train:
        return transforms.Compose(
            [
                transforms.RandomResizedCrop(image_size, scale=(0.85, 1.0)),
                transforms.RandomHorizontalFlip(),
                transforms.RandomRotation(15),
                transforms.ColorJitter(brightness=0.2, contrast=0.2, saturation=0.1),
                transforms.ToTensor(),
                normalize,
            ]
        )

    return transforms.Compose(
        [
            transforms.Resize((image_size, image_size)),
            transforms.ToTensor(),
            normalize,
        ]
    )


def build_dataloaders(data_dir: str | Path, image_size: int, batch_size: int, num_workers: int):
    data_dir = Path(data_dir)

    train_ds = datasets.ImageFolder(data_dir / "train", transform=build_transforms(image_size, train=True))
    val_ds = datasets.ImageFolder(data_dir / "val", transform=build_transforms(image_size, train=False))
    test_ds = datasets.ImageFolder(data_dir / "test", transform=build_transforms(image_size, train=False))

    # ImageFolder sınıfları alfabetik sıralar; config'teki class_names ile tutarlılığı
    # çağıran kodun (train.py) doğrulaması beklenir.
    train_loader = DataLoader(train_ds, batch_size=batch_size, shuffle=True, num_workers=num_workers)
    val_loader = DataLoader(val_ds, batch_size=batch_size, shuffle=False, num_workers=num_workers)
    test_loader = DataLoader(test_ds, batch_size=batch_size, shuffle=False, num_workers=num_workers)

    return train_loader, val_loader, test_loader, train_ds.classes
