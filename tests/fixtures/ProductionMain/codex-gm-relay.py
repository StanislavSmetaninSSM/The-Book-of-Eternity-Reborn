"""Legacy fixture entrypoint forwarding to the maintained developer relay."""
from pathlib import Path
import runpy
import sys

here = Path(__file__).resolve()
shared = here.with_name('relay_cli.py')
if not shared.is_file():
    shared = here.parents[3] / 'tools/gm-relay/relay_cli.py'
sys.path.insert(0, str(shared.parent))
runpy.run_path(str(shared), run_name='__main__')
