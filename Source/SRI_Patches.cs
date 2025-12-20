using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SlaveRealismImproved
{
    // ══════════════════════════════════════════════════════════════════════════════
    //                              HAREM TAB (UI)
    // ══════════════════════════════════════════════════════════════════════════════

    public class HaremTab : MainTabWindow
    {
        Vector2 scroll; Pawn sel; int currentTab = 0;
        public override Vector2 RequestedTabSize => new(700, 580);

        public override void DoWindowContents(Rect r)
        {
            var m = Find.CurrentMap; if (m == null) return;
            var god = m.mapPawns.FreeColonistsSpawned.FirstOrDefault(Defs.IsGod);
            var slaves = m.mapPawns.SlavesOfColonySpawned.ToList();

            // Tab buttons
            float tabWidth = r.width / 3f;
            if (Widgets.ButtonText(new Rect(0, 0, tabWidth - 2, 28), "Harem")) currentTab = 0;
            if (SRI_Mod.S.enableFaithSystem && Widgets.ButtonText(new Rect(tabWidth, 0, tabWidth - 2, 28), $"Faith ({GC.I?.Faith:F0})")) currentTab = 1;
            if (SRI_Mod.S.enableNotoriety && Widgets.ButtonText(new Rect(tabWidth * 2, 0, tabWidth - 2, 28), $"Notoriety ({GC.I?.Notoriety:F0})")) currentTab = 2;

            float y = 35;
            switch (currentTab) { case 0: DrawHaremTab(r, y, god, slaves, m); break; case 1: DrawFaithTab(r, y, god, m); break; case 2: DrawNotorietyTab(r, y, m); break; }
        }

        void DrawHaremTab(Rect r, float y, Pawn god, List<Pawn> slaves, Map m)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, y, r.width, 30), "═══ Harem Management ═══"); Text.Font = GameFont.Small; y += 35;
            if (god != null) { var div = god.health.hediffSet.GetFirstHediffOfDef(Defs.H_Divine); Widgets.Label(new Rect(0, y, r.width, 22), $"God: {god.LabelShort}  |  Slaves: {slaves.Count}  |  Power: {div?.Severity ?? 0:F0}"); }
            else Widgets.Label(new Rect(0, y, r.width, 22), "No God present"); y += 24;

            float avg = slaves.Count > 0 ? slaves.Average(s => s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion)?.Severity ?? 0) * 100 : 0;
            int t4 = slaves.Count(s => Defs.Tier(s) >= 4), t3 = slaves.Count(s => Defs.Tier(s) == 3);
            Widgets.Label(new Rect(0, y, r.width, 22), $"Avg: {avg:F0}%  |  Zealots: {t4}  |  Devoted: {t3}"); y += 28;
            Widgets.DrawLineHorizontal(0, y, r.width); y += 5;

            // Headers
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0, y, 130, 22), "Name"); Widgets.Label(new Rect(130, y, 70, 22), "Tier"); Widgets.Label(new Rect(200, y, 70, 22), "Dev%");
            Widgets.Label(new Rect(270, y, 90, 22), "Spec"); Widgets.Label(new Rect(360, y, 90, 22), "Master"); Widgets.Label(new Rect(460, y, 80, 22), "Status");
            GUI.color = Color.white; y += 22;

            // List
            Rect listR = new(0, y, r.width, r.height - y - 95);
            Rect viewR = new(0, 0, listR.width - 20, slaves.Count * 26);
            Widgets.BeginScrollView(listR, ref scroll, viewR);
            float ly = 0;
            foreach (var s in slaves.OrderByDescending(Defs.Tier))
            {
                Rect row = new(0, ly, viewR.width, 24);
                if ((int)(ly / 26) % 2 == 0) Widgets.DrawLightHighlight(row);
                if (sel == s) Widgets.DrawHighlightSelected(row);
                if (Widgets.ButtonInvisible(row)) sel = s;

                int tier = Defs.Tier(s); var dev = s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion); var master = GC.I?.GetMaster(s);
                string spec = GetSpec(s), status = s.health.hediffSet.HasHediff(Defs.H_Stockholm) ? "Stockholm" : s.health.hediffSet.HasHediff(Defs.H_HeadConcubine) ? "Head" : "-";
                GUI.color = Defs.TierColor(tier); Widgets.Label(new Rect(0, ly, 130, 24), s.LabelShort); GUI.color = Color.white;
                Widgets.Label(new Rect(130, ly, 70, 24), Defs.TierName(tier)); Widgets.Label(new Rect(200, ly, 70, 24), $"{(dev?.Severity ?? 0) * 100:F0}%");
                Widgets.Label(new Rect(270, ly, 90, 24), spec); Widgets.Label(new Rect(360, ly, 90, 24), master?.LabelShort ?? "-"); Widgets.Label(new Rect(460, ly, 80, 24), status);
                ly += 26;
            }
            Widgets.EndScrollView();

            // Actions
            if (sel != null)
            {
                y = r.height - 88; Widgets.DrawLineHorizontal(0, y, r.width); y += 5;
                Widgets.Label(new Rect(0, y, 200, 22), $"Selected: {sel.LabelShort}"); y += 24;
                float bx = 0;
                if (Widgets.ButtonText(new Rect(bx, y, 100, 26), "Assign Master")) { var opts = new List<FloatMenuOption> { new("Unassign", () => GC.I?.SetConcubine(sel, null)) }; foreach (var c in m.mapPawns.FreeColonists) opts.Add(new(c.LabelShort + (GC.I?.IsConcubine(sel, c) == true ? " ✓" : ""), () => GC.I?.SetConcubine(sel, c))); Find.WindowStack.Add(new FloatMenu(opts)); } bx += 105;
                if (Defs.Tier(sel) >= 3 && SRI_Mod.S.enableSpecializations && Widgets.ButtonText(new Rect(bx, y, 90, 26), "Specialize")) { var opts = new List<FloatMenuOption> { new("Warrior", () => SetSpec(sel, Defs.H_Warrior)), new("Healer", () => SetSpec(sel, Defs.H_Healer)), new("Scholar", () => SetSpec(sel, Defs.H_Scholar)), new("Entertainer", () => SetSpec(sel, Defs.H_Entertainer)), new("Remove", () => ClearSpec(sel)) }; Find.WindowStack.Add(new FloatMenu(opts)); } bx += 95;
                if (Widgets.ButtonText(new Rect(bx, y, 60, 26), "Go To")) CameraJumper.TryJumpAndSelect(sel); bx += 65;
                if (Widgets.ButtonText(new Rect(bx, y, 85, 26), "Stockholm") && !sel.health.hediffSet.HasHediff(Defs.H_Stockholm)) sel.health.AddHediff(Defs.H_Stockholm);
            }
        }

        void DrawFaithTab(Rect r, float y, Pawn god, Map m)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, y, r.width, 30), "═══ Faith & Divine Edicts ═══"); Text.Font = GameFont.Small; y += 35;
            float faith = GC.I?.Faith ?? 0; float maxFaith = GC.I?.MaxFaith ?? 500f;
            Widgets.Label(new Rect(0, y, 100, 22), $"Faith: {faith:F0}");
            Widgets.FillableBar(new Rect(100, y + 2, 300, 18), faith / maxFaith, SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 0.7f, 0.2f)));
            Widgets.Label(new Rect(410, y, 100, 22), $"/ {maxFaith:F0}"); y += 28;
            float dailyGen = 0f; foreach (var slave in m.mapPawns.SlavesOfColonySpawned) { var dev = slave?.health?.hediffSet?.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion; if (dev != null) dailyGen += dev.GetFaithGeneration(); }
            Widgets.Label(new Rect(0, y, r.width, 22), $"Daily Faith Generation: +{dailyGen:F1}"); y += 30;
            Widgets.DrawLineHorizontal(0, y, r.width); y += 10;
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, y, r.width, 24), "Divine Edicts"); Text.Font = GameFont.Small; y += 28;

            foreach (var edict in EdictDef.AllEdicts)
            {
                Widgets.DrawBoxSolid(new Rect(0, y, r.width - 10, 70), new Color(0.15f, 0.15f, 0.15f));
                GUI.color = edict.iconColor; Widgets.Label(new Rect(10, y + 5, 200, 22), edict.label); GUI.color = Color.white;
                Widgets.Label(new Rect(10, y + 25, r.width - 130, 40), edict.description);
                bool canAfford = faith >= edict.faithCost;
                if (canAfford) { if (Widgets.ButtonText(new Rect(r.width - 110, y + 20, 100, 30), $"Cost: {edict.faithCost}")) GC.I?.ActivateEdict(edict, m); }
                else { GUI.color = Color.gray; Widgets.ButtonText(new Rect(r.width - 110, y + 20, 100, 30), $"Cost: {edict.faithCost}"); GUI.color = Color.white; }
                y += 75;
            }
        }

        void DrawNotorietyTab(Rect r, float y, Map m)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, y, r.width, 30), "═══ Notoriety & The Palace of Hidra ═══"); Text.Font = GameFont.Small; y += 35;
            float notoriety = GC.I?.Notoriety ?? 0; float maxNotor = GC.I?.MaxNotoriety ?? 1000f;
            Color notorColor = notoriety < 150 ? Color.green : notoriety < 300 ? Color.yellow : notoriety < 500 ? new Color(1f, 0.5f, 0f) : Color.red;
            Widgets.Label(new Rect(0, y, 120, 22), $"Notoriety: {notoriety:F0}");
            Widgets.FillableBar(new Rect(120, y + 2, 300, 18), notoriety / maxNotor, SolidColorMaterials.NewSolidColorTexture(notorColor));
            Widgets.Label(new Rect(430, y, 100, 22), $"/ {maxNotor:F0}"); y += 30;
            string threatLevel = notoriety < 150 ? "Unknown" : notoriety < 300 ? "Watched" : notoriety < 500 ? "Hunted" : notoriety < 800 ? "Priority Target" : "Most Wanted";
            GUI.color = notorColor; Widgets.Label(new Rect(0, y, r.width, 22), $"Threat Level: {threatLevel}"); GUI.color = Color.white; y += 30;
            Widgets.DrawLineHorizontal(0, y, r.width); y += 10;
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, y, r.width, 24), "The Palace of Hidra"); Text.Font = GameFont.Small; y += 28;
            string lore = "A tech-advanced faction led by the 'High God King' - a mere human using technology to simulate divine power. They hunt false gods and free slaves.\n\nAs notoriety rises, they send Hunters. Prove who the REAL god is.";
            Widgets.Label(new Rect(0, y, r.width - 10, 100), lore); y += 110;
            Widgets.DrawLineHorizontal(0, y, r.width); y += 10;
            Widgets.Label(new Rect(0, y, r.width, 22), "Notoriety Sources:"); y += 24;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(10, y, r.width, 22), "• Divine abilities: +2-10 per use"); y += 20;
            Widgets.Label(new Rect(10, y, r.width, 22), "• New slaves: +5-10 per slave"); y += 20;
            Widgets.Label(new Rect(10, y, r.width, 22), "• Edicts: +10-30 per edict"); y += 20;
            Widgets.Label(new Rect(10, y, r.width, 22), "• God resurrection: +50"); GUI.color = Color.white;
        }

        string GetSpec(Pawn p) => p.health.hediffSet.HasHediff(Defs.H_Warrior) ? "Warrior" : p.health.hediffSet.HasHediff(Defs.H_Healer) ? "Healer" : p.health.hediffSet.HasHediff(Defs.H_Scholar) ? "Scholar" : p.health.hediffSet.HasHediff(Defs.H_Entertainer) ? "Entertainer" : "-";
        void SetSpec(Pawn p, HediffDef h) { ClearSpec(p); p.health.AddHediff(h); }
        void ClearSpec(Pawn p) { foreach (var h in new[] { Defs.H_Warrior, Defs.H_Healer, Defs.H_Scholar, Defs.H_Entertainer }) { var x = p.health.hediffSet.GetFirstHediffOfDef(h); if (x != null) p.health.RemoveHediff(x); } }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              HARMONY PATCHES
    // ══════════════════════════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Gizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> r, Pawn __instance)
        {
            foreach (var g in r) yield return g;
            var p = __instance; var gc = GC.I;

            // God abilities
            if (Defs.IsGod(p) && !p.Dead && !p.Downed && !p.IsSlave && SRI_Mod.S.enableAbilities && gc != null)
            {
                var smite = new Command_Target { defaultLabel = gc.Ready(p, "smite") ? "Smite" : $"Smite ({gc.Remaining(p, "smite") / 2500f:F1}h)", defaultDesc = $"Dmg: {Defs.Pwr(p, 20, 5):F0}", icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"), targetingParams = new() { canTargetPawns = true, canTargetBuildings = false }, action = t => p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Smite, t)) }; if (!gc.Ready(p, "smite")) smite.Disable("Cooldown"); yield return smite;
                var calm = new Command_Action { defaultLabel = gc.Ready(p, "calm") ? "Mass Calm" : $"Calm ({gc.Remaining(p, "calm") / 2500f:F1}h)", icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt"), action = () => { foreach (var s in p.Map.mapPawns.SlavesOfColonySpawned.ToList().Where(x => x.Position.InHorDistOf(p.Position, Defs.Pwr(p, 10, 2)))) { if (s.InMentalState) s.MentalState.RecoverFromState(); FleckMaker.ThrowMetaIcon(s.Position, s.Map, FleckDefOf.Heart); } gc.SetCD(p, "calm", Defs.CD_Calm); gc.AddNotoriety(2f, "Mass Calm"); } }; if (!gc.Ready(p, "calm")) calm.Disable("Cooldown"); yield return calm;
                var bless = new Command_Target { defaultLabel = gc.Ready(p, "bless") ? "Blessing" : $"Bless ({gc.Remaining(p, "bless") / 2500f:F1}h)", icon = ContentFinder<Texture2D>.Get("UI/Commands/Draft"), targetingParams = new() { canTargetPawns = true, validator = t => t.Thing is Pawn x && x.Faction == Faction.OfPlayer }, action = t => { if (t.Thing is Pawn x) { x.health.AddHediff(Defs.H_Blessing); x.needs.mood.thoughts.memories.TryGainMemory(Defs.T_Blessed, p); (x.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.05f); FleckMaker.Static(x.Position, x.Map, FleckDefOf.PsycastAreaEffect, 2); gc.SetCD(p, "bless", Defs.CD_Bless); gc.AddNotoriety(3f, "Blessing"); } } }; if (!gc.Ready(p, "bless")) bless.Disable("Cooldown"); yield return bless;
                var wrath = new Command_Action { defaultLabel = gc.Ready(p, "wrath") ? "Wrath" : $"Wrath ({gc.Remaining(p, "wrath") / 2500f:F1}h)", icon = ContentFinder<Texture2D>.Get("UI/Commands/FireAtWill"), action = () => { FleckMaker.Static(p.Position, p.Map, FleckDefOf.PsycastAreaEffect, 8); SoundDefOf.Thunder_OffMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(p.Position, p.Map))); float dmg = Defs.Pwr(p, 15, 3); foreach (var e in p.Map.mapPawns.AllPawnsSpawned.ToList().Where(x => x != p && x.Position.InHorDistOf(p.Position, 8) && x.HostileTo(p))) e.TakeDamage(new DamageInfo(DamageDefOf.Burn, dmg, 0, -1, p)); gc.SetCD(p, "wrath", Defs.CD_Wrath); gc.AddNotoriety(10f, "Divine Wrath"); foreach (var s in p.Map.mapPawns.SlavesOfColonySpawned.ToList().Where(x => x.Position.InHorDistOf(p.Position, 20))) { s.needs.mood.thoughts.memories.TryGainMemory(Defs.T_Wrath); (s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.02f); } } }; if (!gc.Ready(p, "wrath")) wrath.Disable("Cooldown"); yield return wrath;

                yield return new Command_Action { defaultLabel = "Assign Shield", icon = ContentFinder<Texture2D>.Get("UI/Commands/ForPrisoners"), action = () => {
                    var opts = new List<FloatMenuOption>(); var map = p.Map; if (map == null) return;
                    var eligibleSlaves = map.mapPawns.SlavesOfColonySpawned.Where(x => !x.Dead && Defs.Tier(x) >= 3).OrderByDescending(x => Defs.Tier(x)).ToList();
                    if (eligibleSlaves.Count == 0) { opts.Add(new("No eligible Tier 3+ slaves", null)); }
                    else { if (eligibleSlaves.Any(x => x.health.hediffSet.HasHediff(Defs.H_Shield))) opts.Add(new(">> Unassign All <<", () => { foreach (var s in eligibleSlaves) { var h = s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Shield); if (h != null) s.health.RemoveHediff(h); } }));
                        foreach (var slave in eligibleSlaves) { bool hasShield = slave.health.hediffSet.HasHediff(Defs.H_Shield); opts.Add(new($"{slave.LabelShort}{(hasShield ? " (Active)" : "")} - {Defs.TierName(Defs.Tier(slave))}", () => { if (!hasShield) { slave.health.AddHediff(Defs.H_Shield); var h = slave.health.hediffSet.GetFirstHediffOfDef(Defs.H_Shield) as Hediff_Shield; h.master = p; Messages.Message($"{slave.LabelShort} is now a Divine Shield!", slave, MessageTypeDefOf.PositiveEvent); FleckMaker.Static(slave.Position, slave.Map, FleckDefOf.PsycastAreaEffect, 2); } else { var h = slave.health.hediffSet.GetFirstHediffOfDef(Defs.H_Shield); slave.health.RemoveHediff(h); } })); } }
                    Find.WindowStack.Add(new FloatMenu(opts));
                } };
            }

            // Slave gizmos
            if (p.IsSlaveOfColony && p.RaceProps.Humanlike && gc != null)
            {
                yield return new Command_Action { defaultLabel = "Assign", icon = ContentFinder<Texture2D>.Get("UI/Commands/ReleaseAnimals"), action = () => { var opts = new List<FloatMenuOption> { new("Unassign", () => gc.SetConcubine(p, null)) }; foreach (var c in p.Map.mapPawns.FreeColonists.ToList()) opts.Add(new(c.LabelShort + (gc.IsConcubine(p, c) ? " ✓" : ""), () => gc.SetConcubine(p, c))); Find.WindowStack.Add(new FloatMenu(opts)); } };
                yield return new Command_Action { defaultLabel = $"{Defs.TierName(Defs.Tier(p))}", icon = ContentFinder<Texture2D>.Get("UI/Commands/Draft"), action = () => { } };

                // Specialization abilities
                if (Defs.Tier(p) >= 3)
                {
                    if (p.health.hediffSet.HasHediff(Defs.H_Warrior)) { var roar = new Command_Action { defaultLabel = gc.Ready(p, "roar") ? "Roar" : $"Roar ({gc.Remaining(p, "roar") / 2500f:F1}h)", icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"), action = () => p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_ChallengerRoar)) }; if (!gc.Ready(p, "roar")) roar.Disable("Cooldown"); yield return roar; }
                    if (p.health.hediffSet.HasHediff(Defs.H_Healer)) { var soothe = new Command_Target { defaultLabel = gc.Ready(p, "soothe") ? "Soothe" : $"Soothe ({gc.Remaining(p, "soothe") / 2500f:F1}h)", icon = ContentFinder<Texture2D>.Get("UI/Commands/TryReconnect"), targetingParams = new() { canTargetPawns = true, canTargetSelf = false, validator = t => t.Thing is Pawn x && x.Faction == Faction.OfPlayer }, action = t => p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_SoothingTouch, t.Thing)) }; if (!gc.Ready(p, "soothe")) soothe.Disable("Cooldown"); yield return soothe; }
                    if (p.health.hediffSet.HasHediff(Defs.H_Scholar)) { var insight = new Command_Action { defaultLabel = gc.Ready(p, "insight") ? "Insight" : $"Insight ({gc.Remaining(p, "insight") / 2500f:F1}h)", icon = ContentFinder<Texture2D>.Get("UI/Commands/SquadAttack"), action = () => p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_MomentInsight)) }; if (!gc.Ready(p, "insight")) insight.Disable("Cooldown"); yield return insight; }
                    if (p.health.hediffSet.HasHediff(Defs.H_Entertainer)) { var perform = new Command_Action { defaultLabel = gc.Ready(p, "captivate") ? "Perform" : $"Perform ({gc.Remaining(p, "captivate") / 2500f:F1}h)", icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt"), action = () => p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_CaptivatingPerformance)) }; if (!gc.Ready(p, "captivate")) perform.Disable("Cooldown"); yield return perform; }
                }
            }
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    public static class Patch_Float
    {
        static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            if (!pawn.IsColonistPlayerControlled) return;
            foreach (var t in GenUI.TargetsAt(clickPos, TargetingParameters.ForPawns()))
            {
                if (t.Pawn is Pawn v && v != pawn)
                {
                    if (v.IsSlaveOfColony) opts.Add(new($"Lovin' {v.LabelShort}", () => { var (spot, furn) = Defs.FindLovinSpot(pawn, v); if (!spot.IsValid) { Messages.Message("No suitable spot found!", MessageTypeDefOf.RejectInput); return; } if (pawn.Drafted) pawn.drafter.Drafted = false; if (v.Drafted) v.drafter.Drafted = false; var job1 = JobMaker.MakeJob(Defs.Job_Lovin, spot, v); var job2 = JobMaker.MakeJob(Defs.Job_Lovin, spot, pawn); if (furn != null) { job1.targetC = furn; job2.targetC = furn; } pawn.jobs.TryTakeOrderedJob(job1); v.jobs.TryTakeOrderedJob(job2); }));
                    if (v.IsSlaveOfColony || v.IsPrisonerOfColony) { int cd = GC.I?.Remaining(v, "punish") ?? 0; if (cd > 0) opts.Add(new($"Punish (CD {cd / 2500}h)", null) { Disabled = true }); else opts.Add(new($"Punish {v.LabelShort}", () => { if (pawn.Drafted) pawn.drafter.Drafted = false; pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Punish, v)); })); }
                    if (!v.IsPrisoner && !v.IsSlave && !v.IsColonist && v.RaceProps.Humanlike && !v.HostileTo(pawn)) opts.Add(new($"Procure {v.LabelShort} ({(0.1f + pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f) * 100:F0}%)", () => { if (pawn.Drafted) pawn.drafter.Drafted = false; pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Procure, v)); }));
                    if (v.Dead && Defs.IsGod(v) && GC.I?.CanRes(v) == true && SRI_Mod.S.enableResurrection && Defs.Tier(pawn) >= 3 && v.Corpse != null) { int hp = GC.I.GetHP(v); if (hp >= 30) opts.Add(new($"Resurrect ({hp} HP)", () => pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Resurrect, v.Corpse)))); else opts.Add(new($"Gather corpses ({hp}/30)", () => { var c = GenClosest.ClosestThing_Global(pawn.Position, pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse), 50, x => x != v.Corpse) as Corpse; if (c != null) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_GatherCorpse, c, v.Corpse)); })); }
                }
            }
        }
    }

    public static class Patch_NoGuns { public static bool Prefix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result) { if (Defs.IsGod(pawn) && thing.def.IsRangedWeapon) { cantReason = "God refuses guns."; __result = false; return false; } return true; } }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Deflect { static bool Prefix(Projectile __instance, Thing hitThing) { if (hitThing is Pawn p && Defs.IsGod(p)) { int s = GC.I?.CachedSlaveCount ?? 0; if (Rand.Chance(Mathf.Min(0.2f + s * 0.05f, 0.8f))) { MoteMaker.ThrowText(p.DrawPos, p.Map, "DEFLECT!", Color.yellow); SoundDefOf.MetalHitImportant.PlayOneShot(SoundInfo.InMap(new TargetInfo(p.Position, p.Map))); if (__instance.Launcher is Pawn shooter && !shooter.Dead) shooter.TakeDamage(new DamageInfo(__instance.def.projectile.damageDef, __instance.def.projectile.GetDamageAmount(null), 0, -1, p)); return false; } } return true; } }

    [HarmonyPatch(typeof(StatExtension), "GetStatValue")]
    public static class Patch_Stats { static void Postfix(Thing thing, StatDef stat, ref float __result) { try { if (thing is Pawn p && p.Spawned && !p.Dead) { var div = p.health?.hediffSet?.GetFirstHediffOfDef(Defs.H_Divine); if (div != null) { int c = (int)div.Severity; if (stat == StatDefOf.MoveSpeed) __result *= 1 + c * 0.05f; else if (stat == StatDefOf.MeleeDamageFactor) __result += c * 0.1f; else if (stat == StatDefOf.ResearchSpeed) __result += c * 0.1f; else if (stat == StatDefOf.MeleeDodgeChance) __result += c * 0.02f; else if (stat == StatDefOf.MeleeHitChance) __result += c * 0.02f; } var dev = p.health?.hediffSet?.GetFirstHediffOfDef(Defs.H_Devotion); if (dev != null && GC.I != null) { int masterId = GC.I.GetCachedMasterId(p); if (masterId > 0) { int t = Defs.Tier(p); if (t > 0) { if (stat == StatDefOf.WorkSpeedGlobal) __result += t * 0.03f; if (stat == StatDefOf.MoveSpeed) __result += t * 0.03f; if (stat == StatDefOf.MeleeDamageFactor) __result += t * 0.03f; } } } } } catch { } } }

    [HarmonyPatch(typeof(Pawn), "PostApplyDamage")]
    public static class Patch_ProtectiveRage
    {
        private static int lastTriggerTick = -1;
        static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt) => TriggerProtectiveRage(__instance, dinfo.Instigator as Pawn, null);

        public static void TriggerProtectiveRage(Pawn god, Pawn attacker, Pawn shieldBrokenSlave)
        {
            if (god == null || god.Dead || !Defs.IsGod(god) || attacker == null || attacker == god) return;
            int currentTick = Find.TickManager.TicksGame; if (currentTick == lastTriggerTick && shieldBrokenSlave == null) return; lastTriggerTick = currentTick;
            if (!attacker.HostileTo(god) && !attacker.InAggroMentalState) return;
            var map = god.Map; if (map == null) return;
            var loyalSlaves = map.mapPawns.SlavesOfColonySpawned.Where(s => s.health.hediffSet.HasHediff(Defs.H_Stockholm) && !s.Downed && !s.Dead && s.Awake() && !s.InMentalState).ToList();
            if (loyalSlaves.Count == 0) return;
            bool msgSent = false;
            foreach (var slave in loyalSlaves)
            {
                if (!slave.health.hediffSet.HasHediff(Defs.H_Rage)) { slave.health.AddHediff(Defs.H_Rage); if (!msgSent) { Messages.Message($"Slaves are enraged by the attack on {god.LabelShort}!", slave, MessageTypeDefOf.NegativeEvent); msgSent = true; } }
                if (!slave.Drafted) slave.drafter.Drafted = true;
                bool isShieldBroken = shieldBrokenSlave != null && slave == shieldBrokenSlave;
                var shield = slave.health.hediffSet.GetFirstHediffOfDef(Defs.H_Shield) as Hediff_Shield;
                bool hasActiveShield = (shield != null && shield.ready) && !isShieldBroken;
                if (!hasActiveShield) { if (slave.CurJobDef != JobDefOf.AttackMelee || slave.CurJob?.targetA.Thing != attacker) { slave.jobs.StopAll(); Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, attacker); attackJob.maxNumMeleeAttacks = 100; attackJob.expiryInterval = 2500; attackJob.playerForced = true; slave.jobs.TryTakeOrderedJob(attackJob, JobTag.DraftedOrder); } }
            }
        }
    }

    [HarmonyPatch(typeof(Thing), "TakeDamage", new Type[] { typeof(DamageInfo) })]
    public static class Patch_DivineShield
    {
        static bool Prefix(Thing __instance, ref DamageInfo dinfo, ref DamageWorker.DamageResult __result)
        {
            if (!(__instance is Pawn p) || p.Dead || dinfo.Amount <= 0 || !Defs.IsGod(p) || dinfo.Def == null || !dinfo.Def.ExternalViolenceFor(p)) return true;
            var map = p.Map; if (map == null) return true;
            var shielder = map.mapPawns.SlavesOfColonySpawned.Where(s => !s.Dead && !s.Downed && s.Awake() && Defs.Tier(s) >= 3 && s.Position.InHorDistOf(p.Position, 15f) && s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Shield) is Hediff_Shield h && h.ready).OrderBy(s => s.Position.DistanceTo(p.Position)).FirstOrDefault();
            if (shielder != null)
            {
                var h = shielder.health.hediffSet.GetFirstHediffOfDef(Defs.H_Shield) as Hediff_Shield; h.Use();
                MoteMaker.ThrowText(p.DrawPos, p.Map, "Shield Blocked!", Color.white); MoteMaker.ThrowText(shielder.DrawPos, shielder.Map, $"{shielder.LabelShort} ABSORBED!", Color.cyan);
                FleckMaker.Static(p.Position, p.Map, FleckDefOf.PsycastAreaEffect, 2);
                float absorbDmg = dinfo.Amount * 0.2f; if (absorbDmg >= 1f) shielder.TakeDamage(new DamageInfo(dinfo.Def, absorbDmg, 0, -1, dinfo.Instigator, null, dinfo.Weapon));
                Patch_ProtectiveRage.TriggerProtectiveRage(p, dinfo.Instigator as Pawn, shielder);
                __result = new DamageWorker.DamageResult(); return false;
            }
            return true;
        }
    }

    [HarmonyPatch] public static class Patch_Suppress { static bool Prepare() => AccessTools.Method(typeof(Need_Suppression), "NeedInterval") != null; static MethodBase TargetMethod() => AccessTools.Method(typeof(Need_Suppression), "NeedInterval"); static bool Prefix(Need_Suppression __instance, Pawn ___pawn) { if (___pawn.health.hediffSet.HasHediff(Defs.H_Stockholm)) { __instance.CurLevel = __instance.MaxLevel; return false; } return true; } }

    [HarmonyPatch] public static class Patch_SlaveWork { static bool Prepare() => AccessTools.Method(typeof(Pawn), "GetDisabledWorkTypes") != null; static MethodBase TargetMethod() => AccessTools.Method(typeof(Pawn), "GetDisabledWorkTypes");
        static void Postfix(Pawn __instance, ref List<WorkTypeDef> __result) { if (!SRI_Mod.S.unlockSlaveWork || !__instance.IsSlaveOfColony) return; bool isScholar = __instance.health.hediffSet.HasHediff(Defs.H_Scholar); bool highTier = Defs.Tier(__instance) >= 2; bool hasStockholm = __instance.health.hediffSet.HasHediff(Defs.H_Stockholm); if (!isScholar && !highTier && !hasStockholm) return; var toRemove = new List<WorkTypeDef>(); foreach (var w in __result) { if (w == WorkTypeDefOf.Research && (isScholar || highTier)) toRemove.Add(w); if (hasStockholm || highTier) if (w.defName == "Art" || w.defName == "Handling" || w.defName == "Warden") toRemove.Add(w); } __result = __result.Except(toRemove).ToList(); } }

    [HarmonyPatch] public static class Patch_SkillTotallyDisabled { static bool Prepare() { var prop = typeof(SkillRecord).GetProperty("TotallyDisabled"); return prop != null; } static MethodBase TargetMethod() { var prop = typeof(SkillRecord).GetProperty("TotallyDisabled"); return prop?.GetGetMethod(); }
        static void Postfix(SkillRecord __instance, ref bool __result) { if (!__result || !SRI_Mod.S.unlockSlaveWork || __instance.Pawn?.IsSlaveOfColony != true) return; bool isScholar = __instance.Pawn.health.hediffSet.HasHediff(Defs.H_Scholar); bool highTier = Defs.Tier(__instance.Pawn) >= 2; bool hasStockholm = __instance.Pawn.health.hediffSet.HasHediff(Defs.H_Stockholm); if (__instance.def == SkillDefOf.Intellectual && (isScholar || highTier)) __result = false; if ((hasStockholm || highTier) && (__instance.def == SkillDefOf.Artistic || __instance.def == SkillDefOf.Animals || __instance.def == SkillDefOf.Social)) __result = false; } }

    // ONE GOD RULE: Recruits become slaves instead of colonists
    [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), "DoRecruit", new Type[] { typeof(Pawn), typeof(Pawn), typeof(bool) })]
    public static class Patch_OneGodRule
    {
        static void Postfix(Pawn recruiter, Pawn recruitee, bool useAudiovisualEffects)
        {
            if (!SRI_Mod.S.enableOneGodRule) return;
            if (recruitee == null || recruitee.Dead || !recruitee.RaceProps.Humanlike) return;
            if (Defs.IsGod(recruitee)) return; // Don't enslave the God
            if (recruitee.Faction != Faction.OfPlayer) return; // Already handled

            // Check if this pawn is a quest pawn (don't enslave quest pawns)
            if (recruitee.questTags != null && recruitee.questTags.Count > 0) return;

            // Convert to slave
            try
            {
                recruitee.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                recruitee.health.AddHediff(Defs.H_Devotion);
                var dev = recruitee.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion;
                if (dev != null) dev.Severity = 0.1f;
                Messages.Message($"{recruitee.LabelShort} has been enslaved instead of recruited! (One God Rule)", recruitee, MessageTypeDefOf.NeutralEvent);
                GC.I?.AddNotoriety(8f, "New slave recruited");
            }
            catch (Exception e) { Log.Warning($"[SRI] OneGodRule error: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(PawnUIOverlay), "DrawPawnGUIOverlay")]
    public static class Patch_Overlay { static void Postfix(PawnUIOverlay __instance) { if (!SRI_Mod.S.showOverlay) return; var p = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>(); if (p == null || !p.IsSlaveOfColony || !p.Spawned) return; int t = Defs.Tier(p); if (t <= 0) return; GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(p, -0.6f), Defs.TierName(t)[0].ToString(), Defs.TierColor(t)); } }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              SCENARIO
    // ══════════════════════════════════════════════════════════════════════════════

    public class ScenPart_GodSetup : ScenPart
    {
        public override void PostGameStart()
        {
            try
            {
                var cols = Find.GameInitData.startingAndOptionalPawns.Take(5).ToList(); if (cols.Count == 0) return;
                var god = cols[0]; god.gender = Gender.Male;
                var asex = god.story?.traits?.GetTrait(TraitDefOf.Asexual); if (asex != null) god.story.traits.RemoveTrait(asex);
                if (god.story.traits.allTraits.Count >= 3) god.story.traits.allTraits.RemoveAt(0);
                god.story.traits.GainTrait(new Trait(Defs.Trait_God));
                var beauty = TraitDef.Named("Beauty"); if (beauty != null && !god.story.traits.HasTrait(beauty)) god.story.traits.GainTrait(new Trait(beauty, 2));
                foreach (var s in god.skills.skills) if (s.Level < 5) s.Level = 5;
                god.skills.GetSkill(SkillDefOf.Social).Level = Mathf.Max(12, god.skills.GetSkill(SkillDefOf.Social).Level);
                god.skills.GetSkill(SkillDefOf.Melee).Level = Mathf.Max(10, god.skills.GetSkill(SkillDefOf.Melee).Level);
                god.skills.GetSkill(SkillDefOf.Intellectual).Level = Mathf.Max(10, god.skills.GetSkill(SkillDefOf.Intellectual).Level);
                god.health.AddHediff(Defs.H_Divine);

                for (int i = 1; i < cols.Count; i++)
                {
                    var s = cols[i]; s.gender = Gender.Female;
                    s.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                    s.health.AddHediff(Defs.H_Stockholm); s.health.AddHediff(Defs.H_Devotion);
                    var dev = s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion;
                    if (dev != null) dev.Severity = 0.2f;
                    GC.I?.SetConcubine(s, god);
                }
                Messages.Message("The God has descended!", god, MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e) { Log.Error($"[SRI] {e}"); }
        }
    }
}
