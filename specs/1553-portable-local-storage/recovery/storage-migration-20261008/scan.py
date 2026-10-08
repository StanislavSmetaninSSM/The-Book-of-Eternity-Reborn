#!/usr/bin/env python3
"""Reproduce the lexical inventory of pinned tracked source, without builds or tests.
This is an occurrence index, not a compiler call graph or reachability proof.
"""
from pathlib import Path
import collections, gzip, hashlib, json, re, subprocess
ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
BASE = 'd0241e71e349fbe2020e4a41b6be7c627c81cbb4'
OLD = '1fc5e59'
EXTENSIONS = {'.cs','.csx','.ps1','.psm1','.py','.sh','.bash','.ts','.tsx','.js','.jsx','.mjs','.cjs','.c','.h','.cpp','.hpp'}
RULES = {
 'raw_filesystem': r'\b(?:System\.IO\.)?(?:File|Directory|FileInfo|DirectoryInfo)\.\w+\s*\(|\bnew\s+(?:FileStream|StreamWriter|FileInfo|DirectoryInfo|FileSystemWatcher)\s*\(',
 'physical_adapter': r'\b(?:PhysicalFileAuthority|ReversibleFilePublication)\.\w+|\b(?:RenameOpenedObjectRelative|CreateNewWritableFile|DeleteOpenedFile|DeleteOpenedDirectory|CreateHardLinkRelative)\s*\(',
 'current_publisher': r'\b(?:TrustedLocalFilePublication|TrustedLocalFileScope|TrustedLocalFileImage|TrustedLocalNamespacePlan|TrustedLocalPublicationOutcome|TrustedLocalPublicationDisposition|TrustedLocalPublicationPhase)\b|\b(?:PublishLocal\w*|PublishStandaloneDarenProfileAsync|RecoverTrustedLocalStorage|RequireCommittedLocalPublication|UsesTrustedLocalWriter)\s*\(',
 'facade_io': r'\b(?:WriteFile\w*|AppendFile\w*|DeleteFile\w*|DeleteDirectory\w*|CreateDirectory\w*|CopyFile\w*|MoveFile\w*|ReadOriginalFile\w*|WriteRuntime\w*|ReadRuntime\w*|WriteExact\w*)\s*\(',
 'transaction_recovery': r'\b(?:Create\w*Backup\w*|Restore\w*|Recover\w*|Roll[Bb]ack\w*|TryRestore\w*|Cleanup\w*Backup\w*|Resolve\w*(?:Recovery|Publication)\w*|Stage\w*(?:Transaction|Rollback|Snapshot)\w*|Commit\w*(?:Transaction|Publication|Progress)\w*|TryCommit\w*|Publish\w*(?:Async|Outcome|Deferred))\s*\(|\b(?:MutationIntentRecorder|IsLegacyStorageRecovery|ExternalPublicationContext|PendingLocalDecision)\b',
 'observer_hook': r'\b(?:AfterPhysicalFilePublishedAsync|BeforePhysicalSourcePublishedAsync|AfterPhysicalFileAuthorityValidatedAsync|BeforePhysicalRollbackAbsenceFinalValidationAsync|BeforeCanonicalMutationAsync|BeforeCanonicalMutationBoundaryAsync|AfterCanonicalMutationBoundaryValidatedAsync|LocalPublicationObserver|LocalPublicationRecoveryObserver|AfterFileMutationAppliedAsync|BeforeFileMutationRollback|AfterBackupsCapturedAsync|BeforeBackupCleanupAsync|AfterWriteAppliedAsync|afterWriteApplied|afterPublished|beforeSourcePublished)\b',
 'notification_refresh': r'\b(?:FileSystemWatcher|StateChanged|FileChanged)\b|\b(?:Refresh\w*(?:State|View|Snapshot|Runtime|Generation)\w*|Invalidate\w*|Notify\w*|Broadcast\w*|Publish\w*(?:Snapshot|Status|Event|Ready|State)\w*|Signal\w*)\s*\(',
 'stream_lifetime': r'\.\s*(?:Write|WriteAsync|WriteByte|WriteLine|WriteLineAsync|Flush|FlushAsync|Close|Dispose|DisposeAsync|CopyTo|CopyToAsync)\s*\(|FileMode\.(?:CreateNew|Create|OpenOrCreate|Append|Truncate)',
 'script_filesystem': r'\b(?:Set-Content|Add-Content|Out-File|New-Item|Remove-Item|Move-Item|Copy-Item|Rename-Item|Export-Clixml|Export-Csv)\b|\.(?:write_text|write_bytes|unlink|mkdir|rmdir|rename|replace|touch|open)\s*\(|\b(?:os|shutil|fs|fsp)\.(?:replace|rename|remove|unlink|mkdir|makedirs|rmdir|rmtree|copy|copy2|copytree|move|writeFile\w*|appendFile\w*|rename\w*|unlink\w*|mkdir\w*|rm\w*|createWriteStream)\s*\(|\bopen\s*\([^\n]*[\"\'][wax][b+]?|^\s*(?:cp|mv|rm|mkdir|install|tee)\s+',
 'powershell_dotnet_io': r'\[(?:System\.)?IO\.(?:File|Directory|FileStream|StreamWriter)\]::\w+|\b(?:Register-ObjectEvent|Unregister-Event|Wait-Event|WaitForChanged|EnableRaisingEvents)\b',
 'frontend_storage_events': r'\b(?:localStorage|sessionStorage|indexedDB|EventSource)\b|\.\s*(?:addEventListener|removeEventListener|dispatchEvent|invalidateQueries)\s*\(',
 'native_storage': r'\b(?:fsync|fdatasync|renameat2?|unlinkat?|mkdirat?|ftruncate|FlushFileBuffers|MoveFileEx\w*|ReplaceFile\w*|SetFileInformationByHandle|CreateFile\w*|WriteFile)\s*\(',
}
PATTERNS={k:re.compile(v,re.MULTILINE) for k,v in RULES.items()}
def git(*args): return subprocess.check_output(['git',*args],cwd=ROOT).decode()
changed=set(git('diff','--name-only',OLD+'..'+BASE).splitlines())
def classify(path,family,symbol):
    test=any(x in path for x in ['.Tests/','.IntegrationTests/','.TestSupport/','tests/','/tests/'])
    if family=='observer_hook':
        if symbol.startswith(('LocalPublicationObserver','LocalPublicationRecoveryObserver')):
            return 'migrated','current phase API; each use still needs reached-cut and ordering assertions'
        return 'remains-to-migrate','route/phase audit required; retained recorder/recovery uses may be intentional, ordinary-route assumptions are not portable'
    if any(x in path for x in ['ExplorerLocalTurnRollbackArtifacts.cs','DarenRewardProfileRollbackTransaction.cs','DarenRewardProfileFileStore.cs']):
        return 'remains-to-migrate','mixed routing: new Windows consumers retain physical journal; preserve old-evidence handlers until whole consumer cutover'
    if path.endswith('ExplorerMode.PrivateImplementation.cs') and family in ['facade_io','transaction_recovery','notification_refresh']:
        return 'remains-to-migrate','console pending rollback under excluded legacy root; exact method/caller review required'
    if path.endswith('StateDistributor.cs') and family in ['facade_io','transaction_recovery','notification_refresh']:
        return 'remains-to-migrate','sequential committed members and compensation; inspect QTE owner before choosing transaction boundary'
    if family=='physical_adapter' or path.endswith('ReversibleFilePublication.cs'):
        return 'intentional-platform-adapter-or-scoped-compatibility','physical implementation or retained old evidence; caller reachability determines whether new use must migrate'
    if family in ['current_publisher','facade_io']:
        return 'migrated','current publisher or common facade; recorder/legacy/path exclusions remain conditional and are inventoried separately'
    if family=='notification_refresh':
        return 'remains-to-migrate','ordering/outcome review required, not an asserted defect; source occurrence alone cannot establish committed-state visibility'
    if path.endswith('GameEngine.MainMenu.cs') and family=='raw_filesystem':
        return 'remains-to-migrate','unreferenced recursive safety-backup helpers are removal candidates; other calls require local context'
    if test:
        return 'allowed-technical-non-game-transaction-write','fixture setup/evidence/teardown; fault-boundary and public-consumer assertions reviewed by family, not setup count'
    if any(x in path for x in ['/TrustedLocal','/PhysicalFileAuthority','/GmSessionRunPersistence.cs','/WorkerRunLedgerPersistence.cs','/GmRuntime/','/GmWorkers/','BookOfEternityGMBridge/','tools/gm-relay/']):
        return 'intentional-platform-adapter-or-scoped-compatibility','publisher primitive or distinct owned process/IPC/workspace authority; not interchangeable with canonical game journal'
    if family=='transaction_recovery':
        return 'remains-to-migrate','consumer/outcome routing review required; mapped family determines current versus retained recovery obligation'
    return 'allowed-technical-non-game-transaction-write','raw/stream occurrence: export, initialization, tooling or transport; semantic exemptions must be verified against family table'
rows=[]; corpus=[]; omissions=[]
for entry in git('ls-tree','-r',BASE).splitlines():
    meta,path=entry.split('\t',1); blob=meta.split()[2]
    if Path(path).suffix.lower() not in EXTENSIONS: continue
    raw=(ROOT/path).read_bytes()
    actual=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()
    if actual!=blob: raise SystemExit('Pinned source differs: '+path)
    try: text=raw.decode('utf-8-sig')
    except UnicodeError:
        omissions.append({'path':path,'reason':'non-UTF8 tracked source; manual review required','gitBlob':blob});continue
    sha=hashlib.sha256(raw).hexdigest(); count=0
    for family,pattern in PATTERNS.items():
        for match in pattern.finditer(text):
            line=text.count('\n',0,match.start())+1
            symbol=match.group().strip(); classification,basis=classify(path,family,symbol)
            rows.append({'path':path,'line':line,'column':match.start()-text.rfind('\n',0,match.start()),'family':family,'symbol':symbol,'candidateClassification':classification,'reviewStatus':'lexical-only','changedSinceOld':path in changed})
            count+=1
    corpus.append({'path':path,'gitBlob':blob,'sha256':sha,'occurrences':count,'changedSinceOld':path in changed})
rows.sort(key=lambda r:(r['path'],r['line'],r['column'],r['family']))
for i,r in enumerate(rows,1):r['id']='S%05d'%i
OUT.mkdir(exist_ok=True,parents=True)
(OUT/'callsites.jsonl.gz').write_bytes(gzip.compress(''.join(json.dumps(r,ensure_ascii=False,separators=(',',':'))+'\n' for r in rows).encode(),mtime=0))
(OUT/'callsites.jsonl').unlink(missing_ok=True)
(OUT/'corpus.json').write_text(json.dumps(corpus,indent=2)+'\n')
manifest={'schemaVersion':1,'sourceRevision':BASE,'oldComparisonRevision':git('rev-parse',OLD).strip(),'trackedSourceFiles':len(corpus),'occurrences':len(rows),'byFamily':dict(collections.Counter(r['family'] for r in rows)),'byCandidateClassification':dict(collections.Counter(r['candidateClassification'] for r in rows)),'rules':RULES,'extensions':sorted(EXTENSIONS),'exclusions':['non-source extensions including docs/examples/logs/binaries/assets; embedded source in scanned scripts is retained','untracked/generated build artifacts not in pinned Git tree'],'omissions':omissions,'limits':['Lexical occurrences include definitions, comments, strings and intentional test input; not a compiler call graph.','candidateClassification is lexical triage only; reviewStatus=lexical-only grants NO approved migration/exception decision. Human reviewed family decisions are separate, conditional on actual route. Unreviewed sites stay open.','No absence-of-IO or reachability guarantee from regex alone. Human family/caller review and independent completeness review are required.','No builds/tests/runtime probes executed for this inventory.'], 'artifacts':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [OUT/'callsites.jsonl.gz',OUT/'corpus.json',Path(__file__)]}}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({k:manifest[k] for k in ['trackedSourceFiles','occurrences','byFamily','byCandidateClassification','omissions']},indent=2))
