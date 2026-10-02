from pathlib import Path
import os,subprocess,sys
root=Path(__file__).resolve().parent
repo=Path(os.environ['BOE_SOURCE_ROOT']).resolve()
tools=Path(os.environ['BOE_TOOLCHAIN_ROOT']).resolve()
work=root/'source'
if not work.exists(): subprocess.run(['git','clone','--shared','--no-hardlinks',str(repo),str(work)],check=True)
else:
 subprocess.run(['git','fetch','--update-shallow','origin','HEAD'],cwd=work,check=True)
 subprocess.run(['git','reset','--hard','FETCH_HEAD'],cwd=work,check=True)
patch=subprocess.check_output(['git','diff','--binary','HEAD'],cwd=repo)
if patch: subprocess.run(['git','apply','--binary','-'],cwd=work,input=patch,check=True)
for f in subprocess.check_output(['git','ls-files','--others','--exclude-standard'],cwd=repo).decode().splitlines():
 target=work/f;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes((repo/f).read_bytes())
expected_head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo).strip()
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=work).strip()==expected_head
for path in ['tests/categories.json']:
 assert (work/path).read_bytes()==(repo/path).read_bytes()
env=dict(os.environ)
env.update(DOTNET_ROOT=str(tools/'dotnet'),DOTNET_CLI_TELEMETRY_OPTOUT='1',POWERSHELL_TELEMETRY_OPTOUT='1',TESTINGPLATFORM_TELEMETRY_OPTOUT='1',DOTNET_PROCESSOR_COUNT='1',NUGET_PACKAGES=str(tools/'nuget'))
env['PATH']=str(tools/'dotnet')+':'+str(tools/'powershell')+':'+env['PATH']
for key,folder in {'DOTNET_CLI_HOME':'cli','XDG_CONFIG_HOME':'config','XDG_CACHE_HOME':'cache','XDG_DATA_HOME':'data','NUGET_HTTP_CACHE_PATH':'nuget-http','NUGET_SCRATCH':'nuget-scratch','NUGET_PLUGINS_CACHE_PATH':'nuget-plugins','TMPDIR':'temp','TMP':'temp','TEMP':'temp'}.items():
 path=root/folder;path.mkdir(exist_ok=True);env[key]=str(path)
for value in [str(work), str(root), env['DOTNET_CLI_HOME'], env['TMPDIR'], env['TMP'], env['TEMP']]:
 assert Path(value).is_absolute() and '..' not in Path(value).parts
probe = [str(tools/'powershell/pwsh'), '-NoLogo', '-NoProfile', '-Command',
 "if ([IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath()) -ne $env:TMPDIR) { throw 'Unexpected runtime temp root' }; if ([IO.Path]::GetTempPath() -match '(^|[/\\\\])\\.\\.?([/\\\\]|$)') { throw 'Runtime temp contains dot segments' }; Write-Output 'Owned runtime temp root verified'" ]
subprocess.run(probe, cwd=work, env=env, check=True)
args=[str(tools/'powershell/pwsh'),'-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1',*sys.argv[1:]]
print('Running canonical category runner in isolated owned source',flush=True)
r=subprocess.run(args,cwd=work,env=env)
sys.exit(r.returncode)
