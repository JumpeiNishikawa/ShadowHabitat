"""Entry point: run the 4-corner calibration tool.

Usage:
    python calibrate.py [--config config.json]
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from src.camera import Camera
from src.calibration import run_interactive_calibration


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--config", default="config.json")
    args = p.parse_args()

    cfg_path = Path(args.config)
    cfg = json.loads(cfg_path.read_text(encoding="utf-8"))

    cam_cfg = cfg["camera"]
    surf = cfg["surface"]
    out = cfg["calibration"]["file"]

    with Camera(cam_cfg["device_id"], cam_cfg["width"], cam_cfg["height"], cam_cfg["fps"]) as cam:
        run_interactive_calibration(
            cam,
            warp_width=int(surf["warp_width"]),
            warp_height=int(surf["warp_height"]),
            save_path=cfg_path.parent / out,
        )


if __name__ == "__main__":
    main()
