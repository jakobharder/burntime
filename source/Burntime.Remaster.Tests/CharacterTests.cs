using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

using static Program;

static class CharacterTests
{
    internal static IEnumerable<Case<int>> DoctorLocalityCases()
    {
        yield return Int("doctor benefits stay with the patient's group", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var owner = manager.Create<HazardPlayer>(new object[] { 0 });
            var otherOwner = manager.Create<HazardPlayer>(new object[] { 1 });
            var camp = manager.Create<Burntime.Remaster.Logic.Location>();
            var patient = manager.Create(() => new HazardCharacter());
            patient.Player = owner;
            patient.Health = 70;
            patient.Place(camp);
            camp.Characters.Add(patient);
            var doctor = manager.Create(() => new HazardCharacter());
            doctor.Player = owner;
            doctor.Health = 100;
            doctor.Class = CharClass.Doctor;
            owner.Group.Add(doctor);
            Equal(false, patient.HasLocalDoctor, "boss's distant doctor cannot heal camp employees");
            owner.Group.Add(patient);
            Equal(true, patient.HasLocalDoctor, "travelling party shares its doctor");
            owner.Group.Remove(patient);
            owner.Group.Remove(doctor);
            camp.Characters.Add(doctor);
            Equal(true, patient.HasLocalDoctor, "camp doctor heals same-owner employees");
            doctor.Player = otherOwner;
            Equal(false, patient.HasLocalDoctor, "enemy doctor does not heal patient");
            doctor.Player = owner;
            doctor.Health = 0;
            Equal(false, patient.HasLocalDoctor, "dead doctor does not heal patient");
            doctor.Health = 100;
            owner.Group.Add(patient);
            Equal(false, patient.HasLocalDoctor, "camp doctor does not join visiting party treatment");
            return 0;
        });
    }
}
