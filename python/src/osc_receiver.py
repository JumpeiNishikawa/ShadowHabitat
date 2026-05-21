"""OSC server receiving agent state from Unity.

Address handled:
  /agent/state <id:int> <surface:string> <x:float> <y:float> <radius:float>
where x, y, radius are in normalized surface coords (0..1).

The receiver runs in a background thread; consumers call `snapshot()` to get
the latest known agents.
"""
from __future__ import annotations

import threading
import time
from dataclasses import dataclass

from pythonosc.dispatcher import Dispatcher
from pythonosc.osc_server import ThreadingOSCUDPServer


@dataclass
class AgentInfo:
    id: int
    surface: str
    x: float
    y: float
    radius: float
    last_seen: float


class AgentStateReceiver:
    def __init__(self, host: str = "0.0.0.0", port: int = 9001, stale_seconds: float = 0.5):
        self.host = host
        self.port = port
        self.stale_seconds = stale_seconds
        self._agents: dict[int, AgentInfo] = {}
        self._lock = threading.Lock()
        self._dispatcher = Dispatcher()
        self._dispatcher.map("/agent/state", self._on_agent_state)
        self._server: ThreadingOSCUDPServer | None = None
        self._thread: threading.Thread | None = None

    def _on_agent_state(self, address: str, *args) -> None:
        if len(args) < 5:
            return
        try:
            agent = AgentInfo(
                id=int(args[0]),
                surface=str(args[1]),
                x=float(args[2]),
                y=float(args[3]),
                radius=float(args[4]),
                last_seen=time.time(),
            )
        except (TypeError, ValueError):
            return
        with self._lock:
            self._agents[agent.id] = agent

    def start(self) -> None:
        self._server = ThreadingOSCUDPServer((self.host, self.port), self._dispatcher)
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()
        print(f"[AgentStateReceiver] listening on {self.host}:{self.port}")

    def stop(self) -> None:
        if self._server is not None:
            self._server.shutdown()
            self._server.server_close()
            self._server = None
        self._thread = None

    def snapshot(self, surface_id: str | None = None) -> list[AgentInfo]:
        now = time.time()
        with self._lock:
            agents = list(self._agents.values())
            stale = [a.id for a in agents if now - a.last_seen > self.stale_seconds]
            for sid in stale:
                self._agents.pop(sid, None)
            agents = [a for a in self._agents.values()
                      if surface_id is None or a.surface == surface_id]
            return agents
