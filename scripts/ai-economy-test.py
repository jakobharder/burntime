#!/usr/bin/env python3
"""Run fixed-seed economy games and retain snapshots, timelines and window failures."""
import argparse
import concurrent.futures
import importlib.util
import json
from pathlib import Path
import subprocess

spec = importlib.util.spec_from_file_location('economy_check', Path(__file__).with_name('ai-economy-check.py'))
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--seeds', default='29,71,123')
    parser.add_argument('--difficulties', default='easy,normal,hard')
    parser.add_argument('--modes', default='solo,competitive')
    parser.add_argument('--jobs', type=int, default=3)
    parser.add_argument('--record-only', action='store_true', help='Record failures without failing the command; for calibration only.')
    args = parser.parse_args()
    seeds = [int(seed) for seed in args.seeds.split(',')]
    difficulties = args.difficulties.split(',')
    modes = args.modes.split(',')
    if not seeds or len(set(seeds)) != len(seeds) or args.jobs < 1:
        parser.error('Use distinct seeds and a positive job count.')
    if len(set(difficulties)) != len(difficulties) or len(set(modes)) != len(modes):
        parser.error('Use distinct difficulties and modes to avoid colliding report paths.')
    if not set(difficulties) <= {'easy', 'normal', 'hard'} or not set(modes) <= {'solo', 'competitive'}:
        parser.error('Invalid difficulty or mode.')
    args.output.mkdir(parents=True, exist_ok=True)
    cases = [(mode, difficulty, seed) for mode in modes for difficulty in difficulties for seed in seeds]

    def run(case):
        mode, difficulty, seed = case
        name = f'{mode}-{difficulty}-{seed}'
        report = args.output / f'{name}.json'
        # A failed process must never reuse a previous run's successful data.
        report.unlink(missing_ok=True)
        command = ['dotnet', str(args.app.resolve()), '--ai-simulate', '--turns', '200' if mode == 'solo' else '500',
                   '--rules', 'extended', '--difficulty', difficulty,
                   '--ai-difficulties', ','.join([difficulty] * 4), '--seed', str(seed),
                   '--ai-profiles', 'extended,none,none,none' if mode == 'solo' else 'extended,extended,extended,extended',
                   '--economy-report', str(report), '--report', str(args.output / f'{name}.txt')]
        with (args.output / f'{name}.log').open('w') as log:
            process = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=600)
        if process.returncode:
            raise RuntimeError(f'{name}: simulation failed; see {name}.log')
        data = json.loads(report.read_text())
        expected_profiles = ['extended', 'none', 'none', 'none'] if mode == 'solo' else ['extended'] * 4
        if data['Profiles'] != expected_profiles or data['Seed'] != seed or data['Difficulty'] != difficulties_order.index(difficulty):
            raise ValueError(f'{name}: simulation configuration mismatch')
        result = check.evaluate(data, mode)
        result['name'] = name
        print(f"{name}: " + ', '.join(f"P{p['player']} {p['status']}" for p in result['players']), flush=True)
        return result

    difficulties_order = ['easy', 'normal', 'hard']
    def run_safe(case):
        try:
            return run(case)
        except (OSError, ValueError, KeyError, RuntimeError, subprocess.TimeoutExpired) as exception:
            mode, difficulty, seed = case
            name = f'{mode}-{difficulty}-{seed}'
            print(f'{name}: ERROR {exception}', flush=True)
            return dict(name=name, mode=mode, difficulty=difficulties_order.index(difficulty),
                        seed=seed, players=[], error=str(exception))

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.jobs) as executor:
        results = list(executor.map(run_safe, cases))
    checks = check.suite(results)
    ok = all(c['ok'] for c in checks) and not any(r.get('error') for r in results)
    (args.output / 'results.json').write_text(json.dumps(dict(ok=ok, results=results, checks=checks), indent=2) + '\n')
    lines = ['# Extended AI economy smoke tests', '', f"Overall: **{'PASS' if ok else 'FAIL'}**.", '',
             'Uncontested: 200 turns. Competitive: 500 turns. Extended rules.', '',
             '| Mode | Difficulty | Passing / covered | Required passes | Covered / total | Result |',
             '|---|---|---|---|---|---|']
    for c in checks:
        lines.append(f"| {c['mode']} | {difficulties_order[c['difficulty']]} | {c['passed']} / {c['covered']} | "
                     f"{c['required_passes']} | {c['covered']} / {c['total']} | {'PASS' if c['ok'] else 'FAIL'} |")
    for result in results:
        if result.get('error'):
            lines.extend(['', f"## {result['name']}: execution error", '', result['error']])
        for player in result['players']:
            if player['status'] != 'pass':
                lines.extend(['', f"## {result['name']} / P{player['player']}: {player['status']}", '',
                              *[f'- {failure}' for failure in player['failures']]])
    lines.extend(['', 'Detailed window measurements: [results.json](results.json). Each scenario also retains its raw observations, timeline and process log.'])
    (args.output / 'Summary.md').write_text('\n'.join(lines) + '\n')
    print(f"Economy suite: {'PASS' if ok else 'FAIL'}; {args.output / 'Summary.md'}")
    return 0 if (ok or args.record_only) and not any(r.get('error') for r in results) else 1


if __name__ == '__main__':
    raise SystemExit(main())
