"""Reviewed three standard no-build publishes only; no world or phase creation."""
import hashlib, importlib.util, json, os, pathlib, shutil, stat, sys
P=pathlib.Path
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def safe(path):
    path=P(path)
    assert path.is_absolute() and str(path)==os.path.normpath(str(path))
    for p in (*reversed(path.parents),path):
        if p.is_symlink():raise ValueError('Linked path: '+str(p))
        if p.exists() and not (p.is_dir() or stat.S_ISREG(p.stat().st_mode)):
            raise ValueError('Special path: '+str(p))
    return path

def tree(root,source=False):
    root=safe(root);assert root.is_dir();result={}
    skip={'.git','bin','obj','TestResults','node_modules','__pycache__'} if source else set()
    for folder,dirs,files in os.walk(root,followlinks=False):
        dirs[:]=[d for d in dirs if d not in skip]
        for name in dirs+files:safe(P(folder)/name)
        for name in files:
            p=P(folder)/name;result[str(p.relative_to(root))]={'sha256':h(p),'mode':stat.S_IMODE(p.stat().st_mode)}
    return result

def main():
    manifest=safe(P(sys.argv[1]));input_hash=os.environ['MM_REVIEWED_PUBLISH_INPUT_SHA256'];assert h(manifest)==input_hash
    b=json.loads(manifest.read_text());r=safe(P(b['repo']));out=safe(P(b['destination']))
    assert not os.path.lexists(out) and out.parent.is_dir()
    def inputs():
        assert h(manifest)==input_hash
        assert tree(r,True)==b['sourceRows']
        for name,rows in b['bins'].items():assert tree(r/name/'bin/Debug/net8.0')==rows
        for f,row in b['assets'].items():
            p=safe(P(f));assert p.is_file() and h(p)==row['sha256'] and stat.S_IMODE(p.stat().st_mode)==row['mode']
        for f,digest in b['unitOutputs'].items():assert h(safe(r/f))==digest
        for name,row in b['tools'].items():
            assert str(P(shutil.which(name)).resolve(strict=True))==row['path'] and h(P(row['path']))==row['sha256']
    inputs() # Complete admission before any checkout imports or destination writes.
    sys.path.insert(0,str(r/'tests/fixtures/ProductionMain'))
    spec=importlib.util.spec_from_file_location('prep',r/'tests/fixtures/ProductionMain/prepare-mortal-live.py')
    prep=importlib.util.module_from_spec(spec);spec.loader.exec_module(prep)
    native=safe(P(b['nativeDirectory']))
    prep.native_join(r,native,b['nativeBuiltSourceCommit'])
    guardian=prep.guardian_tool(r,b['guardianTool'])
    out.mkdir(mode=0o700);(out/'logs').mkdir(mode=0o700);(out/'ship').mkdir(mode=0o700)
    results={}
    for name,rows in b['bins'].items():
        destination=out/'ship'/name;assert not os.path.lexists(destination)
        argv=[b['tools']['dotnet']['path'],'publish',str(r/name/(name+'.csproj')),'--no-build','--no-restore','-m:1','-c','Debug','-o',str(destination),'-p:BoeNativePackageDirectory='+str(native),'-p:BoeRequireNativePackage=true']
        result=prep.run_owned(argv,r,out/'logs','publish-'+name,guardian,50)
        assert result['exitCode']==0 and result['settled'] and result['stdoutEof'] and result['stderrEof'],result
        actual=tree(destination);expected=dict(rows)
        for rel,source in b['publishExtras'].items():assert rel not in expected;expected[rel]=b['assets'][source]
        assert actual==expected
        results[name]={'process':result,'files':actual}
        inputs() # Publish must not rebuild/alter any admitted source/bin/asset.
    (out/'published.json').write_text(json.dumps({'sourceCommit':b['sourceCommit'],'inputSHA256':input_hash,'zeroOverlays':True,'packages':results},indent=2)+'\n')

if __name__=='__main__':main()
