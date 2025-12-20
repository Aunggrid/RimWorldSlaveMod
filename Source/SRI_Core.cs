using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
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
        public bool enableFaithSystem = true, enableNotoriety = true, enableOneGodRule = true;
        public float devotionRate = 1f, cooldownMult = 1f, powerMult = 1f;
        public float faithGenerationMult = 1f, notorietyGainMult = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableAbilities, "abilities", true);
            Scribe_Values.Look(ref enableResurrection, "resurrection", true);
            Scribe_Values.Look(ref enableSpecializations, "specs", true);
            Scribe_Values.Look(ref unlockSlaveWork, "unlockWork", true);
            Scribe_Values.Look(ref showNotifications, "notifs", true);
            Scribe_Values.Look(ref showOverlay, "overlay", true);
            Scribe_Values.Look(ref showGodGlow, "glow", true);
            Scribe_Values.Look(ref enableFaithSystem, "faithSystem", true);
            Scribe_Values.Look(ref enableNotoriety, "notoriety", true);
            Scribe_Values.Look(ref enableOneGodRule, "oneGodRule", true);
            Scribe_Values.Look(ref devotionRate, "devRate", 1f);
            Scribe_Values.Look(ref cooldownMult, "cdMult", 1f);
            Scribe_Values.Look(ref powerMult, "pwrMult", 1f);
            Scribe_Values.Look(ref faithGenerationMult, "faithMult", 1f);
            Scribe_Values.Look(ref notorietyGainMult, "notorMult", 1f);
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
            L.Label("═══ Core Features ═══");
            L.CheckboxLabeled("Divine Abilities", ref S.enableAbilities);
            L.CheckboxLabeled("Resurrection System", ref S.enableResurrection);
            L.CheckboxLabeled("Slave Specializations", ref S.enableSpecializations);
            L.CheckboxLabeled("Unlock Slave Work", ref S.unlockSlaveWork);
            L.CheckboxLabeled("Tier-Up Notifications", ref S.showNotifications);
            L.GapLine();
            L.Label("═══ New Systems ═══");
            L.CheckboxLabeled("Faith & Edicts System", ref S.enableFaithSystem);
            L.CheckboxLabeled("Notoriety & Hunter Raids", ref S.enableNotoriety);
            L.CheckboxLabeled("One God Rule (Recruits→Slaves)", ref S.enableOneGodRule);
            L.GapLine();
            L.Label("═══ Balance ═══");
            L.Label($"Devotion Growth: {S.devotionRate:P0}"); S.devotionRate = L.Slider(S.devotionRate, 0.1f, 3f);
            L.Label($"Cooldown Multiplier: {S.cooldownMult:P0}"); S.cooldownMult = L.Slider(S.cooldownMult, 0.25f, 3f);
            L.Label($"Ability Power: {S.powerMult:P0}"); S.powerMult = L.Slider(S.powerMult, 0.5f, 2f);
            L.Label($"Faith Generation: {S.faithGenerationMult:P0}"); S.faithGenerationMult = L.Slider(S.faithGenerationMult, 0.25f, 3f);
            L.Label($"Notoriety Gain: {S.notorietyGainMult:P0}"); S.notorietyGainMult = L.Slider(S.notorietyGainMult, 0.25f, 3f);
            L.GapLine();
            L.CheckboxLabeled("Devotion Overlay", ref S.showOverlay);
            L.CheckboxLabeled("God Glow Effect", ref S.showGodGlow);
            L.End();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //                              EDICT DEFINITION
    // ══════════════════════════════════════════════════════════════════════════════

    public class EdictDef
    {
        public string defName, label, description;
        public int faithCost, durationDays;
        public HediffDef appliedHediff;
        public Color iconColor = Color.white;
        public static List<EdictDef> AllEdicts = new();

        public static void Initialize()
        {
            AllEdicts = new List<EdictDef>
            {
                new EdictDef { defName = "SRI_Edict_Harvest", label = "Bountiful Harvest",
                    description = "+40% plant work speed for 5 days.", faithCost = 50, durationDays = 5,
                    appliedHediff = Defs.H_EdictHarvest, iconColor = new Color(0.4f, 0.9f, 0.3f) },
                new EdictDef { defName = "SRI_Edict_Labor", label = "Fervent Labor",
                    description = "+25% work speed for 5 days.", faithCost = 75, durationDays = 5,
                    appliedHediff = Defs.H_EdictLabor, iconColor = new Color(0.9f, 0.7f, 0.2f) },
                new EdictDef { defName = "SRI_Edict_Fortify", label = "Divine Fortification",
                    description = "+20% armor for 5 days.", faithCost = 100, durationDays = 5,
                    appliedHediff = Defs.H_EdictFortify, iconColor = new Color(0.5f, 0.5f, 0.9f) },
                new EdictDef { defName = "SRI_Edict_HolyWar", label = "Holy War",
                    description = "+35% melee damage, +15% speed for 3 days.", faithCost = 150, durationDays = 3,
                    appliedHediff = Defs.H_EdictHolyWar, iconColor = new Color(0.9f, 0.2f, 0.2f) }
            };
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
        public static JobDef Job_ChallengerRoar, Job_SoothingTouch, Job_MomentInsight, Job_CaptivatingPerformance;
        
        // Hediffs
        public static HediffDef H_Stockholm, H_Rage, H_Punished, H_Healing, H_HeadConcubine, H_Divine, H_Devotion, H_GodShielded;
        public static HediffDef H_Shield, H_Blessing, H_ResSick, H_Exhaust;
        public static HediffDef H_Warrior, H_Healer, H_Scholar, H_Entertainer;
        public static HediffDef H_EdictHarvest, H_EdictLabor, H_EdictFortify, H_EdictHolyWar;
        public static HediffDef H_Taunted, H_Captivated;
        
        // Trait & Thoughts
        public static TraitDef Trait_God;
        public static ThoughtDef T_Satisfied, T_Forced, T_Wrath, T_Blessed, T_Resurrected, T_ServedMaster, T_ReceivedService, T_ComfortLovin;
        public static ThoughtDef T_EdictBlessing, T_HuntersFear;
        
        // Constants
        public const float T1 = 0.15f, T2 = 0.35f, T3 = 0.60f, T4 = 0.90f;
        public const int CD_Smite = 2500, CD_Calm = 10000, CD_Bless = 20000, CD_Wrath = 60000;
        public const int CD_Roar = 15000, CD_Soothe = 30000, CD_Insight = 45000, CD_Captivate = 20000;

        static Defs()
        {
            Log.Message("[SRI] Initializing Enhanced Version...");
            
            // Jobs
            Job_Lovin = MkJob("SRI_Lovin", typeof(JD_Lovin), "making love.");
            Job_Punish = MkJob("SRI_Punish", typeof(JD_Punish), "punishing.", true);
            Job_Procure = MkJob("SRI_Procure", typeof(JD_Procure), "procuring.");
            Job_Attend = MkJob("SRI_Attend", typeof(JD_Attend), "attending.");
            Job_Heal = MkJob("SRI_Heal", typeof(JD_Heal), "healing ritual.");
            Job_Smite = MkJob("SRI_Smite", typeof(JD_Smite), "divine wrath.");
            Job_Resurrect = MkJob("SRI_Resurrect", typeof(JD_Resurrect), "resurrection.");
            Job_GatherCorpse = MkJob("SRI_Gather", typeof(JD_Gather), "gathering.");
            Job_ChallengerRoar = MkJob("SRI_ChallengerRoar", typeof(JD_ChallengerRoar), "challenging enemies.");
            Job_SoothingTouch = MkJob("SRI_SoothingTouch", typeof(JD_SoothingTouch), "healing touch.");
            Job_MomentInsight = MkJob("SRI_MomentInsight", typeof(JD_MomentInsight), "moment of insight.");
            Job_CaptivatingPerformance = MkJob("SRI_CaptivatingPerformance", typeof(JD_CaptivatingPerformance), "performing.");

            // Core Hediffs
            H_Stockholm = MkHediff("SRI_Stockholm", "Stockholm Syndrome", "Deep psychological bond with master.\n+20% Learning\nFull suppression\nEnables protective rage", 
                typeof(HediffWithComps), Color.cyan, MkStage(statOff: new() { (StatDefOf.GlobalLearningFactor, 0.2f) }), new() { new HediffCompProperties { compClass = typeof(HC_Stockholm) } });
            
            H_Rage = MkHediff("SRI_Rage", "Protective Rage", "Berserker state protecting master.\n+4 Speed, +30% Hit/Dodge, 3x Attack Speed, +150% Damage",
                typeof(HediffWithComps), Color.red, new HediffStage { painFactor = 0.5f,
                    statOffsets = new() { new() { stat = StatDefOf.MoveSpeed, value = 4f }, new() { stat = StatDefOf.MeleeHitChance, value = 0.3f }, 
                        new() { stat = StatDefOf.MeleeDodgeChance, value = 0.3f }, new() { stat = StatDefOf.MeleeDamageFactor, value = 1.5f } },
                    statFactors = new() { new() { stat = StatDefOf.MeleeWeapon_CooldownMultiplier, value = 0.33f } } },
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(2500, 5000), showRemainingTime = true } });
            
            H_Punished = MkHediff("SRI_Punished", "Punished", "Recently punished. +35% Pain", typeof(HediffWithComps), new Color(0.8f, 0.4f, 0), true,
                MkStage(pain: 0.35f), new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(10000, 20000), showRemainingTime = true } });
            
            H_Healing = MkHediff("SRI_Healing", "Healing Touch", "+50% Immunity & Healing", typeof(HediffWithComps), Color.green,
                MkStage(statOff: new() { (StatDefOf.ImmunityGainSpeed, 0.5f), (StatDefOf.InjuryHealingFactor, 0.5f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(15000, 30000), showRemainingTime = true } });
            
            H_HeadConcubine = MkHediff("SRI_HeadConc", "Head Concubine", "+20% Social Impact", typeof(HediffWithComps), new Color(1, 0.8f, 0),
                MkStage(statOff: new() { (StatDefOf.SocialImpact, 0.2f) }));
            
            H_Divine = MkHediff("SRI_Divine", "Divine Power", "Power from slaves. Scales with slave count.", typeof(Hediff_Divine), new Color(1, 0.9f, 0.2f));
            H_Devotion = MkHediff("SRI_Devotion", "Devotion", "Spiritual connection to God. Grows over time.", typeof(Hediff_Devotion), new Color(0.9f, 0.7f, 1));
            H_Shield = MkHediff("SRI_Shield", "Divine Shield", "Can intercept lethal damage to master.", typeof(Hediff_Shield), new Color(0.5f, 0.8f, 1));
            
            H_Blessing = MkHediff("SRI_Blessing", "Divine Blessing", "+15% Work, +0.5 Speed, +20% Melee", typeof(HediffWithComps), new Color(1, 1, 0.6f),
                MkStage(statOff: new() { (StatDefOf.WorkSpeedGlobal, 0.15f), (StatDefOf.MoveSpeed, 0.5f), (StatDefOf.MeleeDamageFactor, 0.2f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(30000, 60000), showRemainingTime = true } });
            
            H_ResSick = MkHediff("SRI_ResSick", "Resurrection Sickness", "50% Speed & Damage", typeof(HediffWithComps), Color.gray, true,
                MkStage(statFac: new() { (StatDefOf.MoveSpeed, 0.5f), (StatDefOf.MeleeDamageFactor, 0.5f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(60000, 90000), showRemainingTime = true } });
            
            H_Exhaust = MkHediff("SRI_Exhaust", "Ritual Exhaustion", "-30% Consciousness", typeof(HediffWithComps), Color.gray, true,
                MkStage(capMod: new() { (PawnCapacityDefOf.Consciousness, -0.3f) }),
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(30000, 45000), showRemainingTime = true } });
            
            H_GodShielded = MkHediff("SRI_GodShielded", "Divine Protection", "Protected by slave's shield.", typeof(Hediff_GodShielded), Color.cyan);

            // Specializations
            H_Warrior = MkHediff("SRI_Warrior", "Warrior", "+50% Damage, +20% Dodge, +15% Hit\nAbility: Challenger's Roar", typeof(HediffWithComps), Color.red,
                MkStage(statOff: new() { (StatDefOf.MeleeDamageFactor, 0.5f), (StatDefOf.MeleeDodgeChance, 0.2f), (StatDefOf.MeleeHitChance, 0.15f) }));
            H_Healer = MkHediff("SRI_Healer", "Healer", "+30% Tend & Surgery\nAbility: Soothing Touch", typeof(HediffWithComps), Color.green,
                MkStage(statOff: new() { (StatDefOf.MedicalTendQuality, 0.3f), (StatDefOf.MedicalSurgerySuccessChance, 0.3f) }));
            H_Scholar = MkHediff("SRI_Scholar", "Scholar", "+40% Research, +30% Learning\nAbility: Moment of Insight", typeof(HediffWithComps), Color.blue,
                MkStage(statOff: new() { (StatDefOf.ResearchSpeed, 0.4f), (StatDefOf.GlobalLearningFactor, 0.3f) }));
            H_Entertainer = MkHediff("SRI_Entertainer", "Entertainer", "+30% Social, +20% Joy\nAbility: Captivating Performance", typeof(HediffWithComps), Color.magenta,
                MkStage(statOff: new() { (StatDefOf.SocialImpact, 0.3f), (StatDefOf.JoyGainFactor, 0.2f) }));

            // Edict Hediffs
            H_EdictHarvest = MkHediff("SRI_EdictHarvest", "Bountiful Harvest", "+40% Plant Work Speed", typeof(HediffWithComps), new Color(0.4f, 0.9f, 0.3f),
                MkStage(statOff: new() { (StatDefOf.PlantWorkSpeed, 0.4f) }), new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(300000, 300000), showRemainingTime = true } });
            H_EdictLabor = MkHediff("SRI_EdictLabor", "Fervent Labor", "+25% Work Speed", typeof(HediffWithComps), new Color(0.9f, 0.7f, 0.2f),
                MkStage(statOff: new() { (StatDefOf.WorkSpeedGlobal, 0.25f) }), new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(300000, 300000), showRemainingTime = true } });
            H_EdictFortify = MkHediff("SRI_EdictFortify", "Divine Fortification", "+20% Armor", typeof(HediffWithComps), new Color(0.5f, 0.5f, 0.9f),
                MkStage(statOff: new() { (StatDefOf.ArmorRating_Sharp, 0.2f), (StatDefOf.ArmorRating_Blunt, 0.2f) }), new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(300000, 300000), showRemainingTime = true } });
            H_EdictHolyWar = MkHediff("SRI_EdictHolyWar", "Holy War", "+35% Melee, +15% Speed", typeof(HediffWithComps), new Color(0.9f, 0.2f, 0.2f),
                new HediffStage { painFactor = 0.7f, statOffsets = new() { new() { stat = StatDefOf.MeleeDamageFactor, value = 0.35f }, new() { stat = StatDefOf.MoveSpeed, value = 0.7f } } },
                new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(180000, 180000), showRemainingTime = true } });

            H_Taunted = MkHediff("SRI_Taunted", "Taunted", "Must attack the challenger!", typeof(HediffWithComps), Color.red, true,
                MkStage(), new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(600, 1200), showRemainingTime = true } });
            H_Captivated = MkHediff("SRI_Captivated", "Captivated", "+50% Joy Gain", typeof(HediffWithComps), Color.magenta,
                MkStage(statOff: new() { (StatDefOf.JoyGainFactor, 0.5f) }), new() { new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(15000, 30000), showRemainingTime = true } });

            // Trait
            Trait_God = new TraitDef { defName = "SRI_God", degreeDatas = new() { new TraitDegreeData { label = "Reincarnated God", 
                description = "Divine being with supernatural powers. Cannot use ranged weapons. Gains power from slaves." } } };
            DefDatabase<TraitDef>.Add(Trait_God);

            // Thoughts
            T_Satisfied = MkThought("SRI_Satisfied", "Satisfied by master", "Content.", 5, 1);
            T_Forced = MkThought("SRI_Forced", "Forced to serve", "Degrading.", -8, 1);
            T_Wrath = MkThought("SRI_Wrath", "Witnessed divine wrath", "Awe-inspiring!", 10, 3);
            T_Blessed = MkThought("SRI_Blessed", "Divine blessing", "Favored!", 15, 5);
            T_Resurrected = MkThought("SRI_Resurrected", "God returned!", "Unstoppable!", 30, 15);
            T_ServedMaster = MkThought("SRI_ServedMaster", "Served master", "Joy to serve.", 8, 2);
            T_ReceivedService = MkThought("SRI_ReceivedService", "Received service", "Pleasing.", 10, 2);
            T_ComfortLovin = MkThought("SRI_ComfortLovin", "Comfortable intimacy", "Much better.", 6, 1);
            T_EdictBlessing = MkThought("SRI_EdictBlessing", "Divine edict", "Truly favored!", 12, 5);
            T_HuntersFear = MkThought("SRI_HuntersFear", "Hunters coming", "Stay vigilant.", -5, 3);

            // Harmony
            var harmony = new Harmony("com.sri");
            harmony.PatchAll();
            
            try
            {
                var equipMethod = AccessTools.Method(typeof(EquipmentUtility), "CanEquip", new[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) });
                if (equipMethod != null) harmony.Patch(equipMethod, prefix: new HarmonyMethod(typeof(Patch_NoGuns), "Prefix"));
            }
            catch (Exception e) { Log.Warning($"[SRI] Gun patch skipped: {e.Message}"); }
            
            Log.Message("[SRI] Enhanced Version Ready!");
        }

        // Helpers
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
        static ThoughtDef MkThought(string n, string l, string desc, int mood, float days) { var t = new ThoughtDef { defName = n, durationDays = days, stages = new() { new ThoughtStage { label = l, description = desc, baseMoodEffect = mood } } }; DefDatabase<ThoughtDef>.Add(t); return t; }

        // Utility
        public static bool IsGod(Pawn p) => p?.story?.traits?.HasTrait(Trait_God) == true;
        public static int Tier(Pawn p) { var h = p?.health?.hediffSet?.GetFirstHediffOfDef(H_Devotion); if (h == null) return 0; float s = h.Severity; return s >= T4 ? 4 : s >= T3 ? 3 : s >= T2 ? 2 : s >= T1 ? 1 : 0; }
        public static string TierName(int t) => t switch { 4 => "Zealot", 3 => "Devoted", 2 => "Faithful", 1 => "Initiate", _ => "None" };
        public static Color TierColor(int t) => t switch { 4 => Color.yellow, 3 => Color.cyan, 2 => Color.green, 1 => Color.white, _ => Color.gray };
        public static int CD(int b) => (int)(b * SRI_Mod.S.cooldownMult);
        public static float Pwr(Pawn g, float b, float bonus) { int t3 = GC.I?.CachedT3Count ?? 0; int t4 = GC.I?.CachedT4Count ?? 0; return (b + t3 * bonus + t4 * bonus * 2) * SRI_Mod.S.powerMult; }
        
        public static (IntVec3 spot, Thing furniture) FindLovinSpot(Pawn master, Pawn slave)
        {
            Map map = master.Map;
            if (map == null) return (IntVec3.Invalid, null);
            int tier = Tier(slave);
            bool canBePublic = tier >= 3;
            
            var furniture = new List<Thing>();
            foreach (var bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
                if (bed != null && !bed.IsForbidden(master) && !bed.IsBurning() && !bed.Medical && master.CanReserveAndReach(bed, PathEndMode.OnCell, Danger.Some))
                    furniture.Add(bed);
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                if (thing?.def?.building?.isSittable == true && thing.GetStatValue(StatDefOf.Comfort) >= 0.4f && !thing.IsForbidden(master) && master.CanReserveAndReach(thing, PathEndMode.OnCell, Danger.Some))
                    furniture.Add(thing);
            
            if (furniture.Count > 0) { var chosen = furniture.RandomElement(); return (chosen.Position, chosen); }
            
            IntVec3 result = IntVec3.Invalid;
            if (canBePublic)
            {
                for (int i = 0; i < 30; i++)
                {
                    IntVec3 cell = master.Position + GenRadial.RadialPattern[Rand.Range(3, 30)];
                    if (cell.InBounds(map) && cell.Standable(map) && cell.GetDoor(map) == null && master.CanReach(cell, PathEndMode.OnCell, Danger.Some)) { result = cell; break; }
                }
            }
            else
            {
                for (int i = 0; i < 50; i++)
                {
                    IntVec3 cell = master.Position + GenRadial.RadialPattern[Rand.Range(5, 60)];
                    if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetDoor(map) != null || !master.CanReach(cell, PathEndMode.OnCell, Danger.Some)) continue;
                    float light = map.glowGrid.PsychGlowAt(cell) == PsychGlow.Dark ? 0 : map.glowGrid.PsychGlowAt(cell) == PsychGlow.Lit ? 1 : 0.5f;
                    bool isIndoors = cell.GetRoom(map)?.PsychologicallyOutdoors == false;
                    if (light < 0.5f || cell.GetCover(map) != null || isIndoors) { result = cell; break; }
                }
            }
            if (!result.IsValid) result = RCellFinder.RandomWanderDestFor(master, master.Position, 10f, null, Danger.Some);
            return (result, null);
        }
    }
}
