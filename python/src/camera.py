"""Camera capture wrapper."""
from __future__ import annotations

import cv2


class Camera:
    def __init__(self, device_id: int = 0, width: int = 1280, height: int = 720, fps: int = 30):
        self.device_id = device_id
        self.width = width
        self.height = height
        self.fps = fps
        self.cap: cv2.VideoCapture | None = None

    def open(self) -> None:
        cap = cv2.VideoCapture(self.device_id, cv2.CAP_DSHOW)
        if not cap.isOpened():
            cap = cv2.VideoCapture(self.device_id)
        if not cap.isOpened():
            raise RuntimeError(f"Cannot open camera device {self.device_id}")
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, self.width)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self.height)
        cap.set(cv2.CAP_PROP_FPS, self.fps)
        self.cap = cap

    def read(self):
        assert self.cap is not None, "Camera not opened"
        ok, frame = self.cap.read()
        if not ok:
            return None
        return frame

    def release(self) -> None:
        if self.cap is not None:
            self.cap.release()
            self.cap = None

    def __enter__(self):
        self.open()
        return self

    def __exit__(self, exc_type, exc, tb):
        self.release()
