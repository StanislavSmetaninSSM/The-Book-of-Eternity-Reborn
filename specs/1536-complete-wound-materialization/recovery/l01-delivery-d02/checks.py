"""Disposable pure staging safety checks; never import/execute controller main."""
from pathlib import Path
import ast
import hashlib
import json
import shutil
import stat
import tempfile

source=Path(__file__).with_name('phase.py')
text=source.read_text() if source.exists() else ''
nodes=[n for n in ast.parse(text).body if isinstance(n,ast.FunctionDef) and n.name in ('tree_image','mirror')]
ns=dict(Path=Path,hashlib=hashlib,shutil=shutil,stat=stat)
exec(compile(ast.Module(body=nodes,type_ignores=[]),str(source),'exec'),ns)
assert all(name in ns for name in ('tree_image','mirror')),'Missing composite exact-tree staging safety functions.'
image,mirror=ns['tree_image'],ns['mirror']
checks=[]
with tempfile.TemporaryDirectory(prefix='l01d02-pure-') as temp:
    root=Path(temp);a=root/'source';a.mkdir();(a/'a.dll').write_bytes(b'original')
    (a/'locale').mkdir();(a/'locale/x.resources.dll').write_bytes(b'resource')
    rows=image(a);out=root/'copy';mirror(a,out,rows)
    assert image(out)==rows and (out/'a.dll').stat().st_ino!=(a/'a.dll').stat().st_ino
    (out/'a.dll').write_bytes(b'private change');assert (a/'a.dll').read_bytes()==b'original'
    checks.append('exact complete independent copy; no hardlink/original mutation')
    (a/'a.dll').write_bytes(b'drift')
    try:mirror(a,root/'drift',rows)
    except AssertionError:pass
    else:raise AssertionError('Changed source accepted.')
    assert not (root/'drift').exists();checks.append('hash drift refuses before destination creation')
    (a/'a.dll').write_bytes(b'original');(a/'extra').write_bytes(b'extra')
    try:mirror(a,root/'extra-copy',rows)
    except AssertionError:pass
    else:raise AssertionError('Extra source file accepted.')
    assert not (root/'extra-copy').exists();checks.append('complete-set mismatch refuses before copy')
    (a/'extra').unlink();(a/'link').symlink_to(root/'outside')
    try:image(a)
    except AssertionError:pass
    else:raise AssertionError('Symlink accepted.')
    checks.append('symlink refuses even when broken')
print(json.dumps(dict(kind='D02_PURE_STAGING_CHECKS',passed=len(checks),checks=checks,
    controllerSha256=hashlib.sha256(source.read_bytes()).hexdigest(),sdkOrProcessActions=0),indent=2))
