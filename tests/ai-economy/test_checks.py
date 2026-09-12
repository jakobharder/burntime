import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('economy_check', Path(__file__).resolve().parents[2] / 'scripts/ai-economy-check.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)


def game(turns=200, requested=None):
    camp = dict(Id=1, Suitable=True, HeldTurns=0, QuietTurns=1000, Guards=1,
                FoodPerDay=3, WaterPerDay=3, ProductionTools=1, GuardDistress=0,
                FoodStock=54, WaterStock=0)
    rows = []
    for turn in range(1, turns + 1):
        owned = dict(camp, HeldTurns=turn)
        rows.append(dict(Turn=turn, Players=[dict(Player=0, Alive=True, Camps=[owned],
            SupplyDamage=0, SupplyDeaths=0, BossSupplyDeath=False, CampFoodWithdrawn=0)]))
    return dict(SchemaVersion=1, Rules='extended', RequestedTurns=requested or turns,
                Seed=29, Difficulty=2, Profiles=['modern'], Turns=rows)


def player_result(data, mode='solo'):
    return check.evaluate(data, mode)['players'][0]


class EconomyChecks(unittest.TestCase):
    def test_healthy_economy_and_full_stock_pass(self):
        self.assertEqual('pass', player_result(game())['status'])

    def test_short_solo_run_is_rejected(self):
        with self.assertRaises(ValueError):
            player_result(game(199))

    def test_competitive_requires_long_horizon(self):
        with self.assertRaises(ValueError):
            player_result(game(), 'competitive')

    def test_missing_snapshot_is_rejected(self):
        data = game()
        del data['Turns'][30]
        with self.assertRaises(ValueError):
            player_result(data)

    def test_no_development_fails(self):
        data = game()
        for row in data['Turns']:
            row['Players'][0]['Camps'][0]['FoodPerDay'] = 1
        result = player_result(data)
        self.assertEqual('fail', result['status'])
        self.assertTrue(any('development' in f for f in result['failures']))
        self.assertTrue(any('production' in f for f in result['failures']))

    def test_late_collapse_is_not_hidden_by_early_success(self):
        data = game(500)
        for row in data['Turns'][400:]:
            row['Players'][0]['Camps'][0]['FoodPerDay'] = 0
        result = player_result(data, 'competitive')
        self.assertEqual('fail', result['status'])
        self.assertTrue(any('401-500' in f for f in result['failures']))

    def test_recent_capture_gets_grace(self):
        data = game()
        for row in data['Turns']:
            if row['Turn'] < 40:
                row['Players'][0]['Camps'][0]['FoodPerDay'] = 0
        self.assertEqual('pass', player_result(data)['status'])

    def test_battle_does_not_exempt_undisturbed_rear_camp(self):
        data = game()
        for row in data['Turns']:
            camps = row['Players'][0]['Camps']
            camps.append(dict(camps[0], Id=2, QuietTurns=0))
            camps[0]['FoodPerDay'] = 0
        self.assertEqual('fail', player_result(data)['status'])

    def test_late_defeat_keeps_earned_progress(self):
        data = game(500)
        for row in data['Turns'][300:]:
            row['Players'][0].update(Alive=False, Camps=[])
        self.assertEqual('pass', player_result(data, 'competitive')['status'])

    def test_early_defeat_is_insufficient_not_pass(self):
        self.assertEqual('insufficient', player_result(game(40, 500), 'competitive')['status'])

    def test_economic_death_is_failure_even_before_maturity(self):
        data = game(40, 500)
        data['Turns'][-1]['Players'][0].update(Alive=False, BossSupplyDeath=True)
        self.assertEqual('fail', player_result(data, 'competitive')['status'])

    def test_persistent_guard_distress_fails(self):
        data = game()
        for row in data['Turns'][100:]:
            row['Players'][0]['Camps'][0]['GuardDistress'] = 1
        self.assertEqual('fail', player_result(data)['status'])

    def test_supply_damage_window_fails(self):
        data = game()
        for row in data['Turns'][100:110]:
            row['Players'][0]['SupplyDamage'] = 1
        self.assertEqual('fail', player_result(data)['status'])

    def test_zero_guard_camps_cannot_fake_success(self):
        data = game()
        for row in data['Turns']:
            row['Players'][0]['Camps'][0]['Guards'] = 0
        self.assertEqual('fail', player_result(data)['status'])

    def test_insufficient_coverage_cannot_make_suite_green(self):
        result = check.evaluate(game(40, 500), 'competitive')
        self.assertFalse(check.suite([result])[0]['ok'])

    def test_solo_allows_one_seed_variation_but_not_two(self):
        good = check.evaluate(game(), 'solo')
        bad = copy.deepcopy(good)
        bad['players'][0]['status'] = 'fail'
        self.assertTrue(check.suite([good, good, bad])[0]['ok'])
        self.assertFalse(check.suite([good, bad, bad])[0]['ok'])

    def test_maturity_and_combat_boundaries(self):
        camp = game()['Turns'][-1]['Players'][0]['Camps'][0]
        self.assertFalse(check.mature(dict(camp, HeldTurns=39)))
        self.assertFalse(check.mature(dict(camp, QuietTurns=19)))
        self.assertTrue(check.mature(dict(camp, HeldTurns=40, QuietTurns=20)))


if __name__ == '__main__':
    unittest.main()
