import hashlib
import json
from pathlib import Path
import re
import subprocess

repo = Path('D:/Code/The-Book-of-Eternity-Reborn-crash5-windows-20261019')
feature = repo / 'specs/1536-complete-wound-materialization'
scripts = repo / '.specify/scripts/powershell'
phases = []
for name, arguments in (
    ('setup-plan.ps1', ['-Json']),
    ('setup-tasks.ps1', ['-Json']),
    ('check-prerequisites.ps1', ['-Json', '-RequireTasks', '-IncludeTasks']),
):
    p = subprocess.run(['pwsh', '-NoProfile', '-File', str(scripts / name), *arguments],
                       cwd=repo, capture_output=True, text=True, encoding='utf-8')
    assert p.returncode == 0, (name, p.stderr)
    value = json.loads(p.stdout)
    path = value.get('SPECS_DIR', value.get('FEATURE_DIR'))
    assert Path(path) == feature, (name, path)
    phases.append({'Script': name, 'Arguments': arguments, 'ExitCode': p.returncode,
                   'Output': value, 'Stderr': p.stderr})
assert (feature / 'spec.md').is_file()
docs = {}
for name in ('spec.md', 'plan.md', 'tasks.md', 'research.md', 'data-model.md', 'quickstart.md'):
    data = (feature / name).read_bytes()
    whole = data.decode('utf-8-sig')
    active = whole.split('\n# ', 1)[0]
    assert active.strip() and ('revision47' in active or 'T425' in active)
    assert '1536' in active
    docs[name] = {'SHA256': hashlib.sha256(data).hexdigest().upper(),
                  'ActiveSectionBytes': len(active.encode('utf-8'))}
    if name == 'spec.md':
        for number in range(1, 11):
            assert re.search(r'OPCACHE-' + str(number) + r'\b', active)
    if name == 'tasks.md':
        for number in range(425, 430):
            assert f'- [ ] T{number}' in active
coverage = {
    'OPCACHE-1': ['T426', 'T427', 'T428'],
    'OPCACHE-2': ['T426', 'T427', 'T428'],
    'OPCACHE-3': ['T426', 'T427', 'T428'],
    'OPCACHE-4': ['T426', 'T427', 'T428'],
    'OPCACHE-5': ['T427', 'T428'],
    'OPCACHE-6': ['T427', 'T428'],
    'OPCACHE-7': ['T426', 'T427', 'T428'],
    'OPCACHE-8': ['T427', 'T428'],
    'OPCACHE-9': ['T426', 'T427', 'T428', 'T429'],
    'OPCACHE-10': ['T428', 'T429'],
}
report = {
    'Task': 'T425', 'Issue': 1536, 'Revision': 47,
    'Method': 'Bridge-equivalent analysis: parent semantic review of the actual r47 additions plus these executable phase, feature, inventory and hash checks. No claim of a hidden speckit-analyze CLI.',
    'AnalysisMutation': 'Read-only against canonical inputs; this script emits only this report after all checks.',
    'SpecKitPhases': phases,
    'Compatibility': 'Installed analyze skill mentions -RequireSpec but project script lacks it. Initial binding attempt failed before validation; actual supported -RequireTasks -IncludeTasks run succeeded and spec existence was checked explicitly.',
    'ExtensionHooks': 'Existing extensions.yml read; plan/tasks/analyze hooks are optional git/agent-context updates. Skipped in favour of explicit bounded commit/checkpoint; no required hook silently skipped.',
    'CanonicalFiles': docs, 'RequirementTaskCoverage': coverage,
    'SemanticFindings': [],
    'PreservedContractsReviewed': [
        'General production service, not recovery-specific; recovery is first consumer.',
        'Value false/0/null separated from missing and validation success.',
        'Context owns temporary entries, all exits clear own references, late work cannot reinsert; no GC or caller resource disposal promise.',
        'Exact inputs, result copies, complete/transitive versioned dependencies and no per-hit filesystem scan.',
        'Root content/namespace version and exact live lease segment; unknown writers/hooks are ineligible until proven.',
        'Only mechanical/continuation DATA memoized; live claim/item/seal and fresh take/transaction proofs preserved.',
        'Cheap causal fixtures and real attribute counters, selected categories/XML, independent review before original.',
        'Original unchanged600/820/15, full uncapped diagnostics and truthful outcome; T420 remains FAIL.',
        'Historical r46 causal RED is retained, not called GREEN; cancelled four experimental cases distinguished from32 original controls.',
    ],
    'OpenImplementationGates': [
        'T427 writer/cleanup/root lifecycle coverage and actual lock-ordered stamp capture.',
        'T427 allowed two-body dependency closure, exact evidence live/source split and genuine resumer hook eligibility.',
        'T427 forced fresh propagation through all required take/transaction boundaries and refusal parity.',
    ],
    'ConstitutionCheck': {'IssueTraceability': '1536', 'TestFirst': 'Causal RED/GREEN before reuse',
                          'CategoryOnly': True, 'NoPlayerOrGmSchemaChange': True,
                          'TrustedLocalAssumptionsPreserved': True, 'NoNewPackages': True},
    'Verdict': 'CONSISTENT for planned first block; implementation and performance unqualified',
    'GameOrTestsExecuted': False, 'DiagnosticQuotas': False,
}
out = feature / 'recovery/crash5/T425-plan-analysis.json'
out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'Verdict': report['Verdict'], 'Phases': len(phases),
                  'RequirementsCovered': len(coverage), 'CanonicalFiles': docs}, ensure_ascii=False))
