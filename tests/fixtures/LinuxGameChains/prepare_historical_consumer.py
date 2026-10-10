"""Prepare a GitHub-only historical build snapshot for the bounded diagnostic.

This never edits production source or integrates history into the writer branch.
Only the identical diagnostic fixture, its reflection dispatcher and category
owners overlay the historical checkout. Run the selected category separately.
"""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.request

REPOSITORY = "https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git"
BASELINE = "5657107343a0dcb58f09212f7de48f478dea45d3"
PROFILE = "BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.BrowserConsumerProfile.cs"
DRIVER = "BookOfEternityClient.TestSupport/NativeHostScenarioDriver.cs"
CATALOG = "tests/categories.json"


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()


parser = argparse.ArgumentParser()
parser.add_argument("--fixture-source", required=True)
parser.add_argument("--directory", required=True)
args = parser.parse_args()
assert len(args.fixture_source) == 40 and all(c in "0123456789abcdef" for c in args.fixture_source)
directory = Path(args.directory).resolve()
assert directory.parent == Path("/tmp") and directory.name.startswith("boe-c5-duration-baseline-")
assert not directory.exists(), "The historical comparison must use a new empty directory."
directory.mkdir()
subprocess.run(["git", "init", "--quiet", str(directory)], check=True)
subprocess.run(["git", "-C", str(directory), "remote", "add", "origin", REPOSITORY], check=True)
subprocess.run(["git", "-C", str(directory), "fetch", "--quiet", "--depth=1", "origin", BASELINE], check=True)
subprocess.run(["git", "-C", str(directory), "checkout", "--quiet", "--detach", "FETCH_HEAD"], check=True)
assert git(directory, "rev-parse", "HEAD") == BASELINE
assert not git(directory, "status", "--porcelain")
subprocess.run(["git", "-C", str(directory), "fsck", "--full", "--strict"], check=True)


def pinned_source(path):
    url = REPOSITORY.removesuffix(".git").replace("github.com/", "raw.githubusercontent.com/")
    return urllib.request.urlopen(url + "/" + args.fixture_source + "/" + path, timeout=45).read()


(directory / PROFILE).write_bytes(pinned_source(PROFILE))
driver_path = directory / DRIVER
driver = driver_path.read_text()
marker = "    internal static async Task<int> Main(string[] args)\n    {\n"
assert driver.count(marker) == 1
dispatch = """        if (args.Length == 6 && args[0] == "engine-browser-consumer-profile")
        {
            var resolver = new System.Runtime.Loader.AssemblyDependencyResolver(args[1]);
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) =>
            {
                var path = resolver.ResolveAssemblyToPath(name);
                return path == null ? null : System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            };
            var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(args[1]);
            var method = assembly.GetType("BookOfEternityClient.Tests.GameEngineTurnLifecycleTests", true)!
                .GetMethod("WriteBrowserConsumerProfileProbeAsync", BindingFlags.Public | BindingFlags.Static)!;
            await (Task)method.Invoke(null, [args[2], args[3], int.Parse(args[4]), args[5]])!;
            return 0;
        }
"""
driver_path.write_text(driver.replace(marker, marker + dispatch))
catalog_path = directory / CATALOG
catalog = json.loads(catalog_path.read_text())
current_catalog = json.loads(pinned_source(CATALOG))
ids = {"c5-original-consumer-profile", "c5-original-consumer-duration-diagnostic"}
owners = [entry for entry in current_catalog["categories"] if entry["id"] in ids]
assert len(owners) == 2 and not any(entry["id"] in ids for entry in catalog["categories"])
catalog["categories"] = owners + catalog["categories"]
catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=4) + "\n")

changed = git(directory, "diff", "--name-only", "HEAD").splitlines()
untracked = git(directory, "ls-files", "--others", "--exclude-standard").splitlines()
assert set(changed + untracked) == {PROFILE, DRIVER, CATALOG}, (changed, untracked)
production = git(directory, "ls-files", "BookOfEternityClient", "BookOfEternityGMBridge", "native", "scripts").splitlines()
assert not any(path in changed for path in production)
production_manifest = []
for path in production:
    data = (directory / path).read_bytes()
    original = subprocess.check_output(["git", "-C", str(directory), "show", BASELINE + ":" + path])
    assert data == original, path
    production_manifest.append({"Path": path, "Bytes": len(data), "SHA256": hashlib.sha256(data).hexdigest()})
overlays = []
for path in sorted(changed + untracked):
    data = (directory / path).read_bytes()
    overlays.append({"Path": path, "Bytes": len(data), "SHA256": hashlib.sha256(data).hexdigest()})
receipt = directory.parent / (directory.name + "-receipt.json")
receipt.write_text(json.dumps({
    "HistoricalSHA": BASELINE, "FixtureSourceSHA": args.fixture_source,
    "Directory": str(directory), "Source": REPOSITORY,
    "GitHubOnlyNewEmptyDirectory": True, "Shallow": True, "FsckExitCode": 0,
    "ProductionUnchanged": production_manifest, "TestOnlyOverlays": overlays,
    "Scope": "Historical diagnostic build snapshot in the same cloud; no writer branch, production changes, acceptance or history integration."
}, indent=2) + "\n")
print(json.dumps({"Directory": str(directory), "Receipt": str(receipt), "ProductionFiles": len(production), "Overlays": len(overlays)}))
