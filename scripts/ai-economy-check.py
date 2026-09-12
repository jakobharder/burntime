#!/usr/bin/env python3
"""Evaluate structured headless economy observations, never human-readable telemetry."""
import argparse
import json
import math
from pathlib import Path



def mature(camp):
    return camp['Suitable'] and camp['HeldTurns'] >= 40 and camp['QuietTurns'] >= 20


def evaluate(data, mode):
    if data['SchemaVersion'] != 1 or data['Rules'] != 'extended':
        raise ValueError('Expected schema 1 and Extended rules; thresholds are not calibrated for other rules.')
    turns = data['Turns']
    if not turns:
        raise ValueError('No turn observations.')
    horizon = 200 if mode == 'solo' else 500
    window = 50 if mode == 'solo' else 100
    if data['RequestedTurns'] < horizon:
        raise ValueError(f'{mode} economy checks require at least {horizon} requested turns.')
    if [t['Turn'] for t in turns] != list(range(1, len(turns) + 1)):
        raise ValueError('Missing or unordered turn observations.')
    results = []
    for index, profile in enumerate(data['Profiles']):
        if profile != 'modern':
            continue
        rows = [t['Players'][index] for t in turns]
        windows = []
        supply_failures = []
        for start in range(window + 1, len(rows) + 1, window):
            sample = rows[start - 1:start - 1 + window]
            if len(sample) < window:
                continue
            if sum(r['SupplyDeaths'] for r in sample) >= 3:
                supply_failures.append(f'supply: turns {start}-{start + window - 1}: at least three daily supply deaths outside combat grace')
            camps = [c for r in sample for c in r['Camps'] if mature(c)]
            days = sum(any(mature(c) for c in r['Camps']) for r in sample)
            if len(camps) < window or days < window * .4:
                continue
            sustain = sum(c['Guards'] > 0 and c['FoodPerDay'] >= c['Guards'] and
                          c['WaterPerDay'] >= c['Guards'] for c in camps) / len(camps)
            productive = sum(c['Guards'] > 0 and c['ProductionTools'] > 0 and
                             c['FoodPerDay'] > c['Guards'] and c['WaterPerDay'] >= c['Guards']
                             for c in camps) / len(camps)
            base_days = sum(any(mature(c) and c['Guards'] > 0 and
                                c['FoodPerDay'] - c['Guards'] >= 2 and
                                c['WaterPerDay'] - c['Guards'] >= 2 for c in r['Camps']) for r in sample)
            distress = sum(c['GuardDistress'] > 0 for c in camps) / len(camps)
            shortages = sum(r['SupplyDamage'] > 0 and any(mature(c) for c in r['Camps']) for r in sample)
            windows.append(dict(start=start, end=start + window - 1, camp_days=len(camps),
                                player_days=days, sustainable=sustain, productive=productive,
                                base_days=base_days, distress=distress, shortage_days=shortages,
                                food_withdrawn=sum(r.get('CampFoodWithdrawn', 0) for r in sample)))
        failures = supply_failures
        boss_death = any(r['BossSupplyDeath'] for r in rows)
        if boss_death:
            failures.append('supply: boss died from daily supply damage without recent combat/hazard exposure')
        if mode == 'solo' and (len(rows) < horizon or not rows[-1]['Alive']):
            failures.append(f'development: uncontested faction did not complete {horizon} turns alive')
        if windows:
            if max(w['base_days'] for w in windows) < window * .2:
                failures.append(f'development: no {window}-turn window sustained a surplus base for {window // 5} days')
            for w in windows:
                label = f"turns {w['start']}-{w['end']}"
                if w['sustainable'] < .85:
                    failures.append(f"sustainability: {label}: {w['sustainable']:.1%} of mature camp-days (minimum 85%)")
                if w['productive'] < .50:
                    failures.append(f"production: {label}: {w['productive']:.1%} of mature camp-days (minimum 50%)")
                if w['distress'] > .10 or w['shortage_days'] >= math.ceil(window * .15):
                    failures.append(f"supply: {label}: guard distress {w['distress']:.1%}, supply-damage days {w['shortage_days']}")
        if mode == 'solo' and len(windows) < 2:
            failures.append('development: fewer than two qualified 50-turn windows')
        status = 'fail' if failures else 'pass' if windows else 'insufficient'
        results.append(dict(player=index + 1, status=status, failures=failures, windows=windows))
    if not results:
        raise ValueError('No Modern AI was observed.')
    return dict(seed=data['Seed'], difficulty=data['Difficulty'], mode=mode, players=results)


def suite(results):
    groups = {}
    for result in results:
        groups.setdefault((result['mode'], result['difficulty']), []).extend(result['players'])
    checks = []
    for (mode, difficulty), players in sorted(groups.items()):
        covered = [p for p in players if p['status'] != 'insufficient']
        passed = sum(p['status'] == 'pass' for p in covered)
        minimum_covered = len(players) if mode == 'solo' else math.ceil(len(players) / 2)
        minimum_passed = math.ceil(len(covered) * (2 / 3 if mode == 'solo' else .75))
        ok = len(covered) >= minimum_covered and passed >= max(1, minimum_passed)
        checks.append(dict(mode=mode, difficulty=difficulty, passed=passed, covered=len(covered),
                           total=len(players), required_passes=minimum_passed,
                           required_coverage=minimum_covered, ok=ok))
    return checks


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reports', nargs='+', type=Path)
    parser.add_argument('--mode', choices=['solo', 'competitive'], required=True)
    args = parser.parse_args()
    results = [evaluate(json.loads(path.read_text()), args.mode) for path in args.reports]
    checks = suite(results)
    print(json.dumps(dict(results=results, checks=checks), indent=2))
    return 0 if all(c['ok'] for c in checks) else 1


if __name__ == '__main__':
    raise SystemExit(main())
