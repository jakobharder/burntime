#!/usr/bin/env python3
"""Prove the economy suite rejects disabled AI development/collection in isolated source copies."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parent.parent
AI = Path('source/Burntime.Remaster/AI/PlayerBehavior')


def disable_method(path, name):
    source = path.read_text(encoding='utf-8-sig')
    pattern = re.compile(r'((?:internal|public|private|protected)?\s*static\s+void\s+' + re.escape(name) + r'\s*\([^)]*\)\s*\{)')
    source, count = pattern.subn(r'\1\n        return; // Economy regression injected into this disposable copy.\n', source)
    if count != 1:
        raise RuntimeError(f'Expected exactly one mutation target {path}:{name}, found {count}')
    path.write_text(source)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    runner = ROOT / 'scripts/ai-economy-test.py'

    def run(app, output):
        return subprocess.run(['python3', str(runner), '--app', str(app), '--output', str(output),
                               '--modes', 'solo', '--difficulties', 'hard'], check=False).returncode

    if run(args.app, args.output / 'control') != 0:
        raise RuntimeError('Unmodified control failed; mutation sensitivity cannot be established.')
    for mutation in ['no-production-upgrades', 'no-supply-collection']:
        work = args.output / mutation
        source_root = work / 'checkout'
        if source_root.exists():
            shutil.rmtree(source_root)
        shutil.copytree(ROOT / 'source', source_root / 'source', ignore=shutil.ignore_patterns('bin', 'obj', '.DS_Store'))
        # The game project embeds the changelog from outside the source tree.
        (source_root / 'resources').mkdir(exist_ok=True)
        shutil.copy2(ROOT / 'resources/Changelog.md', source_root / 'resources/Changelog.md')
        if mutation == 'no-production-upgrades':
            disable_method(source_root / AI / 'Camps/CampManagement.cs', 'InstallProductionFromPool')
            disable_method(source_root / AI / 'Camps/CampManagement.cs', 'ConstructForCamp')
            disable_method(source_root / AI / 'Economy/Construction.cs', 'ConstructPortableEconomicUpgrade')
        else:
            disable_method(source_root / AI / 'Camps/CampManagement.cs', 'CollectProducedSurplus')
            disable_method(source_root / AI / 'Camps/CampManagement.cs', 'CollectStoredTradeGoods')
            disable_method(source_root / AI / 'Group/GroupManagement.cs', 'ProvisionGroupFromCampSurplus')
            disable_method(source_root / AI / 'Core/AiTurnController.cs', 'UseLocalWaterSource')
            path = source_root / AI / 'Core/AiState.cs'
            source = path.read_text(encoding='utf-8-sig')
            source, count = re.subn(r'internal bool CanCollectLocalLoot => Player\.(?:Party|Group)\s*.*?;',
                                   'internal bool CanCollectLocalLoot => false;', source, flags=re.S)
            if count != 1:
                raise RuntimeError('Missing collection mutation target')
            path.write_text(source)
        runtime = work / 'app'
        shutil.copytree(args.app.resolve().parent, runtime, dirs_exist_ok=True)
        with (work / 'build.log').open('w') as log:
            subprocess.run(['dotnet', 'build', str(source_root / 'source/Burntime.Remaster/Burntime.Remaster.csproj'),
                            '--configuration', 'Debug', '--artifacts-path', str(work.resolve() / 'sdk'),
                            '--output', str(work.resolve() / 'compiled'), '--disable-build-servers', '--maxcpucount:1',
                            '--consoleLoggerParameters:ErrorsOnly;Summary'], stdout=log, stderr=subprocess.STDOUT, check=True)
        shutil.copy2(work / 'compiled/Burntime.Game.dll', runtime / 'Burntime.Game.dll')
        status = run(runtime / 'Burntime.dll', work / 'results')
        results = json.loads((work / 'results/results.json').read_text())
        if status != 1 or all(c['ok'] for c in results['checks']) or any(r.get('error') for r in results['results']):
            raise RuntimeError(f'{mutation}: mutation was not rejected by economic assertions')
        print(f'PASS sensitivity: {mutation} rejected by economy checks', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
