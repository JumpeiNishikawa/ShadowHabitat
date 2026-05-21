"""Stage 1 / Phase 1-2: shadow detection + OSC to Unity.

Usage:
  python main.py [--config config.json] [--no-display] [--no-osc]

Keys (when display enabled):
  q / esc : quit
  b       : re-learn background (3 seconds)
  d       : toggle debug overlay
"""
from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

import cv2
import numpy as np

from src.camera import Camera
from src.calibration import Calibration
from src.shadow_detector import DetectorConfig, ShadowDetector
from src.osc_sender import OscSender
from src.csv_logger import CsvLogger


def draw_overlay(surface_bgr, blobs, mask):
    disp = surface_bgr.copy()
    H, W = disp.shape[:2]
    overlay = disp.copy()
    overlay[mask > 0] = (0, 0, 255)
    disp = cv2.addWeighted(disp, 0.7, overlay, 0.3, 0)
    for b in blobs:
        cx, cy = int(b.x * W), int(b.y * H)
        r = max(4, int(((b.area * W * H) / np.pi) ** 0.5))
        cv2.circle(disp, (cx, cy), r, (0, 255, 0), 2)
        cv2.putText(disp, f"#{b.id}", (cx + 6, cy - 6),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 1)
    return disp


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default="config.json")
    ap.add_argument("--no-display", action="store_true")
    ap.add_argument("--no-osc", action="store_true")
    ap.add_argument("--no-log", action="store_true")
    args = ap.parse_args()

    cfg_path = Path(args.config)
    cfg = json.loads(cfg_path.read_text(encoding="utf-8"))

    cam_cfg = cfg["camera"]
    surf_cfg = cfg["surface"]
    det_cfg_raw = cfg["shadow_detection"]
    osc_cfg = cfg["osc"]
    log_cfg = cfg["logging"]
    show = cfg.get("debug", {}).get("show_windows", True) and not args.no_display

    calib_path = cfg_path.parent / cfg["calibration"]["file"]
    if not calib_path.exists():
        raise SystemExit(
            f"Calibration not found: {calib_path}\n"
            f"Run: python calibrate.py --config {args.config}"
        )
    calib = Calibration.load(calib_path)
    H = calib.homography()
    Wsurf = calib.warp_width
    Hsurf = calib.warp_height

    det = ShadowDetector(
        warp_width=Wsurf,
        warp_height=Hsurf,
        cfg=DetectorConfig(
            blur_kernel=int(det_cfg_raw.get("blur_kernel", 5)),
            morph_kernel=int(det_cfg_raw.get("morph_kernel", 5)),
            diff_threshold=int(det_cfg_raw.get("diff_threshold", 35)),
            min_area_ratio=float(det_cfg_raw.get("min_area_ratio", 0.002)),
            max_components=int(det_cfg_raw.get("max_components", 8)),
            background_alpha=float(det_cfg_raw.get("background_alpha", 0.01)),
            background_freeze_after_seconds=float(det_cfg_raw.get("background_freeze_after_seconds", 3.0)),
            track_max_distance_norm=float(det_cfg_raw.get("track_max_distance_norm", 0.15)),
        ),
    )

    osc = None if args.no_osc else OscSender(osc_cfg["host"], int(osc_cfg["port"]))
    log = None
    if log_cfg.get("enabled", True) and not args.no_log:
        log = CsvLogger(cfg_path.parent / log_cfg.get("dir", "logs"))

    surface_id = str(surf_cfg.get("id", "plane"))

    show_debug = True
    fps_t0 = time.time()
    frames = 0

    try:
        with Camera(cam_cfg["device_id"], cam_cfg["width"], cam_cfg["height"], cam_cfg["fps"]) as cam:
            print("[main] running. press 'q' to quit, 'b' to relearn background, 'd' to toggle debug")
            while True:
                frame = cam.read()
                if frame is None:
                    print("[main] camera read failed")
                    break
                surface = cv2.warpPerspective(frame, H, (Wsurf, Hsurf))
                blobs, mask, _gray = det.process(surface)

                if osc is not None:
                    osc.send_frame(surface_id, blobs)
                if log is not None:
                    log.log(surface_id, blobs)

                frames += 1
                if time.time() - fps_t0 >= 1.0:
                    fps = frames / (time.time() - fps_t0)
                    fps_t0 = time.time()
                    frames = 0
                else:
                    fps = None

                if show:
                    disp = draw_overlay(surface, blobs, mask) if show_debug else surface
                    if fps is not None:
                        cv2.putText(disp, f"{fps:.1f} fps  blobs={len(blobs)}",
                                    (10, 24), cv2.FONT_HERSHEY_SIMPLEX, 0.7,
                                    (255, 255, 255), 2)
                    cv2.imshow("surface (warped)", disp)
                    cv2.imshow("mask", mask)
                    key = cv2.waitKey(1) & 0xFF
                    if key in (ord("q"), 27):
                        break
                    elif key == ord("b"):
                        det.reset_background()
                        print("[main] background reset")
                    elif key == ord("d"):
                        show_debug = not show_debug
    finally:
        if log is not None:
            log.close()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
