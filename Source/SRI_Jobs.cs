using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SlaveRealismImproved
{
    // ══════════════════════════════════════════════════════════════════════════════
    //                              CORE JOB DRIVERS
    // ══════════════════════════════════════════════════════════════════════════════

    public class JD_Lovin : JobDriver
    {
        Pawn Partner => (Pawn)TargetB.Thing;
        Thing Furniture => TargetC.Thing;
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var wait = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            wait.tickAction = () => { pawn.rotationTracker.FaceTarget(Partner); if (pawn.Position.InHorDistOf(Partner.Position, 2) && Partner.CurJobDef == Defs.Job_Lovin) ReadyForNextToil(); if (pawn.IsHashIntervalTick(120) && (Partner.Dead || Partner.Downed)) EndJobWith(JobCondition.Incompletable); };
            yield return wait;
            var love = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 500 };
            love.tickAction = () => { if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            love.AddFinishAction(() => {
                try { pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.GotSomeLovin, Partner);
                    if (pawn.IsSlaveOfColony && !pawn.health.hediffSet.HasHediff(Defs.H_Stockholm) && Rand.Chance(0.2f)) { pawn.health.AddHediff(Defs.H_Stockholm); Messages.Message($"{pawn.LabelShort} → Stockholm!", pawn, MessageTypeDefOf.PositiveEvent); }
                    (pawn.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.05f);
                    if (Furniture != null) { pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_ComfortLovin); Partner.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_ComfortLovin); }
                } catch { }
            });
            yield return love;
        }
    }

    public class JD_Attend : JobDriver
    {
        Pawn Master => (Pawn)TargetA.Thing;
        Thing Furniture => TargetC.Thing;
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            var findSpot = new Toil();
            findSpot.initAction = () => { var (spot, furn) = Defs.FindLovinSpot(Master, pawn); if (spot.IsValid) { job.targetB = spot; if (furn != null) job.targetC = furn; } else EndJobWith(JobCondition.Incompletable); };
            findSpot.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findSpot;
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var attend = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            attend.tickAction = () => { pawn.rotationTracker.FaceTarget(Master); if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            attend.AddFinishAction(() => { try { pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(pawn.health.hediffSet.HasHediff(Defs.H_Stockholm) ? Defs.T_Satisfied : Defs.T_Forced, Master); GC.I?.AddLovin(Master); if (Furniture != null) pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_ComfortLovin); } catch { } });
            yield return attend;
        }
    }

    public class JD_Heal : JobDriver
    {
        Pawn Master => (Pawn)TargetA.Thing;
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 2500 };
            t.tickAction = () => { if (pawn.IsHashIntervalTick(200)) { MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "♥", Color.green); FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); } };
            t.AddFinishAction(() => { if (!Master.health.hediffSet.HasHediff(Defs.H_Healing)) { Master.health.AddHediff(Defs.H_Healing); Messages.Message($"{Master.LabelShort} healed!", Master, MessageTypeDefOf.PositiveEvent); } (pawn.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.03f); pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_ServedMaster, Master); Master.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_ReceivedService, pawn); });
            yield return t;
        }
    }

    public class JD_Punish : JobDriver
    {
        Pawn V => (Pawn)TargetA.Thing;
        public override bool TryMakePreToilReservations(bool e) => pawn.Reserve(TargetA, job);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 300 };
            t.tickAction = () => { pawn.rotationTracker.FaceTarget(V); if (pawn.IsHashIntervalTick(45)) { SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(V.Position, V.Map)); FleckMaker.ThrowMicroSparks(V.DrawPos, V.Map); } };
            t.AddFinishAction(() => { GC.I?.SetCD(V, "punish", 120000); V.health.AddHediff(Defs.H_Punished); var sup = V.needs.TryGetNeed<Need_Suppression>(); if (sup != null) sup.CurLevel = 1; foreach (var o in V.Map.mapPawns.AllPawnsSpawned.ToList().Where(p => p != V && p != pawn && p.Position.InHorDistOf(V.Position, 10) && p.IsSlaveOfColony)) { var s = o.needs.TryGetNeed<Need_Suppression>(); if (s != null) s.CurLevel = Mathf.Min(1, s.CurLevel + 0.3f); MoteMaker.ThrowText(o.DrawPos, o.Map, "!", Color.red); } });
            yield return t;
        }
    }

    public class JD_Procure : JobDriver
    {
        Pawn V => (Pawn)TargetA.Thing;
        public override bool TryMakePreToilReservations(bool e) => pawn.Reserve(TargetA, job);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            t.tickAction = () => { pawn.rotationTracker.FaceTarget(V); if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            t.AddFinishAction(() => {
                try {
                    float chance = 0.1f + pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f;
                    if (Rand.Chance(chance)) {
                        var bed = pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().FirstOrDefault(b => b.ForPrisoners && !b.Medical && !b.Destroyed && b.AnyUnownedSleepingSlot);
                        if (bed != null) { V.SetFaction(Faction.OfPlayer); V.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner); V.ownership.ClaimBedIfNonMedical(bed); if (!V.Downed) { V.jobs.StopAll(); Job gotoBed = JobMaker.MakeJob(JobDefOf.LayDown, bed); gotoBed.forceSleep = true; V.jobs.StartJob(gotoBed, JobCondition.InterruptForced); } Messages.Message($"{V.LabelShort} procured!", V, MessageTypeDefOf.PositiveEvent); MoteMaker.ThrowText(V.DrawPos, V.Map, "Procured!", Color.green); GC.I?.AddNotoriety(10f, "Procured pawn"); }
                        else Messages.Message("No prisoner bed available!", MessageTypeDefOf.RejectInput);
                    } else { MoteMaker.ThrowText(V.DrawPos, V.Map, "Refused", Color.red); Messages.Message($"{V.LabelShort} refused.", V, MessageTypeDefOf.NegativeEvent); }
                } catch (Exception e) { Log.Warning($"[SRI] Procure error: {e.Message}"); }
            });
            yield return t;
        }
    }

    public class JD_Smite : JobDriver
    {
        LocalTargetInfo T => job.targetA;
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 120 };
            t.initAction = () => MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "⚡", Color.yellow);
            t.AddFinishAction(() => {
                try {
                    if (T.Thing is Pawn v && v != null && !v.Dead && v.Spawned && v.Map != null) { v.TakeDamage(new DamageInfo(DamageDefOf.Burn, Defs.Pwr(pawn, 20, 5), 0, -1, pawn)); FleckMaker.Static(v.Position, v.Map, FleckDefOf.PsycastAreaEffect, 2); SoundDefOf.Thunder_OffMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(v.Position, v.Map))); MoteMaker.ThrowText(v.DrawPos, v.Map, "SMITE!", Color.yellow); }
                    GC.I?.SetCD(pawn, "smite", Defs.CD_Smite); GC.I?.AddNotoriety(3f, "Smite");
                    if (pawn.Map != null) foreach (var s in pawn.Map.mapPawns.SlavesOfColonySpawned.ToList().Where(x => x != null && x.Spawned && x.Position.InHorDistOf(T.Cell, 20))) { s.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_Wrath); (s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.02f); }
                } catch { }
            });
            yield return t;
        }
    }

    public class JD_Resurrect : JobDriver
    {
        Corpse C => (Corpse)TargetA.Thing;
        public override bool TryMakePreToilReservations(bool e) => pawn.Reserve(TargetA, job);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 60000 };
            t.tickAction = () => { if (pawn.IsHashIntervalTick(500)) { MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "✝", Color.cyan); FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); } };
            t.AddFinishAction(() => { var g = C?.InnerPawn; if (g != null) { GC.I?.CompleteRes(g, pawn); Messages.Message($"{g.LabelShort} resurrected!", g, MessageTypeDefOf.PositiveEvent); } });
            yield return t;
        }
    }

    public class JD_Gather : JobDriver
    {
        Corpse C => (Corpse)TargetA.Thing; Corpse G => (Corpse)TargetB.Thing;
        public override bool TryMakePreToilReservations(bool e) => pawn.Reserve(TargetA, job);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
            t.initAction = () => { var g = G?.InnerPawn; if (g != null) { int hp = C.InnerPawn.RaceProps.Humanlike ? 30 : (C.InnerPawn.RaceProps.baseBodySize > 0.7f ? 20 : 10); GC.I?.AddHP(g, hp); C.Destroy(); MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"+{hp}", Color.green); } };
            yield return t;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                         SPECIALIZATION ABILITY JOB DRIVERS
    // ══════════════════════════════════════════════════════════════════════════════

    public class JD_ChallengerRoar : JobDriver
    {
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 60 };
            t.initAction = () => { MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "ROAR!", Color.red); SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(pawn.Map); FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 5f); };
            t.AddFinishAction(() => {
                try {
                    int tauntRadius = 12;
                    var enemies = pawn.Map.mapPawns.AllPawnsSpawned.Where(p => p != pawn && !p.Dead && !p.Downed && p.HostileTo(pawn) && p.Position.InHorDistOf(pawn.Position, tauntRadius)).ToList();
                    foreach (var enemy in enemies) {
                        if (!enemy.health.hediffSet.HasHediff(Defs.H_Taunted)) enemy.health.AddHediff(HediffMaker.MakeHediff(Defs.H_Taunted, enemy));
                        enemy.jobs.StopAll(); Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, pawn); attackJob.maxNumMeleeAttacks = 999; attackJob.expiryInterval = 1200; enemy.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                        MoteMaker.ThrowText(enemy.DrawPos, enemy.Map, "!", Color.red);
                    }
                    GC.I?.SetCD(pawn, "roar", Defs.CD_Roar); GC.I?.AddNotoriety(2f, "Challenger's Roar");
                    if (enemies.Count > 0) Messages.Message($"{pawn.LabelShort} taunted {enemies.Count} enemies!", pawn, MessageTypeDefOf.PositiveEvent);
                } catch (Exception e) { Log.Warning($"[SRI] Roar error: {e}"); }
            });
            yield return t;
        }
    }

    public class JD_SoothingTouch : JobDriver
    {
        Pawn Target => (Pawn)TargetA.Thing;
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 180 };
            t.tickAction = () => { if (pawn.IsHashIntervalTick(30)) { FleckMaker.ThrowMetaIcon(Target.Position, Target.Map, FleckDefOf.Heart); MoteMaker.ThrowText(Target.DrawPos + new Vector3(0, 0, Rand.Range(-0.5f, 0.5f)), Target.Map, "♥", Color.green); } };
            t.AddFinishAction(() => {
                try {
                    var injuries = Target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i => !i.IsTended()).ToList();
                    foreach (var injury in injuries) injury.Tended(1f, 1f);
                    var diseases = Target.health.hediffSet.hediffs.Where(h => h.def.makesSickThought && !h.IsPermanent() && h.Severity < 0.7f).ToList();
                    foreach (var disease in diseases) Target.health.RemoveHediff(disease);
                    FleckMaker.Static(Target.Position, Target.Map, FleckDefOf.PsycastAreaEffect, 2f);
                    GC.I?.SetCD(pawn, "soothe", Defs.CD_Soothe);
                    Messages.Message($"{pawn.LabelShort} healed {Target.LabelShort}!", Target, MessageTypeDefOf.PositiveEvent);
                    (pawn.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.02f);
                } catch (Exception e) { Log.Warning($"[SRI] Soothe error: {e}"); }
            });
            yield return t;
        }
    }

    public class JD_MomentInsight : JobDriver
    {
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 300 };
            t.tickAction = () => { if (pawn.IsHashIntervalTick(60)) MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "💡", Color.blue); };
            t.AddFinishAction(() => {
                try {
                    var project = Find.ResearchManager.GetProject();
                    if (project != null) {
                        float basePoints = 500f; float skillBonus = pawn.skills.GetSkill(SkillDefOf.Intellectual).Level * 50f; float totalPoints = basePoints + skillBonus;
                        var researchProgress = Traverse.Create(Find.ResearchManager).Field("progress").GetValue<Dictionary<ResearchProjectDef, float>>();
                        if (researchProgress != null) { if (researchProgress.ContainsKey(project)) researchProgress[project] += totalPoints; else researchProgress[project] = totalPoints; }
                        FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 3f);
                        Messages.Message($"{pawn.LabelShort} gained insight! (+{totalPoints:F0} on {project.label})", pawn, MessageTypeDefOf.PositiveEvent);
                    } else Messages.Message("No research project selected!", MessageTypeDefOf.RejectInput);
                    GC.I?.SetCD(pawn, "insight", Defs.CD_Insight);
                    (pawn.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.02f);
                } catch (Exception e) { Log.Warning($"[SRI] Insight error: {e}"); }
            });
            yield return t;
        }
    }

    public class JD_CaptivatingPerformance : JobDriver
    {
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 300 };
            t.tickAction = () => { if (pawn.IsHashIntervalTick(40)) { FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); MoteMaker.ThrowText(pawn.DrawPos + new Vector3(Rand.Range(-1f, 1f), 0, Rand.Range(-1f, 1f)), pawn.Map, "♪", Color.magenta); } };
            t.AddFinishAction(() => {
                try {
                    int radius = 15;
                    var affected = pawn.Map.mapPawns.AllPawnsSpawned.Where(p => p != pawn && !p.Dead && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && p.Position.InHorDistOf(pawn.Position, radius)).ToList();
                    foreach (var target in affected) {
                        if (!target.health.hediffSet.HasHediff(Defs.H_Captivated)) target.health.AddHediff(Defs.H_Captivated);
                        var joy = target.needs?.joy; if (joy != null) joy.CurLevel = Mathf.Min(1f, joy.CurLevel + 0.5f);
                        if (target.InMentalState && !target.MentalState.def.IsAggro) { target.MentalState.RecoverFromState(); MoteMaker.ThrowText(target.DrawPos, target.Map, "Calmed!", Color.green); }
                        FleckMaker.ThrowMetaIcon(target.Position, target.Map, FleckDefOf.Heart);
                    }
                    FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 6f);
                    GC.I?.SetCD(pawn, "captivate", Defs.CD_Captivate);
                    Messages.Message($"{pawn.LabelShort}'s performance captivated {affected.Count} pawns!", pawn, MessageTypeDefOf.PositiveEvent);
                    (pawn.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.03f);
                } catch (Exception e) { Log.Warning($"[SRI] Performance error: {e}"); }
            });
            yield return t;
        }
    }
}
