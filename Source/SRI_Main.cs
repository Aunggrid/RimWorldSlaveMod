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
    [StaticConstructorOnStartup]
    public static class SRI_Main
    {
        // --- Definitions ---
        public static JobDef Job_SlaveLovin;
        public static JobDef Job_PunishSlave;
        public static JobDef Job_Procure;
        public static JobDef Job_ConcubineLovin;
        public static JobDef Job_HealingTouch;

        public static HediffDef Hediff_Stockholm;
        public static HediffDef Hediff_ProtectiveRage;
        public static HediffDef Hediff_PunishedPain;
        public static HediffDef Hediff_HealingTouch;
        public static HediffDef Hediff_HeadConcubine;
        public static HediffDef Hediff_DivinePower;

        public static TraitDef Trait_ReincarnatedGod;
        public static ThoughtDef Thought_SatisfiedByMaster;
        public static ThoughtDef Thought_ForcedAffection;
        
        public static ScenPartDef ScenPart_GodSetup_Def;

        static SRI_Main()
        {
            // 1. Jobs
            Job_SlaveLovin = CreateJob("SRI_SlaveLovin", typeof(JobDriver_SlaveLovin), "making love.");
            Job_PunishSlave = CreateJob("SRI_PunishSlave", typeof(JobDriver_PunishSlave), "punishing slave.", true);
            Job_Procure = CreateJob("SRI_Procure", typeof(JobDriver_Procure), "procuring victim.");
            Job_ConcubineLovin = CreateJob("SRI_ConcubineLovin", typeof(JobDriver_ConcubineLovin), "attending to master.");
            Job_HealingTouch = CreateJob("SRI_HealingTouch", typeof(JobDriver_HealingTouch), "performing healing ritual.");

            // 2. Definitions
            CreateHediffs();
            CreateTraits();
            CreateThoughts();

            // 3. Get the ScenPartDef (it's loaded from XML already)
            ScenPart_GodSetup_Def = DefDatabase<ScenPartDef>.GetNamedSilentFail("SRI_ScenPart_GodSetup");
            if (ScenPart_GodSetup_Def == null)
            {
                Log.Error("SRI_Main: Could not find SRI_ScenPart_GodSetup!");
            }

            // 4. Harmony
            var harmony = new Harmony("com.slaverealism.improved");
            harmony.PatchAll();
        }

        

        static void AddStartingThing(List<ScenPart> parts, ThingDef thing, int count, ThingDef stuff = null, QualityCategory quality = QualityCategory.Normal)
        {
            ScenPart_StartingThing_Defined part = new ScenPart_StartingThing_Defined();
            part.def = DefDatabase<ScenPartDef>.GetNamed("StartingThing_Defined");
            
            var t = Traverse.Create(part);
            t.Field("thingDef").SetValue(thing);
            t.Field("count").SetValue(count);
            if (stuff != null) t.Field("stuff").SetValue(stuff);
            if (quality != QualityCategory.Normal) t.Field("quality").SetValue(quality);
            parts.Add(part);
        }

        static JobDef CreateJob(string defName, Type driver, string report, bool showWeapon = false)
        {
            JobDef j = new JobDef { defName = defName, driverClass = driver, reportString = report, playerInterruptible = true, checkOverrideOnDamage = CheckJobOverrideOnDamageMode.Always, alwaysShowWeapon = showWeapon, casualInterruptible = false, suspendable = false };
            DefDatabase<JobDef>.Add(j); return j;
        }

        static void CreateTraits()
        {
            Trait_ReincarnatedGod = new TraitDef { defName = "SRI_ReincarnatedGod", degreeDatas = new List<TraitDegreeData> { new TraitDegreeData { label = "Reincarnated God", description = "Reflects projectiles. Power from slaves. No guns." } } };
            DefDatabase<TraitDef>.Add(Trait_ReincarnatedGod);
        }

        static void CreateHediffs()
        {
            Hediff_Stockholm = new HediffDef { defName = "SRI_StockholmSyndrome", label = "Stockholm syndrome", description = "Bond.", hediffClass = typeof(HediffWithComps), defaultLabelColor = Color.cyan, isBad = false, stages = new List<HediffStage> { new HediffStage { statOffsets = new List<StatModifier> { new StatModifier { stat = StatDefOf.GlobalLearningFactor, value = 0.2f } } } }, comps = new List<HediffCompProperties> { new HediffCompProperties { compClass = typeof(HediffComp_StockholmConversion) } } }; DefDatabase<HediffDef>.Add(Hediff_Stockholm);
            
            Hediff_ProtectiveRage = new HediffDef { defName = "SRI_ProtectiveRage", label = "Protective Rage", description = "Adrenaline rush.", hediffClass = typeof(HediffWithComps), defaultLabelColor = Color.red, isBad = false, comps = new List<HediffCompProperties> { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(2500, 5000), showRemainingTime = true }, new HediffCompProperties { compClass = typeof(HediffComp_RageSustain) } } };
            HediffStage rageStage = new HediffStage { painFactor = 0.1f, statOffsets = new List<StatModifier> { new StatModifier { stat = StatDefOf.MoveSpeed, value = 3.0f }, new StatModifier { stat = StatDefOf.MeleeHitChance, value = 30.0f }, new StatModifier { stat = StatDefOf.MeleeDodgeChance, value = 30.0f }, new StatModifier { stat = StatDefOf.IncomingDamageFactor, value = -0.5f }, new StatModifier { stat = StatDefOf.MeleeDamageFactor, value = 1.5f }, new StatModifier { stat = StatDefOf.MeleeWeapon_CooldownMultiplier, value = -0.5f } }, capMods = new List<PawnCapacityModifier> { new PawnCapacityModifier { capacity = PawnCapacityDefOf.Manipulation, offset = 1.0f }, new PawnCapacityModifier { capacity = PawnCapacityDefOf.Consciousness, offset = 0.5f } } }; Hediff_ProtectiveRage.stages = new List<HediffStage> { rageStage }; DefDatabase<HediffDef>.Add(Hediff_ProtectiveRage);
            
            Hediff_PunishedPain = new HediffDef { defName = "SRI_PunishedPain", label = "Punished", description = "Pain from punishment.", hediffClass = typeof(HediffWithComps), defaultLabelColor = new Color(0.8f, 0.4f, 0f), isBad = true, comps = new List<HediffCompProperties> { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(10000, 20000), showRemainingTime = true } }, stages = new List<HediffStage> { new HediffStage { painOffset = 0.35f } } }; DefDatabase<HediffDef>.Add(Hediff_PunishedPain);
            
            Hediff_HealingTouch = new HediffDef { defName = "SRI_HealingTouch", label = "Healing Touch", description = "Recovering faster.", hediffClass = typeof(HediffWithComps), defaultLabelColor = Color.green, isBad = false, comps = new List<HediffCompProperties> { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(15000, 30000), showRemainingTime = true } }, stages = new List<HediffStage> { new HediffStage { statOffsets = new List<StatModifier> { new StatModifier { stat = StatDefOf.ImmunityGainSpeed, value = 0.5f }, new StatModifier { stat = StatDefOf.InjuryHealingFactor, value = 0.5f } } } } }; DefDatabase<HediffDef>.Add(Hediff_HealingTouch);
            
            Hediff_HeadConcubine = new HediffDef { defName = "SRI_HeadConcubine", label = "Head Concubine", description = "Favorite concubine.", hediffClass = typeof(HediffWithComps), defaultLabelColor = new Color(1f, 0.8f, 0f), isBad = false, stages = new List<HediffStage> { new HediffStage { statOffsets = new List<StatModifier> { new StatModifier { stat = StatDefOf.SocialImpact, value = 0.2f } } } } }; DefDatabase<HediffDef>.Add(Hediff_HeadConcubine);
            
            // Divine Power with detailed description
            Hediff_DivinePower = new HediffDef 
            { 
                defName = "SRI_DivinePower", 
                label = "Divine Power", 
                description = "The god draws power from enslaved followers. Each slave grants:\n• +5% Move Speed\n• +10% Melee Damage\n• +10% Research Speed\n• +10% Psychic Sensitivity\n• +5% Bullet Deflection (base 20%, max 80%)", 
                hediffClass = typeof(Hediff_DivineScaling), 
                defaultLabelColor = new Color(1f, 0.9f, 0.2f), 
                isBad = false 
            }; 
            DefDatabase<HediffDef>.Add(Hediff_DivinePower);
        }

        static void CreateThoughts()
        {
            Thought_SatisfiedByMaster = new ThoughtDef { defName = "SRI_SatisfiedByMaster", label = "Satisfied by Master", durationDays = 1f, stages = new List<ThoughtStage> { new ThoughtStage { label = "Satisfied by master", baseMoodEffect = 5 } } }; DefDatabase<ThoughtDef>.Add(Thought_SatisfiedByMaster);
            Thought_ForcedAffection = new ThoughtDef { defName = "SRI_ForcedAffection", label = "Forced affection", durationDays = 1f, stages = new List<ThoughtStage> { new ThoughtStage { label = "Forced affection", baseMoodEffect = -8 } } }; DefDatabase<ThoughtDef>.Add(Thought_ForcedAffection);
        }
    }

    // ----------------------------------------------------------------------
    // SCENARIO LOGIC PART CLASS
    // ----------------------------------------------------------------------
    public class ScenPart_GodSetup : ScenPart
    {
        // CRITICAL: Parameterless constructor required for XML loading
        public ScenPart_GodSetup()
        {
        }

        public override string Summary(Scenario scen)
        {
            return "A reincarnated god with a devoted harem.";
        }

        public override IEnumerable<string> GetSummaryListEntries(string tag)
        {
            if (tag == "MapScenario")
            {
                yield return "Reincarnated God starts with 4 devoted slaves";
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override void PostGameStart()
        {
            base.PostGameStart();
            
            try
            {
                List<Pawn> colonists = Find.GameInitData.startingAndOptionalPawns.Take(5).ToList();
                if (colonists.Count == 0)
                {
                    Log.Error("SRI_Main: No colonists found in PostGameStart!");
                    return;
                }

                // 1. The God
                Pawn god = colonists[0];
                god.gender = Gender.Male;
                
                if (god.story != null)
                {
                    // Remove Asexual trait if present
                    TraitDef asexual = TraitDefOf.Asexual;
                    if (asexual != null && god.story.traits.HasTrait(asexual))
                    {
                        Trait asexualTrait = god.story.traits.GetTrait(asexual);
                        god.story.traits.RemoveTrait(asexualTrait);
                        Log.Message("SRI_Main: Removed Asexual trait from God");
                    }
                    
                    // Add Reincarnated God trait
                    if (god.story.traits.allTraits.Count >= 3) 
                        god.story.traits.allTraits.RemoveAt(0);
                    god.story.traits.GainTrait(new Trait(SRI_Main.Trait_ReincarnatedGod));
                    
                    // Add Beauty trait (Handsome = degree 2)
                    TraitDef beauty = TraitDef.Named("Beauty");
                    if (beauty != null && !god.story.traits.HasTrait(beauty))
                    {
                        god.story.traits.GainTrait(new Trait(beauty, 2));
                    }
                }
                
                // Set minimum skill levels to 5
                if (god.skills != null)
                {
                    foreach (SkillRecord skill in god.skills.skills)
                    {
                        if (skill.Level < 5)
                        {
                            skill.Level = 5;
                            skill.xpSinceLastLevel = 0;
                        }
                    }
                    
                    // Boost key skills even higher
                    god.skills.GetSkill(SkillDefOf.Social).Level = Mathf.Max(12, god.skills.GetSkill(SkillDefOf.Social).Level);
                    god.skills.GetSkill(SkillDefOf.Intellectual).Level = Mathf.Max(10, god.skills.GetSkill(SkillDefOf.Intellectual).Level);
                }
                
                // Add Divine Power hediff
                god.health.AddHediff(SRI_Main.Hediff_DivinePower);

                // 2. The Harem
                for (int i = 1; i < colonists.Count; i++)
                {
                    Pawn slave = colonists[i];
                    slave.gender = Gender.Female;
                    slave.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                    slave.health.AddHediff(SRI_Main.Hediff_Stockholm);
                    if (SRI_GameComponent.Instance != null)
                    {
                        SRI_GameComponent.Instance.SetConcubine(slave, god);
                    }
                }
                
                Messages.Message("The God has descended with his harem!", god, MessageTypeDefOf.PositiveEvent, true);
                Log.Message("SRI_Main: God setup completed successfully");
            }
            catch (Exception e)
            {
                Log.Error("SRI_Main: Error in ScenPart_GodSetup.PostGameStart: " + e.ToString());
            }
        }
    }

    // ----------------------------------------------------------------------
    // HEDIFFS & COMPONENTS
    // ----------------------------------------------------------------------
    public class Hediff_DivineScaling : HediffWithComps
    {
        public override bool ShouldRemove => false; 
        
        public override string LabelInBrackets => "Slaves: " + (int)this.Severity;
        
        public override string TipStringExtra
        {
            get
            {
                int slaves = (int)this.Severity;
                if (slaves == 0)
                    return "No slaves - no divine power.\n• Bullet Deflection: 20%";
                
                float deflectChance = Mathf.Min(0.20f + (slaves * 0.05f), 0.80f) * 100f;
                
                return string.Format(
                    "Bonuses from {0} slave{1}:\n• Move Speed: +{2}%\n• Melee Damage: +{3}%\n• Research Speed: +{4}%\n• Psychic Sensitivity: +{5}%\n• Bullet Deflection: {6}%",
                    slaves,
                    slaves == 1 ? "" : "s",
                    (slaves * 5).ToString("F0"),
                    (slaves * 10).ToString("F0"),
                    (slaves * 10).ToString("F0"),
                    (slaves * 10).ToString("F0"),
                    deflectChance.ToString("F0")
                );
            }
        }
        
        public override void Tick() 
        { 
            base.Tick(); 
            if (pawn.IsHashIntervalTick(200)) 
            { 
                int slaves = 0; 
                if (pawn.Map != null) 
                    slaves = pawn.Map.mapPawns.SlavesOfColonySpawned.Count; 
                this.Severity = (float)slaves; 
            } 
        }
    }
    public class HediffComp_RageSustain : HediffComp { public override void CompPostTick(ref float severityAdjustment) { if (Pawn.IsHashIntervalTick(60) && (Pawn.Drafted || (Pawn.CurJob != null && Pawn.CurJob.def == JobDefOf.AttackMelee))) { HediffComp_Disappears d = parent.TryGetComp<HediffComp_Disappears>(); if (d != null) d.ticksToDisappear = 5000; } } }
    public class HediffComp_StockholmConversion : HediffComp { public override void CompPostTick(ref float severityAdjustment) { if (Pawn.IsHashIntervalTick(2500) && ModsConfig.IdeologyActive && Pawn.Ideo != null) { Ideo p = Faction.OfPlayer?.ideos?.PrimaryIdeo; if (p != null && Pawn.Ideo != p) { Pawn.ideo.OffsetCertainty(-0.00833f); if (Pawn.ideo.Certainty <= 0.01f) { Pawn.ideo.SetIdeo(p); Pawn.ideo.OffsetCertainty(0.5f); Messages.Message(Pawn.LabelShort + " converted via Stockholm.", Pawn, MessageTypeDefOf.PositiveEvent, true); } } } } }

    public class SRI_GameComponent : GameComponent
    {
        public static SRI_GameComponent Instance;
        private HashSet<int> hybridBedIDs = new HashSet<int>();
        private Dictionary<int, int> punishmentCooldowns = new Dictionary<int, int>();
        private Dictionary<int, int> slaveToMasterMap = new Dictionary<int, int>();
        private Dictionary<int, int> dailyLovinCount = new Dictionary<int, int>();
        private int lastDayReset = 0;
        public SRI_GameComponent(Game game) { Instance = this; } public override void FinalizeInit() { Instance = this; }
        public override void GameComponentTick() { int d = GenLocalDate.DayOfYear(Find.CurrentMap); if (d != lastDayReset) { dailyLovinCount.Clear(); lastDayReset = d; UpdateHeadConcubines(); } if (Find.TickManager.TicksGame % 2500 == 0) { ProcessAutoEvents(); ProcessGodAura(); } }
        private void ProcessGodAura() { Map m = Find.CurrentMap; if (m == null) return; foreach (Pawn g in m.mapPawns.FreeColonistsSpawned.Where(p => p.story?.traits.HasTrait(SRI_Main.Trait_ReincarnatedGod) == true)) { if (!g.health.hediffSet.HasHediff(SRI_Main.Hediff_DivinePower)) g.health.AddHediff(SRI_Main.Hediff_DivinePower); foreach (Pawn s in m.mapPawns.SlavesOfColonySpawned) { if (s.Position.InHorDistOf(g.Position, 9f)) { Need_Suppression ns = s.needs.TryGetNeed<Need_Suppression>(); if (ns != null && ns.CurLevel < 1f) { ns.CurLevel = 1f; FleckMaker.ThrowMetaIcon(s.Position, s.Map, FleckDefOf.IncapIcon); } } } } }
        private void UpdateHeadConcubines() { Dictionary<Pawn, List<Pawn>> h = new Dictionary<Pawn, List<Pawn>>(); foreach(Pawn s in Find.CurrentMap.mapPawns.SlavesOfColonySpawned) { if(slaveToMasterMap.TryGetValue(s.thingIDNumber, out int mid)) { Pawn m = Find.CurrentMap.mapPawns.FreeColonistsSpawned.FirstOrDefault(x=>x.thingIDNumber==mid); if(m!=null) { if(!h.ContainsKey(m)) h[m]=new List<Pawn>(); h[m].Add(s); }}} foreach(var k in h) { Pawn fav=null; float ms=-999f; foreach(Pawn c in k.Value) { Hediff hd=c.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_HeadConcubine); if(hd!=null) c.health.RemoveHediff(hd); float sc=c.relations.OpinionOf(k.Key)+(c.skills.GetSkill(SkillDefOf.Social).Level*5f); if(sc>ms) { ms=sc; fav=c; } } if(fav!=null) fav.health.AddHediff(SRI_Main.Hediff_HeadConcubine); } }
        private void ProcessAutoEvents() { Map m=Find.CurrentMap; if(m==null)return; foreach(Pawn p in m.mapPawns.FreeColonistsSpawned){ if(p.Downed||p.Dead)continue; bool night = GenLocalDate.HourInteger(m)>=22 || GenLocalDate.HourInteger(m)<=5; if(night && p.InBed()) { bool inj = p.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury); int c = GetLovinCount(p); if(inj || (c>=2 && Rand.Chance(0.2f))) { List<Pawn> s = GetAvailableConcubines(p, true); if(s.Count>=2) { s.SortByDescending(x=>x.health.hediffSet.HasHediff(SRI_Main.Hediff_HeadConcubine)); StartHealingTouch(p,s[0],s[1]); continue; } } } if(GetLovinCount(p)<2) { List<Pawn> cs = GetAvailableConcubines(p, false); if(cs.Count>0) { Pawn c = cs.FirstOrDefault(x=>x.health.hediffSet.HasHediff(SRI_Main.Hediff_HeadConcubine)) ?? cs.RandomElement(); if(p.InBed() || (p.CurJob!=null && p.CurJob.def==JobDefOf.Wait_MaintainPosture)) StartAutoLovin(p,c); } } } }
        private List<Pawn> GetAvailableConcubines(Pawn m, bool s) { List<Pawn> r = new List<Pawn>(); foreach(Pawn p in m.Map.mapPawns.SlavesOfColonySpawned) { if(IsConcubineOf(p,m)) { if(p.Downed||p.Dead||p.Drafted) continue; if(p.CurJobDef==SRI_Main.Job_ConcubineLovin || p.CurJobDef==SRI_Main.Job_HealingTouch) continue; if(s && !p.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm)) continue; r.Add(p); } } return r; }
        private void StartAutoLovin(Pawn m, Pawn s) { Job j = JobMaker.MakeJob(SRI_Main.Job_ConcubineLovin, m); s.jobs.TryTakeOrderedJob(j, JobTag.Misc); IncrementLovinCount(m); }
        private void StartHealingTouch(Pawn m, Pawn c1, Pawn c2) { c1.jobs.TryTakeOrderedJob(JobMaker.MakeJob(SRI_Main.Job_HealingTouch, m, c2), JobTag.Misc); c2.jobs.TryTakeOrderedJob(JobMaker.MakeJob(SRI_Main.Job_HealingTouch, m, c1), JobTag.Misc); Messages.Message("Healing Touch!", m, MessageTypeDefOf.PositiveEvent, true); }
        
        public void SetConcubine(Pawn s, Pawn m) { if (slaveToMasterMap.ContainsKey(s.thingIDNumber)) slaveToMasterMap[s.thingIDNumber] = m != null ? m.thingIDNumber : -1; else slaveToMasterMap.Add(s.thingIDNumber, m != null ? m.thingIDNumber : -1); UpdateHeadConcubines(); }
        public bool IsConcubineOf(Pawn s, Pawn m) { if (slaveToMasterMap.TryGetValue(s.thingIDNumber, out int mid)) return mid == m.thingIDNumber; return false; }
        public int GetLovinCount(Pawn m) { return dailyLovinCount.TryGetValue(m.thingIDNumber, out int c) ? c : 0; }
        public void IncrementLovinCount(Pawn m) { if (dailyLovinCount.ContainsKey(m.thingIDNumber)) dailyLovinCount[m.thingIDNumber]++; else dailyLovinCount.Add(m.thingIDNumber, 1); }
        public bool IsHybrid(Building_Bed b) { return b != null && hybridBedIDs.Contains(b.thingIDNumber); }
        public void SetHybrid(Building_Bed b, bool s) { if (b == null) return; if (s) { hybridBedIDs.Add(b.thingIDNumber); Traverse.Create(b).Field("forOwnerType").SetValue(BedOwnerType.Colonist); } else { hybridBedIDs.Remove(b.thingIDNumber); } }
        public void SetPunishCooldown(Pawn p, int t) { if (punishmentCooldowns.ContainsKey(p.thingIDNumber)) punishmentCooldowns[p.thingIDNumber] = t; else punishmentCooldowns.Add(p.thingIDNumber, t); }
        public int GetPunishUnlockTick(Pawn p) { return punishmentCooldowns.TryGetValue(p.thingIDNumber, out int val) ? val : 0; }
        public override void ExposeData() { base.ExposeData(); List<int> t1 = hybridBedIDs.ToList(); Scribe_Collections.Look(ref t1, "SRI_Hybrid", LookMode.Value); if (Scribe.mode == LoadSaveMode.PostLoadInit && t1 != null) hybridBedIDs = new HashSet<int>(t1); Scribe_Collections.Look(ref punishmentCooldowns, "SRI_Cool", LookMode.Value, LookMode.Value); if (punishmentCooldowns == null) punishmentCooldowns = new Dictionary<int, int>(); Scribe_Collections.Look(ref slaveToMasterMap, "SRI_Map", LookMode.Value, LookMode.Value); if (slaveToMasterMap == null) slaveToMasterMap = new Dictionary<int, int>(); Scribe_Collections.Look(ref dailyLovinCount, "SRI_Daily", LookMode.Value, LookMode.Value); if (dailyLovinCount == null) dailyLovinCount = new Dictionary<int, int>(); }
    }

    // ----------------------------------------------------------------------
    // JOB DRIVERS (Full)
    // ----------------------------------------------------------------------
    public class JobDriver_SlaveLovin : JobDriver 
    {
        private Pawn Partner => (Pawn)TargetB.Thing; 
        public override bool TryMakePreToilReservations(bool errorOnFailed) { return true; }
        protected override IEnumerable<Toil> MakeNewToils() 
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil sync = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            sync.tickAction = delegate { pawn.rotationTracker.FaceTarget(Partner); if (pawn.Position.InHorDistOf(Partner.Position, 2.0f) && Partner.CurJobDef == SRI_Main.Job_SlaveLovin) ReadyForNextToil(); if (pawn.IsHashIntervalTick(60) && (Partner.Dead || Partner.Downed || (!Partner.CurJobDef.Equals(SRI_Main.Job_SlaveLovin) && !Partner.Drafted && Partner.CurJobDef != JobDefOf.Goto))) EndJobWith(JobCondition.Incompletable); };
            yield return sync;
            Toil lovin = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 500 };
            lovin.initAction = delegate { FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            lovin.tickAction = delegate { pawn.rotationTracker.FaceTarget(Partner); if (pawn.IsHashIntervalTick(100)) { if (Rand.Chance(0.6f)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); else MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0,0,1), pawn.Map, "❤", Color.magenta); } };
            lovin.AddFinishAction(delegate { 
                pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, Partner); 
                if (pawn.IsSlaveOfColony && !pawn.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm) && Rand.Chance(0.20f)) 
                { 
                    pawn.health.AddHediff(SRI_Main.Hediff_Stockholm); 
                    Messages.Message(pawn.LabelShort + " has developed Stockholm Syndrome!", pawn, MessageTypeDefOf.PositiveEvent, true); 
                } 
            });
            yield return lovin;
        }
    }

    public class JobDriver_ConcubineLovin : JobDriver 
    {
        private Pawn Master => (Pawn)TargetA.Thing; 
        public override bool TryMakePreToilReservations(bool errorOnFailed) { return true; }
        protected override IEnumerable<Toil> MakeNewToils() 
        {
            this.FailOnDespawnedOrNull(TargetIndex.A); yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil lovin = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            lovin.initAction = delegate { FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            lovin.tickAction = delegate { pawn.rotationTracker.FaceTarget(Master); if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            lovin.AddFinishAction(delegate {
                if (pawn.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm)) { pawn.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_SatisfiedByMaster, Master); return; }
                float score = 1.0f; TraitDef b = TraitDef.Named("Beauty"); if (b!=null && Master.story.traits.HasTrait(b)) score += Master.story.traits.DegreeOfTrait(b)*0.5f;
                if (Master.skills.GetSkill(SkillDefOf.Social).Level > 5) score += (Master.skills.GetSkill(SkillDefOf.Social).Level-5)*0.1f;
                if (pawn.relations.OpinionOf(Master) < -20) score -= 2.0f;
                if (score >= 1.0f) pawn.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_SatisfiedByMaster, Master); else pawn.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_ForcedAffection, Master);
            }); yield return lovin;
        }
    }

    public class JobDriver_HealingTouch : JobDriver 
    {
        private Pawn Master => (Pawn)TargetA.Thing; private Pawn Partner => (Pawn)TargetB.Thing; 
        public override bool TryMakePreToilReservations(bool errorOnFailed) { return true; }
        protected override IEnumerable<Toil> MakeNewToils() 
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil sync = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            sync.tickAction = delegate { pawn.rotationTracker.FaceTarget(Master); if (Partner.Position.InHorDistOf(pawn.Position, 5f) && Partner.CurJobDef == SRI_Main.Job_HealingTouch) ReadyForNextToil(); if (pawn.IsHashIntervalTick(100) && (Partner.Dead || Partner.Downed || Partner.CurJobDef != SRI_Main.Job_HealingTouch)) EndJobWith(JobCondition.Incompletable); };
            yield return sync;
            Toil r = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 2500 };
            r.tickAction = delegate { pawn.rotationTracker.FaceTarget(Master); if (pawn.IsHashIntervalTick(200)) { MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Healing...", Color.green); FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); } };
            r.AddFinishAction(delegate { if (!Master.health.hediffSet.HasHediff(SRI_Main.Hediff_HealingTouch)) { Master.health.AddHediff(SRI_Main.Hediff_HealingTouch); Messages.Message(Master.LabelShort + " received Healing Touch!", Master, MessageTypeDefOf.PositiveEvent, true); } });
            yield return r;
        }
    }

    public class JobDriver_PunishSlave : JobDriver 
    {
        private Pawn Victim => (Pawn)TargetA.Thing; 
        public override bool TryMakePreToilReservations(bool errorOnFailed) { return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed); }
        protected override IEnumerable<Toil> MakeNewToils() 
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil b = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 300 };
            b.initAction = delegate { Messages.Message(pawn.LabelShort + " is punishing " + Victim.LabelShort, pawn, MessageTypeDefOf.NeutralEvent, false); };
            b.tickAction = delegate { pawn.rotationTracker.FaceTarget(Victim); if (pawn.IsHashIntervalTick(45)) { pawn.Drawer.Notify_MeleeAttackOn(Victim); SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(Victim.Position, Victim.Map)); FleckMaker.ThrowMicroSparks(Victim.DrawPos, Victim.Map); Victim.Drawer.Notify_DamageApplied(new DamageInfo(DamageDefOf.Blunt, 1)); } };
            b.AddFinishAction(delegate {
                if (SRI_GameComponent.Instance != null) SRI_GameComponent.Instance.SetPunishCooldown(Victim, Find.TickManager.TicksGame + 120000);
                Victim.health.AddHediff(SRI_Main.Hediff_PunishedPain);
                if (Victim.guest != null && Victim.IsPrisoner && Victim.guest.will > 0) { Victim.guest.will = Mathf.Max(0, Victim.guest.will - 5.0f); Messages.Message(Victim.LabelShort + " will broken (-5)", Victim, MessageTypeDefOf.PositiveEvent, true); }
                if (Victim.IsSlaveOfColony) { Need_Suppression sup = Victim.needs.TryGetNeed<Need_Suppression>(); if (sup != null) sup.CurLevel = 1.0f; }
                foreach (Pawn obs in Victim.Map.mapPawns.AllPawnsSpawned.Where(p => p != Victim && p != pawn && p.Position.InHorDistOf(Victim.Position, 10f) && (p.IsSlaveOfColony || p.IsPrisoner))) {
                    if (obs.IsPrisoner && obs.guest != null) obs.guest.will = Mathf.Max(0, obs.guest.will - 0.5f);
                    else if (obs.IsSlaveOfColony) { Need_Suppression obsSup = obs.needs.TryGetNeed<Need_Suppression>(); if (obsSup != null) obsSup.CurLevel += 0.3f; }
                    MoteMaker.ThrowText(obs.DrawPos, obs.Map, "Fear!", Color.red);
                }
            });
            yield return b;
        }
    }

    public class JobDriver_Procure : JobDriver 
    {
        private Pawn Victim => (Pawn)TargetA.Thing; 
        public override bool TryMakePreToilReservations(bool errorOnFailed) { return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed); }
        protected override IEnumerable<Toil> MakeNewToils() 
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil l = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            l.initAction = delegate { Messages.Message(pawn.LabelShort + " procuring " + Victim.LabelShort, pawn, MessageTypeDefOf.NeutralEvent, true); };
            l.tickAction = delegate { pawn.rotationTracker.FaceTarget(Victim); if (pawn.IsHashIntervalTick(100)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            l.AddFinishAction(delegate {
                if (Rand.Chance(Mathf.Clamp01(0.10f + (pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f)))) {
                    if (Victim.guest != null) {
                        Building_Bed bed = null; foreach (var b in Victim.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()) if (b.ForPrisoners && !b.Medical && !b.Destroyed && !b.IsBurning() && b.AnyUnownedSleepingSlot && b.CurOccupants.EnumerableCount() < b.SleepingSlotsCount) { bed = b; break; }
                        if (bed != null) {
                            Victim.SetFaction(Faction.OfPlayer); Victim.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner); Victim.ownership.ClaimBedIfNonMedical(bed);
                            Job gotoBed = JobMaker.MakeJob(JobDefOf.Goto, bed); Job sleep = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                            Victim.jobs.StartJob(gotoBed, JobCondition.InterruptForced); Victim.jobs.jobQueue.EnqueueFirst(sleep);
                            Messages.Message("Procured!", Victim, MessageTypeDefOf.PositiveEvent, true); MoteMaker.ThrowText(Victim.DrawPos, Victim.Map, "Procured!", Color.green);
                        } else Messages.Message("No Bed!", MessageTypeDefOf.CautionInput, false);
                    }
                } else { Messages.Message("Refused.", Victim, MessageTypeDefOf.NegativeEvent, true); MoteMaker.ThrowText(Victim.DrawPos, Victim.Map, "Refused", Color.red); }
            });
            yield return l;
        }
    }

    // ----------------------------------------------------------------------
    // UI & PATCHES
    // ----------------------------------------------------------------------
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_Gizmos 
    { 
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance) 
        { 
            foreach (var g in __result) yield return g; 
            if (__instance.IsSlaveOfColony && __instance.RaceProps.Humanlike) 
            { 
                yield return new Command_Action { 
                    defaultLabel = "Assign Concubine", icon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/TendedNeed", true), 
                    action = delegate { 
                        List<FloatMenuOption> o = new List<FloatMenuOption> { new FloatMenuOption("Unassign", delegate { SRI_GameComponent.Instance.SetConcubine(__instance, null); }) }; 
                        foreach (Pawn c in __instance.Map.mapPawns.FreeColonists) o.Add(new FloatMenuOption(c.LabelShort + (SRI_GameComponent.Instance.IsConcubineOf(__instance, c) ? " (Current)" : ""), delegate { SRI_GameComponent.Instance.SetConcubine(__instance, c); Messages.Message("Assigned.", __instance, MessageTypeDefOf.TaskCompletion, false); })); 
                        Find.WindowStack.Add(new FloatMenu(o)); 
                    } 
                }; 
            } 
        } 
    }

    // FIX: Removed [HarmonyPatch] attributes with specific types to avoid confusion.
    // We use TargetMethod() to find the method manually via Reflection.
    // --- FIX: "Nuclear" Search (Matches by Name, ignores Version Mismatch) ---
    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    public static class Patch_FloatMenuMakerMap 
    { 
        public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts) 
        { 
            if (!pawn.IsColonistPlayerControlled) return; 

            foreach (LocalTargetInfo t in GenUI.TargetsAt(clickPos, TargetingParameters.ForPawns(), true)) 
            { 
                Pawn v = t.Pawn; 
                if (v != null && v != pawn) 
                {
                    if (v.IsSlaveOfColony) 
                    {
                        opts.Add(new FloatMenuOption("Take " + v.LabelShort + " to bed (Lovin')", delegate { StartLovinScene(pawn, v); }, MenuOptionPriority.Default, null, null, 0f, null, null));
                    }
                    
                    if (v.IsSlaveOfColony || v.IsPrisonerOfColony) 
                    { 
                        int l = (SRI_GameComponent.Instance != null ? SRI_GameComponent.Instance.GetPunishUnlockTick(v) : 0) - Find.TickManager.TicksGame; 
                        if (l > 0) 
                        { 
                            FloatMenuOption d = new FloatMenuOption("Punish (Cool " + (l/2500) + "h)", null); 
                            d.Disabled = true; 
                            opts.Add(d); 
                        } 
                        else 
                        {
                            opts.Add(new FloatMenuOption("Punish " + v.LabelShort, delegate { StartPunishment(pawn, v); }, MenuOptionPriority.Default, null, null, 0f, null, null)); 
                        }
                    }
                    
                    if (!v.IsPrisoner && !v.IsSlave && !v.IsColonist && v.RaceProps.Humanlike && !v.HostileTo(pawn)) 
                    { 
                        bool hb = false; 
                        foreach(var b in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()) 
                            if(b.ForPrisoners && !b.Medical && b.AnyUnownedSleepingSlot) { hb=true; break; } 
                        
                        if(!hb) 
                        { 
                            FloatMenuOption d = new FloatMenuOption("Procure (No Bed)", null); 
                            d.Disabled=true; 
                            opts.Add(d); 
                        } 
                        else 
                        { 
                            string ch = (Mathf.Clamp01(0.10f + (pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f)) * 100).ToString("F0") + "%"; 
                            opts.Add(new FloatMenuOption("Procure " + v.LabelShort + " (" + ch + ")", delegate { StartProcure(pawn, v); }, MenuOptionPriority.Default, null, null, 0f, null, null));
                        } 
                    }
                }
            }
        }
        
        static void StartLovinScene(Pawn m, Pawn s) { DoJob(m, s, SRI_Main.Job_SlaveLovin, true); }
        static void StartPunishment(Pawn m, Pawn v) { DoJob(m, v, SRI_Main.Job_PunishSlave, false); }
        static void StartProcure(Pawn m, Pawn v) { DoJob(m, v, SRI_Main.Job_Procure, false); }
        
        static void DoJob(Pawn m, Pawn t, JobDef def, bool both) 
        { 
            if (m.Drafted) m.drafter.Drafted = false; m.jobs.EndCurrentJob(JobCondition.InterruptForced, true); 
            if (t.Drafted) t.drafter.Drafted = false; t.jobs.EndCurrentJob(JobCondition.InterruptForced, true); 
            
            if (def == SRI_Main.Job_SlaveLovin) 
            { 
                Building_Bed bed = (Building_Bed)GenClosest.ClosestThing_Global(m.Position, m.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>(), 9999f, (Thing x) => ((Building_Bed)x).SleepingSlotsCount >= 2 && !((Building_Bed)x).IsBurning()); 
                if (bed == null) { Messages.Message("No Bed!", MessageTypeDefOf.RejectInput, false); return; } 
                
                Job jm = JobMaker.MakeJob(def, bed, t); jm.playerForced = true; jm.expiryInterval = -1; m.jobs.TryTakeOrderedJob(jm, JobTag.Misc); 
                Job jt = JobMaker.MakeJob(def, bed, m); jt.playerForced = true; jt.expiryInterval = -1; t.jobs.TryTakeOrderedJob(jt, JobTag.Misc); 
            } 
            else 
            { 
                Job j = JobMaker.MakeJob(def, t); j.playerForced = true; m.jobs.TryTakeOrderedJob(j, JobTag.Misc); 
            } 
        } 
    }

    [HarmonyPatch(typeof(Building_Bed), "GetGizmos")] 
    public static class Patch_Bed_GetGizmos 
    { 
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance) 
        { 
            foreach (var g in __result) yield return g; 
            if (__instance.Faction == Faction.OfPlayer && __instance.SleepingSlotsCount >= 2) 
            { 
                yield return new Command_Toggle { 
                    defaultLabel = "Bed Mode: Master & Slave", icon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/TendedNeed", true), 
                    isActive = () => SRI_GameComponent.Instance != null && SRI_GameComponent.Instance.IsHybrid(__instance), 
                    toggleAction = delegate { 
                        if (SRI_GameComponent.Instance != null) 
                        { 
                            SRI_GameComponent.Instance.SetHybrid(__instance, !SRI_GameComponent.Instance.IsHybrid(__instance)); 
                            foreach (Pawn p in __instance.CompAssignableToPawn.AssignedPawns.ToList()) __instance.CompAssignableToPawn.TryUnassignPawn(p); 
                        } 
                    } 
                }; 
            } 
        } 
    }

    [HarmonyPatch(typeof(Pawn), "PreApplyDamage")] 
    public static class Patch_InstantRage 
    { 
        public static void Prefix(Pawn __instance, ref DamageInfo dinfo, out bool __state) 
        { 
            __state = false; 
            if (__instance.IsColonist && !__instance.Dead && dinfo.Instigator != null && dinfo.Instigator != __instance) __state = true; 
        } 
        
        public static void Postfix(Pawn __instance, DamageInfo dinfo, bool __state) 
        { 
            if (!__state) return; 
            
            bool isEnemy = dinfo.Instigator.HostileTo(__instance) || (dinfo.Instigator is Pawn att && (att.IsPrisoner || att.InAggroMentalState)); 
            if (!isEnemy && dinfo.Instigator is Pawn animal && animal.RaceProps.Animal) isEnemy = true; 
            
            if (isEnemy) 
            { 
                // Only trigger rage for slaves assigned to THIS master
                foreach (Pawn s in __instance.MapHeld.mapPawns.SlavesOfColonySpawned.ToList()) 
                { 
                    // Check if this slave is a concubine of the attacked master
                    if (SRI_GameComponent.Instance != null && !SRI_GameComponent.Instance.IsConcubineOf(s, __instance))
                        continue;
                    
                    // Must have Stockholm Syndrome to protect master
                    if (!s.Dead && !s.Downed && s.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm)) 
                    { 
                        if (s.CurJobDef == JobDefOf.AttackMelee && s.CurJob.targetA.Thing == dinfo.Instigator) continue; 
                        
                        Hediff rage = s.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_ProtectiveRage); 
                        if (rage != null) s.health.RemoveHediff(rage); 
                        s.health.AddHediff(SRI_Main.Hediff_ProtectiveRage); 
                        
                        if (rage == null) Messages.Message(s.LabelShort + " protects " + __instance.LabelShort + "!", s, MessageTypeDefOf.ThreatBig, true); 
                        
                        if (s.Drafted) s.drafter.Drafted = false; 
                        s.jobs.EndCurrentJob(JobCondition.InterruptForced, true); s.jobs.ClearQueuedJobs(); 
                        
                        Job atk = JobMaker.MakeJob(JobDefOf.AttackMelee, dinfo.Instigator); 
                        atk.playerForced = true; atk.expiryInterval = 2000; atk.killIncappedTarget = true; 
                        s.jobs.TryTakeOrderedJob(atk, JobTag.Misc); 
                    } 
                } 
            } 
        } 
    }

    [HarmonyPatch(typeof(Need_Suppression), "NeedInterval")] 
    public static class Patch_Suppression_Stockholm 
    { 
        public static bool Prefix(Need_Suppression __instance, Pawn ___pawn) 
        { 
            if (___pawn.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm)) 
            { 
                __instance.CurLevel = __instance.MaxLevel; return false; 
            } 
            return true; 
        } 
    }

    [HarmonyPatch] 
    public static class Patch_IsValidBedFor_Mechanic 
    { 
        static MethodBase TargetMethod() { return AccessTools.GetDeclaredMethods(typeof(RestUtility)).Where(m => m.Name == "IsValidBedFor").OrderByDescending(m => m.GetParameters().Length).First(); } 
        public static bool Prefix(Thing bedThing, Pawn sleeper, ref bool __result) 
        { 
            if (bedThing is Building_Bed bed && SRI_GameComponent.Instance != null && SRI_GameComponent.Instance.IsHybrid(bed)) 
            { 
                if ((sleeper.IsSlaveOfColony || sleeper.IsColonist) && !bed.Destroyed && !bed.IsBurning()) { __result = true; return false; } 
            } 
            return true; 
        } 
    }

    [HarmonyPatch(typeof(ForbidUtility), "IsForbidden", new Type[] { typeof(Thing), typeof(Pawn) })] 
    public static class Patch_IsForbidden 
    { 
        public static bool Prefix(Thing t, Pawn pawn, ref bool __result) 
        { 
            if (pawn.IsSlaveOfColony && t is Building_Bed bed && SRI_GameComponent.Instance != null && SRI_GameComponent.Instance.IsHybrid(bed)) 
            { 
                __result = false; return false; 
            } 
            return true; 
        } 
    }

    [HarmonyPatch(typeof(CompAssignableToPawn), "CanAssignTo")] 
    public static class Patch_CanAssignTo 
    { 
        public static void Postfix(CompAssignableToPawn __instance, Pawn pawn, ref AcceptanceReport __result) 
        { 
            if (!__result.Accepted && __instance.parent is Building_Bed bed && SRI_GameComponent.Instance != null && SRI_GameComponent.Instance.IsHybrid(bed)) 
            { 
                if (pawn.IsSlaveOfColony || pawn.IsColonist) __result = AcceptanceReport.WasAccepted; 
            } 
        } 
    }

    [HarmonyPatch(typeof(BedUtility), "WillingToShareBed")] 
    public static class Patch_WillingToShareBed 
    { 
        public static void Postfix(Pawn pawn1, Pawn pawn2, ref bool __result) 
        { 
            if (!__result && ((pawn1.IsSlaveOfColony && pawn2.IsColonist) || (pawn2.IsSlaveOfColony && pawn1.IsColonist))) 
            { 
                Building_Bed bed1 = pawn1.ownership.OwnedBed; Building_Bed bed2 = pawn2.ownership.OwnedBed; 
                if (bed1 != null && bed2 != null && bed1 == bed2 && SRI_GameComponent.Instance != null && SRI_GameComponent.Instance.IsHybrid(bed1)) { __result = true; } 
            } 
        } 
    }

    [HarmonyPatch(typeof(LovePartnerRelationUtility), "LovePartnerRelationExists")] 
    public static class Patch_FakeLoveRelation 
    { 
        public static void Postfix(Pawn first, Pawn second, ref bool __result) 
        { 
            if (!__result && ((first.IsSlaveOfColony && second.IsColonist) || (second.IsSlaveOfColony && first.IsColonist))) 
            { 
                Building_Bed bed1 = first.ownership.OwnedBed; Building_Bed bed2 = second.ownership.OwnedBed; 
                if (bed1 != null && bed2 != null && bed1 == bed2 && SRI_GameComponent.Instance != null && SRI_GameComponent.Instance.IsHybrid(bed1)) { __result = true; } 
            } 
        } 
    }

    [HarmonyPatch]
    public static class Patch_GodWeapons
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EquipmentUtility), "CanEquip", new Type[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) });
        }

        public static bool Prefix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (pawn.story != null && pawn.story.traits.HasTrait(SRI_Main.Trait_ReincarnatedGod))
            {
                if (thing.def.IsRangedWeapon) { cantReason = "God refuses guns."; __result = false; return false; }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Projectile_Impact
    {
        public static bool Prefix(Projectile __instance, Thing hitThing)
        {
            if (hitThing is Pawn pawn && pawn.story != null && pawn.story.traits.HasTrait(SRI_Main.Trait_ReincarnatedGod))
            {
                int slaveCount = 0;
                if (pawn.Map != null) 
                {
                    slaveCount = pawn.Map.mapPawns.SlavesOfColonySpawned.Count;
                }
                float chance = 0.20f + (slaveCount * 0.05f);
                if (chance > 0.80f) chance = 0.80f;

                if (Rand.Chance(chance))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "REFLECT!", Color.yellow);
                    
                    // FIX: Wrapped TargetInfo in SoundInfo.InMap() so it plays correctly
                    SoundDefOf.MetalHitImportant.PlayOneShot(SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map)));
                    
                    Thing launcher = __instance.Launcher;
                    if (launcher != null && launcher is Pawn shooter && !shooter.Dead)
                    {
                        // FIX: GetDamageAmount requires a weapon argument. Passing 'null' gets the base damage.
                        // Also cast result to float to fix CS1503 error.
                        float dmg = (float)__instance.def.projectile.GetDamageAmount(null);

                        DamageInfo dinfo = new DamageInfo(__instance.def.projectile.damageDef, dmg, 0f, -1f, pawn, null, null);
                        shooter.TakeDamage(dinfo);
                        MoteMaker.ThrowText(shooter.DrawPos, shooter.Map, "Karma!", Color.red);
                    }
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(StatExtension), "GetStatValue")]
    public static class Patch_DivineStats
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            if (thing is Pawn pawn)
            {
                Hediff divine = pawn.health?.hediffSet?.GetFirstHediffOfDef(SRI_Main.Hediff_DivinePower);
                if (divine != null)
                {
                    int count = (int)divine.Severity;
                    if (count > 0)
                    {
                        if (stat == StatDefOf.MoveSpeed) __result *= (1f + (count * 0.05f)); 
                        else if (stat == StatDefOf.MeleeDamageFactor) __result += (count * 0.10f); 
                        else if (stat == StatDefOf.ResearchSpeed) __result += (count * 0.10f);
                        else if (stat == StatDefOf.PsychicSensitivity) __result += (count * 0.10f);
                    }
                }
            }
        }
    }
}