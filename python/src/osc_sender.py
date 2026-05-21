"""OSC sender for shadow blobs.

Protocol (per frame, ordered):
  /shadow/begin  <surface:string> <count:int> <frame:int>
  /shadow/blob   <id:int> <x:float> <y:float> <area:float>
                 <major:float> <minor:float> <angle:float>
                 <vx:float> <vy:float>           (repeat <count> times)
  /shadow/end    <frame:int>

Coordinates are normalized to the surface (0..1, top-left origin).
"""
from __future__ import annotations

from pythonosc.udp_client import SimpleUDPClient

from .shadow_detector import ShadowBlob


class OscSender:
    def __init__(self, host: str = "127.0.0.1", port: int = 9000):
        self.host = host
        self.port = port
        self.client = SimpleUDPClient(host, port)
        self._frame = 0

    def send_frame(self, surface_id: str, blobs: list[ShadowBlob]) -> None:
        self._frame += 1
        self.client.send_message("/shadow/begin", [surface_id, len(blobs), self._frame])
        for b in blobs:
            self.client.send_message(
                "/shadow/blob",
                [
                    int(b.id),
                    float(b.x),
                    float(b.y),
                    float(b.area),
                    float(b.major),
                    float(b.minor),
                    float(b.angle),
                    float(b.vx),
                    float(b.vy),
                ],
            )
        self.client.send_message("/shadow/end", [self._frame])
