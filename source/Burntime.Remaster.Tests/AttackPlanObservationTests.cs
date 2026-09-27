using System.Collections.Generic;
using Burntime.Remaster.AI;

namespace Burntime.Remaster.Tests;

static class AttackPlanObservationTests
{
    internal static IEnumerable<Program.Case<bool>> Cases()
    {
        yield return Program.Bool("ordinary preparation has no repeat warning", true, () =>
        {
            var report = new AttackPlanObservation();
            report.Record(0, 3, 10);
            report.Record(0, 3, 20);
            return report.Describe() == "None.\n";
        });
        yield return Program.Bool("three starts within sixty days are reported", true, () =>
        {
            var report = new AttackPlanObservation();
            foreach (int day in new[] { 10, 30, 70 }) report.Record(0, 3, day);
            return report.Describe().Contains("P1, location 3: 3 attack-plan starts on days 10–70");
        });
        yield return Program.Bool("separate factions and targets do not combine", true, () =>
        {
            var report = new AttackPlanObservation();
            report.Record(0, 3, 10); report.Record(1, 3, 20); report.Record(0, 4, 30);
            return report.Describe() == "None.\n";
        });
        yield return Program.Bool("widely spaced restarts are not flagged", true, () =>
        {
            var report = new AttackPlanObservation();
            foreach (int day in new[] { 10, 40, 71 }) report.Record(0, 3, day);
            return report.Describe() == "None.\n";
        });
        yield return Program.Bool("later repeated plans are found after an old start", true, () =>
        {
            var report = new AttackPlanObservation();
            foreach (int day in new[] { 1, 100, 110, 120 }) report.Record(0, 3, day);
            return report.Describe().Contains("3 attack-plan starts on days 100–120");
        });
    }
}
