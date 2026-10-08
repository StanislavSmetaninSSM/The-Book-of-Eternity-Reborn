"""Read-only Git inventory. Writes only audit data beside this script; no runtime execution."""
import collections, gzip, hashlib, json, pathlib, re, subprocess
OUT = pathlib.Path(__file__).resolve().parent
BASE = '1fc5e59b253c358a8622df66a4e9459f5f3fe78b'
MAIN = 'd0241e71e349fbe2020e4a41b6be7c627c81cbb4'
MIGRATION = '26b36a08e833849db3e3176355e8d61c88100d2f'
def git(*args): return subprocess.check_output(['git', *args])
def tree(ref):
    result = {}
    for item in git('ls-tree', '-rz', ref).split(b'\0'):
        if item:
            meta, path = item.split(b'\t', 1)
            mode, kind, oid = meta.decode().split()
            result[path.decode()] = oid
    return result

def classify(path):
    if '/recovery/fixtures/' in path: return 'historical-recovery-fixture', 'Retained source-derived recovery fixture; indexed, not current execution evidence'
    if '/recovery/' in path:
        return 'historical-evidence', 'Retained logs, archives, evidence, verifier/probe source; no runtime repair authority or current PASS claim'
    if path.startswith('specs/'): return 'specification', 'Governance/contract/provenance; read relevant current plans, not every historical narrative'
    if path.startswith(('BookOfEternityClient.Tests/', 'BookOfEternityClient.IntegrationTests/', 'BookOfEternityClient.TestSupport/', 'tests/')) or '/test/' in path: return 'test-support-catalog', ''
    if path.endswith(('.csproj','.props','.targets','.yml','.yaml')) or path.startswith(('.github/','.specify/')) or pathlib.Path(path).name == '.gitignore': return 'build-config', ''
    if path.endswith(('.md','.txt')): return 'documentation', ''
    if path.endswith(('.ps1','.psm1','.sh','.py','.c','.h')): return 'script-native', ''
    if path.endswith(('.cs','.ts','.tsx','.js','.jsx','.json','.html','.css')): return 'production-source-config', ''
    return 'other', 'Per-file inventory only; inspect if later identified as runtime input'

def family(path):
    if '/recovery/' in path: return 'evidence-fixture'
    if path.startswith('specs/'): return 'specifications'
    if path.startswith('BookOfEternityClient.WebFrontend/'): return 'frontend-load-save-settings'
    if '/GmWorkers/' in path or 'GmWorker' in path or 'NativePool' in path or path.startswith('native/'): return 'gm-workers-native'
    if '/GmRuntime/' in path or path.startswith('BookOfEternityGMBridge/') or any(s in path for s in ('GmMain','GmBridge','GmLoad','GmSession','OwnedTerminal','GmDaemon','GmRelay','GmSynchronized','GmExternal','GmConnected','GmMini')): return 'gm-main-bridge-protocol'
    if '/Launcher/' in path or path.endswith('game_master_daemon.ps1') or path.startswith('tools/'): return 'launcher-daemon-relay'
    if any(s in path for s in ('Audio','Clipboard','Desktop','TextComposer','ImageService','ConsoleAppearance')): return 'desktop-input-audio'
    if any(s in path for s in ('LocalSettings','SettingsPreview','SettingsSession','SystemMod','PortableBrowserSettings','PortablePreparedSettings')): return 'settings-publication'
    if '/WebUi/' in path or 'Browser' in path: return 'web-coordinator-http'
    if '/Core/GameEngine' in path or 'GameEngine' in path or 'StateManager' in path or '/UI/' in path: return 'game-lifecycle-commands'
    if any(s in path for s in ('FileSystem','TrustedLocal','Portable','SaveLoad','CanonicalRoot','PhysicalFile','SessionOperation')): return 'storage-and-load'
    if path.startswith(('scripts/','tests/','.github/')): return 'test-runner-catalog-ci'
    if '.Tests/' in path or '.IntegrationTests/' in path or '.TestSupport/' in path: return 'affected-test-neighbors'
    if path.startswith('BookOfEternityClient/Services/') or '/IO/' in path: return 'normalizer-gameplay-consumers'
    return 'governance-build-docs'

# These are boundary search keys, not parser-resolved call edges. Include protocol/path strings.
GROUPS = {
 'R01': 'AppendFileAtomicIfCurrentSessionAsync WriteFileAtomicBytesCoreAsync WriteFileAtomicAsync WriteFileAtomicBytesAsync AppendFileAtomicAsync CompareExchangeFileBytesAsync DeleteFile UsesTrustedLocalWriter LocalPublicationObserver LocalPublicationRecoveryObserver AfterPhysicalFilePublishedAsync BeforePhysicalSourcePublishedAsync AfterPhysicalFileAuthorityValidatedAsync BeforePhysicalRollbackAbsenceFinalValidationAsync BeforeCanonicalMutationAsync BeforeCanonicalMutationBoundaryAsync AfterCanonicalMutationBoundaryValidatedAsync',
 'R02': 'ReadOriginalFileBytesAsync OriginalFileExists ReadFileSnapshotAsync OpenOrdinaryReadFileAsync AfterExactPhysicalReadInitialValidationAsync AfterCanonicalReadInitialValidationAsync BeforeCanonicalReadOpenAsync AfterCanonicalReadAttemptAsync BeforeCanonicalExistenceFollowUpProbeAsync',
 'R03': 'CreateBackup RestoreBackup CleanupBackup ResolveBackupPublicationRecovery DistributeAsync',
 'R04': 'CanonicalDirectoryDeletionUncertainException DeleteDirectoryTree FindStorageDecisionFailure TryCommitAsync TryCommitWithHookAsync CoordinatedStatePublicationUncertainException RequireCommittedLocalPublication CanonicalStateWriteException RestoreBeforeImagesAsync FailClosedAcceptedTurnCanonicalRefreshAsync',
 'R05': 'EnsurePendingLocalTurnRollbackSnapshotAsync ConsumePendingLocalTurnRollbackSnapshot RestorePendingLocalTurnRollbackSnapshot CleanupPendingLocalTurnRollbackSnapshot explorer_local_turn_rollback StageBrowserWriteTransactionAsync StageLocalBrowserTransactionAsync afterRollback prepareAfterRollback RefreshGameStateAfterExactRollbackAsync',
 'R06': 'DarenRewardProfileFileStore DarenRewardProfileRollbackTransaction PublishStandaloneDarenProfileAsync qte_showcase_rewards.json @daren_reward_profile',
 'R07': 'WriteFileAtomicWithPublicationAsync WriteFileAtomicWithPublicationIfCurrentAuthorityAsync WriteFileAtomicBytesIfCurrentOwnedAsync WriteFileAtomicBytesIfCurrentAuthorityAsync DeleteFileIfCurrentOwnedAsync DeleteFileIfCurrentAuthorityAsync RetireInactivePendingTurnSnapshotEvidenceAsync',
 'R08': 'SaveGameAsync AutosaveAsync CreateSaveAsync CreateAutosaveAsync PrepareSaveArchiveAsync PublishPreparedSaveAsync BeforeSaveCommitAsync BeforeAutosaveCleanupLeaseAcquisitionAsync BeforeAutosaveDeletionAsync BeforeAutosaveRetentionAsync CommittedSaveContinuationException',
 'R09': 'CreateGameSessionSafetyBackup RestoreGameSessionSafetyBackup CleanupGameSessionSafetyBackup CopyDirectoryRecursive LoadGameAsync LoadGameWithOutcomeAsync LoadGameWithAdmissionAsync LoadSelectedSaveAndRebindRuntimeAsync RebindRuntimeAfterSessionReplacementAsync ILoadTransactionOperations BeforeLoadDirectoryMoveAsync AfterLoadDirectoryMoveAsync AfterLoadStagingFinalValidationAsync BeforeLoadPublicationGuardRootDeleteAsync AfterLoadPublicationValidatedAsync LoadOperationObserver',
 'R10': 'ClearGameStateAsync RotateSessionGeneration GetOrCreateSessionGeneration ReadExistingSessionGeneration SessionOperationContext RunBoundAsync RunParticipatingCurrentSessionAsync RunParticipatingBootstrapAsync MainOperationContinuationException SessionOperationFailure',
 'R11': 'LoadSettingsAsync SaveSettingsAsync BootstrapLocalStorageAsync EnsureSettingsFileExistsAsync WriteManifestForGmAsync WriteGameSettingsForGm LocalSettingsPreparation ConsoleSettingsSession PersistenceWarning persistenceStatus',
 'R12': 'ApplyPlayerSoulProfileClientAuthorityAsync ApplyPlayerSoulProfileClientAuthorityForRefreshAsync RefreshGameStateAsync AcceptedMechanicsPlanCache InvalidateAcceptedMechanicsHandoffs UiNotification WoundPlayerNotification AfterlifeNotificationState',
 'R13': 'AfterRuntimeWrittenAsync AfterDeferredEffectMutationAsync AfterLegacyAcceptanceRuntimeWrittenAsync AfterLegacyDeferredEffectMutationAsync TryCommitWithHookAsync ApplyTerminalOutcomeStateChangesCoreAsync',
 'R14': 'WriteStatusFile QueueOriginalStatusPublication OpenOriginalStatusPublication gm_bridge_status.json inputBindingId Get-GmBridgeStatus New-GmPromptOperation Read-BridgeStatus Invoke-BridgeRequest',
 'R15': 'GmMainParticipatingControl Invoke-GmParticipatingConsumer Invoke-GmParticipatingMutation BeginMainAdmission GmMainOperationProtocol mutationParticipating beginMainOperation endMainOperation ClosedObserved',
 'R16': 'GmLoadSessionOperation beginLoadSession finishLoadSession StartedNotReady FreshLaunchRequired freshLaunchRequired load-complete load-cancel load-state executeBrowserLoad',
 'R17': 'RunTaskAsync HasValidatedExecutionFor GmWorkerExecutionAuthority WorkerRelease GmWorkerBackendSelector GmWorkerNativePoolAdmission GmWorkerRunLedger WorkerPurpose GmWorkerDispatchAdmission',
 'R18': 'GmWorkerProcessHostFrameChannel GmWorkerProcessHostPeerIdentity GetNamedPipeClientProcessId SO_PEERCRED WorkerPid Launch Release Ready GmWorkerExecutionWorkspace GmWorkerQuarantineReaper',
 'R19': 'GmWorkerApplyGate BeginWorkerApplyTransactionAsync CommitWorkerApplyTransactionAsync RollbackWorkerApplyTransactionAsync PendingLocalDecision GmWorkerSyntheticBundlePublication MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync',
 'R20': 'Write-BoeJson FileSystemWatcher Register-ObjectEvent Process-Turn Process-Qte Process-ValidationRepair Ready gm_main_operation.ps1',
 'R21': 'ConPTYBridge OwnedTerminal NativeLineage SystemdUser GmBridgeBackend GmCliInputProfile PromptDispatch input-binding retention-full TerminalScreen InputLifetime',
 'R22': 'relay_cli.py relay_contract relay_platform relay_worker.py relay-apply-response.ps1 BOE_RELAY requestId started.json response.json closed.json',
 'R23': 'TryReadText ClipboardReadOutcome TextComposerInputClosedException TryResolveClipboardShortcut ResolveClipboardPlayerInput IsClipboardPasteShortcut /paste /вставить',
 'R24': 'PlayCue ApplySettingsAsync StopAllAsync DisposeAsync AudioOutcome CleanupUncertain BrowserManaged BeginSettingsPreview',
 'R25': 'OpenImageInViewer OpenImagesFolder OpenFolderOrPrintPath DesktopPathOpener CleanupExtraImages TryDeleteFile ExportEntityImage WriteAttractionRequestAsync',
 'R26': 'BrowserRequestAdmission RunStateRequestAsync BuildGameScreenAsync loadBrowserState refreshAfterLoad refreshAfterSave operationEpoch publicationOwner continuationBlocked',
 'R27': 'Get-TestCategoryCiMatrix ciRunner BOE_CI_SELECTION PlanOnly ValidateCatalog BOE_REPO_ROOT BoeNativePackageDirectory operational-resources',
}
# Include the complete facade-hook declaration census from all pinned versions,
# not only the publication hooks selected by name above. Definitions and consumers
# remain lexical references until a reviewer traces their concrete route.
for ref in (BASE, MAIN, MIGRATION):
    source = git('show', ref + ':BookOfEternityClient/Core/FileSystemManager.cs').decode()
    header = source.split('internal sealed class FileSystemManagerHooks', 1)[1].split('public enum CanonicalFileMutationResult', 1)[0]
    GROUPS.setdefault('R28', '')
    GROUPS['R28'] += ' ' + ' '.join(re.findall(r'internal[^\n]+?\s+(\w+)\s*\{\s*get;', header))
GROUPS['R28'] = ' '.join(sorted(set(GROUPS['R28'].split())))
(OUT/'boundary-keys.json').write_text(json.dumps(GROUPS,ensure_ascii=False,indent=2)+'\n')
trees={r:tree(r) for r in (BASE,MAIN,MIGRATION)}
net={}
for line in git('diff','--numstat','--no-renames',BASE,MAIN).decode().splitlines():
    added, deleted, path=line.split('\t',2); net[path]=(added,deleted)
commits=git('rev-list','--reverse',f'{BASE}..{MAIN}').decode().splitlines()
changes=collections.defaultdict(list)
commit_rows=[]
for commit in commits:
    parents=git('show','-s','--format=%P',commit).decode().strip().split()
    subject=git('show','-s','--format=%s',commit).decode().strip()
    eligible=[parent for parent in parents if subprocess.run(['git','merge-base','--is-ancestor',BASE,parent],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0]
    comparison=eligible[0] if eligible else parents[0]
    # PR1554 contains the starting checkpoint as its second parent. Compare that
    # parent, not pre-WIP main, or unrelated wound changes become false audit scope.
    changed=git('diff-tree','--no-commit-id','--name-only','-r','--no-renames',comparison,commit).decode().splitlines()
    commit_rows.append({'sha':commit,'parents':parents,'subject':subject,'comparisonParent':comparison,'changedCount':len(changed)})
    for path in changed: changes[path].append(commit)
rows=[]
for path in sorted(net.keys()|changes.keys()):
    kind,reason=classify(path)
    rows.append(dict(path=path,kind=kind,family=family(path),excludedFromRuntimeSemanticAudit=bool(reason),reason=reason,
        netChanged=path in net,added=net.get(path,(None,None))[0],deleted=net.get(path,(None,None))[1],
        baseBlob=trees[BASE].get(path),mainBlob=trees[MAIN].get(path),migrationBlob=trees[MIGRATION].get(path),
        changesets=changes[path],review='family coverage; not an assertion that every line/caller was semantically verified'))
def gzjson(name,records):
    data=''.join(json.dumps(r,ensure_ascii=False,separators=(',',':'))+'\n' for r in records).encode()
    (OUT/name).write_bytes(gzip.compress(data,mtime=0));return len(records)
gzjson('changed-files.jsonl.gz',rows)
(OUT/'changed-runtime-files.tsv').write_text('path\tkind\tfamily\tadded\tdeleted\n'+''.join(f"{r['path']}\t{r['kind']}\t{r['family']}\t{r['added']}\t{r['deleted']}\n" for r in rows if not r['excludedFromRuntimeSemanticAudit']))
(OUT/'changesets.json').write_text(json.dumps(commit_rows,ensure_ascii=False,indent=2)+'\n')
# Batch pinned blobs; the worktree may contain only the report or another checkout.
keys=collections.defaultdict(list)
for group,value in GROUPS.items():
    for key in value.split():keys[key].append(group)
pattern=re.compile('|'.join(re.escape(k) for k in sorted(keys,key=len,reverse=True)))
allowed={'.cs','.csx','.ps1','.psm1','.sh','.py','.c','.h','.cpp','.ts','.tsx','.js','.jsx','.json','.yml','.yaml','.csproj','.props','.targets','.md','.txt','.html'}
corpus=[];oids=set()
for ref,members in trees.items():
    for path,oid in members.items():
        if '/recovery/' in path or path.startswith('specs/') or pathlib.Path(path).suffix.lower() not in allowed: continue
        corpus.append((ref,path,oid));oids.add(oid)
proc=subprocess.Popen(['git','cat-file','--batch'],stdin=subprocess.PIPE,stdout=subprocess.PIPE)
content={}
for oid in sorted(oids):
    proc.stdin.write((oid+'\n').encode());proc.stdin.flush()
    header=proc.stdout.readline().split();size=int(header[2]);data=proc.stdout.read(size);proc.stdout.read(1)
    content[oid]=data
proc.stdin.close();proc.wait()
refs=[];pins=[]
for ref,path,oid in corpus:
    data=content[oid]
    if b'\0' in data: continue
    try: lines=data.decode('utf-8-sig').splitlines()
    except UnicodeDecodeError: continue
    pins.append({'ref':ref,'path':path,'blob':oid,'sha256':hashlib.sha256(data).hexdigest()})
    for line,source in enumerate(lines,1):
        found=sorted(set(pattern.findall(source)))
        if found:refs.append({'ref':ref,'path':path,'line':line,'keys':found,'groups':sorted({g for k in found for g in keys[k]}),'text':source.strip(),'review':'lexical-reference-not-proven-call-edge'})
gzjson('boundary-references.jsonl.gz',refs);gzjson('reference-corpus.jsonl.gz',pins)
summary={'base':BASE,'main':MAIN,'migration':MIGRATION,'equalBaselineIntegrationTree':git('rev-parse',BASE+'^{tree}').decode().strip()==git('rev-parse','f6dc2a1c^{tree}').decode().strip(),
 'baselineTree':git('rev-parse',BASE+'^{tree}').decode().strip(), 'netChangedFiles':len(net),'historyUnionFiles':len(rows),'historyOnlyFiles':sum(not r['netChanged'] for r in rows),'commits':len(commits),
 'byKind':dict(collections.Counter(r['kind'] for r in rows)),'byFamily':dict(collections.Counter(r['family'] for r in rows)),
 'referenceCorpusRows':len(pins),'lexicalReferenceRows':len(refs),'boundaryGroups':len(GROUPS),
 'limits':['All changed path metadata enumerated; family-level semantic review is not every-edge proof.','Historical evidence blobs/archives not decompressed or treated as current test results.','Reflection/generated paths/string concatenation can escape literal boundary keys.','No builds, tests, discovery, PlanOnly, native probes or live processes executed.']}
(OUT/'manifest.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(summary,ensure_ascii=False,indent=2))
