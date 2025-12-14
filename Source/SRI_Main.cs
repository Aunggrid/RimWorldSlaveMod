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
    //                              MOD SETTINGS
    // ══════════════════════════════════════════════════════════════════════════════

    public class SRI_Settings : ModSettings
    {
        public bool enableAbilities = true, enableResurrection = true, enableSpecializations = true;
        public bool unlockSlaveWork = true, showNotifications = true, showOverlay = true, showGodGlow = true;
        public float devotionRate = 1f, cooldownMult = 1f, powerMult = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableAbilities, "abilities", true);
            Scribe_Values.Look(ref enableResurrection, "resurrection", true);
            Scribe_Values.Look(ref enableSpecializations, "specs", true);
            Scribe_Values.Look(ref unlockSlaveWork, "unlockWork", true);
            Scribe_Values.Look(ref showNotifications, "notifs", true);
            Scribe_Values.Look(ref showOverlay, "overlay", true);
            Scribe_Values.Look(ref showGodGlow, "glow", true);
            Scribe_Values.Look(ref devotionRate, "devRate", 1f);
            Scribe_Values.Look(ref cooldownMult, "cdMult", 1f);
            Scribe_Values.Look(ref powerMult, "pwrMult", 1f);
        }
    }

    public class SRI_Mod : Mod
    {
        public static SRI_Settings S;
        public SRI_Mod(ModContentPack c) : base(c) => S = GetSettings<SRI_Settings>();
        public override string SettingsCategory() => "Slave Realism Improved";

        public override void DoSettingsWindowContents(Rect r)
        {
            var L = new Listing_Standard();
            L.Begin(r);
            L.Label("═══ Features ═══");
            L.CheckboxLabeled("Divine Abilities", ref S.enableAbilities);
            L.CheckboxLabeled("Resurrection System", ref S.enableResurrection);
            L.CheckboxLabeled("Slave Specializations", ref S.enableSpecializations);
            L.CheckboxLabeled("Unlock Slave Work (Research, Art, etc)", ref S.unlockSlaveWork);
            L.CheckboxLabeled("Tier-Up Notifications", ref S.showNotifications);
            L.GapLine();
            L.Label("═══ Balance ═══");
            L.Label($"Devotion Growth: {S.devotionRate:P0}"); S.devotionRate = L.Slider(S.devotionRate, 0.1f, 3f);
            L.Label($"Cooldown Multiplier: {S.cooldownMult:P0}"); S.cooldownMult = L.Slider(S.cooldownMult, 0.25f, 3f);
            L.Label($"Ability Power: {S.powerMult:P0}"); S.powerMult = L.Slider(S.powerMult, 0.5f, 2f);
            L.GapLine();
            L.Label("═══ Visuals ═══");
            L.CheckboxLabeled("Devotion Overlay on Slaves", ref S.showOverlay);
            L.CheckboxLabeled("God Glow Effect", ref S.showGodGlow);
            L.End();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              DEFINITIONS
    // ══════════════════════════════════════════════════════════════════════════════

    [StaticConstructorOnStartup]
    public static class Defs
    {
        // Jobs
        public static JobDef Job_Lovin, Job_Punish, Job_Procure, Job_Attend, Job_Heal, Job_Smite, Job_Resurrect, Job_GatherCorpse;
        
        // Hediffs
        public static HediffDef H_Stockholm, H_Rage, H_Punished, H_Healing, H_HeadConcubine, H_Divine, H_Devotion;
        public static HediffDef H_Shield, H_Blessing, H_ResSick, H_Exhaust;
        public static HediffDef H_Warrior, H_Healer, H_Scholar, H_Entertainer;
        
        // Other
        public static TraitDef Trait_God;
        public static ThoughtDef T_Satisfied, T_Forced, T_Wrath, T_Blessed, T_Resurrected, T_ServedMaster, T_ReceivedService;
        
        // Constants
        public const float T1 = 0.15f, T2 = 0.35f, T3 = 0.60f, T4 = 0.90f;
        public const int CD_Smite = 2500, CD_Calm = 10000, CD_Bless = 20000, CD_Wrath = 60000;

        static Defs()
        {
            Log.Message("[SRI] Initializing...");
            
            // Jobs
            Job_Lovin = MkJob("SRI_Lovin", typeof(JD_Lovin), "making love.");
            Job_Punish = MkJob("SRI_Punish", typeof(JD_Punish), "punishing.", true);
            Job_Procure = MkJob("SRI_Procure", typeof(JD_Procure), "procuring.");
            Job_Attend = MkJob("SRI_Attend", typeof(JD_Attend), "attending.");
            Job_Heal = MkJob("SRI_Heal", typeof(JD_Heal), "healing ritual.");
            Job_Smite = MkJob("SRI_Smite", typeof(JD_Smite), "divine wrath.");
            Job_Resurrect = MkJob("SRI_Resurrect", typeof(JD_Resurrect), "resurrection.");
            Job_GatherCorpse = MkJob("SRI_Gather", typeof(JD_Gather), "gathering.");

            // Core Hediffs
            H_Stockholm = MkHediff("SRI_Stockholm", "Stockholm Syndrome", "Bonded to master.", typeof(HediffWithComps), Color.cyan,
                MkStage(statOff: new() { (StatDefOf.GlobalLearningFactor, 0.2f) }), new() { new HediffCompProperties { compClass = typeof(HC_Stockholm) } });
            
            H_Rage = MkHediff("SRI_Rage", "Protective Rage", "Protecting master!", typeof(HediffWithComps), Color.red,
                MkStage(pain: 0.1f, statOff: new() { (StatDefOf.MoveSpeed, 3f), (StatDefOf.MeleeHitChance, 0.3f), (StatDefOf.MeleeDodgeChance, 0.3f), (StatDefOf.MeleeDamageFactor, 1.5f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(2500, 5000), showRemainingTime = true } });
            
            H_Punished = MkHediff("SRI_Punished", "Punished", "Pain.", typeof(HediffWithComps), new Color(0.8f, 0.4f, 0), true,
                MkStage(pain: 0.35f), new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(10000, 20000), showRemainingTime = true } });
            
            H_Healing = MkHediff("SRI_Healing", "Healing Touch", "Faster recovery.", typeof(HediffWithComps), Color.green,
                MkStage(statOff: new() { (StatDefOf.ImmunityGainSpeed, 0.5f), (StatDefOf.InjuryHealingFactor, 0.5f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(15000, 30000), showRemainingTime = true } });
            
            H_HeadConcubine = MkHediff("SRI_HeadConc", "Head Concubine", "Favorite.", typeof(HediffWithComps), new Color(1, 0.8f, 0),
                MkStage(statOff: new() { (StatDefOf.SocialImpact, 0.2f) }));
            
            H_Divine = MkHediff("SRI_Divine", "Divine Power", "Power from followers.", typeof(Hediff_Divine), new Color(1, 0.9f, 0.2f));
            H_Devotion = MkHediff("SRI_Devotion", "Devotion", "Connection to the God.", typeof(Hediff_Devotion), new Color(0.9f, 0.7f, 1));
            H_Shield = MkHediff("SRI_Shield", "Divine Shield", "Protecting God.", typeof(Hediff_Shield), new Color(0.5f, 0.8f, 1));
            
            H_Blessing = MkHediff("SRI_Blessing", "Divine Blessing", "Blessed.", typeof(HediffWithComps), new Color(1, 1, 0.6f),
                MkStage(statOff: new() { (StatDefOf.WorkSpeedGlobal, 0.15f), (StatDefOf.MoveSpeed, 0.5f), (StatDefOf.MeleeDamageFactor, 0.2f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(30000, 60000), showRemainingTime = true } });
            
            H_ResSick = MkHediff("SRI_ResSick", "Resurrection Sickness", "Weakened.", typeof(HediffWithComps), Color.gray, true,
                MkStage(statFac: new() { (StatDefOf.MoveSpeed, 0.5f), (StatDefOf.MeleeDamageFactor, 0.5f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(60000, 90000), showRemainingTime = true } });
            
            H_Exhaust = MkHediff("SRI_Exhaust", "Ritual Exhaustion", "Drained.", typeof(HediffWithComps), Color.gray, true,
                MkStage(capMod: new() { (PawnCapacityDefOf.Consciousness, -0.3f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(30000, 45000), showRemainingTime = true } });

            // Specializations
            H_Warrior = MkHediff("SRI_Warrior", "Warrior", "+50% melee damage.", typeof(HediffWithComps), Color.red,
                MkStage(statOff: new() { (StatDefOf.MeleeDamageFactor, 0.5f), (StatDefOf.MeleeDodgeChance, 0.2f), (StatDefOf.MeleeHitChance, 0.15f) }));
            H_Healer = MkHediff("SRI_Healer", "Healer", "+30% tend quality.", typeof(HediffWithComps), Color.green,
                MkStage(statOff: new() { (StatDefOf.MedicalTendQuality, 0.3f), (StatDefOf.MedicalSurgerySuccessChance, 0.3f) }));
            H_Scholar = MkHediff("SRI_Scholar", "Scholar", "+40% research.", typeof(HediffWithComps), Color.blue,
                MkStage(statOff: new() { (StatDefOf.ResearchSpeed, 0.4f), (StatDefOf.GlobalLearningFactor, 0.3f) }));
            H_Entertainer = MkHediff("SRI_Entertainer", "Entertainer", "+30% social.", typeof(HediffWithComps), Color.magenta,
                MkStage(statOff: new() { (StatDefOf.SocialImpact, 0.3f), (StatDefOf.JoyGainFactor, 0.2f) }));

            // Trait
            Trait_God = new TraitDef { defName = "SRI_God", degreeDatas = new() { new TraitDegreeData { label = "Reincarnated God", description = "Divine being. Reflects bullets. No ranged weapons." } } };
            DefDatabase<TraitDef>.Add(Trait_God);

            // Thoughts
            T_Satisfied = MkThought("SRI_Satisfied", "Satisfied", 5, 1);
            T_Forced = MkThought("SRI_Forced", "Forced", -8, 1);
            T_Wrath = MkThought("SRI_Wrath", "Witnessed wrath", 10, 3);
            T_Blessed = MkThought("SRI_Blessed", "Blessed", 15, 5);
            T_Resurrected = MkThought("SRI_Resurrected", "God returned!", 30, 15);
            T_ServedMaster = MkThought("SRI_ServedMaster", "Served my Master", 8, 2);
            T_ReceivedService = MkThought("SRI_ReceivedService", "Received devoted service", 10, 2);

            var harmony = new Harmony("com.sri");
            harmony.PatchAll();
            
            // Manual patches for methods that may have changed signatures
            try
            {
                var bedMethod = AccessTools.GetDeclaredMethods(typeof(RestUtility)).Where(m => m.Name == "IsValidBedFor").OrderByDescending(m => m.GetParameters().Length).FirstOrDefault();
                if (bedMethod != null) harmony.Patch(bedMethod, prefix: new HarmonyMethod(typeof(Patch_BedValid), "Prefix"));
            }
            catch (Exception e) { Log.Warning($"[SRI] Bed patch skipped: {e.Message}"); }
            
            try
            {
                var equipMethod = AccessTools.Method(typeof(EquipmentUtility), "CanEquip", new[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) });
                if (equipMethod != null) harmony.Patch(equipMethod, prefix: new HarmonyMethod(typeof(Patch_NoGuns), "Prefix"));
            }
            catch (Exception e) { Log.Warning($"[SRI] Gun restriction patch skipped: {e.Message}"); }
            
            Log.Message("[SRI] Done!");
        }

        // Helper methods
        static JobDef MkJob(string n, Type d, string r, bool w = false) { var j = new JobDef { defName = n, driverClass = d, reportString = r, playerInterruptible = true, alwaysShowWeapon = w, casualInterruptible = false, suspendable = false }; DefDatabase<JobDef>.Add(j); return j; }
        
        static HediffStage MkStage(float pain = 0, List<(StatDef, float)> statOff = null, List<(StatDef, float)> statFac = null, List<(PawnCapacityDef, float)> capMod = null)
        {
            var s = new HediffStage { painOffset = pain };
            if (statOff != null) { s.statOffsets = new(); foreach (var (st, v) in statOff) s.statOffsets.Add(new StatModifier { stat = st, value = v }); }
            if (statFac != null) { s.statFactors = new(); foreach (var (st, v) in statFac) s.statFactors.Add(new StatModifier { stat = st, value = v }); }
            if (capMod != null) { s.capMods = new(); foreach (var (c, v) in capMod) s.capMods.Add(new PawnCapacityModifier { capacity = c, offset = v }); }
            return s;
        }
        
        static HediffDef MkHediff(string n, string l, string d, Type c, Color col, bool bad = false, HediffStage stage = null, List<HediffCompProperties> comps = null)
        {
            var h = new HediffDef { defName = n, label = l, description = d, hediffClass = c, defaultLabelColor = col, isBad = bad };
            if (stage != null) h.stages = new() { stage };
            if (comps != null) h.comps = comps;
            DefDatabase<HediffDef>.Add(h);
            return h;
        }
        static HediffDef MkHediff(string n, string l, string d, Type c, Color col, HediffStage stage, List<HediffCompProperties> comps = null) => MkHediff(n, l, d, c, col, false, stage, comps);
        
        static ThoughtDef MkThought(string n, string l, int mood, float days) { var t = new ThoughtDef { defName = n, durationDays = days, stages = new() { new ThoughtStage { label = l, baseMoodEffect = mood } } }; DefDatabase<ThoughtDef>.Add(t); return t; }

        // Utility
        public static bool IsGod(Pawn p) => p?.story?.traits?.HasTrait(Trait_God) == true;
        public static int Tier(Pawn p) { var h = p?.health?.hediffSet?.GetFirstHediffOfDef(H_Devotion); if (h == null) return 0; float s = h.Severity; return s >= T4 ? 4 : s >= T3 ? 3 : s >= T2 ? 2 : s >= T1 ? 1 : 0; }
        public static string TierName(int t) => t switch { 4 => "Zealot", 3 => "Devoted", 2 => "Faithful", 1 => "Initiate", _ => "None" };
        public static Color TierColor(int t) => t switch { 4 => Color.yellow, 3 => Color.cyan, 2 => Color.green, 1 => Color.white, _ => Color.gray };
        public static int CD(int b) => (int)(b * SRI_Mod.S.cooldownMult);
        // Use CACHED tier counts - NO pawn list iteration!
        public static float Pwr(Pawn g, float b, float bonus) { int t3 = GC.I?.CachedT3Count ?? 0; int t4 = GC.I?.CachedT4Count ?? 0; return (b + t3 * bonus + t4 * bonus * 2) * SRI_Mod.S.powerMult; }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              HEDIFFS
    // ══════════════════════════════════════════════════════════════════════════════

    public class Hediff_Divine : HediffWithComps
    {
        private bool markedForRemoval = false;
        public override bool ShouldRemove => markedForRemoval;
        public override string LabelInBrackets => $"{(int)Severity} slaves";
        public override string TipStringExtra => $"+{(int)Severity * 5}% move, +{(int)Severity * 10}% melee/research\nDeflect: {Mathf.Min(20 + (int)Severity * 5, 80)}%";
        public override void Tick() 
        { 
            base.Tick(); 
            if (pawn.IsHashIntervalTick(199)) 
            { 
                // Use cached check - NO pawn list iteration during tick
                if (GC.I == null || !GC.I.IsGodCached(pawn)) { markedForRemoval = true; return; } 
                // Use cached slave count - NO pawn list iteration
                Severity = GC.I?.CachedSlaveCount ?? 0; 
            } 
        }
    }

    public class Hediff_Devotion : HediffWithComps
    {
        private Pawn master; private int lastTier = -1; private int cachedMasterId = -1;
        public override string LabelInBrackets => $"{Defs.TierName(Defs.Tier(pawn))} {Severity * 100:F0}%";
        public override string TipStringExtra { get { int t = Defs.Tier(pawn); return $"Tier: {Defs.TierName(t)}\nMaster: {master?.LabelShort ?? "None"}\nProximity buffs: +{t * 5}% work/move/melee"; } }
        
        public override void Tick()
        {
            base.Tick();
            if (!pawn.IsHashIntervalTick(251)) return;
            try
            {
                // Use ONLY cached data - absolutely NO pawn list iteration during tick
                int masterId = GC.I?.GetCachedMasterId(pawn) ?? -1;
                
                // Get cached master pawn reference from GC (updated safely in GameComponentTick)
                master = GC.I?.GetCachedMasterPawn(masterId);
                cachedMasterId = masterId;
                
                // Check proximity using cached master reference
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
        public override void ExposeData() { base.ExposeData(); Scribe_References.Look(ref master, "m"); Scribe_Values.Look(ref cachedMasterId, "mid", -1); }
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
            if (master == null || master.Dead || !pawn.Position.InHorDistOf(master.Position, 15f)) 
                markedForRemoval = true; 
        }
        public void Use() { ready = false; cdTick = Find.TickManager.TicksGame + Defs.CD(5000); }
        public override void ExposeData() { base.ExposeData(); Scribe_References.Look(ref master, "m"); Scribe_Values.Look(ref ready, "r", true); Scribe_Values.Look(ref cdTick, "cd"); }
    }

    public class HC_Stockholm : HediffComp
    {
        public override void CompPostTick(ref float s)
        {
            if (!Pawn.IsHashIntervalTick(2500) || !ModsConfig.IdeologyActive) return;
            var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (ideo != null && Pawn.Ideo != ideo) { Pawn.ideo.OffsetCertainty(-0.01f); if (Pawn.ideo.Certainty <= 0.01f) { Pawn.ideo.SetIdeo(ideo); Pawn.ideo.OffsetCertainty(0.5f); Messages.Message($"{Pawn.LabelShort} converted!", Pawn, MessageTypeDefOf.PositiveEvent); } }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              GAME COMPONENT
    // ══════════════════════════════════════════════════════════════════════════════

    public class GC : GameComponent
    {
        public static GC I;
        HashSet<int> hybridBeds = new(); Dictionary<int, int> slaveMap = new(), dailyLovin = new();
        Dictionary<int, int> cdPunish = new(), cdSmite = new(), cdCalm = new(), cdBless = new(), cdWrath = new();
        Dictionary<int, int> godDeaths = new(), corpseHP = new();
        List<(Pawn, HediffDef, bool)> deferred = new();
        int lastDay, lastHeal;
        
        // CACHE - Updated only in GameComponentTick, read by hediffs
        public int CachedSlaveCount { get; private set; } = 0;
        Dictionary<int, int> cachedMasterIds = new(); // slaveId -> masterId
        HashSet<int> cachedGodIds = new();
        bool cacheValid = false;

        public GC(Game g) => I = this;
        public override void FinalizeInit() => I = this;

        public override void GameComponentTick()
        {
            // Apply deferred from PREVIOUS tick first (safe - new tick frame)
            if (pendingApply)
            {
                pendingApply = false;
                ApplyAllDeferred();
            }
            
            // Update cache (read only - safe)
            UpdateCache();
            
            try
            {
                var m = Find.CurrentMap; if (m == null) return;
                int d = GenLocalDate.DayOfYear(m);
                if (d != lastDay) { dailyLovin.Clear(); lastDay = d; QueueUpdateHeads(m); }
                if (Find.TickManager.TicksGame % 2503 == 0) { QueueProcessAura(m); QueueProcessAuto(m); }
            }
            catch (Exception e) { Log.ErrorOnce($"[SRI] {e.Message}", 95847); }
            
            // Mark for application NEXT tick (not this tick!)
            if (deferred.Count > 0 || deferredActions.Count > 0)
            {
                pendingApply = true;
            }
        }
        
        // Cached pawn references - updated safely in GameComponentTick
        Dictionary<int, Pawn> cachedMasterPawns = new();
        public int CachedT3Count { get; private set; } = 0;
        public int CachedT4Count { get; private set; } = 0;
        List<Action> deferredActions = new();
        bool pendingApply = false;
        
        void UpdateCache()
        {
            try
            {
                var m = Find.CurrentMap;
                if (m == null) { cacheValid = false; return; }
                
                // Cache slave count and tier counts (READ ONLY)
                CachedSlaveCount = 0;
                CachedT3Count = 0;
                CachedT4Count = 0;
                var slaves = m.mapPawns.SlavesOfColonySpawned;
                if (slaves != null)
                {
                    foreach (var s in slaves)
                    {
                        if (s == null || s.Dead) continue;
                        CachedSlaveCount++;
                        int t = Defs.Tier(s);
                        if (t >= 4) CachedT4Count++;
                        else if (t >= 3) CachedT3Count++;
                    }
                }
                
                // Cache god IDs and master pawns (READ ONLY)
                cachedGodIds.Clear();
                cachedMasterPawns.Clear();
                var colonists = m.mapPawns.FreeColonistsSpawned;
                if (colonists != null)
                {
                    foreach (var c in colonists)
                    {
                        if (c == null || c.Dead) continue;
                        if (Defs.IsGod(c)) cachedGodIds.Add(c.thingIDNumber);
                        cachedMasterPawns[c.thingIDNumber] = c;
                    }
                }
                
                // Cache master assignments from slaveMap (no pawn list access needed)
                cachedMasterIds.Clear();
                foreach (var kv in slaveMap)
                {
                    if (kv.Value > 0) cachedMasterIds[kv.Key] = kv.Value;
                }
                
                cacheValid = true;
            }
            catch { cacheValid = false; }
        }
        
        void ApplyAllDeferred() 
        { 
            // Apply hediff changes
            if (deferred.Count > 0)
            {
                var l = deferred.ToList(); 
                deferred.Clear();
                foreach (var (p, h, add) in l) 
                { 
                    try 
                    { 
                        if (p == null || p.Dead || p.Destroyed) continue; 
                        if (add && !p.health.hediffSet.HasHediff(h)) 
                            p.health.AddHediff(h); 
                        else if (!add) 
                        { 
                            var x = p.health.hediffSet.GetFirstHediffOfDef(h); 
                            if (x != null) p.health.RemoveHediff(x); 
                        } 
                    } 
                    catch { } 
                }
            }
            
            // Apply other deferred actions
            if (deferredActions.Count > 0)
            {
                var actions = deferredActions.ToList();
                deferredActions.Clear();
                foreach (var a in actions)
                {
                    try { a(); } catch { }
                }
            }
        }
        
        public void Queue(Pawn p, HediffDef h, bool add) => deferred.Add((p, h, add));
        public void QueueAction(Action a) => deferredActions.Add(a);
        
        // Safe cached access for hediffs - NO pawn list iteration
        public int GetCachedMasterId(Pawn s) => cachedMasterIds.TryGetValue(s.thingIDNumber, out int id) ? id : -1;
        public bool IsGodCached(Pawn p) => cachedGodIds.Contains(p.thingIDNumber);
        public Pawn GetCachedMasterPawn(int masterId) => masterId > 0 && cachedMasterPawns.TryGetValue(masterId, out var p) ? p : null;

        // Concubine - these methods are called from UI/jobs, not during tick iteration
        public void SetConcubine(Pawn s, Pawn m) { slaveMap[s.thingIDNumber] = m?.thingIDNumber ?? -1; if (m != null && !s.health.hediffSet.HasHediff(Defs.H_Devotion)) Queue(s, Defs.H_Devotion, true); }
        public bool IsConcubine(Pawn s, Pawn m) => slaveMap.TryGetValue(s.thingIDNumber, out int id) && id == m.thingIDNumber;
        
        // GetMaster - ONLY call from safe contexts (UI, job finish actions), not during ticks
        public Pawn GetMaster(Pawn s) 
        { 
            if (s?.Map == null) return null; 
            if (!slaveMap.TryGetValue(s.thingIDNumber, out int id) || id <= 0) return null; 
            // Only iterate when explicitly needed, with try-catch
            try 
            { 
                foreach (var p in s.Map.mapPawns.FreeColonistsSpawned) 
                { 
                    if (p.thingIDNumber == id) return p; 
                } 
            } 
            catch { } 
            return null; 
        }
        
        // GetMasterFromCache - SAFE to call during ticks
        public Pawn GetMasterFromCache(Pawn s)
        {
            if (s?.Map == null || !cacheValid) return null;
            int masterId = GetCachedMasterId(s);
            if (masterId <= 0) return null;
            // Find master without iterating - use Map's pawn lookup
            try { return s.Map.mapPawns.AllPawns.FirstOrDefault(p => p.thingIDNumber == masterId && !p.Dead); }
            catch { return null; }
        }
        
        public List<Pawn> GetConcubines(Pawn m) 
        { 
            if (m?.Map == null) return new(); 
            try 
            { 
                var result = new List<Pawn>();
                foreach (var s in m.Map.mapPawns.SlavesOfColonySpawned) 
                { 
                    if (IsConcubine(s, m)) result.Add(s); 
                }
                return result;
            } 
            catch { return new(); } 
        }

        // Beds
        public bool IsHybrid(Building_Bed b) => b != null && hybridBeds.Contains(b.thingIDNumber);
        public void SetHybrid(Building_Bed b, bool v) { if (v) hybridBeds.Add(b.thingIDNumber); else hybridBeds.Remove(b.thingIDNumber); }

        // Cooldowns
        public bool Ready(Pawn p, string a) => GetCD(a)?.TryGetValue(p.thingIDNumber, out int v) != true || v <= Find.TickManager.TicksGame;
        public int Remaining(Pawn p, string a) => Math.Max(0, (GetCD(a)?.TryGetValue(p.thingIDNumber, out int v) == true ? v : 0) - Find.TickManager.TicksGame);
        public void SetCD(Pawn p, string a, int dur) { var d = GetCD(a); if (d != null) d[p.thingIDNumber] = Find.TickManager.TicksGame + Defs.CD(dur); }
        Dictionary<int, int> GetCD(string a) => a switch { "smite" => cdSmite, "calm" => cdCalm, "bless" => cdBless, "wrath" => cdWrath, "punish" => cdPunish, _ => null };

        // Lovin
        public int GetLovin(Pawn p) => dailyLovin.TryGetValue(p.thingIDNumber, out int v) ? v : 0;
        public void AddLovin(Pawn p) => dailyLovin[p.thingIDNumber] = GetLovin(p) + 1;

        // Resurrection
        public void RegisterDeath(Pawn g) => godDeaths[g.thingIDNumber] = Find.TickManager.TicksGame;
        public bool CanRes(Pawn g) => godDeaths.TryGetValue(g.thingIDNumber, out int t) && Find.TickManager.TicksGame - t < 180000;
        public int GetHP(Pawn g) => corpseHP.TryGetValue(g.thingIDNumber, out int v) ? v : 0;
        public void AddHP(Pawn g, int hp) => corpseHP[g.thingIDNumber] = GetHP(g) + hp;
        public void CompleteRes(Pawn g, Pawn perf) { 
            QueueAction(() => {
                ResurrectionUtility.TryResurrect(g); 
                Queue(g, Defs.H_ResSick, true); 
                Queue(perf, Defs.H_Exhaust, true); 
                try { foreach (var s in g.Map?.mapPawns.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>()) s.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_Resurrected); } catch {}
                godDeaths.Remove(g.thingIDNumber); 
                corpseHP.Remove(g.thingIDNumber); 
            });
        }

        // Queue methods - use ONLY cached data, never iterate pawn lists
        void QueueProcessAura(Map m)
        {
            // Use only cached data - NO pawn list iteration!
            foreach (var godId in cachedGodIds)
            {
                var g = GetCachedMasterPawn(godId);
                if (g == null || g.Dead) continue;
                if (!g.health.hediffSet.HasHediff(Defs.H_Divine)) Queue(g, Defs.H_Divine, true);
            }
            
            // Queue suppression updates to run safely later
            QueueAction(() => {
                try
                {
                    var gods = m?.mapPawns?.FreeColonistsSpawned?.ToList()?.Where(Defs.IsGod);
                    if (gods == null) return;
                    foreach (var g in gods)
                    {
                        if (g == null || g.Dead) continue;
                        foreach (var s in m.mapPawns.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>())
                        {
                            if (s == null || s.Dead) continue;
                            if (s.Position.InHorDistOf(g.Position, 9f))
                            {
                                var sup = s.needs?.TryGetNeed<Need_Suppression>();
                                if (sup != null && sup.CurLevel < 1) { sup.CurLevel = 1; FleckMaker.ThrowMetaIcon(s.Position, s.Map, FleckDefOf.IncapIcon); }
                            }
                        }
                    }
                }
                catch { }
            });
        }

        void QueueUpdateHeads(Map m)
        {
            // Use only cached data to collect IDs
            var slavesToRemoveHead = new List<int>();
            var bestPerMaster = new Dictionary<int, int>(); // masterId -> slaveId
            
            // Build groups from cached data (NO pawn iteration)
            var groups = new Dictionary<int, List<int>>(); // masterId -> list of slaveIds
            foreach (var kv in cachedMasterIds)
            {
                int slaveId = kv.Key;
                int masterId = kv.Value;
                if (masterId <= 0) continue;
                if (!groups.ContainsKey(masterId)) groups[masterId] = new();
                groups[masterId].Add(slaveId);
            }
            
            // Queue the actual work for safe execution
            QueueAction(() => {
                try
                {
                    foreach (var s in m?.mapPawns?.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>())
                    {
                        if (s == null || s.Dead) continue;
                        if (s.health.hediffSet.HasHediff(Defs.H_HeadConcubine)) 
                            Queue(s, Defs.H_HeadConcubine, false);
                    }
                    
                    var pawnGroups = new Dictionary<Pawn, List<Pawn>>();
                    foreach (var s in m?.mapPawns?.SlavesOfColonySpawned?.ToList() ?? new List<Pawn>())
                    {
                        if (s == null || s.Dead) continue;
                        var master = GetMaster(s);
                        if (master != null) { if (!pawnGroups.ContainsKey(master)) pawnGroups[master] = new(); pawnGroups[master].Add(s); }
                    }
                    foreach (var kv in pawnGroups)
                    {
                        Pawn best = null; float sc = -999;
                        foreach (var c in kv.Value) 
                        { 
                            if (c == null) continue;
                            float s = c.relations.OpinionOf(kv.Key) + c.skills.GetSkill(SkillDefOf.Social).Level * 5; 
                            if (s > sc) { sc = s; best = c; } 
                        }
                        if (best != null) Queue(best, Defs.H_HeadConcubine, true);
                    }
                }
                catch { }
            });
        }

        void QueueProcessAuto(Map m)
        {
            int d = GenLocalDate.DayOfYear(m);
            if (d == lastHeal) return;
            
            bool night = GenLocalDate.HourInteger(m) >= 22 || GenLocalDate.HourInteger(m) <= 5;
            if (!night) return;
            
            // Mark as done immediately to prevent re-triggering
            lastHeal = d;
            
            // Queue job assignments
            QueueAction(() => {
                try
                {
                    foreach (var master in m?.mapPawns?.FreeColonistsSpawned?.ToList() ?? new List<Pawn>())
                    {
                        if (master == null || master.Downed || master.Dead || !master.InBed()) continue;
                        var cons = GetConcubines(master)?.Where(c => c != null && !c.Downed && !c.Dead && c.health.hediffSet.HasHediff(Defs.H_Stockholm))?.ToList();
                        if (cons != null && cons.Count >= 2) 
                        { 
                            cons[0].jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Heal, master, cons[1])); 
                            cons[1].jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Heal, master, cons[0])); 
                            return; // Only one heal per night
                        }
                    }
                }
                catch { }
            });
        }

        public override void ExposeData()
        {
            var beds = hybridBeds.ToList();
            Scribe_Collections.Look(ref beds, "beds", LookMode.Value);
            Scribe_Collections.Look(ref slaveMap, "slaves", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdPunish, "cdP", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdSmite, "cdS", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdCalm, "cdC", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdBless, "cdB", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cdWrath, "cdW", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref godDeaths, "deaths", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref corpseHP, "hp", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref lastDay, "day"); Scribe_Values.Look(ref lastHeal, "heal");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                hybridBeds = beds != null ? new(beds) : new();
                slaveMap ??= new(); cdPunish ??= new(); cdSmite ??= new(); cdCalm ??= new(); cdBless ??= new(); cdWrath ??= new(); godDeaths ??= new(); corpseHP ??= new();
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              JOB DRIVERS
    // ══════════════════════════════════════════════════════════════════════════════

    public class JD_Lovin : JobDriver
    {
        Pawn Partner => (Pawn)TargetB.Thing;
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var wait = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            wait.tickAction = () => { pawn.rotationTracker.FaceTarget(Partner); if (pawn.Position.InHorDistOf(Partner.Position, 2) && Partner.CurJobDef == Defs.Job_Lovin) ReadyForNextToil(); };
            yield return wait;
            var love = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 500 };
            love.tickAction = () => { if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            love.AddFinishAction(() => {
                pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, Partner);
                if (pawn.IsSlaveOfColony && !pawn.health.hediffSet.HasHediff(Defs.H_Stockholm) && Rand.Chance(0.2f)) { pawn.health.AddHediff(Defs.H_Stockholm); Messages.Message($"{pawn.LabelShort} → Stockholm!", pawn, MessageTypeDefOf.PositiveEvent); }
                (pawn.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.05f);
            });
            yield return love;
        }
    }

    public class JD_Attend : JobDriver
    {
        Pawn Master => (Pawn)TargetA.Thing;
        public override bool TryMakePreToilReservations(bool e) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            t.tickAction = () => { pawn.rotationTracker.FaceTarget(Master); if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            t.AddFinishAction(() => { pawn.needs.mood.thoughts.memories.TryGainMemory(pawn.health.hediffSet.HasHediff(Defs.H_Stockholm) ? Defs.T_Satisfied : Defs.T_Forced, Master); GC.I?.AddLovin(Master); });
            yield return t;
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
            t.AddFinishAction(() => { 
                // Healing effect
                if (!Master.health.hediffSet.HasHediff(Defs.H_Healing)) { Master.health.AddHediff(Defs.H_Healing); Messages.Message($"{Master.LabelShort} healed!", Master, MessageTypeDefOf.PositiveEvent); } 
                // Devotion gain
                (pawn.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.03f); 
                // Mood buffs
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_ServedMaster, Master);
                Master.needs?.mood?.thoughts?.memories?.TryGainMemory(Defs.T_ReceivedService, pawn);
            });
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
            t.AddFinishAction(() => {
                GC.I?.SetCD(V, "punish", 120000);
                V.health.AddHediff(Defs.H_Punished);
                var sup = V.needs.TryGetNeed<Need_Suppression>(); if (sup != null) sup.CurLevel = 1;
                foreach (var o in V.Map.mapPawns.AllPawnsSpawned.ToList().Where(p => p != V && p != pawn && p.Position.InHorDistOf(V.Position, 10) && p.IsSlaveOfColony))
                { var s = o.needs.TryGetNeed<Need_Suppression>(); if (s != null) s.CurLevel = Mathf.Min(1, s.CurLevel + 0.3f); MoteMaker.ThrowText(o.DrawPos, o.Map, "!", Color.red); }
            });
            yield return t;
        }
    }

    public class JD_Procure : JobDriver
    {
        Pawn V => (Pawn)TargetA.Thing;
        public override bool TryMakePreToilReservations(bool e) => pawn.Reserve(TargetA, job);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var t = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            t.tickAction = () => { pawn.rotationTracker.FaceTarget(V); if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            t.AddFinishAction(() => {
                if (Rand.Chance(0.1f + pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f))
                {
                    var bed = pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().FirstOrDefault(b => b.ForPrisoners && !b.Medical && b.AnyUnownedSleepingSlot);
                    if (bed != null) { V.SetFaction(Faction.OfPlayer); V.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner); V.ownership.ClaimBedIfNonMedical(bed); Messages.Message("Procured!", V, MessageTypeDefOf.PositiveEvent); }
                }
                else MoteMaker.ThrowText(V.DrawPos, V.Map, "Refused", Color.red);
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
                if (T.Thing is Pawn v) { v.TakeDamage(new DamageInfo(DamageDefOf.Burn, Defs.Pwr(pawn, 20, 5), 0, -1, pawn)); FleckMaker.Static(v.Position, v.Map, FleckDefOf.PsycastAreaEffect, 2); SoundDefOf.Thunder_OffMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(v.Position, v.Map))); MoteMaker.ThrowText(v.DrawPos, v.Map, "SMITE!", Color.yellow); }
                GC.I?.SetCD(pawn, "smite", Defs.CD_Smite);
                foreach (var s in pawn.Map.mapPawns.SlavesOfColonySpawned.ToList().Where(x => x.Position.InHorDistOf(T.Cell, 20))) { s.needs.mood.thoughts.memories.TryGainMemory(Defs.T_Wrath); (s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.02f); }
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
    //                              HAREM TAB
    // ══════════════════════════════════════════════════════════════════════════════

    public class HaremTab : MainTabWindow
    {
        Vector2 scroll; Pawn sel;
        public override Vector2 RequestedTabSize => new(620, 520);

        public override void DoWindowContents(Rect r)
        {
            var m = Find.CurrentMap; if (m == null) return;
            var god = m.mapPawns.FreeColonistsSpawned.FirstOrDefault(Defs.IsGod);
            var slaves = m.mapPawns.SlavesOfColonySpawned.ToList();

            // Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, r.width, 30), "═══ Harem Management ═══");
            Text.Font = GameFont.Small;

            float y = 35;
            if (god != null)
            {
                var div = god.health.hediffSet.GetFirstHediffOfDef(Defs.H_Divine);
                Widgets.Label(new Rect(0, y, r.width, 22), $"God: {god.LabelShort}  |  Slaves: {slaves.Count}  |  Power: {div?.Severity ?? 0:F0}");
            }
            else Widgets.Label(new Rect(0, y, r.width, 22), "No God present");
            y += 24;

            float avg = slaves.Count > 0 ? slaves.Average(s => s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion)?.Severity ?? 0) * 100 : 0;
            int t4 = slaves.Count(s => Defs.Tier(s) >= 4), t3 = slaves.Count(s => Defs.Tier(s) == 3);
            Widgets.Label(new Rect(0, y, r.width, 22), $"Avg: {avg:F0}%  |  Zealots: {t4}  |  Devoted: {t3}");
            y += 28;

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

                int tier = Defs.Tier(s);
                var dev = s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion);
                var master = GC.I?.GetMaster(s);
                string spec = GetSpec(s), status = s.health.hediffSet.HasHediff(Defs.H_Stockholm) ? "Stockholm" : s.health.hediffSet.HasHediff(Defs.H_HeadConcubine) ? "Head" : "-";

                GUI.color = Defs.TierColor(tier);
                Widgets.Label(new Rect(0, ly, 130, 24), s.LabelShort);
                GUI.color = Color.white;
                Widgets.Label(new Rect(130, ly, 70, 24), Defs.TierName(tier));
                Widgets.Label(new Rect(200, ly, 70, 24), $"{(dev?.Severity ?? 0) * 100:F0}%");
                Widgets.Label(new Rect(270, ly, 90, 24), spec);
                Widgets.Label(new Rect(360, ly, 90, 24), master?.LabelShort ?? "-");
                Widgets.Label(new Rect(460, ly, 80, 24), status);
                ly += 26;
            }
            Widgets.EndScrollView();

            // Actions
            if (sel != null)
            {
                y = r.height - 88;
                Widgets.DrawLineHorizontal(0, y, r.width); y += 5;
                Widgets.Label(new Rect(0, y, 200, 22), $"Selected: {sel.LabelShort}"); y += 24;

                if (Widgets.ButtonText(new Rect(0, y, 110, 26), "Assign Master"))
                {
                    var opts = new List<FloatMenuOption> { new("Unassign", () => GC.I?.SetConcubine(sel, null)) };
                    foreach (var c in m.mapPawns.FreeColonists) opts.Add(new(c.LabelShort + (GC.I?.IsConcubine(sel, c) == true ? " ✓" : ""), () => GC.I?.SetConcubine(sel, c)));
                    Find.WindowStack.Add(new FloatMenu(opts));
                }

                if (Defs.Tier(sel) >= 3 && SRI_Mod.S.enableSpecializations && Widgets.ButtonText(new Rect(120, y, 100, 26), "Specialize"))
                {
                    var opts = new List<FloatMenuOption>
                    {
                        new("Warrior", () => SetSpec(sel, Defs.H_Warrior)), new("Healer", () => SetSpec(sel, Defs.H_Healer)),
                        new("Scholar", () => SetSpec(sel, Defs.H_Scholar)), new("Entertainer", () => SetSpec(sel, Defs.H_Entertainer)),
                        new("Remove", () => ClearSpec(sel))
                    };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }

                if (Widgets.ButtonText(new Rect(230, y, 70, 26), "Go To")) CameraJumper.TryJumpAndSelect(sel);
                if (Widgets.ButtonText(new Rect(310, y, 100, 26), "Stockholm") && !sel.health.hediffSet.HasHediff(Defs.H_Stockholm)) sel.health.AddHediff(Defs.H_Stockholm);
            }
        }

        string GetSpec(Pawn p) => p.health.hediffSet.HasHediff(Defs.H_Warrior) ? "Warrior" : p.health.hediffSet.HasHediff(Defs.H_Healer) ? "Healer" : p.health.hediffSet.HasHediff(Defs.H_Scholar) ? "Scholar" : p.health.hediffSet.HasHediff(Defs.H_Entertainer) ? "Entertainer" : "-";
        void SetSpec(Pawn p, HediffDef h) { ClearSpec(p); p.health.AddHediff(h); }
        void ClearSpec(Pawn p) { foreach (var h in new[] { Defs.H_Warrior, Defs.H_Healer, Defs.H_Scholar, Defs.H_Entertainer }) { var x = p.health.hediffSet.GetFirstHediffOfDef(h); if (x != null) p.health.RemoveHediff(x); } }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              HARMONY PATCHES
    // ══════════════════════════════════════════════════════════════════════════════

    // Gizmos
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Gizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> r, Pawn __instance)
        {
            foreach (var g in r) yield return g;
            var p = __instance;

            // God abilities
            if (Defs.IsGod(p) && !p.Dead && !p.Downed && !p.IsSlave && SRI_Mod.S.enableAbilities)
            {
                var gc = GC.I; if (gc == null) yield break;

                // Smite
                var smite = new Command_Target { defaultLabel = gc.Ready(p, "smite") ? "Smite" : $"Smite ({gc.Remaining(p, "smite") / 2500f:F1}h)", defaultDesc = $"Dmg: {Defs.Pwr(p, 20, 5):F0}", icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"), targetingParams = new() { canTargetPawns = true, canTargetBuildings = false }, action = t => p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Smite, t)) };
                if (!gc.Ready(p, "smite")) smite.Disable("Cooldown");
                yield return smite;

                // Calm
                var calm = new Command_Action { defaultLabel = gc.Ready(p, "calm") ? "Mass Calm" : $"Calm ({gc.Remaining(p, "calm") / 2500f:F1}h)", defaultDesc = "Calm slaves.", icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt"), action = () => { foreach (var s in p.Map.mapPawns.SlavesOfColonySpawned.ToList().Where(x => x.Position.InHorDistOf(p.Position, Defs.Pwr(p, 10, 2)))) { if (s.InMentalState) s.MentalState.RecoverFromState(); FleckMaker.ThrowMetaIcon(s.Position, s.Map, FleckDefOf.Heart); } gc.SetCD(p, "calm", Defs.CD_Calm); } };
                if (!gc.Ready(p, "calm")) calm.Disable("Cooldown");
                yield return calm;

                // Bless
                var bless = new Command_Target { defaultLabel = gc.Ready(p, "bless") ? "Blessing" : $"Bless ({gc.Remaining(p, "bless") / 2500f:F1}h)", defaultDesc = "Buff ally.", icon = ContentFinder<Texture2D>.Get("UI/Commands/Draft"), targetingParams = new() { canTargetPawns = true, validator = t => t.Thing is Pawn x && x.Faction == Faction.OfPlayer }, action = t => { if (t.Thing is Pawn x) { x.health.AddHediff(Defs.H_Blessing); x.needs.mood.thoughts.memories.TryGainMemory(Defs.T_Blessed, p); (x.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.05f); FleckMaker.Static(x.Position, x.Map, FleckDefOf.PsycastAreaEffect, 2); gc.SetCD(p, "bless", Defs.CD_Bless); } } };
                if (!gc.Ready(p, "bless")) bless.Disable("Cooldown");
                yield return bless;

                // Wrath
                var wrath = new Command_Action { defaultLabel = gc.Ready(p, "wrath") ? "Wrath" : $"Wrath ({gc.Remaining(p, "wrath") / 2500f:F1}h)", defaultDesc = $"AoE Dmg: {Defs.Pwr(p, 15, 3):F0}", icon = ContentFinder<Texture2D>.Get("UI/Commands/FireAtWill"), action = () => { FleckMaker.Static(p.Position, p.Map, FleckDefOf.PsycastAreaEffect, 8); SoundDefOf.Thunder_OffMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(p.Position, p.Map))); float dmg = Defs.Pwr(p, 15, 3); foreach (var e in p.Map.mapPawns.AllPawnsSpawned.ToList().Where(x => x != p && x.Position.InHorDistOf(p.Position, 8) && x.HostileTo(p))) e.TakeDamage(new DamageInfo(DamageDefOf.Burn, dmg, 0, -1, p)); gc.SetCD(p, "wrath", Defs.CD_Wrath); foreach (var s in p.Map.mapPawns.SlavesOfColonySpawned.ToList().Where(x => x.Position.InHorDistOf(p.Position, 20))) { s.needs.mood.thoughts.memories.TryGainMemory(Defs.T_Wrath); (s.health.hediffSet.GetFirstHediffOfDef(Defs.H_Devotion) as Hediff_Devotion)?.Add(0.02f); } } };
                if (!gc.Ready(p, "wrath")) wrath.Disable("Cooldown");
                yield return wrath;
            }

            // Slave gizmos
            if (p.IsSlaveOfColony && p.RaceProps.Humanlike)
            {
                yield return new Command_Action { defaultLabel = "Assign", icon = ContentFinder<Texture2D>.Get("UI/Commands/ReleaseAnimals"), action = () => { var opts = new List<FloatMenuOption> { new("Unassign", () => GC.I?.SetConcubine(p, null)) }; foreach (var c in p.Map.mapPawns.FreeColonists.ToList()) opts.Add(new(c.LabelShort + (GC.I?.IsConcubine(p, c) == true ? " ✓" : ""), () => GC.I?.SetConcubine(p, c))); Find.WindowStack.Add(new FloatMenu(opts)); } };
                yield return new Command_Action { defaultLabel = $"{Defs.TierName(Defs.Tier(p))}", icon = ContentFinder<Texture2D>.Get("UI/Commands/Draft"), action = () => { } };
            }
        }
    }

    // Float menu
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
                    if (v.IsSlaveOfColony) opts.Add(new($"Lovin' {v.LabelShort}", () => { var bed = pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().FirstOrDefault(b => b.SleepingSlotsCount >= 2 && !b.IsBurning()); if (bed == null) { Messages.Message("No bed!", MessageTypeDefOf.RejectInput); return; } if (pawn.Drafted) pawn.drafter.Drafted = false; if (v.Drafted) v.drafter.Drafted = false; pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Lovin, bed, v)); v.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Lovin, bed, pawn)); }));
                    if (v.IsSlaveOfColony || v.IsPrisonerOfColony) { int cd = GC.I?.Remaining(v, "punish") ?? 0; if (cd > 0) opts.Add(new($"Punish (CD {cd / 2500}h)", null) { Disabled = true }); else opts.Add(new($"Punish {v.LabelShort}", () => { if (pawn.Drafted) pawn.drafter.Drafted = false; pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Punish, v)); })); }
                    if (!v.IsPrisoner && !v.IsSlave && !v.IsColonist && v.RaceProps.Humanlike && !v.HostileTo(pawn)) opts.Add(new($"Procure {v.LabelShort} ({(0.1f + pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f) * 100:F0}%)", () => { if (pawn.Drafted) pawn.drafter.Drafted = false; pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Procure, v)); }));
                    if (v.Dead && Defs.IsGod(v) && GC.I?.CanRes(v) == true && SRI_Mod.S.enableResurrection && Defs.Tier(pawn) >= 3 && v.Corpse != null) { int hp = GC.I.GetHP(v); if (hp >= 30) opts.Add(new($"Resurrect ({hp} HP)", () => pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_Resurrect, v.Corpse)))); else opts.Add(new($"Gather corpses ({hp}/30)", () => { var c = GenClosest.ClosestThing_Global(pawn.Position, pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse), 50, x => x != v.Corpse) as Corpse; if (c != null) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Defs.Job_GatherCorpse, c, v.Corpse)); })); }
                }
            }
        }
    }

    // Bed gizmos
    [HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
    public static class Patch_BedGiz
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> r, Building_Bed __instance)
        {
            foreach (var g in r) yield return g;
            if (__instance.Faction == Faction.OfPlayer && __instance.SleepingSlotsCount >= 2)
                yield return new Command_Toggle { defaultLabel = "Master+Slave", icon = ContentFinder<Texture2D>.Get("UI/Commands/ForPrisoners"), isActive = () => GC.I?.IsHybrid(__instance) == true, toggleAction = () => { GC.I?.SetHybrid(__instance, !GC.I?.IsHybrid(__instance) == true); foreach (var p in __instance.CompAssignableToPawn.AssignedPawns.ToList()) __instance.CompAssignableToPawn.TryUnassignPawn(p); } };
        }
    }

    // Bed validity - Applied manually in Defs static constructor
    public static class Patch_BedValid
    {
        public static bool Prefix(Thing bedThing, Pawn sleeper, ref bool __result) { if (bedThing is Building_Bed bed && GC.I?.IsHybrid(bed) == true) { if (bed.Destroyed || bed.IsBurning() || bed.IsForbidden(sleeper)) return true; if (bed.Medical && !HealthAIUtility.ShouldSeekMedicalRest(sleeper)) return true; if ((sleeper.IsSlaveOfColony || sleeper.IsColonist) && (bed.CompAssignableToPawn.AssignedPawns.Contains(sleeper) || bed.AnyUnownedSleepingSlot)) { __result = true; return false; } } return true; }
    }

    [HarmonyPatch(typeof(ForbidUtility), "IsForbidden", new[] { typeof(Thing), typeof(Pawn) })]
    public static class Patch_Forbid { static bool Prefix(Thing t, Pawn pawn, ref bool __result) { if (pawn.IsSlaveOfColony && t is Building_Bed bed && GC.I?.IsHybrid(bed) == true) { __result = false; return false; } return true; } }

    [HarmonyPatch]
    public static class Patch_Assign { 
        static bool Prepare() => AccessTools.Method(typeof(CompAssignableToPawn), "CanAssignTo") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(CompAssignableToPawn), "CanAssignTo");
        static void Postfix(CompAssignableToPawn __instance, Pawn pawn, ref AcceptanceReport __result) { if (!__result.Accepted && __instance.parent is Building_Bed bed && GC.I?.IsHybrid(bed) == true && (pawn.IsSlaveOfColony || pawn.IsColonist)) __result = AcceptanceReport.WasAccepted; } 
    }

    [HarmonyPatch(typeof(BedUtility), "WillingToShareBed")]
    public static class Patch_Share { static void Postfix(Pawn pawn1, Pawn pawn2, ref bool __result) { if (!__result && ((pawn1.IsSlaveOfColony && pawn2.IsColonist) || (pawn2.IsSlaveOfColony && pawn1.IsColonist))) { var b1 = pawn1.ownership.OwnedBed; var b2 = pawn2.ownership.OwnedBed; if (b1 != null && b1 == b2 && GC.I?.IsHybrid(b1) == true) __result = true; } } }

    [HarmonyPatch(typeof(LovePartnerRelationUtility), "LovePartnerRelationExists")]
    public static class Patch_Love { static void Postfix(Pawn first, Pawn second, ref bool __result) { if (!__result && ((first.IsSlaveOfColony && second.IsColonist) || (second.IsSlaveOfColony && first.IsColonist))) { var b1 = first.ownership.OwnedBed; var b2 = second.ownership.OwnedBed; if (b1 != null && b1 == b2 && GC.I?.IsHybrid(b1) == true) __result = true; } } }

    // God mechanics - Applied manually in Defs static constructor
    public static class Patch_NoGuns
    {
        public static bool Prefix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result) { if (Defs.IsGod(pawn) && thing.def.IsRangedWeapon) { cantReason = "God refuses guns."; __result = false; return false; } return true; }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Deflect
    {
        static bool Prefix(Projectile __instance, Thing hitThing)
        {
            // Use CACHED slave count - NO pawn list access!
            if (hitThing is Pawn p && Defs.IsGod(p)) { int s = GC.I?.CachedSlaveCount ?? 0; if (Rand.Chance(Mathf.Min(0.2f + s * 0.05f, 0.8f))) { MoteMaker.ThrowText(p.DrawPos, p.Map, "DEFLECT!", Color.yellow); SoundDefOf.MetalHitImportant.PlayOneShot(SoundInfo.InMap(new TargetInfo(p.Position, p.Map))); if (__instance.Launcher is Pawn shooter && !shooter.Dead) shooter.TakeDamage(new DamageInfo(__instance.def.projectile.damageDef, __instance.def.projectile.GetDamageAmount(null), 0, -1, p)); return false; } }
            return true;
        }
    }

    [HarmonyPatch(typeof(StatExtension), "GetStatValue")]
    public static class Patch_Stats
    {
        static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            try
            {
                if (thing is Pawn p && p.Spawned && !p.Dead)
                {
                    var div = p.health?.hediffSet?.GetFirstHediffOfDef(Defs.H_Divine);
                    if (div != null) { int c = (int)div.Severity; if (stat == StatDefOf.MoveSpeed) __result *= 1 + c * 0.05f; else if (stat == StatDefOf.MeleeDamageFactor) __result += c * 0.1f; else if (stat == StatDefOf.ResearchSpeed) __result += c * 0.1f; }
                    
                    var dev = p.health?.hediffSet?.GetFirstHediffOfDef(Defs.H_Devotion);
                    if (dev != null && GC.I != null) 
                    { 
                        // Use CACHED master ID - NO pawn list iteration!
                        int masterId = GC.I.GetCachedMasterId(p);
                        if (masterId > 0)
                        {
                            // Check proximity using cached hediff's master reference
                            var devHediff = dev as Hediff_Devotion;
                            // Just use tier bonus based on cached data, don't iterate pawns
                            int t = Defs.Tier(p); 
                            if (t > 0)
                            {
                                // Apply tier bonuses (proximity check skipped for safety during stat calc)
                                if (stat == StatDefOf.WorkSpeedGlobal) __result += t * 0.03f; 
                                if (stat == StatDefOf.MoveSpeed) __result += t * 0.03f; 
                                if (stat == StatDefOf.MeleeDamageFactor) __result += t * 0.03f;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch]
    public static class Patch_Suppress { 
        static bool Prepare() => AccessTools.Method(typeof(Need_Suppression), "NeedInterval") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(Need_Suppression), "NeedInterval");
        static bool Prefix(Need_Suppression __instance, Pawn ___pawn) { if (___pawn.health.hediffSet.HasHediff(Defs.H_Stockholm)) { __instance.CurLevel = __instance.MaxLevel; return false; } return true; } 
    }

    // SLAVE WORK UNLOCK - This is the key patch!
    [HarmonyPatch]
    public static class Patch_SlaveWork
    {
        static bool Prepare()
        {
            var method = AccessTools.Method(typeof(Pawn), "GetDisabledWorkTypes");
            return method != null;
        }
        
        static MethodBase TargetMethod() => AccessTools.Method(typeof(Pawn), "GetDisabledWorkTypes");
        
        static void Postfix(Pawn __instance, ref List<WorkTypeDef> __result)
        {
            if (!SRI_Mod.S.unlockSlaveWork || !__instance.IsSlaveOfColony) return;
            
            // Check if slave qualifies for work unlocks
            bool isScholar = __instance.health.hediffSet.HasHediff(Defs.H_Scholar);
            bool highTier = Defs.Tier(__instance) >= 2;
            bool hasStockholm = __instance.health.hediffSet.HasHediff(Defs.H_Stockholm);
            
            if (!isScholar && !highTier && !hasStockholm) return;
            
            // Remove these work type restrictions
            var toRemove = new List<WorkTypeDef>();
            foreach (var w in __result)
            {
                // Scholars or Tier 2+ can research
                if (w == WorkTypeDefOf.Research && (isScholar || highTier)) toRemove.Add(w);
                // Stockholm or Tier 2+ can do art, handle, wardening
                if (hasStockholm || highTier)
                {
                    if (w.defName == "Art" || w.defName == "Handling" || w.defName == "Warden") toRemove.Add(w);
                }
            }
            __result = __result.Except(toRemove).ToList();
        }
    }

    // Fix skill display for unlocked slaves
    [HarmonyPatch]
    public static class Patch_SkillShow
    {
        static bool Prepare()
        {
            var prop = typeof(SkillRecord).GetProperty("PermanentlyDisabledBecauseOfWorkTypes");
            return prop != null;
        }
        
        static MethodBase TargetMethod()
        {
            var prop = typeof(SkillRecord).GetProperty("PermanentlyDisabledBecauseOfWorkTypes");
            return prop?.GetGetMethod();
        }
        
        static void Postfix(SkillRecord __instance, ref bool __result)
        {
            if (!__result || !SRI_Mod.S.unlockSlaveWork || __instance.Pawn?.IsSlaveOfColony != true) return;
            
            bool isScholar = __instance.Pawn.health.hediffSet.HasHediff(Defs.H_Scholar);
            bool highTier = Defs.Tier(__instance.Pawn) >= 2;
            bool hasStockholm = __instance.Pawn.health.hediffSet.HasHediff(Defs.H_Stockholm);
            
            // Show intellectual for scholars/high tier
            if (__instance.def == SkillDefOf.Intellectual && (isScholar || highTier)) __result = false;
            // Show artistic/animals/social for stockholm/high tier
            if ((hasStockholm || highTier) && (__instance.def == SkillDefOf.Artistic || __instance.def == SkillDefOf.Animals || __instance.def == SkillDefOf.Social)) __result = false;
        }
    }

    // Visual overlay
    [HarmonyPatch(typeof(PawnUIOverlay), "DrawPawnGUIOverlay")]
    public static class Patch_Overlay
    {
        static void Postfix(PawnUIOverlay __instance)
        {
            if (!SRI_Mod.S.showOverlay) return;
            var p = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (p == null || !p.IsSlaveOfColony || !p.Spawned) return;
            int t = Defs.Tier(p); if (t <= 0) return;
            GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(p, -0.6f), Defs.TierName(t)[0].ToString(), Defs.TierColor(t));
        }
    }

    // Scenario
    public class ScenPart_GodSetup : ScenPart
    {
        public override void PostGameStart()
        {
            try
            {
                var cols = Find.GameInitData.startingAndOptionalPawns.Take(5).ToList();
                if (cols.Count == 0) return;

                var god = cols[0];
                god.gender = Gender.Male;
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
                    var s = cols[i];
                    s.gender = Gender.Female;
                    s.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                    s.health.AddHediff(Defs.H_Stockholm);
                    s.health.AddHediff(Defs.H_Devotion);
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
