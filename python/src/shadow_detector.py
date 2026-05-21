"""Shadow detection on the warped surface image.

Pipeline:
  1. BGR -> gray, blur
  2. Maintain a slow-EMA background of the bright surface (frozen after N seconds)
  3. mask = (background - current) > threshold
  4. morphological open/close
  5. connectedComponentsWithStats
  6. fit ellipse for shape features
  7. normalize centroid/size to 0..1 surface coords
  8. simple nearest-neighbor tracking for stable ids + velocity
"""
from __future__ import annotations

import time
from dataclasses import dataclass, field

import cv2
import numpy as np


@dataclass
class ShadowBlob:
    id: int
    x: float
    y: float
    area: float
    major: float
    minor: float
    angle: float
    vx: float = 0.0
    vy: float = 0.0
    age: float = 0.0  # seconds since first seen
    last_seen: float = 0.0


@dataclass
class DetectorConfig:
    blur_kernel: int = 5
    morph_kernel: int = 5
    diff_threshold: int = 35
    min_area_ratio: float = 0.002
    max_components: int = 8
    # Background learning. "max" = per-pixel running max (immune to transient
    # darkening like the agent passing through). "ema" = exponential mean
    # (legacy; learns the agent into the background and breaks).
    background_method: str = "max"
    background_alpha: float = 0.01   # ema mode only
    background_freeze_after_seconds: float = 8.0
    # After freeze, slowly raise the background if the live pixel is brighter
    # (handles gentle lighting drift). Set 0 to disable.
    background_post_freeze_brighten_alpha: float = 0.0
    track_max_distance_norm: float = 0.15


class ShadowDetector:
    def __init__(self, warp_width: int, warp_height: int, cfg: DetectorConfig):
        self.W = warp_width
        self.H = warp_height
        self.cfg = cfg
        self.background: np.ndarray | None = None
        self._t0 = time.time()
        self._background_frozen = False
        self._tracks: dict[int, ShadowBlob] = {}
        self._next_id = 0
        self._last_time = time.time()

    def reset_background(self) -> None:
        self.background = None
        self._t0 = time.time()
        self._background_frozen = False

    def background_status(self) -> tuple[str, float]:
        """Return (state, seconds_remaining). state in {"empty","learning","frozen"}."""
        if self.background is None:
            return "empty", 0.0
        if self._background_frozen:
            return "frozen", 0.0
        remaining = max(0.0, self.cfg.background_freeze_after_seconds - (time.time() - self._t0))
        return "learning", remaining

    @property
    def total_area(self) -> int:
        return self.W * self.H

    def _update_background(self, gray: np.ndarray) -> None:
        if self.background is None:
            # Seed with the first frame. If the agent already occludes some pixels,
            # the running-max update below will lift those pixels as soon as they
            # become bright again.
            self.background = gray.astype(np.float32)
            return

        elapsed = time.time() - self._t0
        gray_f = gray.astype(np.float32)

        if not self._background_frozen:
            if self.cfg.background_method == "max":
                # Per-pixel running max. Transient darkening (agent passing through,
                # short shadows) is ignored. Each pixel locks in its brightest
                # state seen so far during the learn window.
                np.maximum(self.background, gray_f, out=self.background)
            else:  # "ema"
                a = self.cfg.background_alpha
                cv2.accumulateWeighted(gray_f, self.background, a)

            if elapsed >= self.cfg.background_freeze_after_seconds:
                self._background_frozen = True
            return

        # Frozen branch: optional slow brightening to track lighting drift.
        a = self.cfg.background_post_freeze_brighten_alpha
        if a > 0.0:
            brighter = gray_f > self.background
            if np.any(brighter):
                self.background[brighter] = (
                    (1.0 - a) * self.background[brighter] + a * gray_f[brighter]
                )

    def _detect_mask(self, gray: np.ndarray) -> np.ndarray:
        bg = self.background
        if bg is None:
            return np.zeros_like(gray, dtype=np.uint8)
        diff = cv2.subtract(bg.astype(np.uint8), gray)
        _, mask = cv2.threshold(diff, self.cfg.diff_threshold, 255, cv2.THRESH_BINARY)
        k = max(1, int(self.cfg.morph_kernel))
        kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        return mask

    def _extract_components(self, mask: np.ndarray) -> list[ShadowBlob]:
        min_area_px = max(50, int(self.cfg.min_area_ratio * self.total_area))
        num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(mask, connectivity=8)
        out: list[ShadowBlob] = []
        # label 0 is background; iterate 1..N-1
        candidates = []
        for i in range(1, num_labels):
            area_px = int(stats[i, cv2.CC_STAT_AREA])
            if area_px < min_area_px:
                continue
            cx, cy = centroids[i]
            candidates.append((area_px, i, cx, cy))

        candidates.sort(reverse=True)  # largest first
        for area_px, i, cx, cy in candidates[: self.cfg.max_components]:
            comp_mask = (labels == i).astype(np.uint8) * 255
            major = minor = 0.0
            angle = 0.0
            contours, _ = cv2.findContours(comp_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            if contours:
                cnt = max(contours, key=cv2.contourArea)
                if len(cnt) >= 5:
                    (_ecx, _ecy), (ax1, ax2), ang = cv2.fitEllipse(cnt)
                    major = max(ax1, ax2)
                    minor = min(ax1, ax2)
                    angle = float(ang)
                else:
                    x, y, w, h = cv2.boundingRect(cnt)
                    major = float(max(w, h))
                    minor = float(min(w, h))
                    angle = 0.0
            blob = ShadowBlob(
                id=-1,  # assigned after tracking
                x=float(cx) / self.W,
                y=float(cy) / self.H,
                area=float(area_px) / self.total_area,
                major=major / max(self.W, self.H),
                minor=minor / max(self.W, self.H),
                angle=angle,
            )
            out.append(blob)
        return out

    def _track(self, detections: list[ShadowBlob]) -> list[ShadowBlob]:
        now = time.time()
        dt = max(1e-3, now - self._last_time)
        self._last_time = now

        unmatched_dets = list(range(len(detections)))
        matched: dict[int, int] = {}
        max_d = self.cfg.track_max_distance_norm

        for tid, t in list(self._tracks.items()):
            best_j = -1
            best_d = max_d
            for j in unmatched_dets:
                d = detections[j]
                dist = ((d.x - t.x) ** 2 + (d.y - t.y) ** 2) ** 0.5
                if dist < best_d:
                    best_d = dist
                    best_j = j
            if best_j >= 0:
                matched[tid] = best_j
                unmatched_dets.remove(best_j)

        result: list[ShadowBlob] = []
        seen_ids: set[int] = set()
        for tid, j in matched.items():
            d = detections[j]
            prev = self._tracks[tid]
            d.id = tid
            d.vx = (d.x - prev.x) / dt
            d.vy = (d.y - prev.y) / dt
            d.age = prev.age + dt
            d.last_seen = now
            self._tracks[tid] = d
            result.append(d)
            seen_ids.add(tid)

        for j in unmatched_dets:
            d = detections[j]
            tid = self._next_id
            self._next_id += 1
            d.id = tid
            d.age = 0.0
            d.last_seen = now
            self._tracks[tid] = d
            result.append(d)
            seen_ids.add(tid)

        # drop stale
        for tid in list(self._tracks.keys()):
            if tid not in seen_ids and now - self._tracks[tid].last_seen > 0.5:
                del self._tracks[tid]

        return result

    def process(self, surface_bgr: np.ndarray) -> tuple[list[ShadowBlob], np.ndarray, np.ndarray]:
        gray = cv2.cvtColor(surface_bgr, cv2.COLOR_BGR2GRAY)
        k = max(1, int(self.cfg.blur_kernel))
        if k % 2 == 0:
            k += 1
        gray = cv2.GaussianBlur(gray, (k, k), 0)
        self._update_background(gray)
        mask = self._detect_mask(gray)
        raw_blobs = self._extract_components(mask)
        tracked = self._track(raw_blobs)
        return tracked, mask, gray
