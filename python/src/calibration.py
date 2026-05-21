"""Projection-surface calibration: 4 corner clicks -> homography to a fixed warp plane.

Coordinate convention for clicks (in this order):
  0: top-left, 1: top-right, 2: bottom-right, 3: bottom-left
of the projected surface as seen by the camera.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

import cv2
import numpy as np


CORNER_LABELS = ["TL (top-left)", "TR (top-right)", "BR (bottom-right)", "BL (bottom-left)"]


def _fit_window_size(src_w: int, src_h: int, max_w: int = 1280, max_h: int = 720) -> tuple[int, int]:
    """Return a (w, h) that keeps src aspect ratio and fits within (max_w, max_h)."""
    if src_w <= 0 or src_h <= 0:
        return max_w, max_h
    scale = min(max_w / src_w, max_h / src_h, 1.0)
    return max(1, int(round(src_w * scale))), max(1, int(round(src_h * scale)))


@dataclass
class Calibration:
    src_points: list[tuple[float, float]] = field(default_factory=list)
    warp_width: int = 1024
    warp_height: int = 576

    def is_complete(self) -> bool:
        return len(self.src_points) == 4

    def homography(self) -> np.ndarray:
        if not self.is_complete():
            raise ValueError("Need 4 source points")
        src = np.array(self.src_points, dtype=np.float32)
        dst = np.array(
            [
                [0, 0],
                [self.warp_width - 1, 0],
                [self.warp_width - 1, self.warp_height - 1],
                [0, self.warp_height - 1],
            ],
            dtype=np.float32,
        )
        H, _ = cv2.findHomography(src, dst)
        return H

    def to_dict(self) -> dict:
        return {
            "src_points": [list(p) for p in self.src_points],
            "warp_width": self.warp_width,
            "warp_height": self.warp_height,
        }

    @classmethod
    def from_dict(cls, d: dict) -> "Calibration":
        return cls(
            src_points=[tuple(p) for p in d.get("src_points", [])],
            warp_width=int(d.get("warp_width", 1024)),
            warp_height=int(d.get("warp_height", 576)),
        )

    def save(self, path: str | Path) -> None:
        Path(path).write_text(json.dumps(self.to_dict(), indent=2), encoding="utf-8")

    @classmethod
    def load(cls, path: str | Path) -> "Calibration":
        return cls.from_dict(json.loads(Path(path).read_text(encoding="utf-8")))


def run_interactive_calibration(camera, warp_width: int, warp_height: int, save_path: str | Path) -> Calibration:
    """Open a window, ask the user to click 4 corners, save the homography.

    Controls:
      - left click: add corner
      - u / backspace: undo last corner
      - r: reset
      - s: save (only after 4 points)
      - q / esc: quit without saving
    """
    calib = Calibration(warp_width=warp_width, warp_height=warp_height)
    win = "Calibration (click 4 corners: TL -> TR -> BR -> BL)"
    preview_win = "Calibration preview (warped surface)"
    cv2.namedWindow(win, cv2.WINDOW_NORMAL)

    # Probe one frame so we can size the window to the camera aspect ratio.
    probe = camera.read()
    if probe is not None:
        h, w = probe.shape[:2]
        win_w, win_h = _fit_window_size(w, h)
        cv2.resizeWindow(win, win_w, win_h)
    win_initialized = probe is not None
    preview_initialized = False

    def on_mouse(event, x, y, flags, _userdata):
        if event == cv2.EVENT_LBUTTONDOWN and len(calib.src_points) < 4:
            calib.src_points.append((float(x), float(y)))

    cv2.setMouseCallback(win, on_mouse)

    print("[calibration] Click the 4 corners of the projected surface in order:")
    for i, label in enumerate(CORNER_LABELS):
        print(f"  {i}: {label}")
    print("[calibration] Keys: u=undo  r=reset  s=save  q/esc=quit")

    saved = False
    while True:
        frame = probe if probe is not None else camera.read()
        probe = None
        if frame is None:
            print("[calibration] Camera read failed")
            break
        if not win_initialized:
            h, w = frame.shape[:2]
            win_w, win_h = _fit_window_size(w, h)
            cv2.resizeWindow(win, win_w, win_h)
            win_initialized = True
        disp = frame.copy()
        for i, (x, y) in enumerate(calib.src_points):
            cv2.circle(disp, (int(x), int(y)), 8, (0, 255, 0), -1)
            cv2.putText(disp, str(i), (int(x) + 10, int(y) - 10),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
        if len(calib.src_points) >= 2:
            pts = np.array(calib.src_points, dtype=np.int32)
            cv2.polylines(disp, [pts], isClosed=len(calib.src_points) == 4,
                          color=(0, 200, 255), thickness=2)
        if len(calib.src_points) < 4:
            msg = f"Click {CORNER_LABELS[len(calib.src_points)]} ({len(calib.src_points)}/4)"
        else:
            msg = "4/4 corners set. Press 's' to save, 'r' to reset."
        cv2.putText(disp, msg, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
        cv2.imshow(win, disp)

        if calib.is_complete():
            H = calib.homography()
            warped = cv2.warpPerspective(frame, H, (warp_width, warp_height))
            if not preview_initialized:
                cv2.namedWindow(preview_win, cv2.WINDOW_NORMAL)
                pw, ph = _fit_window_size(warp_width, warp_height)
                cv2.resizeWindow(preview_win, pw, ph)
                preview_initialized = True
            cv2.imshow(preview_win, warped)

        key = cv2.waitKey(15) & 0xFF
        if key in (ord("q"), 27):
            break
        elif key in (ord("u"), 8):
            if calib.src_points:
                calib.src_points.pop()
        elif key == ord("r"):
            calib.src_points.clear()
            try:
                cv2.destroyWindow(preview_win)
            except cv2.error:
                pass
            preview_initialized = False
        elif key == ord("s") and calib.is_complete():
            calib.save(save_path)
            print(f"[calibration] Saved to {save_path}")
            saved = True
            break

    cv2.destroyAllWindows()
    if not saved:
        raise SystemExit("[calibration] Aborted without saving")
    return calib
