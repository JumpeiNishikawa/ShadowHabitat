"""CSV logger for shadow blobs (per spec 4.16)."""
from __future__ import annotations

import csv
import time
from pathlib import Path

from .shadow_detector import ShadowBlob


class CsvLogger:
    HEADER = [
        "time", "surface", "frame", "blob_id",
        "x", "y", "area", "major", "minor", "angle", "vx", "vy",
    ]

    def __init__(self, log_dir: str | Path):
        self.dir = Path(log_dir)
        self.dir.mkdir(parents=True, exist_ok=True)
        ts = time.strftime("%Y%m%d_%H%M%S")
        self.path = self.dir / f"shadow_{ts}.csv"
        self.fp = self.path.open("w", encoding="utf-8", newline="")
        self.writer = csv.writer(self.fp)
        self.writer.writerow(self.HEADER)
        self._frame = 0

    def log(self, surface_id: str, blobs: list[ShadowBlob]) -> None:
        self._frame += 1
        t = time.time()
        if not blobs:
            self.writer.writerow([f"{t:.4f}", surface_id, self._frame, "", "", "", "", "", "", "", "", ""])
            return
        for b in blobs:
            self.writer.writerow([
                f"{t:.4f}", surface_id, self._frame, b.id,
                f"{b.x:.5f}", f"{b.y:.5f}", f"{b.area:.6f}",
                f"{b.major:.5f}", f"{b.minor:.5f}", f"{b.angle:.2f}",
                f"{b.vx:.5f}", f"{b.vy:.5f}",
            ])

    def close(self) -> None:
        try:
            self.fp.flush()
            self.fp.close()
        except Exception:
            pass

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        self.close()
