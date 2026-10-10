"""Replay retained-data checks without changing the as-executed verifier."""
import runpy
from pathlib import Path

if not __debug__:
    raise SystemExit("Retained-data verification requires Python without -O/-OO or PYTHONOPTIMIZE; assertions must remain enabled.")

runpy.run_path(
    str(Path(__file__).with_name("c5-active-poll-actual-crash-green") / "verify-retained-raw.py"),
    run_name="__main__",
)
