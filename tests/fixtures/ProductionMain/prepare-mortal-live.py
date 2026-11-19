"""T028 prepare-only coordinator RED scaffold; no subprocesses or world writes yet.

No command-line launch path exists in this checkpoint. Injected stage tests are
orchestration DATA contracts, never qualification of a real prepared package.
"""
from relay_phase_evidence import require_native_bootstrap_path_budget


def inventory(root):
    """Return exact relative file size/hash/mode DATA; RED stub emits no inventory."""
    return None


def admit_inputs(destination, source_root, output_root, source_pins, output_pins):
    """Check fresh inputs without creating anything; RED stub always refuses."""
    return {"admitted": False, "reason": "not-implemented"}


def prepare_stages(run_stage):
    """RED coordinator performs no stages and cannot issue a prepared manifest.

The eventual stage runner owns deadlines, retained logs and exact child/pipe
settlement. A failed/unsettled stage prevents all later stages. Injected runners
exercise ordering only, not publisher/exporter validity or actual readiness.
"""
    return {"prepared": False, "failedStage": None}
