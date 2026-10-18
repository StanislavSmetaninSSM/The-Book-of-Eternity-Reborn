"""Independent read-only SOURCE/hash/selection review; never invoke product or controllers."""
from pathlib import Path
import ast, datetime, fnmatch, hashlib, json, os, stat, subprocess

R = Path('/workspace/The-Book-of-Eternity-Reborn')
C = Path('/workspace/1536-cloud-clean-restore')
D = Path('/tmp/1536-cloud-tools/review')
REC = 'specs/1536-complete-wound-materialization/recovery/'
HEAD = 'a0da72f2c21fca66193314954cfdf87992752876'
BASE = '5687e0e603509b01070d4e999ebb6e0c58d4d9df'
PACKET = REC + 't179pd-ni-iobs-l01-source-packet-20261018.json'
PRODUCT = 'BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs'
PREP = REC + 't179pd-ni-iobs-l01-process-preparation-run.py'
FIRST = REC + 't179pd-ni-iobs-l01-process-first-run.py'

def h(b): return hashlib.sha256(b).hexdigest()
def blob(b): return hashlib.sha1(b'blob ' + str(len(b)).encode() + b'\0' + b).hexdigest()
def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root,
                                   env={**os.environ, 'GIT_OPTIONAL_LOCKS': '0'})
def tree(root, rev):
    out = {}
    for row in git(root, 'ls-tree', '-rz', rev).split(b'\0'):
        if not row: continue
        meta, path = row.split(b'\t', 1)
        mode, kind, oid = meta.decode().split()
        out[path.decode()] = (mode, kind, oid)
    return out
def data(path): return json.loads(path.read_bytes())
def literal_assignment(module, name):
    return next(n.value for n in ast.walk(module) if isinstance(n, ast.Assign)
                and any(isinstance(t, ast.Name) and t.id == name for t in n.targets))

for root in (R, C):
    assert git(root, 'rev-parse', 'HEAD').decode().strip() == HEAD
    assert not git(root, 'status', '--porcelain')
    assert not (root / '.git/objects/info/alternates').exists()
remote = git(C, 'ls-remote', 'origin', 'refs/heads/1536-wound-b2-cloud-20261012').decode().strip()
assert remote == HEAD + '\trefs/heads/1536-wound-b2-cloud-20261012'
rt, ct, bt = tree(R, HEAD), tree(C, HEAD), tree(R, BASE)
assert rt == ct
changed = git(R, 'diff', '--name-only', BASE, HEAD).decode().splitlines()
assert len(changed) == 12
assert [p for p in changed if not p.startswith('specs/1536-complete-wound-materialization/')] == [PRODUCT]
assert not git(R, 'diff', '--check', BASE, HEAD)
receipt_path = Path('/tmp/1536-cloud-tools/carrier-recovery-a0da72f2.json')
receipt = data(receipt_path)
assert receipt['sha'] == HEAD and receipt['base'] == BASE
assert receipt['changedPathsVerified'] == 12
assert sorted(x['path'] for x in receipt['files']) == changed
recovery = []
for x in receipt['files']:
    b = (R / x['path']).read_bytes()
    assert b == (C / x['path']).read_bytes() == git(C, 'show', HEAD + ':' + x['path'])
    assert (len(b), h(b), blob(b)) == (x['bytes'], x['sha256'], x['gitBlob'])
    assert rt[x['path']] == ct[x['path']] == (x['mode'], 'blob', x['gitBlob'])
    recovery.append(x)

p = data(R / PACKET)
old_raw = git(R, 'show', BASE + ':' + PACKET)
old = json.loads(old_raw)
assert p['sourceBase'] == BASE and p['sourcePinCount'] == len(p['sourcePins']) == 98
assert h(json.dumps(p['sourcePins'], sort_keys=True, separators=(',', ':')).encode()) == p['pinDigest']
assert p['pinDigest'] == 'b29c971342ee772b13cd123a954d7afa8937e1f50a7e5f4c4c14403e4de4c347'
assert len({x['path'] for x in p['sourcePins']}) == 98
oldpins = {x['path']:x for x in old['sourcePins']}
assert set(oldpins) == {x['path'] for x in p['sourcePins']}
changedpins = []
for x in p['sourcePins']:
    b = (R / x['path']).read_bytes()
    assert b == (C / x['path']).read_bytes()
    assert (len(b), h(b), blob(b)) == (x['bytes'], x['sha256'], x['gitBlob'])
    assert rt[x['path']] == ct[x['path']] == (x['mode'], 'blob', x['gitBlob'])
    if any(x[k] != oldpins[x['path']][k] for k in ('bytes', 'sha256', 'gitBlob', 'mode')):
        changedpins.append(x['path'])
assert set(changedpins) == {PRODUCT, PREP, FIRST}
historical = p['historicalPacket']
assert (R / historical['path']).read_bytes() == (C / historical['path']).read_bytes() == old_raw
assert (len(old_raw), h(old_raw)) == (historical['bytes'], historical['sha256'])
assert h((R / p['ledger']['path']).read_bytes()) == p['ledger']['sha256'] == old['ledger']['sha256']
assert git(R, 'diff', '--name-only', p['productBase'], HEAD, '--', 'BookOfEternityClient', 'BookOfEternityGMBridge').decode().splitlines() == p['productDelta']
assert len(p['productDelta']) == 20
for x in p['verificationTools'] + p['causalReview']:
    b = (R / x['path']).read_bytes()
    assert h(b) == x['sha256'] and len(b) == x['bytes']

before = git(R, 'show', BASE + ':' + PRODUCT)
after = (R / PRODUCT).read_bytes()
old_chunk = b'''            catch (Exception exception)
            {
                await beforeDestructiveRefusal();
                await FailClosedAcceptedTurnCanonicalRefreshAsync('''
new_chunk = b'''            catch (Exception exception)
            {
                try
                {
                    await beforeDestructiveRefusal();
                }
                catch (MortalJointRepairPublicationHoldException)
                {
                    _logger.LogError(
                        exception,
                        "The source-bound joint repair owner is blocked; outer rollback is suppressed.");
                    throw;
                }
                await FailClosedAcceptedTurnCanonicalRefreshAsync('''
assert before.count(old_chunk) == after.count(new_chunk) == 1
assert after.replace(new_chunk, old_chunk, 1) == before
assert git(R, 'diff', '--numstat', BASE, HEAD, '--', PRODUCT).decode().strip() == '11\t1\t' + PRODUCT
assert not git(R, 'diff', '--name-only', BASE, HEAD, '--', 'BookOfEternityClient.IntegrationTests', 'BookOfEternityClient.TestSupport', 'BookOfEternityClient.Tests', 'tests', 'scripts', 'docs', 'AGENTS.md')

prep_text = (R / PREP).read_text()
first_text = (R / FIRST).read_text()
prep_ast, first_ast = ast.parse(prep_text), ast.parse(first_text)
groups = ast.literal_eval(literal_assignment(first_ast, 'groups'))
expected_groups = {
    'crash5': (['fr063-joint-interface-crash-current'], 5),
    'integrity4': (['fr063-joint-interface-current-integrity'], 4),
    'close2': (['fr063-joint-interface-close-retention'], 2),
    'races2': (['fr063-joint-interface-source-races'], 2),
    'settlement3': (['fr063-joint-interface-settlement'], 3),
    'uncertain1': (['fr063-joint-interface-uncertain-current'], 1)}
assert groups == expected_groups
allowed = next(n for n in ast.walk(prep_ast) if isinstance(n, ast.Compare)
               and isinstance(n.left, ast.Name) and n.left.id == 'mode'
               and isinstance(n.comparators[0], ast.Set)
               and 'catalog' in ast.literal_eval(n.comparators[0]))
assert ast.literal_eval(allowed.comparators[0]) == {'catalog', 'plan', 'corrective-plan', 'remaining-plan'}
pending_expr = literal_assignment(prep_ast, 'pending_categories')
assert isinstance(pending_expr, ast.IfExp)
assert ast.literal_eval(pending_expr.body) == set(groups['close2'][0])
remaining_cats = {v[0][0] for k,v in groups.items() if k != 'close2'}
assert ast.literal_eval(pending_expr.orelse) == remaining_cats
prep_if = next(n for n in ast.walk(first_ast) if isinstance(n, ast.IfExp)
               and isinstance(n.body, ast.Constant) and n.body.value == 'corrective-plan')
assert ast.dump(prep_if.test) == ast.dump(ast.parse("mode=='close2'", mode='eval').body)
assert prep_if.orelse.value == 'remaining-plan'
for module in (prep_ast, first_ast):
    assert len([n for n in ast.walk(module) if isinstance(n, ast.Call)
                and isinstance(n.func, ast.Attribute) and n.func.attr == 'Popen']) == 1
    assert not any(isinstance(n, ast.While) for n in ast.walk(module))
for text_ in (prep_text, first_text):
    assert "sentinel.open('x')" in text_ and 'assert not out.exists()' in text_
    assert '(out/\'record.json\').write_text' in text_
    assert "'sourcePins'" in text_ and "'allOutputInventory'" in text_
assert "argv+=['-ValidateCatalog']" in prep_text
assert "argv+=['-SelectionFile',selection,'-PlanOnly','-NoBuild']" in prep_text
assert "'-NoBuild'" in first_text and "'-ValidateCatalog'" not in first_text

ledger = data(R / p['ledger']['path'])
assert (ledger['staticTotals']['newMethods'], ledger['staticTotals']['newRows']) == (28,51)
counts = {x['method']:x['staticRows'] for x in ledger['rows']}
prefix = 'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.'
counts.update({
    prefix+'GuaranteedTreatment_CurrentCaptureStopPrecedesPreparationAndExpiresActualOwner': 1,
    prefix+'GuaranteedTreatment_CurrentCaptureObservationPreservesOrdinaryCompletion': 2,
    prefix+'GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression': 1,
    prefix+'GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce': 1,
    'BookOfEternityClient.Tests.PromptDocumentationCoverageTests.MortalJointSeverityRepairGuidance_PreservesPublicTransportAndOnceOnlyClientOwnership': 1})
selection = data(R / 'tests/selection.json')['selections']
assert len(selection) == 16
assert (R/'tests/selection.json').read_bytes() == (R/'tests/selections/1536-joint-interface-lifecycle-stage.json').read_bytes()
catalog = {x['id']:x for x in data(R / 'tests/categories.json')['categories']}
whole, bycat = {}, {}
for row in selection:
    methods = {}
    for selector in catalog[row['category']]['selectors']:
        for pattern in selector['tests']:
            matches = {m:n for m,n in counts.items() if fnmatch.fnmatchcase(m,pattern)}
            assert matches and not set(methods)&set(matches)
            methods.update(matches)
    assert not set(whole)&set(methods)
    whole.update(methods); bycat[row['category']] = methods
assert (len(whole), sum(whole.values())) == (33,57)
mode_rows, selected = {}, {}
cloud = data(R / (REC + 't179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
for mode,(cats,n) in groups.items():
    rows = {m:v for cat in cats for m,v in bycat[cat].items()}
    assert sum(rows.values()) == n and not set(selected)&set(rows)
    selected.update(rows)
    expected = next(x for x in cloud['pending'] if x['mode'] == mode)
    assert {x['method']:x['cases'] for x in expected['methods']} == rows
    cat = catalog[cats[0]]
    assert cat['requirements']['exclusive'] and not cat['requirements']['frontendBuild']
    mode_rows[mode] = dict(categories=cats, methods=len(rows), cases=n,
                           methodCases=rows, timeoutMinutes=cat['timeoutMinutes'])
corrective = mode_rows['close2']['methodCases']
remaining = {m:n for mode,x in mode_rows.items() if mode != 'close2' for m,n in x['methodCases'].items()}
retained = {m:n for m,n in whole.items() if m not in selected}
prior_check = data(D / 't179pd-ni-iobs-l01-cloud-source-check-20261018.json')
legacy_check = data(D / 't179pd-ni-iobs-l01-cloud-legacy3-independent-check-61e712e3-20261018.json')
retained_evidence = dict(prior_check['retained']['methodCases'])
for case in legacy_check['cases']:
    assert case['outcome'] == 'Passed' and case['method'] not in retained_evidence
    retained_evidence[case['method']] = 1
assert retained == retained_evidence and (len(retained),sum(retained.values())) == (24,40)
assert (len(corrective),sum(corrective.values())) == (2,2)
assert (len(remaining),sum(remaining.values())) == (7,15)
assert (len(selected),sum(selected.values())) == (9,17)
assert {m:n for m,n in retained.items() if m.endswith('FirstInstallOwningCloseCannotArmInterfacePermission')} == {prefix+'GuaranteedTreatment_FirstInstallOwningCloseCannotArmInterfacePermission':1}
assert cloud['qualifiedCases']['totalDistinctTargets'] == 40
assert cloud['release']['mode'] is None
assert cloud['diagnosticCorrection']['freshPreparation'] == 'REQUIRED_ZERO4_BEFORE_ANY_ACTUAL'
assert cloud['oldCorrectedCrash']['outcome'] == 'UNKNOWN' and cloud['oldCorrectedCrash']['artifactAvailability'] == 'UNAVAILABLE' and not cloud['oldCorrectedCrash']['recoveryClaim']
oldcloud = json.loads(git(R,'show',BASE+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
for key in ('rootIsolation','processIsolation','stateIsolation','assertions'):
    assert cloud['crashRequalificationPlan'][key] == oldcloud['crashRequalificationPlan'][key]
frozen_archives = []
for name, sha in [
    ('t179pd-ni-iobs-l01-cloud-close2-frozen-813f1057-files-20261018/archive.json','82477104f4dc7b6e2c68b7b56300223b6d44075f811b57f0ffbded33d1e04319'),
    ('t179pd-ni-iobs-l01-cloud-legacy3-813f1057-files-20261018/archive.json','9858024f7ba343a82f0667c65bd8d33079e709ec6463b5575e02fbad4fec57c8')]:
    path = REC+name
    assert h((R/path).read_bytes()) == sha
    assert not git(R, 'diff', '--name-only', BASE, HEAD, '--', str(Path(path).parent))
    frozen_archives.append(dict(path=path, sha256=sha, unchangedAgainstBase=True))
env_path = Path('/tmp/1536-cloud-tools/env.sh')
assert h(env_path.read_bytes()) == '5d61c19c1341cf9a26b9d361c81da8c89d09924381791c4108d968555f899c53'
root = Path(cloud['environment']['root']); runtime = root/'runtime'
assert runtime.resolve() == runtime and stat.S_IMODE(runtime.stat().st_mode) == 0o700 and runtime.stat().st_uid == 1000
assert not any(line.startswith('export HOME=') for line in env_path.read_text().splitlines())
assert Path('/proc/sys/kernel/random/boot_id').read_text().strip() == cloud['environment']['linuxBootId']
assert Path('/etc/hostname').read_text().strip() == cloud['environment']['hostname']
for root_ in (R,C):
    assert git(root_, 'rev-parse','HEAD').decode().strip() == HEAD and not git(root_, 'status','--porcelain')

out = dict(kind='INDEPENDENT_CLOSE_DIAGNOSTIC_CORRECTION_SOURCE_CHECK',
    checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    candidate=HEAD, base=BASE, sourcePacketSha256=h((R/PACKET).read_bytes()),
    sourcePinCount=98, sourcePinDigest=p['pinDigest'], changedPinnedPaths=changedpins,
    pinsMatchBothCheckoutsGitBlobModeBytes=True, productDeltaPaths=20,
    productionDiff=dict(path=PRODUCT, insertions=11, deletions=1,
        exactInverseRestoresBase=True, sha256=h(after)),
    testsFixturesLoggerAssertionsCatalogSelectionsRunnerDocsUnchanged=True,
    historicalPacketExact=historical, causalArtifacts=p['causalReview'],
    controllerSyntaxParsedWithoutExecution=True, verificationTools=p['verificationTools'],
    whole=dict(descriptors=16,methods=33,cases=57),
    corrective=dict(descriptors=1,methods=2,cases=2),
    remaining=dict(descriptors=5,methods=7,cases=15),
    selected=dict(descriptors=6,methods=9,cases=17),
    modes=mode_rows, retained=dict(methods=24,cases=40,methodCases=retained,
        provenance='Original37 compiled45cc/4c6d plus legacy3 actual9acc/compiled6ab43. No cross-build equivalence or replay claim.'),
    staticSelectionBasis='Unchanged selectors expanded against fixed28method51row ledger plus unchanged5methods6rows; exact disjoint bijection, not new discovery.',
    recovery=dict(directory=str(C), parentReceiptSha256=h(receipt_path.read_bytes()),
        independentlyVerifiedFiles=recovery, changedPaths=12, cleanBoth=True,
        fullGitTreesEqual=True, noGitAlternates=True, independentlyReadGitHubBranch=remote),
    originalArchivesPreserved=frozen_archives,
    crashIsolationSourceAndPlanUnchanged=True, oldCorrectedCrash='UNKNOWN_UNAVAILABLE_NO_RECOVERY',
    environment=dict(envSha256=h(env_path.read_bytes()),runtime=str(runtime),mode='0700',uid=1000,
        sameBootAndHostname=True,noHomeReassignment=True),
    metadataObservations=[
        'cloud.crashRequalificationPlan status/gates retain historical PREP accepted/pending20 wording; replace with current ZERO4 and separate crash5 release gate.',
        'cloud.sourceReview and freshPreparation must be explicitly labeled historical pre-correction acceptance; keep exact records/hashes.',
        'packet.ledger.currentRuntimePointer names removed fields; repoint descriptive text to currentVerificationContract/next and cloud diagnosticCorrection.'
    ],
    findings=[], eligibility='SOURCE_ONLY_NAMED_ZERO4_AFTER_SAVED_ACCEPTANCE; NO_ACTUAL_RELEASE',
    limitations='No C# compile, catalog/discovery, test/runtime execution, new PREP, current corrected runtime PASS or old lost-result recovery. No repo writes/process intervention.')
dest=D/'t179pd-ni-iobs-l01-close-diagnostic-source-independent-check-a0da72f2-20261018.json'
dest.write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps(dict(path=str(dest),bytes=dest.stat().st_size,sha256=h(dest.read_bytes()),
                     checkedPins=98,changedPins=len(changedpins),recoveredPaths=12,findings=[]),indent=2))
