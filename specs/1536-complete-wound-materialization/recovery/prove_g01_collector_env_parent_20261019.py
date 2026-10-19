#!/usr/bin/env python3
"""Only the exact pinned environment() function with owned temporary files; no main/import/process/API."""
from pathlib import Path
import ast,hashlib,json,tempfile,stat
R=Path('/workspace/The-Book-of-Eternity-Reborn');O=Path('/tmp/1536-cloud-tools/review')
p=R/'specs/1536-complete-wound-materialization/recovery/l01-managed-stack-s01/smoke.py';raw=p.read_bytes();tree=ast.parse(raw);f=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='environment')
ns={'Path':Path};exec(compile(ast.fix_missing_locations(ast.Module(body=[f],type_ignores=[])),str(p),'exec'),ns);environment=ns['environment'];checks=[]
with tempfile.TemporaryDirectory(prefix='g01-env-parent-pure-',dir=O) as tmp:
 root=Path(tmp);phase=root/'collector-env';assert not phase.exists();caught=None
 try:environment(phase)
 except FileNotFoundError as error:caught=dict(type=type(error).__name__,errno=error.errno,failedRelativePath=str(Path(error.filename).relative_to(root)))
 assert caught==dict(type='FileNotFoundError',errno=2,failedRelativePath='collector-env/tmp');checks.append(dict(check='original absent caller-owned parent reproduces first child mkdir ENOENT',passed=True,observation=caught))
 assert not phase.exists();phase.mkdir(mode=0o700);env=environment(phase)
 keys=['TMPDIR','XDG_CACHE_HOME','XDG_CONFIG_HOME','XDG_DATA_HOME','XDG_RUNTIME_DIR','DOTNET_CLI_HOME']
 assert len({env[k] for k in keys})==6 and all(Path(env[k]).parent==phase and Path(env[k]).is_dir() and stat.S_IMODE(Path(env[k]).stat().st_mode)==0o700 for k in keys)
 checks.append(dict(check='one strict caller mkdir0700 makes unchanged function create six private children',passed=True))
 refused=False
 try:phase.mkdir(mode=0o700)
 except FileExistsError:refused=True
 assert refused;checks.append(dict(check='same caller prefix refuses existing root; no exist_ok or reuse',passed=True))
 assert set(env)=={'PATH','DOTNET_ROOT','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_SKIP_FIRST_TIME_EXPERIENCE','LANG',*keys};checks.append(dict(check='original sanitized allowlist unchanged',passed=True))
out=dict(kind='G01_SOURCE_BOUND_ENV_PARENT_FILESYSTEM_ONLY_PROOF',sourcePath=str(p.relative_to(R)),sourceSha256=hashlib.sha256(raw).hexdigest(),functionAstSha256=hashlib.sha256(ast.dump(f,include_attributes=False).encode()).hexdigest(),checks=checks,passed=len(checks),nativeChildren=0,gameCalls=0,diagnosticRequests=0,sourceEdited=False,originalRootsReadOrChanged=False,limit='Candidate caller-prefix witness only, not runtime/SOURCE/PREP acceptance for edited code.')
q=O/'g01-collector-env-parent-pure-proof-20261019.json';q.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out))
