#!/usr/bin/env python3
"""
Train Material AI model from ReferenceImages folders.

Output:
    C:\\Users\\RealTech\\Documents\\MaterialAI\\Model\\material_model.tflite
    C:\\Users\\RealTech\\Documents\\MaterialAI\\Model\\labels.txt
    C:\\Users\\RealTech\\Documents\\MaterialAI\\Model\\material_model.keras
    C:\\Users\\RealTech\\Documents\\MaterialAI\\Model\\training_summary.txt

Run:
    python train_material_ai_model.py
"""

from __future__ import annotations

import configparser
import os
import random
import sys
import traceback
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np

try:
    import cv2
except ImportError:
    print("Missing opencv-python. Run: pip install opencv-python")
    raise

try:
    import tensorflow as tf
except ImportError:
    print("Missing tensorflow. Run: pip install tensorflow")
    raise


# =============================================================================
# PATH + CONFIG
# =============================================================================

def documents_dir() -> Path:
    return Path(os.environ.get("USERPROFILE", str(Path.home()))) / "Documents"


SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_CONFIG_PATH = documents_dir() / "MaterialAI" / "config.ini"
PROJECT_CONFIG_PATH = SCRIPT_DIR / "config.ini"

IMG_EXT = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}


def choose_config_path() -> Path:
    if PROJECT_CONFIG_PATH.exists():
        return PROJECT_CONFIG_PATH
    return DEFAULT_CONFIG_PATH


CONFIG_PATH = choose_config_path()


def load_config() -> configparser.ConfigParser:
    cfg = configparser.ConfigParser(interpolation=None, strict=False)
    if CONFIG_PATH.exists():
        cfg.read(CONFIG_PATH, encoding="utf-8")
    return cfg


CFG = load_config()


def cstr(section: str, key: str, default: str = "") -> str:
    try:
        if CFG.has_option(section, key):
            value = CFG.get(section, key, fallback="").strip()
            return value if value else default
    except Exception:
        pass
    return default


def cint(section: str, key: str, default: int) -> int:
    try:
        return CFG.getint(section, key, fallback=default)
    except Exception:
        return default


def cfloat(section: str, key: str, default: float) -> float:
    try:
        return CFG.getfloat(section, key, fallback=default)
    except Exception:
        return default


def safe_dir(raw: str, fallback: Path) -> Path:
    raw = (raw or "").strip().strip('"')
    if not raw:
        return fallback

    low = raw.lower().replace("/", "\\")
    if "c:\\users\\youruser" in low or "c:\\users\\yourname" in low or "<your" in low:
        return fallback

    return Path(raw).expanduser()


APP_DIR = safe_dir(cstr("App", "app_data_folder", ""), documents_dir() / "MaterialAI")

REFERENCE_DIR = safe_dir(
    cstr("Storage", "reference_directory", ""),
    APP_DIR / "ReferenceImages",
)

MODEL_DIR = safe_dir(
    cstr("Storage", "model_directory", ""),
    APP_DIR / "Model",
)

IMAGE_SIZE = cint("Training", "image_size", 224)
EPOCHS = cint("Training", "epochs", 15)
BATCH_SIZE = cint("Training", "batch_size", 16)
VALIDATION_SPLIT = cfloat("Training", "validation_split", 0.20)
MIN_IMAGES_PER_MATERIAL = cint("Training", "min_images_per_material", 10)

MODEL_DIR.mkdir(parents=True, exist_ok=True)

TFLITE_PATH = MODEL_DIR / "material_model.tflite"
KERAS_PATH = MODEL_DIR / "material_model.keras"
LABELS_PATH = MODEL_DIR / "labels.txt"
SUMMARY_PATH = MODEL_DIR / "training_summary.txt"


# =============================================================================
# DATASET
# =============================================================================

def pretty_label(folder_name: str) -> str:
    name = folder_name.strip()
    name = name.replace("_", " ")
    name = " ".join(name.split())
    return name or folder_name


def is_valid_image(path: Path) -> bool:
    try:
        data = np.fromfile(str(path), dtype=np.uint8)
        img = cv2.imdecode(data, cv2.IMREAD_COLOR)
        return img is not None and img.size > 0
    except Exception:
        return False


def collect_images() -> Tuple[List[str], List[str], Dict[str, List[Path]]]:
    if not REFERENCE_DIR.exists():
        raise RuntimeError(f"ReferenceImages folder not found: {REFERENCE_DIR}")

    material_map: Dict[str, List[Path]] = {}

    for folder in sorted([p for p in REFERENCE_DIR.iterdir() if p.is_dir()], key=lambda p: p.name.lower()):
        files: List[Path] = []

        for path in sorted(folder.rglob("*")):
            if path.is_file() and path.suffix.lower() in IMG_EXT:
                if is_valid_image(path):
                    files.append(path)
                else:
                    print(f"Skipping invalid image: {path}")

        if len(files) >= MIN_IMAGES_PER_MATERIAL:
            label = pretty_label(folder.name)
            material_map[label] = files
        else:
            print(
                f"Skipping material '{folder.name}' because it has only "
                f"{len(files)} images. Minimum required: {MIN_IMAGES_PER_MATERIAL}"
            )

    labels = list(material_map.keys())

    if len(labels) < 2:
        raise RuntimeError(
            "Need at least 2 materials with enough images to train AI. "
            f"Found valid materials: {labels}"
        )

    return labels, [str(p) for paths in material_map.values() for p in paths], material_map


def split_dataset(
    labels: List[str],
    material_map: Dict[str, List[Path]],
) -> Tuple[List[str], List[int], List[str], List[int]]:
    label_to_index = {label: index for index, label in enumerate(labels)}

    train_paths: List[str] = []
    train_labels: List[int] = []
    val_paths: List[str] = []
    val_labels: List[int] = []

    rng = random.Random(42)

    split = max(0.05, min(0.40, VALIDATION_SPLIT))

    for label in labels:
        paths = material_map[label][:]
        rng.shuffle(paths)

        label_index = label_to_index[label]
        val_count = max(1, int(len(paths) * split))

        val_items = paths[:val_count]
        train_items = paths[val_count:]

        if not train_items:
            train_items = val_items[:]
            val_items = val_items[:1]

        for path in train_items:
            train_paths.append(str(path))
            train_labels.append(label_index)

        for path in val_items:
            val_paths.append(str(path))
            val_labels.append(label_index)

    combined_train = list(zip(train_paths, train_labels))
    combined_val = list(zip(val_paths, val_labels))

    rng.shuffle(combined_train)
    rng.shuffle(combined_val)

    train_paths, train_labels = zip(*combined_train)
    val_paths, val_labels = zip(*combined_val)

    return list(train_paths), list(train_labels), list(val_paths), list(val_labels)


def load_image_np(path_bytes: bytes, label: np.int32) -> Tuple[np.ndarray, np.int32]:
    path = path_bytes.decode("utf-8")

    data = np.fromfile(path, dtype=np.uint8)
    img = cv2.imdecode(data, cv2.IMREAD_COLOR)

    if img is None:
        img = np.zeros((IMAGE_SIZE, IMAGE_SIZE, 3), dtype=np.uint8)
    else:
        img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        img = cv2.resize(img, (IMAGE_SIZE, IMAGE_SIZE), interpolation=cv2.INTER_AREA)

    img = img.astype(np.float32) / 255.0
    return img, np.int32(label)


def make_dataset(paths: List[str], labels: List[int], training: bool) -> tf.data.Dataset:
    path_tensor = tf.constant(paths)
    label_tensor = tf.constant(labels, dtype=tf.int32)

    ds = tf.data.Dataset.from_tensor_slices((path_tensor, label_tensor))

    def mapper(path: tf.Tensor, label: tf.Tensor) -> Tuple[tf.Tensor, tf.Tensor]:
        image, out_label = tf.numpy_function(
            load_image_np,
            [path, label],
            [tf.float32, tf.int32],
        )
        image.set_shape((IMAGE_SIZE, IMAGE_SIZE, 3))
        out_label.set_shape(())
        return image, out_label

    ds = ds.map(mapper, num_parallel_calls=tf.data.AUTOTUNE)

    if training:
        ds = ds.shuffle(buffer_size=max(100, len(paths)), reshuffle_each_iteration=True)

    ds = ds.batch(BATCH_SIZE)
    ds = ds.prefetch(tf.data.AUTOTUNE)

    return ds


# =============================================================================
# MODEL
# =============================================================================

def build_small_cnn(num_classes: int) -> tf.keras.Model:
    inputs = tf.keras.Input(shape=(IMAGE_SIZE, IMAGE_SIZE, 3), name="image")

    x = tf.keras.layers.RandomRotation(0.04)(inputs)
    x = tf.keras.layers.RandomZoom(0.08)(x)
    x = tf.keras.layers.RandomContrast(0.10)(x)

    x = tf.keras.layers.Conv2D(32, 3, padding="same", activation="relu")(x)
    x = tf.keras.layers.MaxPooling2D()(x)

    x = tf.keras.layers.Conv2D(64, 3, padding="same", activation="relu")(x)
    x = tf.keras.layers.MaxPooling2D()(x)

    x = tf.keras.layers.Conv2D(128, 3, padding="same", activation="relu")(x)
    x = tf.keras.layers.MaxPooling2D()(x)

    x = tf.keras.layers.Conv2D(192, 3, padding="same", activation="relu")(x)
    x = tf.keras.layers.GlobalAveragePooling2D()(x)

    x = tf.keras.layers.Dropout(0.25)(x)
    outputs = tf.keras.layers.Dense(num_classes, activation="softmax", name="material")(x)

    model = tf.keras.Model(inputs, outputs, name="MaterialAI_SmallCNN")
    return model


def build_mobilenet_model(num_classes: int) -> tf.keras.Model:
    inputs = tf.keras.Input(shape=(IMAGE_SIZE, IMAGE_SIZE, 3), name="image")

    x = tf.keras.layers.RandomRotation(0.04)(inputs)
    x = tf.keras.layers.RandomZoom(0.08)(x)
    x = tf.keras.layers.RandomContrast(0.10)(x)

    # App sends image as 0.0 to 1.0.
    # MobileNetV2 expects -1.0 to 1.0.
    x = tf.keras.layers.Rescaling(scale=2.0, offset=-1.0)(x)

    base = tf.keras.applications.MobileNetV2(
        input_shape=(IMAGE_SIZE, IMAGE_SIZE, 3),
        include_top=False,
        weights="imagenet",
    )
    base.trainable = False

    x = base(x, training=False)
    x = tf.keras.layers.GlobalAveragePooling2D()(x)
    x = tf.keras.layers.Dropout(0.25)(x)
    outputs = tf.keras.layers.Dense(num_classes, activation="softmax", name="material")(x)

    model = tf.keras.Model(inputs, outputs, name="MaterialAI_MobileNetV2")
    return model


def build_model(num_classes: int) -> Tuple[tf.keras.Model, str]:
    try:
        model = build_mobilenet_model(num_classes)
        return model, "MobileNetV2 transfer learning"
    except Exception as exc:
        print("MobileNetV2 ImageNet model could not be loaded.")
        print(f"Reason: {exc}")
        print("Using small CNN fallback model.")
        model = build_small_cnn(num_classes)
        return model, "Small CNN fallback"


def train_model(
    model: tf.keras.Model,
    train_ds: tf.data.Dataset,
    val_ds: tf.data.Dataset,
) -> tf.keras.callbacks.History:
    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=0.001),
        loss=tf.keras.losses.SparseCategoricalCrossentropy(),
        metrics=["accuracy"],
    )

    callbacks = [
        tf.keras.callbacks.EarlyStopping(
            monitor="val_accuracy",
            patience=5,
            restore_best_weights=True,
        ),
        tf.keras.callbacks.ReduceLROnPlateau(
            monitor="val_loss",
            factor=0.5,
            patience=2,
            min_lr=0.00001,
        ),
    ]

    history = model.fit(
        train_ds,
        validation_data=val_ds,
        epochs=EPOCHS,
        callbacks=callbacks,
    )

    return history


def convert_to_tflite(model: tf.keras.Model) -> bytes:
    converter = tf.lite.TFLiteConverter.from_keras_model(model)
    converter.optimizations = [tf.lite.Optimize.DEFAULT]
    return converter.convert()


# =============================================================================
# MAIN
# =============================================================================

def write_summary(
    labels: List[str],
    material_map: Dict[str, List[Path]],
    model_type: str,
    train_count: int,
    val_count: int,
    history: tf.keras.callbacks.History,
) -> None:
    best_val_accuracy = None
    best_train_accuracy = None

    if "val_accuracy" in history.history and history.history["val_accuracy"]:
        best_val_accuracy = max(history.history["val_accuracy"])

    if "accuracy" in history.history and history.history["accuracy"]:
        best_train_accuracy = max(history.history["accuracy"])

    lines = [
        "Material AI Training Summary",
        "============================",
        "",
        f"Config path      : {CONFIG_PATH}",
        f"Reference folder : {REFERENCE_DIR}",
        f"Model folder     : {MODEL_DIR}",
        f"Model type       : {model_type}",
        f"Image size       : {IMAGE_SIZE}",
        f"Epochs requested : {EPOCHS}",
        f"Batch size       : {BATCH_SIZE}",
        f"Train images     : {train_count}",
        f"Validation images: {val_count}",
        f"Best train acc   : {best_train_accuracy}",
        f"Best val acc     : {best_val_accuracy}",
        "",
        "Materials:",
    ]

    for label in labels:
        lines.append(f"  - {label}: {len(material_map[label])} images")

    lines.extend([
        "",
        f"Saved Keras model: {KERAS_PATH}",
        f"Saved TFLite     : {TFLITE_PATH}",
        f"Saved labels     : {LABELS_PATH}",
        "",
    ])

    SUMMARY_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    print("============================================================")
    print("Material AI Training")
    print("============================================================")
    print(f"TensorFlow       : {tf.__version__}")
    print(f"Config path      : {CONFIG_PATH}")
    print(f"Reference folder : {REFERENCE_DIR}")
    print(f"Model folder     : {MODEL_DIR}")
    print("============================================================")
    print()

    labels, _all_paths, material_map = collect_images()

    train_paths, train_labels, val_paths, val_labels = split_dataset(labels, material_map)

    print("Training materials:")
    for label in labels:
        print(f"  {label}: {len(material_map[label])} images")

    print()
    print(f"Train images     : {len(train_paths)}")
    print(f"Validation images: {len(val_paths)}")
    print()

    LABELS_PATH.write_text("\n".join(labels), encoding="utf-8")
    print(f"Labels saved: {LABELS_PATH}")

    train_ds = make_dataset(train_paths, train_labels, training=True)
    val_ds = make_dataset(val_paths, val_labels, training=False)

    model, model_type = build_model(num_classes=len(labels))
    print(f"Model type: {model_type}")
    print()

    history = train_model(model, train_ds, val_ds)

    print()
    print("Saving Keras model...")
    model.save(KERAS_PATH)

    print("Converting to TFLite...")
    tflite_data = convert_to_tflite(model)
    TFLITE_PATH.write_bytes(tflite_data)

    write_summary(
        labels=labels,
        material_map=material_map,
        model_type=model_type,
        train_count=len(train_paths),
        val_count=len(val_paths),
        history=history,
    )

    print()
    print("============================================================")
    print("Training completed successfully")
    print("============================================================")
    print(f"TFLite model: {TFLITE_PATH}")
    print(f"Labels      : {LABELS_PATH}")
    print(f"Summary     : {SUMMARY_PATH}")
    print()
    print("Now set this in config.ini:")
    print()
    print("[AI]")
    print("enabled = yes")
    print("input_normalization = zero_to_one")
    print("============================================================")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print()
        print("============================================================")
        print("Training failed")
        print("============================================================")
        print(str(exc))
        print()
        traceback.print_exc()
        sys.exit(1)