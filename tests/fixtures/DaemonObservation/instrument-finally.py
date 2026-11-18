"""#1589 deployment-only diagnostic markers; never execute or modify the source daemon.
Usage: python3 instrument-finally.py ORIGINAL NEW_OUTPUT NEW_LEDGER
Same output is reusable across phases; BOE_DAEMON_FINALLY_TRACE selects only the marker file.
"""
import hashlib
import json
import pathlib
import sys

ENV = 'BOE_DAEMON_FINALLY_TRACE'
INVALID = 'BOE_DAEMON_FINALLY_DIAGNOSTIC_INVALID'

def marker(name):
    return (f'# BOE_FINALLY_DIAGNOSTIC_BEGIN {name}\n'
            'try {\n'
            f"    if ([string]::IsNullOrWhiteSpace($env:{ENV}) -or -not [IO.Path]::IsPathFullyQualified($env:{ENV})) {{ throw 'Absolute diagnostic marker path required.' }}\n"
            f'    [IO.File]::AppendAllText($env:{ENV}, ("{name}|" + $PID + "|" + [DateTime]::UtcNow.ToString("o") + [Environment]::NewLine))\n'
            '} catch {\n'
            f'    try {{ [Console]::Error.WriteLine("{INVALID}:{name}: " + $_.Exception.ToString()) }} catch {{ }}\n'
            '}\n'
            f'# BOE_FINALLY_DIAGNOSTIC_END {name}\n')

def instrument(raw):
    if b'BOE_FINALLY_DIAGNOSTIC_BEGIN' in raw:
        raise ValueError('Source already instrumented')
    newline = '\r\n' if b'\r\n' in raw else '\n'
    source = raw.decode('utf-8')
    text = source.replace('\r\n', '\n')
    replacements = []
    def add(anchor, replacement, names):
        if text.count(anchor) != 1:
            raise ValueError('Expected one original anchor: ' + repr(anchor))
        replacements.append((anchor, replacement, names))
    add('$daemonWorkSignal = $null\n', marker('diagnostic.loaded')+'$daemonWorkSignal = $null\n', ['diagnostic.loaded'])
    anchor='finally {\n    $script:DaemonWatcherCleanupFailures = @('
    add(anchor,'finally {\n'+marker('inner-finally.enter')+marker('watcher-cleanup.before')+'    $script:DaemonWatcherCleanupFailures = @(',['inner-finally.enter','watcher-cleanup.before'])
    anchor='    ) -Subscriptions $daemonWatcherSubscriptions -Signal $daemonWorkSignal)\n'
    add(anchor,anchor+marker('watcher-cleanup.after'),['watcher-cleanup.after'])
    for kind, suffix in [('failed',' -FatalError $script:DaemonFatalError'),('stopped','')]:
        anchor=f'        Write-DaemonStatus -Status "{kind}"{suffix}\n'
        add(anchor,marker(f'status.{kind}.before')+anchor+marker(f'status.{kind}.after'),[f'status.{kind}.before',f'status.{kind}.after'])
    anchor='    Write-Log "Daemon $shutdownLabel. Turns: $($script:TurnCount), Errors: $($script:ErrorCount)" -Level "SHUTDOWN" -Color Yellow\n'
    add(anchor,anchor+marker('inner-finally.exit'),['inner-finally.exit'])
    anchor='} finally {\n    if ($script:LinuxDaemonGuard) {\n'
    add(anchor,'} finally {\n'+marker('outer-finally.enter')+'    if ($script:LinuxDaemonGuard) {\n',['outer-finally.enter'])
    anchor='        try { $script:LinuxDaemonGuard.Dispose() } finally { $script:LinuxDaemonGuard = $null }\n'
    add(anchor,marker('guard-dispose.before')+anchor+marker('guard-dispose.after'),['guard-dispose.before','guard-dispose.after'])
    # The terminal closing braces are part of the final original suffix, not a generic brace search.
    if not text.endswith(anchor+'    }\n}\n'):
        raise ValueError('Unexpected daemon final suffix')
    for old, new, _ in replacements:
        text=text.replace(old,new,1)
    text=text[:-2]+marker('outer-finally.exit')+'}\n'
    final=text.replace('\n',newline).encode('utf-8')
    # Remove marker-only blocks and require byte-exact recovery of the source.
    restored=text
    names=[name for _,_,group in replacements for name in group]+['outer-finally.exit']
    for name in names:
        if restored.count(marker(name))!=1: raise ValueError('Marker ambiguity')
        restored=restored.replace(marker(name),'',1)
    if restored.replace('\n',newline).encode('utf-8')!=raw: raise ValueError('Non-marker source delta')
    return final, {'DiagnosticOnly':True,'ProductionControlFlowUnchanged':True,
        'MarkerEnvironment':ENV,'InvalidStderrMarker':INVALID,
        'OriginalSHA256':hashlib.sha256(raw).hexdigest(),'InstrumentedSHA256':hashlib.sha256(final).hexdigest(),
        'Markers':names,'Anchors':[{'Original':old,'Count':1,'Markers':group} for old,_,group in replacements],
        'FinalSuffixAnchorCount':1,'SourceRestorationByteExact':True}

if __name__=='__main__':
    source, output, ledger=map(pathlib.Path,sys.argv[1:])
    if output.exists() or ledger.exists() or source.resolve()==output.resolve(): raise ValueError('Fresh output and ledger required')
    raw=source.read_bytes(); patched, evidence=instrument(raw)
    evidence.update(Source=str(source.resolve()),Output=str(output.resolve()),
                    PatcherSHA256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest())
    with output.open('xb') as stream: stream.write(patched)
    with ledger.open('x') as stream: json.dump(evidence,stream,indent=2)
