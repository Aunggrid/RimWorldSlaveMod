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
    //                              HEDIFFS
    // ══════════════════════════════════════════════════════════════════════════════

    public class Hediff_Divine : HediffWithComps
    {
        private bool markedForRemoval = false;
        public override bool ShouldRemove => markedForRemoval;
        public override string LabelInBrackets => $"{(int)Severity} slaves";
        public override string TipStringExtra => $"+{(int)Severity * 5}% move, +{(int)Severity * 10}% melee/research\n+{(int)Severity * 2}% dodge/hit, Deflect: {Mathf.Min(20 + (int)Severity * 5, 80)}%";
        
        public override void Tick()
        {
            base.Tick();
            if (pawn.IsHashIntervalTick(199))
            {
                if (GC.I == null || !GC.I.IsGodCached(pawn)) { markedForRemoval = true; return; }
                Severity = GC.I?.CachedSlaveCount ?? 0;
            }
            
            // Divine Healing
            if (pawn.IsHashIntervalTick(2500))
            {
                float diseaseHealRate = 0.008f, chronicHealRate = 0.002f;
                bool hasRitualBuff = pawn.health.hediffSet.HasHediff(Defs.H_Healing);
                if (hasRitualBuff) { diseaseHealRate *= 5f; chronicHealRate *= 5f; }

                var badHediffs = pawn.health.hediffSet.hediffs.Where(h =>
                    h.def.isBad && h.Severity > 0 && !(h is Hediff_MissingPart) && !(h is Hediff_AddedPart) && !(h is Hediff_Implant) &&
                    h.def != Defs.H_ResSick && h.def != Defs.H_Exhaust && h.def != HediffDefOf.Anesthetic).ToList();

                foreach (var h in badHediffs)
                {
                    if (h is Hediff_Injury injury && !injury.IsPermanent()) continue;
                    bool isChronic = h.IsPermanent() || IsChronicCondition(h.def);
                    h.Severity = Mathf.Max(0, h.Severity - (isChronic ? chronicHealRate : diseaseHealRate));
                }
            }
        }

        private bool IsChronicCondition(HediffDef def)
        {
            if (def == null) return false;
            string[] chronicNames = { "Asthma", "BadBack", "Frail", "Cataract", "Dementia", "HeartArteryBlockage", "Carcinoma", "Cirrhosis" };
            foreach (var name in chronicNames) if (def.defName.Contains(name)) return true;
            if (def.chronic) return true;
            return false;
        }
    }

    public class Hediff_Devotion : HediffWithComps
    {
        private Pawn master; private int lastTier = -1;
        public override string LabelInBrackets => $"{Defs.TierName(Defs.Tier(pawn))} {Severity * 100:F0}%";
        public override string TipStringExtra => $"Tier: {Defs.TierName(Defs.Tier(pawn))}\nMaster: {master?.LabelShort ?? "None"}\nFaith/day: {GetFaithGeneration():F1}";

        public float GetFaithGeneration() => Defs.Tier(pawn) switch { 4 => 5f, 3 => 3f, 2 => 1.5f, 1 => 0.5f, _ => 0f } * SRI_Mod.S.faithGenerationMult;

        public override void Tick()
        {
            base.Tick();
            if (!pawn.IsHashIntervalTick(251)) return;
            try
            {
                int masterId = GC.I?.GetCachedMasterId(pawn) ?? -1;
                master = GC.I?.GetCachedMasterPawn(masterId);

                if (master != null && !master.Dead && pawn.Map == master.Map && pawn.Position.InHorDistOf(master.Position, 15f))
                {
                    float r = 0.00008f * SRI_Mod.S.devotionRate;
                    if (pawn.health.hediffSet.HasHediff(Defs.H_Stockholm)) r *= 2;
                    if (pawn.needs?.mood?.CurLevel > 0.6f) r *= 1.2f;
                    Severity = Mathf.Min(1, Severity + r);
                }

                int t = Defs.Tier(pawn);
                if (t > lastTier && lastTier >= 0 && SRI_Mod.S.showNotifications)
                    Messages.Message($"{pawn.LabelShort} → {Defs.TierName(t)}!", pawn, MessageTypeDefOf.PositiveEvent);
                lastTier = t;
            }
            catch { }
        }

        public void Add(float a) => Severity = Mathf.Min(1, Severity + a);
        public override void ExposeData() { base.ExposeData(); Scribe_References.Look(ref master, "m"); }
    }

    public class Hediff_Shield : HediffWithComps
    {
        public Pawn master; public bool ready = true; private int cdTick;
        private bool markedForRemoval = false;
        public override bool ShouldRemove => markedForRemoval;
        public override string LabelInBrackets => ready ? "Ready" : $"CD {(cdTick - Find.TickManager.TicksGame) / 2500f:F1}h";

        public override void Tick()
        {
            base.Tick();
            if (!ready && Find.TickManager.TicksGame >= cdTick) ready = true;
            if (master == null || master.Dead || Defs.Tier(pawn) < 3) { markedForRemoval = true; return; }

            if (pawn.IsHashIntervalTick(60))
            {
                bool isActive = ready && pawn.Position.InHorDistOf(master.Position, 15f);
                var indicator = master.health.hediffSet.hediffs.OfType<Hediff_GodShielded>().FirstOrDefault(h => h.shielder == pawn);
                if (isActive && indicator == null) { indicator = (Hediff_GodShielded)HediffMaker.MakeHediff(Defs.H_GodShielded, master); indicator.shielder = pawn; master.health.AddHediff(indicator); }
                else if (!isActive && indicator != null) master.health.RemoveHediff(indicator);
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (master != null && !master.Dead)
            {
                var indicator = master.health.hediffSet.hediffs.OfType<Hediff_GodShielded>().FirstOrDefault(h => h.shielder == pawn);
                if (indicator != null) master.health.RemoveHediff(indicator);
            }
        }

        public void Use() { ready = false; cdTick = Find.TickManager.TicksGame + Defs.CD(5000); }
        public override void ExposeData() { base.ExposeData(); Scribe_References.Look(ref master, "m"); Scribe_Values.Look(ref ready, "r", true); Scribe_Values.Look(ref cdTick, "cd"); }
    }

    public class Hediff_GodShielded : HediffWithComps
    {
        public Pawn shielder;
        public override string LabelInBrackets => shielder?.LabelShort ?? "Unknown";
        public override bool ShouldRemove => shielder == null || shielder.Dead || !shielder.health.hediffSet.HasHediff(Defs.H_Shield);
        public override void ExposeData() { base.ExposeData(); Scribe_References.Look(ref shielder, "shielder"); }
    }

    public class HC_Stockholm : HediffComp
    {
        public override void CompPostTick(ref float s)
        {
            if (!Pawn.IsHashIntervalTick(2500) || !ModsConfig.IdeologyActive) return;
            var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (ideo != null && Pawn.Ideo != ideo)
            {
                Pawn.ideo.OffsetCertainty(-0.01f);
                if (Pawn.ideo.Certainty <= 0.01f) { Pawn.ideo.SetIdeo(ideo); Pawn.ideo.OffsetCertainty(0.5f); Messages.Message($"{Pawn.LabelShort} converted!", Pawn, MessageTypeDefOf.PositiveEvent); }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              GAME COMPONENT
    // ══════════════════════════════════════════════════════════════════════════════

    public class GC : GameComponent
    {
        public static GC I;
        Dictionary<int, int> slaveMap = new(), dailyLovin = new();
        Dictionary<int, int> cdPunish = new(), cdSmite = new(), cdCalm = new(), cdBless = new(), cdWrath = new();
        Dictionary<int, int> cdRoar = new(), cdSoothe = new(), cdInsight = new(), cdCaptivate = new();
        Dictionary<int, int> godDeaths = new(), corpseHP = new();
        List<(Pawn, HediffDef, bool)> deferred = new();
        List<Action> deferredActions = new();
        bool pendingApply = false;
        int lastDay, lastHeal, lastFaithDay = -1, lastNotorietyRaidDay = -1;

        // Faith & Notoriety
        public float faith = 0f;
        public float Faith { get => faith; set => faith = value; }
        public float MaxFaith => 500f;
        public float notoriety = 0f;
        public float Notoriety { get => notoriety; set => notoriety = value; }
        public float MaxNotoriety => 1000f;

        // Cache
        public int CachedSlaveCount { get; private set; } = 0;
        public int CachedT3Count { get; private set; } = 0;
        public int CachedT4Count { get; private set; } = 0;
        Dictionary<int, int> cachedMasterIds = new();
        Dictionary<int, Pawn> cachedMasterPawns = new();
        HashSet<int> cachedGodIds = new();

        public GC(Game g) { I = this; EdictDef.Initialize(); }
        public override void FinalizeInit() { I = this; EdictDef.Initialize(); }

        public override void GameComponentTick()
        {
            if (pendingApply) { pendingApply = false; ApplyAllDeferred(); }
            UpdateCache();

            try
            {
                var m = Find.CurrentMap; if (m == null) return;
                int d = GenLocalDate.DayOfYear(m);

                if (d != lastDay)
                {
                    dailyLovin.Clear(); lastDay = d; QueueUpdateHeads(m);
                    if (SRI_Mod.S.enableFaithSystem && d != lastFaithDay) { lastFaithDay = d; GenerateDailyFaith(m); }
                    if (SRI_Mod.S.enableNotoriety) CheckHunterRaid(m, d);
                }

                if (Find.TickManager.TicksGame % 2503 == 0) { QueueProcessAura(m); QueueProcessAuto(m); }
            }
            catch (Exception e) { Log.ErrorOnce($"[SRI] {e.Message}", 95847); }

            if (deferred.Count > 0 || deferredActions.Count > 0) pendingApply = true;
        }

        void UpdateCache()
        {
            try
            {
                var m = Find.CurrentMap; if (m == null) return;
                CachedSlaveCount = CachedT3Count = CachedT4Count = 0;
                var slaves = m.mapPawns.SlavesOfColonySpawned;
                if (slaves != null) foreach (var s in slaves) { if (s == null || s.Dead) continue; CachedSlaveCount++; int t = Defs.Tier(s); if (t >= 4) CachedT4Count++; else if (t >= 3) CachedT3Count++; }

                cachedGodIds.Clear(); cachedMasterPawns.Clear();
                var colonists = m.mapPawns.FreeColonistsSpawned;
                if (colonists != null) foreach (var c in colonists) { if (c == null || c.Dead) continue; if (Defs.IsGod(c)) cachedGodIds.Add(c.thingIDNumber); cachedMasterPawns[c.thingIDNumber] = c; }

                cachedMasterIds.Clear();
                foreach (var kv in slaveMap) if (kv.Value > 0) cachedMasterIds[kv.Key] = kv.Value;
            }
            catch { }
        }

        void ApplyAllDeferred()
        {
            if (deferred.Count > 0)
            {
                var l = deferred.ToList(); deferred.Clear();
                foreach (var (p, h, add) in l) { try { if (p == null || p.Dead || p.Destroyed) continue; if (add && !p.health.hediffSet.HasHediff(h)) p.health.AddHediff(h); else if (!add) { var x = p.health.hediffSet.GetFirstHediffOfDef(h); if (x != null) p.health.RemoveHediff(x); } } catch { } }
            }
            if (deferredActions.Count > 0) { var actions = deferredActions.ToList(); deferredActions.Clear(); foreach (var a in actions) { try { a(); } catch { } } }
        }

        public void Queue(Pawn p, HediffDef h, bool add) => deferred.Add((p, h, add));
        public void QueueAction(Action a) => deferredActions.Add(a);

        // Faith System
        void GenerateDailyFaith(Map m)
        {
            float dailyFaith = 0f;
            try { foreach (var slave in m.mapPawns.SlavesOfColonySpawned) { if (slave == null || slave.Dead) continue; var dev = slave.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion; if (dev != null) dailyFaith += dev.GetFaithGeneration(); } } catch { }
            if (dailyFaith > 0) { Faith = Mathf.Min(MaxFaith, Faith + dailyFaith); if (SRI_Mod.S.showNotifications && dailyFaith >= 1f) Messages.Message($"Faith +{dailyFaith:F1} (Total: {Faith:F0})", MessageTypeDefOf.SilentInput); }
        }

        public void AddNotoriety(float amount, string reason = null)
        {
            if (!SRI_Mod.S.enableNotoriety) return;
            Notoriety = Mathf.Min(MaxNotoriety, Notoriety + amount * SRI_Mod.S.notorietyGainMult);
        }

        void CheckHunterRaid(Map m, int currentDay)
        {
            if (lastNotorietyRaidDay >= 0 && currentDay - lastNotorietyRaidDay < 15) return;
            float raidChance = Notoriety >= 800 ? 0.15f : Notoriety >= 500 ? 0.08f : Notoriety >= 300 ? 0.04f : Notoriety >= 150 ? 0.02f : 0f;
            if (raidChance > 0 && Rand.Chance(raidChance)) { TriggerHunterRaid(m); lastNotorietyRaidDay = currentDay; }
        }

        void TriggerHunterRaid(Map m)
        {
            try
            {
                var hidraFaction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.def.defName == "SRI_PalaceOfHidra") ??
                    Find.FactionManager.AllFactions.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction && f.def.techLevel >= TechLevel.Industrial);
                if (hidraFaction == null) return;

                var raidParms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, m);
                raidParms.faction = hidraFaction;
                raidParms.points = 500f + (Notoriety * 2f);
                raidParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                raidParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;

                if (IncidentDefOf.RaidEnemy.Worker.TryExecute(raidParms))
                {
                    Find.LetterStack.ReceiveLetter("Hunters Approach!", $"The Palace of Hidra has sent hunters! (Notoriety: {Notoriety:F0})", LetterDefOf.ThreatBig);
                    foreach (var slave in m.mapPawns.SlavesOfColonySpawned.ToList()) slave.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_HuntersFear);
                    Notoriety = Mathf.Max(0, Notoriety - 100);
                }
            }
            catch (Exception e) { Log.Error($"[SRI] Hunter raid error: {e}"); }
        }

        public bool ActivateEdict(EdictDef edict, Map m)
        {
            if (Faith < edict.faithCost) return false;
            Faith -= edict.faithCost;
            AddNotoriety(edict.faithCost / 5f, $"Edict: {edict.label}");

            try
            {
                var allPawns = m.mapPawns.FreeColonistsAndPrisonersSpawned.Concat(m.mapPawns.SlavesOfColonySpawned).ToList();
                foreach (var pawn in allPawns)
                {
                    if (pawn == null || pawn.Dead) continue;
                    var existing = pawn.health.hediffSet.GetFirstHediffOfDef(edict.appliedHediff);
                    if (existing != null) pawn.health.RemoveHediff(existing);
                    pawn.health.AddHediff(edict.appliedHediff);
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_EdictBlessing);
                }
                Messages.Message($"Edict Proclaimed: {edict.label}!", MessageTypeDefOf.PositiveEvent);
                var god = m.mapPawns.FreeColonistsSpawned.FirstOrDefault(Defs.IsGod);
                if (god != null) { FleckMaker.Static(god.Position, m, FleckDefOf.PsycastAreaEffect, 8f); SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(m); }
            }
            catch (Exception e) { Log.Error($"[SRI] Edict error: {e}"); return false; }
            return true;
        }

        // Cache access
        public int GetCachedMasterId(Pawn s) => cachedMasterIds.TryGetValue(s.thingIDNumber, out int id) ? id : -1;
        public bool IsGodCached(Pawn p) => cachedGodIds.Contains(p.thingIDNumber);
        public Pawn GetCachedMasterPawn(int masterId) => masterId > 0 && cachedMasterPawns.TryGetValue(masterId, out var p) ? p : null;

        // Concubine system
        public void SetConcubine(Pawn s, Pawn m) { slaveMap[s.thingIDNumber] = m?.thingIDNumber ?? -1; if (m != null && !s.health.hediffSet.HasHediff(Defs.H_Devotion)) Queue(s, Defs.H_Devotion, true); if (m != null) AddNotoriety(5f, "New concubine"); }
        public bool IsConcubine(Pawn s, Pawn m) => slaveMap.TryGetValue(s.thingIDNumber, out int id) && id == m.thingIDNumber;
        public Pawn GetMaster(Pawn s) { if (s?.Map == null) return null; if (!slaveMap.TryGetValue(s.thingIDNumber, out int id) || id <= 0) return null; try { foreach (var p in s.Map.mapPawns.FreeColonistsSpawned) if (p.thingIDNumber == id) return p; } catch { } return null; }
        public List<Pawn> GetConcubines(Pawn m) { if (m?.Map == null) return new(); try { var result = new List<Pawn>(); foreach (var s in m.Map.mapPawns.SlavesOfColonySpawned) if (IsConcubine(s, m)) result.Add(s); return result; } catch { return new(); } }

        // Cooldowns
        public bool Ready(Pawn p, string a) => GetCD(a)?.TryGetValue(p.thingIDNumber, out int v) != true || v <= Find.TickManager.TicksGame;
        public int Remaining(Pawn p, string a) => Math.Max(0, (GetCD(a)?.TryGetValue(p.thingIDNumber, out int v) == true ? v : 0) - Find.TickManager.TicksGame);
        public void SetCD(Pawn p, string a, int dur) { var d = GetCD(a); if (d != null) d[p.thingIDNumber] = Find.TickManager.TicksGame + Defs.CD(dur); }
        Dictionary<int, int> GetCD(string a) => a switch { "smite" => cdSmite, "calm" => cdCalm, "bless" => cdBless, "wrath" => cdWrath, "punish" => cdPunish, "roar" => cdRoar, "soothe" => cdSoothe, "insight" => cdInsight, "captivate" => cdCaptivate, _ => null };

        // Lovin & Resurrection
        public int GetLovin(Pawn p) => dailyLovin.TryGetValue(p.thingIDNumber, out int v) ? v : 0;
        public void AddLovin(Pawn p) => dailyLovin[p.thingIDNumber] = GetLovin(p) + 1;
        public void RegisterDeath(Pawn g) => godDeaths[g.thingIDNumber] = Find.TickManager.TicksGame;
        public bool CanRes(Pawn g) => godDeaths.TryGetValue(g.thingIDNumber, out int t) && Find.TickManager.TicksGame - t < 180000;
        public int GetHP(Pawn g) => corpseHP.TryGetValue(g.thingIDNumber, out int v) ? v : 0;
        public void AddHP(Pawn g, int hp) => corpseHP[g.thingIDNumber] = GetHP(g) + hp;
        public void CompleteRes(Pawn g, Pawn perf) { QueueAction(() => { ResurrectionUtility.TryResurrect(g); Queue(g, Defs.H_ResSick, true); Queue(perf, Defs.H_Exhaust, true); try { foreach (var s in g.Map?.mapPawns.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>()) s.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_Resurrected); } catch { } godDeaths.Remove(g.thingIDNumber); corpseHP.Remove(g.thingIDNumber); AddNotoriety(50f, "God resurrected"); }); }

        // Queue helpers
        void QueueProcessAura(Map m) { foreach (var godId in cachedGodIds) { var g = GetCachedMasterPawn(godId); if (g == null || g.Dead) continue; if (!g.health.hediffSet.HasHediff(Defs.H_Divine)) Queue(g, Defs.H_Divine, true); } QueueAction(() => { try { var gods = m?.mapPawns?.FreeColonistsSpawned?.ToList()?.Where(Defs.IsGod); if (gods == null) return; foreach (var g in gods) { if (g == null || g.Dead) continue; foreach (var s in m.mapPawns.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>()) { if (s == null || s.Dead) continue; if (s.Position.InHorDistOf(g.Position, 9f)) { var sup = s.needs?.TryGetNeed<Need_Suppression>(); if (sup != null && sup.CurLevel < 1) { sup.CurLevel = 1; FleckMaker.ThrowMetaIcon(s.Position, s.Map, FleckDefOf.IncapIcon); } } } } } catch { } }); }
        void QueueUpdateHeads(Map m) { QueueAction(() => { try { foreach (var s in m?.mapPawns?.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>()) { if (s == null || s.Dead) continue; if (s.health.hediffSet.HasHediff(Defs.H_HeadConcubine)) Queue(s, Defs.H_HeadConcubine, false); } var pawnGroups = new Dictionary<Pawn, List<Pawn>>(); foreach (var s in m?.mapPawns?.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>()) { if (s == null || s.Dead) continue; var master = GetMaster(s); if (master != null) { if (!pawnGroups.ContainsKey(master)) pawnGroups[master] = new(); pawnGroups[master].Add(s); } } foreach (var kv in pawnGroups) { Pawn best = null; float sc = -999; foreach (var c in kv.Value) { if (c == null) continue; float s = c.relations.OpinionOf(kv.Key) + c.skills.GetSkill(SkillDefOf.Social).Level * 5; if (s > sc) { sc = s; best = c; } } if (best != null) Queue(best, Defs.H_HeadConcubine, true); } } catch { } }); }
        void QueueProcessAuto(Map m) { int d = GenLocalDate.DayOfYear(m); if (d == lastHeal) return; bool night = GenLocalDate.HourInteger(m) >= 22 || GenLocalDate.HourInteger(m) <= 5; if (!night) return; lastHeal = d; QueueAction(() => { try { foreach (var master in m?.mapPawns?.FreeColonistsSpawned?.ToList() ?? new List<Pawn>()) { if (master == null || master.Downed || master.Dead || !master.InBed()) continue; var cons = GetConcubines(master)?.Where(c => c != null && !c.Downed && !c.Dead && c.health.hediffSet.HasHediff(Defs.H_Stockholm))?.ToList(); if (cons != null && cons.Count >= 2) { cons[0].jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Heal, master, cons[1])); cons[1].jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Heal, master, cons[0])); return; } } } catch { } }); }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref slaveMap, "slaves", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdPunish, "cdP", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdSmite, "cdS", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdCalm, "cdC", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdBless, "cdB", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdWrath, "cdW", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdRoar, "cdR", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdSoothe, "cdSo", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdInsight, "cdIn", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdCaptivate, "cdCa", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref godDeaths, "deaths", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref corpseHP, "hp", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastDay, "day"); Scribe_Values.Look(ref lastHeal, "heal");
            Scribe_Values.Look(ref faith, "faith", 0f); Scribe_Values.Look(ref lastFaithDay, "faithDay", -1);
            Scribe_Values.Look(ref notoriety, "notoriety", 0f); Scribe_Values.Look(ref lastNotorietyRaidDay, "notorRaidDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) { slaveMap ??= new(); cdPunish ??= new(); cdSmite ??= new(); cdCalm ??= new(); cdBless ??= new(); cdWrath ??= new(); cdRoar ??= new(); cdSoothe ??= new(); cdInsight ??= new(); cdCaptivate ??= new(); godDeaths ??= new(); corpseHP ??= new(); }
        }
    }
}
