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
        // ==================== DEFINITIONS ====================
        
        // Jobs
        public static JobDef Job_SlaveLovin;
        public static JobDef Job_PunishSlave;
        public static JobDef Job_Procure;
        public static JobDef Job_ConcubineLovin;
        public static JobDef Job_HealingTouch;
        public static JobDef Job_DivineSmite;
        public static JobDef Job_ResurrectionRitual;
        public static JobDef Job_GatherCorpse;

        // Hediffs
        public static HediffDef Hediff_Stockholm;
        public static HediffDef Hediff_ProtectiveRage;
        public static HediffDef Hediff_PunishedPain;
        public static HediffDef Hediff_HealingTouch;
        public static HediffDef Hediff_HeadConcubine;
        public static HediffDef Hediff_DivinePower;
        public static HediffDef Hediff_Devotion;
        public static HediffDef Hediff_DivineShield;
        public static HediffDef Hediff_DivineBlessing;
        public static HediffDef Hediff_ResurrectionSickness;
        public static HediffDef Hediff_RitualExhaustion;
        public static HediffDef Hediff_ConsortStatus;

        // Traits
        public static TraitDef Trait_ReincarnatedGod;

        // Thoughts
        public static ThoughtDef Thought_SatisfiedByMaster;
        public static ThoughtDef Thought_ForcedAffection;
        public static ThoughtDef Thought_WitnessedDivineWrath;
        public static ThoughtDef Thought_ReceivedBlessing;
        public static ThoughtDef Thought_GodResurrected;

        // ScenPart
        public static ScenPartDef ScenPart_GodSetup_Def;

        // ==================== CONSTANTS ====================
        
        // Devotion Tiers (severity thresholds)
        public const float DEVOTION_TIER_1 = 0.15f;  // ~5 days
        public const float DEVOTION_TIER_2 = 0.35f;  // ~12 days
        public const float DEVOTION_TIER_3 = 0.60f;  // ~20 days
        public const float DEVOTION_TIER_4 = 0.90f;  // ~30 days
        
        // Ability Cooldowns (in ticks, 2500 ticks = 1 hour)
        public const int COOLDOWN_SMITE = 2500;           // 1 hour
        public const int COOLDOWN_MASS_CALM = 10000;      // 4 hours
        public const int COOLDOWN_BLESSING = 20000;       // 8 hours
        public const int COOLDOWN_WRATH = 60000;          // 24 hours
        public const int COOLDOWN_DIVINE_SHIELD = 5000;   // 2 hours
        
        // Resurrection
        public const int RESURRECTION_TIME_LIMIT = 180000; // 3 days
        public const int RESURRECTION_RITUAL_DURATION = 60000; // 1 day
        public const float HP_PER_HUMAN_CORPSE = 30f;
        public const float HP_PER_LARGE_ANIMAL = 20f;
        public const float HP_PER_SMALL_ANIMAL = 10f;
        public const int MIN_CORPSES_FOR_RESURRECTION = 3;

        // ==================== INITIALIZATION ====================
        
        static SRI_Main()
        {
            Log.Message("[SRI] Initializing Slave Realism Improved...");
            
            // 1. Create Jobs
            CreateJobs();
            
            // 2. Create Hediffs
            CreateHediffs();
            
            // 3. Create Traits
            CreateTraits();
            
            // 4. Create Thoughts
            CreateThoughts();

            // 5. Get XML-defined ScenPartDef
            ScenPart_GodSetup_Def = DefDatabase<ScenPartDef>.GetNamedSilentFail("SRI_ScenPart_GodSetup");
            if (ScenPart_GodSetup_Def == null)
            {
                Log.Warning("[SRI] Could not find SRI_ScenPart_GodSetup - creating programmatically");
                CreateScenPartDef();
            }

            // 6. Get XML-defined Consort hediff
            Hediff_ConsortStatus = DefDatabase<HediffDef>.GetNamedSilentFail("SRI_ConsortStatus");

            // 7. Apply Harmony Patches
            var harmony = new Harmony("com.slaverealism.improved");
            harmony.PatchAll();
            
            Log.Message("[SRI] Initialization complete!");
        }

        static void CreateJobs()
        {
            Job_SlaveLovin = CreateJob("SRI_SlaveLovin", typeof(JobDriver_SlaveLovin), "making love.");
            Job_PunishSlave = CreateJob("SRI_PunishSlave", typeof(JobDriver_PunishSlave), "punishing slave.", true);
            Job_Procure = CreateJob("SRI_Procure", typeof(JobDriver_Procure), "procuring victim.");
            Job_ConcubineLovin = CreateJob("SRI_ConcubineLovin", typeof(JobDriver_ConcubineLovin), "attending to master.");
            Job_HealingTouch = CreateJob("SRI_HealingTouch", typeof(JobDriver_HealingTouch), "performing healing ritual.");
            Job_DivineSmite = CreateJob("SRI_DivineSmite", typeof(JobDriver_DivineSmite), "channeling divine wrath.");
            Job_ResurrectionRitual = CreateJob("SRI_ResurrectionRitual", typeof(JobDriver_ResurrectionRitual), "performing resurrection ritual.");
            Job_GatherCorpse = CreateJob("SRI_GatherCorpse", typeof(JobDriver_GatherCorpse), "gathering corpse for ritual.");
        }

        static JobDef CreateJob(string defName, Type driver, string report, bool showWeapon = false)
        {
            JobDef j = new JobDef
            {
                defName = defName,
                driverClass = driver,
                reportString = report,
                playerInterruptible = true,
                checkOverrideOnDamage = CheckJobOverrideOnDamageMode.Always,
                alwaysShowWeapon = showWeapon,
                casualInterruptible = false,
                suspendable = false
            };
            DefDatabase<JobDef>.Add(j);
            return j;
        }

        static void CreateHediffs()
        {
            // Stockholm Syndrome
            Hediff_Stockholm = new HediffDef
            {
                defName = "SRI_StockholmSyndrome",
                label = "Stockholm Syndrome",
                description = "This slave has developed a deep psychological bond with their master.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = Color.cyan,
                isBad = false,
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        statOffsets = new List<StatModifier>
                        {
                            new StatModifier { stat = StatDefOf.GlobalLearningFactor, value = 0.2f }
                        }
                    }
                },
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties { compClass = typeof(HediffComp_StockholmConversion) }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_Stockholm);

            // Protective Rage
            Hediff_ProtectiveRage = new HediffDef
            {
                defName = "SRI_ProtectiveRage",
                label = "Protective Rage",
                description = "Adrenaline-fueled rage to protect their master!",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = Color.red,
                isBad = false,
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(2500, 5000), showRemainingTime = true },
                    new HediffCompProperties { compClass = typeof(HediffComp_RageSustain) }
                },
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        painFactor = 0.1f,
                        statOffsets = new List<StatModifier>
                        {
                            new StatModifier { stat = StatDefOf.MoveSpeed, value = 3.0f },
                            new StatModifier { stat = StatDefOf.MeleeHitChance, value = 0.30f },
                            new StatModifier { stat = StatDefOf.MeleeDodgeChance, value = 0.30f },
                            new StatModifier { stat = StatDefOf.MeleeDamageFactor, value = 1.5f },
                            new StatModifier { stat = StatDefOf.MeleeWeapon_CooldownMultiplier, value = -0.5f }
                        },
                        capMods = new List<PawnCapacityModifier>
                        {
                            new PawnCapacityModifier { capacity = PawnCapacityDefOf.Manipulation, offset = 1.0f },
                            new PawnCapacityModifier { capacity = PawnCapacityDefOf.Consciousness, offset = 0.5f }
                        }
                    }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_ProtectiveRage);

            // Punished Pain
            Hediff_PunishedPain = new HediffDef
            {
                defName = "SRI_PunishedPain",
                label = "Punished",
                description = "Still aching from recent punishment.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = new Color(0.8f, 0.4f, 0f),
                isBad = true,
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(10000, 20000), showRemainingTime = true }
                },
                stages = new List<HediffStage>
                {
                    new HediffStage { painOffset = 0.35f }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_PunishedPain);

            // Healing Touch
            Hediff_HealingTouch = new HediffDef
            {
                defName = "SRI_HealingTouch",
                label = "Healing Touch",
                description = "Blessed with accelerated healing.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = Color.green,
                isBad = false,
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(15000, 30000), showRemainingTime = true }
                },
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        statOffsets = new List<StatModifier>
                        {
                            new StatModifier { stat = StatDefOf.ImmunityGainSpeed, value = 0.5f },
                            new StatModifier { stat = StatDefOf.InjuryHealingFactor, value = 0.5f }
                        }
                    }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_HealingTouch);

            // Head Concubine
            Hediff_HeadConcubine = new HediffDef
            {
                defName = "SRI_HeadConcubine",
                label = "Head Concubine",
                description = "The master's favorite.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = new Color(1f, 0.8f, 0f),
                isBad = false,
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        statOffsets = new List<StatModifier>
                        {
                            new StatModifier { stat = StatDefOf.SocialImpact, value = 0.2f }
                        }
                    }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_HeadConcubine);

            // Divine Power (God's scaling hediff)
            Hediff_DivinePower = new HediffDef
            {
                defName = "SRI_DivinePower",
                label = "Divine Power",
                description = "The god draws power from devoted followers.\n\nPer slave:\n• +5% Move Speed\n• +10% Melee Damage\n• +10% Research Speed\n• +10% Psychic Sensitivity\n• +5% Bullet Deflection",
                hediffClass = typeof(Hediff_DivineScaling),
                defaultLabelColor = new Color(1f, 0.9f, 0.2f),
                isBad = false
            };
            DefDatabase<HediffDef>.Add(Hediff_DivinePower);

            // Devotion (Slave tier system)
            Hediff_Devotion = new HediffDef
            {
                defName = "SRI_Devotion",
                label = "Devotion",
                description = "Measures the slave's devotion to their master.\n\nTier 0: Unwilling (0-14%)\nTier 1: Obedient (15-34%)\nTier 2: Devoted (35-59%)\nTier 3: Fanatical (60-89%)\nTier 4: Ascended (90-100%)",
                hediffClass = typeof(Hediff_Devotion),
                defaultLabelColor = new Color(0.8f, 0.5f, 1f),
                isBad = false,
                minSeverity = 0.01f,
                maxSeverity = 1.0f,
                initialSeverity = 0.01f
            };
            DefDatabase<HediffDef>.Add(Hediff_Devotion);

            // Divine Shield
            Hediff_DivineShield = new HediffDef
            {
                defName = "SRI_DivineShield",
                label = "Divine Shield",
                description = "Projecting a protective shield around their master. Cannot move while shielding.",
                hediffClass = typeof(Hediff_DivineShield),
                defaultLabelColor = new Color(0.3f, 0.7f, 1f),
                isBad = false,
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        capMods = new List<PawnCapacityModifier>
                        {
                            new PawnCapacityModifier { capacity = PawnCapacityDefOf.Moving, setMax = 0f }
                        }
                    }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_DivineShield);

            // Divine Blessing (temporary buff)
            Hediff_DivineBlessing = new HediffDef
            {
                defName = "SRI_DivineBlessing",
                label = "Divine Blessing",
                description = "Blessed by the living god with enhanced abilities.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = new Color(1f, 0.95f, 0.5f),
                isBad = false,
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(30000, 30000), showRemainingTime = true }
                },
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        statOffsets = new List<StatModifier>
                        {
                            new StatModifier { stat = StatDefOf.WorkSpeedGlobal, value = 0.15f },
                            new StatModifier { stat = StatDefOf.MoveSpeed, value = 0.5f },
                            new StatModifier { stat = StatDefOf.MeleeDamageFactor, value = 0.2f },
                            new StatModifier { stat = StatDefOf.ImmunityGainSpeed, value = 0.3f }
                        }
                    }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_DivineBlessing);

            // Resurrection Sickness
            Hediff_ResurrectionSickness = new HediffDef
            {
                defName = "SRI_ResurrectionSickness",
                label = "Resurrection Sickness",
                description = "Recovering from being brought back from the dead.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = new Color(0.5f, 0.5f, 0.5f),
                isBad = true,
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(120000, 180000), showRemainingTime = true }
                },
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        statFactors = new List<StatModifier>
                        {
                            new StatModifier { stat = StatDefOf.MoveSpeed, value = 0.7f },
                            new StatModifier { stat = StatDefOf.WorkSpeedGlobal, value = 0.5f }
                        },
                        capMods = new List<PawnCapacityModifier>
                        {
                            new PawnCapacityModifier { capacity = PawnCapacityDefOf.Consciousness, offset = -0.2f }
                        }
                    }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_ResurrectionSickness);

            // Ritual Exhaustion
            Hediff_RitualExhaustion = new HediffDef
            {
                defName = "SRI_RitualExhaustion",
                label = "Ritual Exhaustion",
                description = "Completely drained from performing the resurrection ritual.",
                hediffClass = typeof(HediffWithComps),
                defaultLabelColor = new Color(0.6f, 0.4f, 0.6f),
                isBad = true,
                comps = new List<HediffCompProperties>
                {
                    new HediffCompProperties_Disappears { disappearsAfterTicks = new IntRange(60000, 90000), showRemainingTime = true }
                },
                stages = new List<HediffStage>
                {
                    new HediffStage
                    {
                        statFactors = new List<StatModifier>
                        {
                            new StatModifier { stat = StatDefOf.MoveSpeed, value = 0.5f },
                            new StatModifier { stat = StatDefOf.WorkSpeedGlobal, value = 0.3f }
                        },
                        capMods = new List<PawnCapacityModifier>
                        {
                            new PawnCapacityModifier { capacity = PawnCapacityDefOf.Consciousness, offset = -0.3f }
                        }
                    }
                }
            };
            DefDatabase<HediffDef>.Add(Hediff_RitualExhaustion);
        }

        static void CreateTraits()
        {
            Trait_ReincarnatedGod = new TraitDef
            {
                defName = "SRI_ReincarnatedGod",
                degreeDatas = new List<TraitDegreeData>
                {
                    new TraitDegreeData
                    {
                        label = "Reincarnated God",
                        description = "A divine being reborn in mortal flesh.\n\n• Cannot use ranged weapons\n• Deflects projectiles (scales with slaves)\n• Gains power from devoted followers\n• Can use Divine Abilities"
                    }
                }
            };
            DefDatabase<TraitDef>.Add(Trait_ReincarnatedGod);
        }

        static void CreateThoughts()
        {
            Thought_SatisfiedByMaster = new ThoughtDef
            {
                defName = "SRI_SatisfiedByMaster",
                durationDays = 1f,
                stages = new List<ThoughtStage>
                {
                    new ThoughtStage { label = "Satisfied by master", baseMoodEffect = 5 }
                }
            };
            DefDatabase<ThoughtDef>.Add(Thought_SatisfiedByMaster);

            Thought_ForcedAffection = new ThoughtDef
            {
                defName = "SRI_ForcedAffection",
                durationDays = 1f,
                stages = new List<ThoughtStage>
                {
                    new ThoughtStage { label = "Forced affection", baseMoodEffect = -8 }
                }
            };
            DefDatabase<ThoughtDef>.Add(Thought_ForcedAffection);

            Thought_WitnessedDivineWrath = new ThoughtDef
            {
                defName = "SRI_WitnessedDivineWrath",
                durationDays = 3f,
                stages = new List<ThoughtStage>
                {
                    new ThoughtStage { label = "Witnessed divine wrath", baseMoodEffect = 10 }
                }
            };
            DefDatabase<ThoughtDef>.Add(Thought_WitnessedDivineWrath);

            Thought_ReceivedBlessing = new ThoughtDef
            {
                defName = "SRI_ReceivedBlessing",
                durationDays = 5f,
                stages = new List<ThoughtStage>
                {
                    new ThoughtStage { label = "Received divine blessing", baseMoodEffect = 15 }
                }
            };
            DefDatabase<ThoughtDef>.Add(Thought_ReceivedBlessing);

            Thought_GodResurrected = new ThoughtDef
            {
                defName = "SRI_GodResurrected",
                durationDays = 15f,
                stages = new List<ThoughtStage>
                {
                    new ThoughtStage { label = "Witnessed God's resurrection", baseMoodEffect = 30 }
                }
            };
            DefDatabase<ThoughtDef>.Add(Thought_GodResurrected);
        }

        static void CreateScenPartDef()
        {
            ScenPart_GodSetup_Def = new ScenPartDef
            {
                defName = "SRI_ScenPart_GodSetup",
                label = "God and Harem Setup",
                description = "Configures the starting God and harem.",
                scenPartClass = typeof(ScenPart_GodSetup),
                category = ScenPartCategory.StartingImportant
            };
            DefDatabase<ScenPartDef>.Add(ScenPart_GodSetup_Def);
        }

        // ==================== UTILITY METHODS ====================
        
        public static int GetDevotionTier(Pawn pawn)
        {
            Hediff devotion = pawn.health?.hediffSet?.GetFirstHediffOfDef(Hediff_Devotion);
            if (devotion == null) return 0;
            
            float severity = devotion.Severity;
            if (severity >= DEVOTION_TIER_4) return 4;
            if (severity >= DEVOTION_TIER_3) return 3;
            if (severity >= DEVOTION_TIER_2) return 2;
            if (severity >= DEVOTION_TIER_1) return 1;
            return 0;
        }

        public static string GetDevotionTierName(int tier)
        {
            switch (tier)
            {
                case 0: return "Unwilling";
                case 1: return "Obedient";
                case 2: return "Devoted";
                case 3: return "Fanatical";
                case 4: return "Ascended";
                default: return "Unknown";
            }
        }

        public static bool IsGod(Pawn pawn)
        {
            return pawn?.story?.traits?.HasTrait(Trait_ReincarnatedGod) == true;
        }

        public static Pawn GetGodOnMap(Map map)
        {
            if (map == null) return null;
            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => IsGod(p));
        }

        public static int CountDevotedSlaves(Map map, int minTier = 3)
        {
            if (map == null) return 0;
            int count = 0;
            foreach (Pawn slave in map.mapPawns.SlavesOfColonySpawned.ToList())
            {
                if (GetDevotionTier(slave) >= minTier) count++;
            }
            return count;
        }

        public static float CalculateAbilityPower(Pawn god, float basePower, float bonusPerDevoted)
        {
            if (god?.Map == null) return basePower;
            
            int tier3Count = 0;
            int tier4Count = 0;
            
            foreach (Pawn slave in god.Map.mapPawns.SlavesOfColonySpawned.ToList())
            {
                int tier = GetDevotionTier(slave);
                if (tier >= 4) tier4Count++;
                else if (tier >= 3) tier3Count++;
            }
            
            // Tier 4 counts double
            return basePower + (tier3Count * bonusPerDevoted) + (tier4Count * bonusPerDevoted * 2f);
        }
    }

    // ======================================================================
    // HEDIFF CLASSES
    // ======================================================================

    #region Hediff Classes

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
                    slaves, slaves == 1 ? "" : "s",
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

    public class Hediff_Devotion : HediffWithComps
    {
        private int cachedTier = -1;
        private Pawn cachedMaster = null;

        public int Tier
        {
            get
            {
                if (cachedTier < 0) UpdateTier();
                return cachedTier;
            }
        }

        public override string LabelInBrackets => SRI_Main.GetDevotionTierName(Tier) + " (" + (Severity * 100f).ToString("F0") + "%)";

        public override string TipStringExtra
        {
            get
            {
                string tip = "Devotion Tier: " + SRI_Main.GetDevotionTierName(Tier) + "\n";
                tip += "Progress: " + (Severity * 100f).ToString("F1") + "%\n\n";
                
                if (cachedMaster != null)
                    tip += "Master: " + cachedMaster.LabelShort + "\n";
                
                tip += "Proximity Buffs (when near master):\n";
                tip += GetProximityBonusDescription();
                
                if (Tier >= 3)
                    tip += "\n\n• Can use Divine Shield";
                
                return tip;
            }
        }

        private string GetProximityBonusDescription()
        {
            switch (Tier)
            {
                case 1: return "• Work Speed: +5%\n• Move Speed: +5%\n• Melee Damage: +5%";
                case 2: return "• Work Speed: +10%\n• Move Speed: +10%\n• Melee Damage: +10%\n• Melee Dodge: +5%";
                case 3: return "• Work Speed: +15%\n• Move Speed: +15%\n• Melee Damage: +20%\n• Melee Dodge: +10%\n• Pain Threshold: +20%";
                case 4: return "• Work Speed: +25%\n• Move Speed: +20%\n• Melee Damage: +30%\n• Melee Dodge: +15%\n• Pain Threshold: +30%\n• Consciousness: +10%";
                default: return "• None (increase devotion)";
            }
        }

        private void UpdateTier()
        {
            if (Severity >= SRI_Main.DEVOTION_TIER_4) cachedTier = 4;
            else if (Severity >= SRI_Main.DEVOTION_TIER_3) cachedTier = 3;
            else if (Severity >= SRI_Main.DEVOTION_TIER_2) cachedTier = 2;
            else if (Severity >= SRI_Main.DEVOTION_TIER_1) cachedTier = 1;
            else cachedTier = 0;
        }

        public override void Tick()
        {
            base.Tick();
            
            if (!pawn.IsHashIntervalTick(250)) return; // Every ~10 seconds
            
            // Update master reference
            if (SRI_GameComponent.Instance != null)
            {
                cachedMaster = SRI_GameComponent.Instance.GetMasterOf(pawn);
            }
            
            // Grow devotion
            GrowDevotion();
            
            // Update tier cache
            UpdateTier();
        }

        private void GrowDevotion()
        {
            if (cachedMaster == null || cachedMaster.Dead) return;
            
            float growthRate = 0f;
            
            // Base growth: near master
            if (pawn.Map == cachedMaster.Map && pawn.Position.InHorDistOf(cachedMaster.Position, 15f))
            {
                growthRate += 0.00008f; // ~0.5% per hour when nearby
            }
            
            // Bonus: Has Stockholm Syndrome (2x growth)
            if (pawn.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm))
            {
                growthRate *= 2f;
            }
            
            // Bonus: Good mood
            if (pawn.needs?.mood?.CurLevel > 0.6f)
            {
                growthRate *= 1.2f;
            }
            
            // Apply growth (severity can only increase)
            if (growthRate > 0 && Severity < 1f)
            {
                Severity = Mathf.Min(1f, Severity + growthRate);
            }
        }

        public void AddDevotion(float amount)
        {
            if (amount > 0)
            {
                Severity = Mathf.Min(1f, Severity + amount);
                UpdateTier();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref cachedMaster, "cachedMaster");
        }
    }

    public class Hediff_DivineShield : HediffWithComps
    {
        public Pawn protectedMaster;
        public bool shieldReady = true;
        private int shieldCooldownTick = 0;

        public override string LabelInBrackets
        {
            get
            {
                if (!shieldReady)
                {
                    int remaining = shieldCooldownTick - Find.TickManager.TicksGame;
                    if (remaining > 0)
                        return "Cooldown: " + (remaining / 2500f).ToString("F1") + "h";
                }
                return shieldReady ? "Ready" : "Recovering";
            }
        }

        public override void Tick()
        {
            base.Tick();
            
            // Check cooldown
            if (!shieldReady && Find.TickManager.TicksGame >= shieldCooldownTick)
            {
                shieldReady = true;
                Messages.Message(pawn.LabelShort + "'s Divine Shield is ready!", pawn, MessageTypeDefOf.PositiveEvent, false);
            }
            
            // Remove if master dead or too far
            if (protectedMaster == null || protectedMaster.Dead || 
                !pawn.Position.InHorDistOf(protectedMaster.Position, 10f))
            {
                pawn.health.RemoveHediff(this);
            }
        }

        public bool TryBlockAttack(DamageInfo dinfo, out float damageToSlave)
        {
            damageToSlave = 0f;
            
            if (!shieldReady) return false;
            if (pawn.Dead || pawn.Downed) return false;
            
            // Block the attack!
            shieldReady = false;
            shieldCooldownTick = Find.TickManager.TicksGame + SRI_Main.COOLDOWN_DIVINE_SHIELD;
            
            // Slave takes 20% of blocked damage
            damageToSlave = dinfo.Amount * 0.2f;
            
            // Visual feedback
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "SHIELD!", new Color(0.3f, 0.7f, 1f));
            FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 2f);
            SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map)));
            
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref protectedMaster, "protectedMaster");
            Scribe_Values.Look(ref shieldReady, "shieldReady", true);
            Scribe_Values.Look(ref shieldCooldownTick, "shieldCooldownTick", 0);
        }
    }

    public class HediffComp_RageSustain : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn.IsHashIntervalTick(60))
            {
                if (Pawn.Drafted || (Pawn.CurJob != null && Pawn.CurJob.def == JobDefOf.AttackMelee))
                {
                    HediffComp_Disappears d = parent.TryGetComp<HediffComp_Disappears>();
                    if (d != null) d.ticksToDisappear = 5000;
                }
            }
        }
    }

    public class HediffComp_StockholmConversion : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn.IsHashIntervalTick(2500) && ModsConfig.IdeologyActive && Pawn.Ideo != null)
            {
                Ideo playerIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (playerIdeo != null && Pawn.Ideo != playerIdeo)
                {
                    Pawn.ideo.OffsetCertainty(-0.00833f);
                    if (Pawn.ideo.Certainty <= 0.01f)
                    {
                        Pawn.ideo.SetIdeo(playerIdeo);
                        Pawn.ideo.OffsetCertainty(0.5f);
                        Messages.Message(Pawn.LabelShort + " has been converted to " + playerIdeo.name + " through Stockholm Syndrome.", Pawn, MessageTypeDefOf.PositiveEvent, true);
                    }
                }
            }
        }
    }

    #endregion

    // ======================================================================
    // GAME COMPONENT
    // ======================================================================

    #region Game Component

    public class SRI_GameComponent : GameComponent
    {
        public static SRI_GameComponent Instance;
        
        // Existing data
        private HashSet<int> hybridBedIDs = new HashSet<int>();
        private Dictionary<int, int> punishmentCooldowns = new Dictionary<int, int>();
        private Dictionary<int, int> slaveToMasterMap = new Dictionary<int, int>();
        private Dictionary<int, int> dailyLovinCount = new Dictionary<int, int>();
        private int lastDayReset = 0;
        
        // Ability cooldowns
        private Dictionary<int, int> smiteCooldowns = new Dictionary<int, int>();
        private Dictionary<int, int> massCalmCooldowns = new Dictionary<int, int>();
        private Dictionary<int, int> blessingCooldowns = new Dictionary<int, int>();
        private Dictionary<int, int> wrathCooldowns = new Dictionary<int, int>();
        
        // Resurrection tracking
        private Dictionary<int, int> godDeathTicks = new Dictionary<int, int>(); // GodID -> death tick
        private Dictionary<int, List<Thing>> gatheredCorpses = new Dictionary<int, List<Thing>>(); // GodID -> corpses
        
        // Divine Shield tracking
        private Dictionary<int, int> shieldCooldowns = new Dictionary<int, int>(); // SlaveID -> cooldown tick

        public SRI_GameComponent(Game game) { Instance = this; }

        public override void FinalizeInit() { Instance = this; }

        public override void GameComponentTick()
        {
            int day = GenLocalDate.DayOfYear(Find.CurrentMap);
            if (day != lastDayReset)
            {
                dailyLovinCount.Clear();
                lastDayReset = day;
                UpdateHeadConcubines();
            }

            if (Find.TickManager.TicksGame % 2500 == 0)
            {
                ProcessAutoEvents();
                ProcessGodAura();
                CleanupExpiredResurrections();
            }
        }

        // ==================== MASTER/CONCUBINE SYSTEM ====================

        public void SetConcubine(Pawn slave, Pawn master)
        {
            int masterId = master != null ? master.thingIDNumber : -1;
            if (slaveToMasterMap.ContainsKey(slave.thingIDNumber))
                slaveToMasterMap[slave.thingIDNumber] = masterId;
            else
                slaveToMasterMap.Add(slave.thingIDNumber, masterId);
            
            // Initialize devotion if needed
            if (master != null && !slave.health.hediffSet.HasHediff(SRI_Main.Hediff_Devotion))
            {
                slave.health.AddHediff(SRI_Main.Hediff_Devotion);
            }
            
            UpdateHeadConcubines();
        }

        public bool IsConcubineOf(Pawn slave, Pawn master)
        {
            if (slaveToMasterMap.TryGetValue(slave.thingIDNumber, out int masterId))
                return masterId == master.thingIDNumber;
            return false;
        }

        public Pawn GetMasterOf(Pawn slave)
        {
            if (!slaveToMasterMap.TryGetValue(slave.thingIDNumber, out int masterId) || masterId == -1)
                return null;
            
            if (slave.Map == null) return null;
            return slave.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == masterId);
        }

        public List<Pawn> GetConcubinesOf(Pawn master)
        {
            List<Pawn> concubines = new List<Pawn>();
            if (master?.Map == null) return concubines;
            
            foreach (Pawn slave in master.Map.mapPawns.SlavesOfColonySpawned.ToList())
            {
                if (IsConcubineOf(slave, master))
                    concubines.Add(slave);
            }
            return concubines;
        }

        // ==================== ABILITY COOLDOWNS ====================

        public bool IsAbilityReady(Pawn god, string ability)
        {
            Dictionary<int, int> cooldowns = GetCooldownDict(ability);
            if (cooldowns == null) return true;
            
            if (!cooldowns.TryGetValue(god.thingIDNumber, out int unlockTick))
                return true;
            
            return Find.TickManager.TicksGame >= unlockTick;
        }

        public int GetAbilityCooldownRemaining(Pawn god, string ability)
        {
            Dictionary<int, int> cooldowns = GetCooldownDict(ability);
            if (cooldowns == null) return 0;
            
            if (!cooldowns.TryGetValue(god.thingIDNumber, out int unlockTick))
                return 0;
            
            return Mathf.Max(0, unlockTick - Find.TickManager.TicksGame);
        }

        public void SetAbilityCooldown(Pawn god, string ability, int cooldownTicks)
        {
            Dictionary<int, int> cooldowns = GetCooldownDict(ability);
            if (cooldowns == null) return;
            
            int unlockTick = Find.TickManager.TicksGame + cooldownTicks;
            if (cooldowns.ContainsKey(god.thingIDNumber))
                cooldowns[god.thingIDNumber] = unlockTick;
            else
                cooldowns.Add(god.thingIDNumber, unlockTick);
        }

        private Dictionary<int, int> GetCooldownDict(string ability)
        {
            switch (ability)
            {
                case "Smite": return smiteCooldowns;
                case "MassCalm": return massCalmCooldowns;
                case "Blessing": return blessingCooldowns;
                case "Wrath": return wrathCooldowns;
                default: return null;
            }
        }

        // ==================== DIVINE SHIELD ====================

        public bool IsShieldReady(Pawn slave)
        {
            if (!shieldCooldowns.TryGetValue(slave.thingIDNumber, out int unlockTick))
                return true;
            return Find.TickManager.TicksGame >= unlockTick;
        }

        public void SetShieldCooldown(Pawn slave)
        {
            int unlockTick = Find.TickManager.TicksGame + SRI_Main.COOLDOWN_DIVINE_SHIELD;
            if (shieldCooldowns.ContainsKey(slave.thingIDNumber))
                shieldCooldowns[slave.thingIDNumber] = unlockTick;
            else
                shieldCooldowns.Add(slave.thingIDNumber, unlockTick);
        }

        // ==================== RESURRECTION ====================

        public void RegisterGodDeath(Pawn god)
        {
            if (god == null) return;
            godDeathTicks[god.thingIDNumber] = Find.TickManager.TicksGame;
            gatheredCorpses[god.thingIDNumber] = new List<Thing>();
            
            Messages.Message("The God has fallen! Devoted followers may attempt resurrection within 3 days.", MessageTypeDefOf.ThreatBig, true);
        }

        public bool CanResurrect(Pawn god)
        {
            if (god == null || !god.Dead) return false;
            
            if (!godDeathTicks.TryGetValue(god.thingIDNumber, out int deathTick))
                return false;
            
            // Check time limit
            if (Find.TickManager.TicksGame - deathTick > SRI_Main.RESURRECTION_TIME_LIMIT)
                return false;
            
            // Check corpses
            if (!gatheredCorpses.TryGetValue(god.thingIDNumber, out List<Thing> corpses))
                return false;
            
            return corpses.Count >= SRI_Main.MIN_CORPSES_FOR_RESURRECTION;
        }

        public void AddCorpseForResurrection(Pawn god, Thing corpse)
        {
            if (!gatheredCorpses.ContainsKey(god.thingIDNumber))
                gatheredCorpses[god.thingIDNumber] = new List<Thing>();
            
            gatheredCorpses[god.thingIDNumber].Add(corpse);
        }

        public float CalculateResurrectionHP(Pawn god)
        {
            if (!gatheredCorpses.TryGetValue(god.thingIDNumber, out List<Thing> corpses))
                return 0f;
            
            float totalHP = 0f;
            foreach (Thing corpse in corpses)
            {
                if (corpse is Corpse c && c.InnerPawn != null)
                {
                    Pawn p = c.InnerPawn;
                    if (p.RaceProps.Humanlike)
                        totalHP += SRI_Main.HP_PER_HUMAN_CORPSE;
                    else if (p.RaceProps.baseBodySize >= 1f)
                        totalHP += SRI_Main.HP_PER_LARGE_ANIMAL;
                    else
                        totalHP += SRI_Main.HP_PER_SMALL_ANIMAL;
                }
            }
            return totalHP;
        }

        public void CompleteResurrection(Pawn god, Pawn performer)
        {
            if (god == null || !god.Dead) return;
            
            float hp = CalculateResurrectionHP(god);
            
            // Consume corpses
            if (gatheredCorpses.TryGetValue(god.thingIDNumber, out List<Thing> corpses))
            {
                foreach (Thing corpse in corpses)
                {
                    if (corpse != null && !corpse.Destroyed)
                        corpse.Destroy();
                }
                gatheredCorpses.Remove(god.thingIDNumber);
            }
            godDeathTicks.Remove(god.thingIDNumber);
            
            // Resurrect
            ResurrectionUtility.TryResurrect(god);
            
            // Set health based on gathered corpses
            float maxHealth = god.health.summaryHealth.SummaryHealthPercent;
            float targetHealth = Mathf.Clamp01(hp / 100f);
            
            // Add resurrection sickness
            god.health.AddHediff(SRI_Main.Hediff_ResurrectionSickness);
            
            // Exhaust performer
            if (performer != null)
            {
                performer.health.AddHediff(SRI_Main.Hediff_RitualExhaustion);
            }
            
            // Notify
            Messages.Message("The God has been resurrected!", god, MessageTypeDefOf.PositiveEvent, true);
            
            // Give thought to all slaves
            if (god.Map != null)
            {
                foreach (Pawn slave in god.Map.mapPawns.SlavesOfColonySpawned.ToList())
                {
                    slave.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_GodResurrected);
                }
            }
        }

        private void CleanupExpiredResurrections()
        {
            List<int> expired = new List<int>();
            foreach (var kvp in godDeathTicks)
            {
                if (Find.TickManager.TicksGame - kvp.Value > SRI_Main.RESURRECTION_TIME_LIMIT)
                {
                    expired.Add(kvp.Key);
                }
            }
            foreach (int id in expired)
            {
                godDeathTicks.Remove(id);
                gatheredCorpses.Remove(id);
            }
        }

        // ==================== EXISTING SYSTEMS ====================

        public int GetLovinCount(Pawn m) => dailyLovinCount.TryGetValue(m.thingIDNumber, out int c) ? c : 0;
        
        public void IncrementLovinCount(Pawn m)
        {
            if (dailyLovinCount.ContainsKey(m.thingIDNumber))
                dailyLovinCount[m.thingIDNumber]++;
            else
                dailyLovinCount.Add(m.thingIDNumber, 1);
        }

        public bool IsHybrid(Building_Bed b) => b != null && hybridBedIDs.Contains(b.thingIDNumber);

        public void SetHybrid(Building_Bed b, bool s)
        {
            if (b == null) return;
            if (s)
            {
                hybridBedIDs.Add(b.thingIDNumber);
                Traverse.Create(b).Field("forOwnerType").SetValue(BedOwnerType.Colonist);
            }
            else
            {
                hybridBedIDs.Remove(b.thingIDNumber);
            }
        }

        public void SetPunishCooldown(Pawn p, int t)
        {
            if (punishmentCooldowns.ContainsKey(p.thingIDNumber))
                punishmentCooldowns[p.thingIDNumber] = t;
            else
                punishmentCooldowns.Add(p.thingIDNumber, t);
        }

        public int GetPunishUnlockTick(Pawn p) => punishmentCooldowns.TryGetValue(p.thingIDNumber, out int val) ? val : 0;

        private void ProcessGodAura()
        {
            Map m = Find.CurrentMap;
            if (m == null) return;

            foreach (Pawn g in m.mapPawns.FreeColonistsSpawned.ToList().Where(p => SRI_Main.IsGod(p)))
            {
                if (!g.health.hediffSet.HasHediff(SRI_Main.Hediff_DivinePower))
                    g.health.AddHediff(SRI_Main.Hediff_DivinePower);

                foreach (Pawn s in m.mapPawns.SlavesOfColonySpawned.ToList())
                {
                    if (s.Position.InHorDistOf(g.Position, 9f))
                    {
                        Need_Suppression ns = s.needs.TryGetNeed<Need_Suppression>();
                        if (ns != null && ns.CurLevel < 1f)
                        {
                            ns.CurLevel = 1f;
                            FleckMaker.ThrowMetaIcon(s.Position, s.Map, FleckDefOf.IncapIcon);
                        }
                    }
                }
            }
        }

        private void UpdateHeadConcubines()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            Dictionary<Pawn, List<Pawn>> masterConcubines = new Dictionary<Pawn, List<Pawn>>();

            foreach (Pawn slave in map.mapPawns.SlavesOfColonySpawned.ToList())
            {
                Pawn master = GetMasterOf(slave);
                if (master != null)
                {
                    if (!masterConcubines.ContainsKey(master))
                        masterConcubines[master] = new List<Pawn>();
                    masterConcubines[master].Add(slave);
                }
            }

            foreach (var kvp in masterConcubines)
            {
                Pawn favorite = null;
                float maxScore = -999f;

                foreach (Pawn concubine in kvp.Value)
                {
                    // Remove existing head concubine status
                    Hediff existing = concubine.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_HeadConcubine);
                    if (existing != null) concubine.health.RemoveHediff(existing);

                    float score = concubine.relations.OpinionOf(kvp.Key) + (concubine.skills.GetSkill(SkillDefOf.Social).Level * 5f);
                    if (score > maxScore)
                    {
                        maxScore = score;
                        favorite = concubine;
                    }
                }

                if (favorite != null)
                    favorite.health.AddHediff(SRI_Main.Hediff_HeadConcubine);
            }
        }

        private void ProcessAutoEvents()
        {
            Map m = Find.CurrentMap;
            if (m == null) return;

            foreach (Pawn p in m.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.Downed || p.Dead) continue;

                bool night = GenLocalDate.HourInteger(m) >= 22 || GenLocalDate.HourInteger(m) <= 5;
                if (night && p.InBed())
                {
                    bool injured = p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury);
                    int lovinCount = GetLovinCount(p);

                    if (injured || (lovinCount >= 2 && Rand.Chance(0.2f)))
                    {
                        List<Pawn> concubines = GetAvailableConcubines(p, true);
                        if (concubines.Count >= 2)
                        {
                            concubines.SortByDescending(x => x.health.hediffSet.HasHediff(SRI_Main.Hediff_HeadConcubine));
                            StartHealingTouch(p, concubines[0], concubines[1]);
                            continue;
                        }
                    }
                }

                if (GetLovinCount(p) < 2)
                {
                    List<Pawn> concubines = GetAvailableConcubines(p, false);
                    if (concubines.Count > 0)
                    {
                        Pawn c = concubines.FirstOrDefault(x => x.health.hediffSet.HasHediff(SRI_Main.Hediff_HeadConcubine)) ?? concubines.RandomElement();
                        if (p.InBed() || (p.CurJob != null && p.CurJob.def == JobDefOf.Wait_MaintainPosture))
                            StartAutoLovin(p, c);
                    }
                }
            }
        }

        private List<Pawn> GetAvailableConcubines(Pawn master, bool requireStockholm)
        {
            List<Pawn> result = new List<Pawn>();
            foreach (Pawn p in master.Map.mapPawns.SlavesOfColonySpawned.ToList())
            {
                if (!IsConcubineOf(p, master)) continue;
                if (p.Downed || p.Dead || p.Drafted) continue;
                if (p.CurJobDef == SRI_Main.Job_ConcubineLovin || p.CurJobDef == SRI_Main.Job_HealingTouch) continue;
                if (requireStockholm && !p.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm)) continue;
                result.Add(p);
            }
            return result;
        }

        private void StartAutoLovin(Pawn master, Pawn slave)
        {
            Job j = JobMaker.MakeJob(SRI_Main.Job_ConcubineLovin, master);
            slave.jobs.TryTakeOrderedJob(j, JobTag.Misc);
            IncrementLovinCount(master);
        }

        private void StartHealingTouch(Pawn master, Pawn c1, Pawn c2)
        {
            c1.jobs.TryTakeOrderedJob(JobMaker.MakeJob(SRI_Main.Job_HealingTouch, master, c2), JobTag.Misc);
            c2.jobs.TryTakeOrderedJob(JobMaker.MakeJob(SRI_Main.Job_HealingTouch, master, c1), JobTag.Misc);
            Messages.Message("Healing Touch ritual begins!", master, MessageTypeDefOf.PositiveEvent, true);
        }

        // ==================== SAVE/LOAD ====================

        public override void ExposeData()
        {
            base.ExposeData();
            
            // Existing
            List<int> hybridList = hybridBedIDs.ToList();
            Scribe_Collections.Look(ref hybridList, "SRI_Hybrid", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && hybridList != null)
                hybridBedIDs = new HashSet<int>(hybridList);
            
            Scribe_Collections.Look(ref punishmentCooldowns, "SRI_Cool", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref slaveToMasterMap, "SRI_Map", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref dailyLovinCount, "SRI_Daily", LookMode.Value, LookMode.Value);
            
            // Ability cooldowns
            Scribe_Collections.Look(ref smiteCooldowns, "SRI_SmiteCool", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref massCalmCooldowns, "SRI_CalmCool", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref blessingCooldowns, "SRI_BlessCool", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref wrathCooldowns, "SRI_WrathCool", LookMode.Value, LookMode.Value);
            
            // Shield cooldowns
            Scribe_Collections.Look(ref shieldCooldowns, "SRI_ShieldCool", LookMode.Value, LookMode.Value);
            
            // Resurrection
            Scribe_Collections.Look(ref godDeathTicks, "SRI_GodDeath", LookMode.Value, LookMode.Value);
            
            // Initialize nulls
            if (punishmentCooldowns == null) punishmentCooldowns = new Dictionary<int, int>();
            if (slaveToMasterMap == null) slaveToMasterMap = new Dictionary<int, int>();
            if (dailyLovinCount == null) dailyLovinCount = new Dictionary<int, int>();
            if (smiteCooldowns == null) smiteCooldowns = new Dictionary<int, int>();
            if (massCalmCooldowns == null) massCalmCooldowns = new Dictionary<int, int>();
            if (blessingCooldowns == null) blessingCooldowns = new Dictionary<int, int>();
            if (wrathCooldowns == null) wrathCooldowns = new Dictionary<int, int>();
            if (shieldCooldowns == null) shieldCooldowns = new Dictionary<int, int>();
            if (godDeathTicks == null) godDeathTicks = new Dictionary<int, int>();
            if (gatheredCorpses == null) gatheredCorpses = new Dictionary<int, List<Thing>>();
        }
    }

    #endregion

    // ======================================================================
    // SCENARIO PART
    // ======================================================================

    #region Scenario Part

    public class ScenPart_GodSetup : ScenPart
    {
        public ScenPart_GodSetup() { }

        public override string Summary(Scenario scen) => "A reincarnated god with a devoted harem.";

        public override IEnumerable<string> GetSummaryListEntries(string tag)
        {
            if (tag == "MapScenario")
                yield return "Reincarnated God starts with 4 devoted slaves";
        }

        public override void PostGameStart()
        {
            base.PostGameStart();

            try
            {
                List<Pawn> colonists = Find.GameInitData.startingAndOptionalPawns.Take(5).ToList();
                if (colonists.Count == 0)
                {
                    Log.Error("[SRI] No colonists found in PostGameStart!");
                    return;
                }

                // Setup the God
                Pawn god = colonists[0];
                SetupGod(god);

                // Setup the Harem
                for (int i = 1; i < colonists.Count; i++)
                {
                    SetupConcubine(colonists[i], god);
                }

                // Create Harem Cult ideology if Ideology is active
                if (ModsConfig.IdeologyActive)
                {
                    CreateHaremCultIdeology(god, colonists);
                }

                Messages.Message("The God has descended with his harem!", god, MessageTypeDefOf.PositiveEvent, true);
                Log.Message("[SRI] God setup completed successfully");
            }
            catch (Exception e)
            {
                Log.Error("[SRI] Error in ScenPart_GodSetup.PostGameStart: " + e.ToString());
            }
        }

        private void SetupGod(Pawn god)
        {
            god.gender = Gender.Male;

            if (god.story != null)
            {
                // Remove Asexual trait
                TraitDef asexual = TraitDefOf.Asexual;
                if (asexual != null && god.story.traits.HasTrait(asexual))
                {
                    Trait asexualTrait = god.story.traits.GetTrait(asexual);
                    god.story.traits.RemoveTrait(asexualTrait);
                }

                // Make room for new traits
                while (god.story.traits.allTraits.Count >= 3)
                    god.story.traits.allTraits.RemoveAt(0);

                // Add Reincarnated God trait
                god.story.traits.GainTrait(new Trait(SRI_Main.Trait_ReincarnatedGod));

                // Add Beauty (Handsome = degree 2)
                TraitDef beauty = TraitDef.Named("Beauty");
                if (beauty != null && !god.story.traits.HasTrait(beauty))
                    god.story.traits.GainTrait(new Trait(beauty, 2));
            }

            // Set minimum skill levels
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

                god.skills.GetSkill(SkillDefOf.Social).Level = Mathf.Max(12, god.skills.GetSkill(SkillDefOf.Social).Level);
                god.skills.GetSkill(SkillDefOf.Intellectual).Level = Mathf.Max(10, god.skills.GetSkill(SkillDefOf.Intellectual).Level);
                god.skills.GetSkill(SkillDefOf.Melee).Level = Mathf.Max(10, god.skills.GetSkill(SkillDefOf.Melee).Level);
            }

            // Add Divine Power hediff
            god.health.AddHediff(SRI_Main.Hediff_DivinePower);
        }

        private void SetupConcubine(Pawn slave, Pawn god)
        {
            slave.gender = Gender.Female;
            slave.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
            slave.health.AddHediff(SRI_Main.Hediff_Stockholm);
            slave.health.AddHediff(SRI_Main.Hediff_Devotion);

            // Give starting devotion boost
            Hediff devotion = slave.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion);
            if (devotion != null)
                devotion.Severity = 0.20f; // Start at Tier 1

            if (SRI_GameComponent.Instance != null)
                SRI_GameComponent.Instance.SetConcubine(slave, god);
        }

        private void CreateHaremCultIdeology(Pawn god, List<Pawn> colonists)
        {
            // Note: Creating a full ideology programmatically is very complex.
            // The player should select an appropriate ideology during game setup.
            // We just ensure all starting pawns share the same ideology.
            try
            {
                if (!ModsConfig.IdeologyActive) return;
                
                Ideo playerIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (playerIdeo == null)
                {
                    Log.Warning("[SRI] No player ideology found, skipping ideology setup");
                    return;
                }

                // Assign player ideology to all colonists with high certainty
                foreach (Pawn p in colonists)
                {
                    if (p.ideo != null)
                    {
                        p.ideo.SetIdeo(playerIdeo);
                        p.ideo.OffsetCertainty(0.8f);
                    }
                }

                Log.Message("[SRI] All colonists assigned to ideology: " + playerIdeo.name);
            }
            catch (Exception e)
            {
                Log.Warning("[SRI] Could not setup ideology: " + e.Message);
            }
        }
    }

    #endregion

    // ======================================================================
    // JOB DRIVERS
    // ======================================================================

    #region Job Drivers

    public class JobDriver_SlaveLovin : JobDriver
    {
        private Pawn Partner => (Pawn)TargetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil sync = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            sync.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Partner);
                if (pawn.Position.InHorDistOf(Partner.Position, 2.0f) && Partner.CurJobDef == SRI_Main.Job_SlaveLovin)
                    ReadyForNextToil();
                if (pawn.IsHashIntervalTick(60) && (Partner.Dead || Partner.Downed ||
                    (!Partner.CurJobDef.Equals(SRI_Main.Job_SlaveLovin) && !Partner.Drafted && Partner.CurJobDef != JobDefOf.Goto)))
                    EndJobWith(JobCondition.Incompletable);
            };
            yield return sync;

            Toil lovin = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 500 };
            lovin.initAction = delegate { FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            lovin.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Partner);
                if (pawn.IsHashIntervalTick(100))
                {
                    if (Rand.Chance(0.6f))
                        FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                    else
                        MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0, 0, 1), pawn.Map, "❤", Color.magenta);
                }
            };
            lovin.AddFinishAction(delegate
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, Partner);

                // Boost devotion
                Hediff devotion = pawn.IsSlaveOfColony ?
                    pawn.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion) :
                    Partner.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion);

                if (devotion is Hediff_Devotion dev)
                    dev.AddDevotion(0.05f);

                // Chance for Stockholm
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

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil lovin = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            lovin.initAction = delegate { FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart); };
            lovin.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Master);
                if (pawn.IsHashIntervalTick(100))
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
            };
            lovin.AddFinishAction(delegate
            {
                // Boost devotion
                Hediff devotion = pawn.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion);
                if (devotion is Hediff_Devotion dev)
                    dev.AddDevotion(0.05f);

                // Mood effect based on Stockholm
                if (pawn.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm))
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_SatisfiedByMaster, Master);
                    return;
                }

                float score = 1.0f;
                TraitDef beauty = TraitDef.Named("Beauty");
                if (beauty != null && Master.story.traits.HasTrait(beauty))
                    score += Master.story.traits.DegreeOfTrait(beauty) * 0.5f;
                if (Master.skills.GetSkill(SkillDefOf.Social).Level > 5)
                    score += (Master.skills.GetSkill(SkillDefOf.Social).Level - 5) * 0.1f;
                if (pawn.relations.OpinionOf(Master) < -20)
                    score -= 2.0f;

                if (score >= 1.0f)
                    pawn.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_SatisfiedByMaster, Master);
                else
                    pawn.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_ForcedAffection, Master);
            });
            yield return lovin;
        }
    }

    public class JobDriver_HealingTouch : JobDriver
    {
        private Pawn Master => (Pawn)TargetA.Thing;
        private Pawn Partner => (Pawn)TargetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil sync = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            sync.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Master);
                if (Partner.Position.InHorDistOf(pawn.Position, 5f) && Partner.CurJobDef == SRI_Main.Job_HealingTouch)
                    ReadyForNextToil();
                if (pawn.IsHashIntervalTick(100) && (Partner.Dead || Partner.Downed || Partner.CurJobDef != SRI_Main.Job_HealingTouch))
                    EndJobWith(JobCondition.Incompletable);
            };
            yield return sync;

            Toil ritual = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 2500 };
            ritual.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Master);
                if (pawn.IsHashIntervalTick(200))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Healing...", Color.green);
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }
            };
            ritual.AddFinishAction(delegate
            {
                if (!Master.health.hediffSet.HasHediff(SRI_Main.Hediff_HealingTouch))
                {
                    Master.health.AddHediff(SRI_Main.Hediff_HealingTouch);
                    Messages.Message(Master.LabelShort + " received Healing Touch!", Master, MessageTypeDefOf.PositiveEvent, true);
                }

                // Boost devotion for performers
                Hediff devotion = pawn.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion);
                if (devotion is Hediff_Devotion dev)
                    dev.AddDevotion(0.03f);
            });
            yield return ritual;
        }
    }

    public class JobDriver_PunishSlave : JobDriver
    {
        private Pawn Victim => (Pawn)TargetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil punish = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 300 };
            punish.initAction = delegate { Messages.Message(pawn.LabelShort + " is punishing " + Victim.LabelShort, pawn, MessageTypeDefOf.NeutralEvent, false); };
            punish.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Victim);
                if (pawn.IsHashIntervalTick(45))
                {
                    pawn.Drawer.Notify_MeleeAttackOn(Victim);
                    SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(Victim.Position, Victim.Map));
                    FleckMaker.ThrowMicroSparks(Victim.DrawPos, Victim.Map);
                    Victim.Drawer.Notify_DamageApplied(new DamageInfo(DamageDefOf.Blunt, 1));
                }
            };
            punish.AddFinishAction(delegate
            {
                if (SRI_GameComponent.Instance != null)
                    SRI_GameComponent.Instance.SetPunishCooldown(Victim, Find.TickManager.TicksGame + 120000);

                Victim.health.AddHediff(SRI_Main.Hediff_PunishedPain);

                if (Victim.guest != null && Victim.IsPrisoner && Victim.guest.will > 0)
                {
                    Victim.guest.will = Mathf.Max(0, Victim.guest.will - 5.0f);
                    Messages.Message(Victim.LabelShort + " will broken (-5)", Victim, MessageTypeDefOf.PositiveEvent, true);
                }

                if (Victim.IsSlaveOfColony)
                {
                    Need_Suppression sup = Victim.needs.TryGetNeed<Need_Suppression>();
                    if (sup != null) sup.CurLevel = 1.0f;
                }

                // Fear effect on observers
                foreach (Pawn obs in Victim.Map.mapPawns.AllPawnsSpawned.ToList().Where(p =>
                    p != Victim && p != pawn && p.Position.InHorDistOf(Victim.Position, 10f) &&
                    (p.IsSlaveOfColony || p.IsPrisoner)))
                {
                    if (obs.IsPrisoner && obs.guest != null)
                        obs.guest.will = Mathf.Max(0, obs.guest.will - 0.5f);
                    else if (obs.IsSlaveOfColony)
                    {
                        Need_Suppression obsSup = obs.needs.TryGetNeed<Need_Suppression>();
                        if (obsSup != null) obsSup.CurLevel += 0.3f;
                    }
                    MoteMaker.ThrowText(obs.DrawPos, obs.Map, "Fear!", Color.red);
                }
            });
            yield return punish;
        }
    }

    public class JobDriver_Procure : JobDriver
    {
        private Pawn Victim => (Pawn)TargetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil procure = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 400 };
            procure.initAction = delegate { Messages.Message(pawn.LabelShort + " procuring " + Victim.LabelShort, pawn, MessageTypeDefOf.NeutralEvent, true); };
            procure.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Victim);
                if (pawn.IsHashIntervalTick(100))
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
            };
            procure.AddFinishAction(delegate
            {
                float chance = 0.10f + (pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f);
                if (Rand.Chance(Mathf.Clamp01(chance)))
                {
                    if (Victim.guest != null)
                    {
                        Building_Bed bed = null;
                        foreach (var b in Victim.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
                        {
                            if (b.ForPrisoners && !b.Medical && !b.Destroyed && !b.IsBurning() &&
                                b.AnyUnownedSleepingSlot && b.CurOccupants.EnumerableCount() < b.SleepingSlotsCount)
                            {
                                bed = b;
                                break;
                            }
                        }

                        if (bed != null)
                        {
                            Victim.SetFaction(Faction.OfPlayer);
                            Victim.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                            Victim.ownership.ClaimBedIfNonMedical(bed);

                            Job gotoBed = JobMaker.MakeJob(JobDefOf.Goto, bed);
                            Job sleep = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                            Victim.jobs.StartJob(gotoBed, JobCondition.InterruptForced);
                            Victim.jobs.jobQueue.EnqueueFirst(sleep);

                            Messages.Message("Procured!", Victim, MessageTypeDefOf.PositiveEvent, true);
                            MoteMaker.ThrowText(Victim.DrawPos, Victim.Map, "Procured!", Color.green);
                        }
                        else
                        {
                            Messages.Message("No Bed!", MessageTypeDefOf.CautionInput, false);
                        }
                    }
                }
                else
                {
                    Messages.Message("Refused.", Victim, MessageTypeDefOf.NegativeEvent, true);
                    MoteMaker.ThrowText(Victim.DrawPos, Victim.Map, "Refused", Color.red);
                }
            });
            yield return procure;
        }
    }

    public class JobDriver_DivineSmite : JobDriver
    {
        private LocalTargetInfo Target => job.targetA;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil channel = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, defaultDuration = 60 };
            channel.initAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Target);
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Divine Smite!", new Color(1f, 0.9f, 0.2f));
            };
            channel.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Target);
            };
            channel.AddFinishAction(delegate
            {
                // Calculate damage
                float damage = SRI_Main.CalculateAbilityPower(pawn, 20f, 5f);

                // Visual effect
                FleckMaker.Static(Target.Cell, pawn.Map, FleckDefOf.PsycastAreaEffect, 3f);
                SoundDefOf.Thunder_OffMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(Target.Cell, pawn.Map)));

                // Deal damage
                if (Target.Thing is Pawn targetPawn)
                {
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Burn, damage, 0f, -1f, pawn);
                    targetPawn.TakeDamage(dinfo);
                }

                // Set cooldown
                if (SRI_GameComponent.Instance != null)
                    SRI_GameComponent.Instance.SetAbilityCooldown(pawn, "Smite", SRI_Main.COOLDOWN_SMITE);

                // Thought to witnesses
                foreach (Pawn slave in pawn.Map.mapPawns.SlavesOfColonySpawned.ToList())
                {
                    if (slave.Position.InHorDistOf(Target.Cell, 20f))
                    {
                        slave.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_WitnessedDivineWrath);

                        Hediff devotion = slave.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion);
                        if (devotion is Hediff_Devotion dev)
                            dev.AddDevotion(0.02f);
                    }
                }
            });
            yield return channel;
        }
    }

    public class JobDriver_ResurrectionRitual : JobDriver
    {
        private Corpse GodCorpse => (Corpse)TargetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil ritual = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = SRI_Main.RESURRECTION_RITUAL_DURATION
            };
            ritual.initAction = delegate
            {
                Messages.Message(pawn.LabelShort + " begins the resurrection ritual...", pawn, MessageTypeDefOf.PositiveEvent, true);
            };
            ritual.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(GodCorpse);
                if (pawn.IsHashIntervalTick(500))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Resurrecting...", new Color(1f, 0.9f, 0.2f));
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }
            };
            ritual.AddFinishAction(delegate
            {
                if (GodCorpse?.InnerPawn != null && SRI_GameComponent.Instance != null)
                {
                    SRI_GameComponent.Instance.CompleteResurrection(GodCorpse.InnerPawn, pawn);
                }
            });
            yield return ritual;
        }
    }

    public class JobDriver_GatherCorpse : JobDriver
    {
        private Thing Corpse => TargetA.Thing;
        private Thing DestinationCorpse => TargetB.Thing; // God's corpse

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            Toil deliver = new Toil();
            deliver.initAction = delegate
            {
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried != null && DestinationCorpse is Corpse godCorpse && godCorpse.InnerPawn != null)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped);
                    if (SRI_GameComponent.Instance != null && dropped != null)
                    {
                        SRI_GameComponent.Instance.AddCorpseForResurrection(godCorpse.InnerPawn, dropped);
                        Messages.Message("Corpse gathered for resurrection ritual.", pawn, MessageTypeDefOf.PositiveEvent, false);
                    }
                }
            };
            yield return deliver;
        }
    }

    #endregion

    // ======================================================================
    // HARMONY PATCHES
    // ======================================================================

    #region Harmony Patches

    // Divine Abilities Gizmos
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_Gizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;

            // God abilities
            if (SRI_Main.IsGod(__instance) && !__instance.Dead && !__instance.Downed)
            {
                foreach (Gizmo gizmo in GetGodAbilityGizmos(__instance))
                    yield return gizmo;
            }

            // Slave concubine assignment
            if (__instance.IsSlaveOfColony && __instance.RaceProps.Humanlike)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Assign Concubine",
                    defaultDesc = "Assign this slave as a concubine to a master.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/ReleaseAnimals", true),
                    action = delegate
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>
                        {
                            new FloatMenuOption("Unassign", delegate
                            {
                                SRI_GameComponent.Instance?.SetConcubine(__instance, null);
                            })
                        };

                        foreach (Pawn colonist in __instance.Map.mapPawns.FreeColonists)
                        {
                            bool isCurrent = SRI_GameComponent.Instance?.IsConcubineOf(__instance, colonist) == true;
                            options.Add(new FloatMenuOption(
                                colonist.LabelShort + (isCurrent ? " (Current)" : ""),
                                delegate { SRI_GameComponent.Instance?.SetConcubine(__instance, colonist); }
                            ));
                        }

                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                };

                // Show devotion info
                int tier = SRI_Main.GetDevotionTier(__instance);
                yield return new Command_Action
                {
                    defaultLabel = "Devotion: " + SRI_Main.GetDevotionTierName(tier),
                    defaultDesc = "Current devotion tier. Higher tiers provide better buffs when near master.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Draft", true),
                    action = delegate { }
                };
            }
        }

        private static IEnumerable<Gizmo> GetGodAbilityGizmos(Pawn god)
        {
            SRI_GameComponent gc = SRI_GameComponent.Instance;
            if (gc == null) yield break;

            // Divine Smite
            int smiteRemaining = gc.GetAbilityCooldownRemaining(god, "Smite");
            Command_Target smiteCmd = new Command_Target
            {
                defaultLabel = smiteRemaining > 0 ? $"Smite ({smiteRemaining / 2500f:F1}h)" : "Divine Smite",
                defaultDesc = $"Strike a target with divine lightning.\nDamage: {SRI_Main.CalculateAbilityPower(god, 20f, 5f):F0}",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
                targetingParams = new TargetingParameters { canTargetPawns = true, canTargetBuildings = false },
                action = delegate (LocalTargetInfo target)
                {
                    Job j = JobMaker.MakeJob(SRI_Main.Job_DivineSmite, target);
                    god.jobs.TryTakeOrderedJob(j, JobTag.Misc);
                }
            };
            if (smiteRemaining > 0) smiteCmd.Disable("On cooldown");
            yield return smiteCmd;

            // Mass Calm
            int calmRemaining = gc.GetAbilityCooldownRemaining(god, "MassCalm");
            float calmRadius = SRI_Main.CalculateAbilityPower(god, 10f, 2f);
            Command_Action calmCmd = new Command_Action
            {
                defaultLabel = calmRemaining > 0 ? $"Calm ({calmRemaining / 2500f:F1}h)" : "Mass Calm",
                defaultDesc = $"Calm all slaves in radius, removing mental breaks.\nRadius: {calmRadius:F0} tiles",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt", true),
                action = delegate
                {
                    int calmed = 0;
                    foreach (Pawn slave in god.Map.mapPawns.SlavesOfColonySpawned.ToList())
                    {
                        if (slave.Position.InHorDistOf(god.Position, calmRadius))
                        {
                            if (slave.InMentalState)
                                slave.MentalState.RecoverFromState();

                            slave.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.ArtifactMoodBoost);
                            FleckMaker.ThrowMetaIcon(slave.Position, slave.Map, FleckDefOf.Heart);
                            calmed++;
                        }
                    }
                    gc.SetAbilityCooldown(god, "MassCalm", SRI_Main.COOLDOWN_MASS_CALM);
                    Messages.Message($"Calmed {calmed} slaves.", god, MessageTypeDefOf.PositiveEvent, true);
                }
            };
            if (calmRemaining > 0) calmCmd.Disable("On cooldown");
            yield return calmCmd;

            // Divine Blessing
            int blessRemaining = gc.GetAbilityCooldownRemaining(god, "Blessing");
            Command_Target blessCmd = new Command_Target
            {
                defaultLabel = blessRemaining > 0 ? $"Bless ({blessRemaining / 2500f:F1}h)" : "Divine Blessing",
                defaultDesc = "Grant a powerful blessing to a pawn, boosting their abilities.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/ReleaseAnimals", true),
                targetingParams = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = false,
                    validator = (TargetInfo t) => t.Thing is Pawn p && p.Faction == Faction.OfPlayer
                },
                action = delegate (LocalTargetInfo target)
                {
                    if (target.Thing is Pawn targetPawn)
                    {
                        targetPawn.health.AddHediff(SRI_Main.Hediff_DivineBlessing);
                        targetPawn.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_ReceivedBlessing, god);

                        if (targetPawn.IsSlaveOfColony)
                        {
                            Hediff devotion = targetPawn.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion);
                            if (devotion is Hediff_Devotion dev)
                                dev.AddDevotion(0.05f);
                        }

                        FleckMaker.Static(targetPawn.Position, targetPawn.Map, FleckDefOf.PsycastAreaEffect, 2f);
                        gc.SetAbilityCooldown(god, "Blessing", SRI_Main.COOLDOWN_BLESSING);
                        Messages.Message($"{targetPawn.LabelShort} received divine blessing!", targetPawn, MessageTypeDefOf.PositiveEvent, true);
                    }
                }
            };
            if (blessRemaining > 0) blessCmd.Disable("On cooldown");
            yield return blessCmd;

            // Wrath of God
            int wrathRemaining = gc.GetAbilityCooldownRemaining(god, "Wrath");
            float wrathDamage = SRI_Main.CalculateAbilityPower(god, 15f, 3f);
            Command_Action wrathCmd = new Command_Action
            {
                defaultLabel = wrathRemaining > 0 ? $"Wrath ({wrathRemaining / 2500f:F1}h)" : "Divine Wrath",
                defaultDesc = $"Unleash devastating AoE damage around the God.\nDamage: {wrathDamage:F0} in 8 tile radius",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/FireAtWill", true),
                action = delegate
                {
                    // Visual
                    FleckMaker.Static(god.Position, god.Map, FleckDefOf.PsycastAreaEffect, 8f);
                    SoundDefOf.Thunder_OffMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(god.Position, god.Map)));

                    // Damage enemies
                    int hit = 0;
                    foreach (Pawn target in god.Map.mapPawns.AllPawnsSpawned.ToList())
                    {
                        if (target == god) continue;
                        if (!target.Position.InHorDistOf(god.Position, 8f)) continue;
                        if (!target.HostileTo(god)) continue;

                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Burn, wrathDamage, 0f, -1f, god);
                        target.TakeDamage(dinfo);
                        hit++;
                    }

                    gc.SetAbilityCooldown(god, "Wrath", SRI_Main.COOLDOWN_WRATH);
                    MoteMaker.ThrowText(god.DrawPos, god.Map, "DIVINE WRATH!", Color.yellow);

                    // Boost devotion of witnesses
                    foreach (Pawn slave in god.Map.mapPawns.SlavesOfColonySpawned.ToList())
                    {
                        if (slave.Position.InHorDistOf(god.Position, 20f))
                        {
                            slave.needs.mood.thoughts.memories.TryGainMemory(SRI_Main.Thought_WitnessedDivineWrath);
                            Hediff devotion = slave.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_Devotion);
                            if (devotion is Hediff_Devotion dev)
                                dev.AddDevotion(0.02f);
                        }
                    }
                }
            };
            if (wrathRemaining > 0) wrathCmd.Disable("On cooldown");
            yield return wrathCmd;
        }
    }

    // Float menu options
    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    public static class Patch_FloatMenuMakerMap
    {
        public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            if (!pawn.IsColonistPlayerControlled) return;

            foreach (LocalTargetInfo t in GenUI.TargetsAt(clickPos, TargetingParameters.ForPawns(), true))
            {
                Pawn v = t.Pawn;
                if (v == null || v == pawn) continue;

                // Slave lovin
                if (v.IsSlaveOfColony)
                {
                    opts.Add(new FloatMenuOption("Take " + v.LabelShort + " to bed (Lovin')", delegate
                    {
                        StartLovinScene(pawn, v);
                    }));
                }

                // Punish
                if (v.IsSlaveOfColony || v.IsPrisonerOfColony)
                {
                    int cooldown = (SRI_GameComponent.Instance?.GetPunishUnlockTick(v) ?? 0) - Find.TickManager.TicksGame;
                    if (cooldown > 0)
                    {
                        FloatMenuOption disabled = new FloatMenuOption($"Punish (Cooldown {cooldown / 2500}h)", null);
                        disabled.Disabled = true;
                        opts.Add(disabled);
                    }
                    else
                    {
                        opts.Add(new FloatMenuOption("Punish " + v.LabelShort, delegate
                        {
                            StartPunishment(pawn, v);
                        }));
                    }
                }

                // Procure
                if (!v.IsPrisoner && !v.IsSlave && !v.IsColonist && v.RaceProps.Humanlike && !v.HostileTo(pawn))
                {
                    bool hasBed = pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                        .Any(b => b.ForPrisoners && !b.Medical && b.AnyUnownedSleepingSlot);

                    if (!hasBed)
                    {
                        FloatMenuOption disabled = new FloatMenuOption("Procure (No Bed)", null);
                        disabled.Disabled = true;
                        opts.Add(disabled);
                    }
                    else
                    {
                        float chance = Mathf.Clamp01(0.10f + (pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.045f)) * 100f;
                        opts.Add(new FloatMenuOption($"Procure {v.LabelShort} ({chance:F0}%)", delegate
                        {
                            StartProcure(pawn, v);
                        }));
                    }
                }

                // Resurrection (for devoted slaves near dead god)
                if (v.Dead && SRI_Main.IsGod(v) && pawn.IsSlaveOfColony && SRI_Main.GetDevotionTier(pawn) >= 3)
                {
                    if (SRI_GameComponent.Instance?.CanResurrect(v) == true)
                    {
                        opts.Add(new FloatMenuOption("Begin Resurrection Ritual", delegate
                        {
                            Job j = JobMaker.MakeJob(SRI_Main.Job_ResurrectionRitual, v.Corpse);
                            pawn.jobs.TryTakeOrderedJob(j, JobTag.Misc);
                        }));
                    }
                    else
                    {
                        opts.Add(new FloatMenuOption($"Gather corpses for resurrection (need {SRI_Main.MIN_CORPSES_FOR_RESURRECTION})", delegate
                        {
                            // Find nearest corpse
                            Corpse corpse = (Corpse)GenClosest.ClosestThing_Global(pawn.Position,
                                pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse),
                                50f, c => c != v.Corpse);

                            if (corpse != null)
                            {
                                Job j = JobMaker.MakeJob(SRI_Main.Job_GatherCorpse, corpse, v.Corpse);
                                pawn.jobs.TryTakeOrderedJob(j, JobTag.Misc);
                            }
                        }));
                    }
                }
            }
        }

        static void StartLovinScene(Pawn m, Pawn s)
        {
            if (m.Drafted) m.drafter.Drafted = false;
            if (s.Drafted) s.drafter.Drafted = false;
            m.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            s.jobs.EndCurrentJob(JobCondition.InterruptForced, true);

            Building_Bed bed = (Building_Bed)GenClosest.ClosestThing_Global(m.Position,
                m.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>(), 9999f,
                x => ((Building_Bed)x).SleepingSlotsCount >= 2 && !((Building_Bed)x).IsBurning());

            if (bed == null)
            {
                Messages.Message("No suitable bed found!", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Job jm = JobMaker.MakeJob(SRI_Main.Job_SlaveLovin, bed, s);
            jm.playerForced = true;
            m.jobs.TryTakeOrderedJob(jm, JobTag.Misc);

            Job js = JobMaker.MakeJob(SRI_Main.Job_SlaveLovin, bed, m);
            js.playerForced = true;
            s.jobs.TryTakeOrderedJob(js, JobTag.Misc);
        }

        static void StartPunishment(Pawn m, Pawn v)
        {
            if (m.Drafted) m.drafter.Drafted = false;
            Job j = JobMaker.MakeJob(SRI_Main.Job_PunishSlave, v);
            j.playerForced = true;
            m.jobs.TryTakeOrderedJob(j, JobTag.Misc);
        }

        static void StartProcure(Pawn m, Pawn v)
        {
            if (m.Drafted) m.drafter.Drafted = false;
            Job j = JobMaker.MakeJob(SRI_Main.Job_Procure, v);
            j.playerForced = true;
            m.jobs.TryTakeOrderedJob(j, JobTag.Misc);
        }
    }

    // Protective Rage + Divine Shield
    [HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
    public static class Patch_InstantRage
    {
        public static void Prefix(Pawn __instance, ref DamageInfo dinfo, out bool __state)
        {
            __state = false;
            if (__instance.IsColonist && !__instance.Dead && dinfo.Instigator != null && dinfo.Instigator != __instance)
                __state = true;
        }

        public static void Postfix(Pawn __instance, ref DamageInfo dinfo, bool __state)
        {
            if (!__state) return;

            bool isEnemy = dinfo.Instigator.HostileTo(__instance) ||
                (dinfo.Instigator is Pawn att && (att.IsPrisoner || att.InAggroMentalState));
            if (!isEnemy && dinfo.Instigator is Pawn animal && animal.RaceProps.Animal)
                isEnemy = true;

            if (!isEnemy) return;

            // Check for Divine Shield first (for Gods)
            if (SRI_Main.IsGod(__instance))
            {
                // Find a Tier 3+ slave with ready shield
                foreach (Pawn slave in __instance.Map.mapPawns.SlavesOfColonySpawned.ToList())
                {
                    if (SRI_GameComponent.Instance?.IsConcubineOf(slave, __instance) != true) continue;
                    if (SRI_Main.GetDevotionTier(slave) < 3) continue;
                    if (!slave.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm)) continue;
                    if (slave.Dead || slave.Downed) continue;
                    if (!slave.Position.InHorDistOf(__instance.Position, 8f)) continue;
                    if (SRI_GameComponent.Instance?.IsShieldReady(slave) != true) continue;

                    // Activate Divine Shield!
                    Hediff_DivineShield shield = (Hediff_DivineShield)slave.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_DivineShield);
                    if (shield == null)
                    {
                        shield = (Hediff_DivineShield)HediffMaker.MakeHediff(SRI_Main.Hediff_DivineShield, slave);
                        shield.protectedMaster = __instance;
                        slave.health.AddHediff(shield);
                    }

                    if (shield.TryBlockAttack(dinfo, out float damageToSlave))
                    {
                        // Block successful!
                        dinfo.SetAmount(0f);

                        // Slave takes reduced damage
                        if (damageToSlave > 0)
                        {
                            DamageInfo slaveDamage = new DamageInfo(dinfo.Def, damageToSlave, 0f, -1f, dinfo.Instigator);
                            slave.TakeDamage(slaveDamage);
                        }

                        SRI_GameComponent.Instance?.SetShieldCooldown(slave);

                        // Remove shield hediff after use
                        slave.health.RemoveHediff(shield);

                        Messages.Message($"{slave.LabelShort} blocked an attack on {__instance.LabelShort}!", slave, MessageTypeDefOf.PositiveEvent, true);

                        // Only one shield per attack
                        break;
                    }
                }
            }

            // Trigger Protective Rage for concubines
            foreach (Pawn slave in __instance.MapHeld.mapPawns.SlavesOfColonySpawned.ToList())
            {
                if (SRI_GameComponent.Instance?.IsConcubineOf(slave, __instance) != true) continue;
                if (!slave.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm)) continue;
                if (slave.Dead || slave.Downed) continue;

                // Already attacking this target?
                if (slave.CurJobDef == JobDefOf.AttackMelee && slave.CurJob.targetA.Thing == dinfo.Instigator) continue;

                // Add/refresh rage
                Hediff rage = slave.health.hediffSet.GetFirstHediffOfDef(SRI_Main.Hediff_ProtectiveRage);
                if (rage != null) slave.health.RemoveHediff(rage);
                slave.health.AddHediff(SRI_Main.Hediff_ProtectiveRage);

                if (rage == null)
                    Messages.Message($"{slave.LabelShort} protects {__instance.LabelShort}!", slave, MessageTypeDefOf.ThreatBig, true);

                // Force attack
                if (slave.Drafted) slave.drafter.Drafted = false;
                slave.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                slave.jobs.ClearQueuedJobs();

                Job atk = JobMaker.MakeJob(JobDefOf.AttackMelee, dinfo.Instigator);
                atk.playerForced = true;
                atk.expiryInterval = 2000;
                atk.killIncappedTarget = true;
                slave.jobs.TryTakeOrderedJob(atk, JobTag.Misc);
            }
        }
    }

    // Track God death for resurrection
    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class Patch_GodDeath
    {
        public static void Postfix(Pawn __instance)
        {
            if (SRI_Main.IsGod(__instance))
            {
                SRI_GameComponent.Instance?.RegisterGodDeath(__instance);
            }
        }
    }

    // Proximity stat bonuses for devoted slaves
    [HarmonyPatch(typeof(StatExtension), "GetStatValue")]
    public static class Patch_DevotionStats
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            if (!(thing is Pawn pawn)) return;

            // God's Divine Power scaling
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

            // Slave's Devotion proximity buffs
            if (pawn.IsSlaveOfColony)
            {
                Pawn master = SRI_GameComponent.Instance?.GetMasterOf(pawn);
                if (master != null && !master.Dead && pawn.Map == master.Map &&
                    pawn.Position.InHorDistOf(master.Position, 15f))
                {
                    int tier = SRI_Main.GetDevotionTier(pawn);
                    if (tier > 0)
                    {
                        if (stat == StatDefOf.WorkSpeedGlobal)
                        {
                            float bonus = tier switch { 1 => 0.05f, 2 => 0.10f, 3 => 0.15f, 4 => 0.25f, _ => 0f };
                            __result += bonus;
                        }
                        else if (stat == StatDefOf.MoveSpeed)
                        {
                            float bonus = tier switch { 1 => 0.05f, 2 => 0.10f, 3 => 0.15f, 4 => 0.20f, _ => 0f };
                            __result *= (1f + bonus);
                        }
                        else if (stat == StatDefOf.MeleeDamageFactor)
                        {
                            float bonus = tier switch { 1 => 0.05f, 2 => 0.10f, 3 => 0.20f, 4 => 0.30f, _ => 0f };
                            __result += bonus;
                        }
                        else if (stat == StatDefOf.MeleeDodgeChance)
                        {
                            float bonus = tier switch { 1 => 0f, 2 => 0.05f, 3 => 0.10f, 4 => 0.15f, _ => 0f };
                            __result += bonus;
                        }
                    }
                }
            }
        }
    }

    // Stockholm prevents suppression decay
    [HarmonyPatch(typeof(Need_Suppression), "NeedInterval")]
    public static class Patch_Suppression_Stockholm
    {
        public static bool Prefix(Need_Suppression __instance, Pawn ___pawn)
        {
            if (___pawn.health.hediffSet.HasHediff(SRI_Main.Hediff_Stockholm))
            {
                __instance.CurLevel = __instance.MaxLevel;
                return false;
            }
            return true;
        }
    }

    // God can't use ranged weapons
    [HarmonyPatch]
    public static class Patch_GodWeapons
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EquipmentUtility), "CanEquip",
                new Type[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) });
        }

        public static bool Prefix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (SRI_Main.IsGod(pawn) && thing.def.IsRangedWeapon)
            {
                cantReason = "The God refuses to use such weapons.";
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Bullet reflection
    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_Projectile_Impact
    {
        public static bool Prefix(Projectile __instance, Thing hitThing)
        {
            if (hitThing is Pawn pawn && SRI_Main.IsGod(pawn))
            {
                int slaveCount = pawn.Map?.mapPawns.SlavesOfColonySpawned.Count ?? 0;
                float chance = Mathf.Min(0.20f + (slaveCount * 0.05f), 0.80f);

                if (Rand.Chance(chance))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "REFLECT!", Color.yellow);
                    SoundDefOf.MetalHitImportant.PlayOneShot(SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map)));

                    Thing launcher = __instance.Launcher;
                    if (launcher is Pawn shooter && !shooter.Dead)
                    {
                        float dmg = (float)__instance.def.projectile.GetDamageAmount(null);
                        DamageInfo dinfo = new DamageInfo(__instance.def.projectile.damageDef, dmg, 0f, -1f, pawn);
                        shooter.TakeDamage(dinfo);
                        MoteMaker.ThrowText(shooter.DrawPos, shooter.Map, "Karma!", Color.red);
                    }
                    return false;
                }
            }
            return true;
        }
    }

    // Hybrid bed mechanics
    [HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
    public static class Patch_Bed_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance)
        {
            foreach (var g in __result) yield return g;

            if (__instance.Faction == Faction.OfPlayer && __instance.SleepingSlotsCount >= 2)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Master & Slave Mode",
                    defaultDesc = "Allow both colonists and slaves to share this bed.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/AsColonist", true),
                    isActive = () => SRI_GameComponent.Instance?.IsHybrid(__instance) == true,
                    toggleAction = delegate
                    {
                        if (SRI_GameComponent.Instance != null)
                        {
                            bool newState = !SRI_GameComponent.Instance.IsHybrid(__instance);
                            SRI_GameComponent.Instance.SetHybrid(__instance, newState);
                            foreach (Pawn p in __instance.CompAssignableToPawn.AssignedPawns.ToList())
                                __instance.CompAssignableToPawn.TryUnassignPawn(p);
                        }
                    }
                };
            }
        }
    }

    [HarmonyPatch]
    public static class Patch_IsValidBedFor
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(RestUtility))
                .Where(m => m.Name == "IsValidBedFor")
                .OrderByDescending(m => m.GetParameters().Length)
                .First();
        }

        public static bool Prefix(Thing bedThing, Pawn sleeper, ref bool __result)
        {
            if (bedThing is Building_Bed bed && SRI_GameComponent.Instance?.IsHybrid(bed) == true)
            {
                if ((sleeper.IsSlaveOfColony || sleeper.IsColonist) && !bed.Destroyed && !bed.IsBurning())
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ForbidUtility), "IsForbidden", new Type[] { typeof(Thing), typeof(Pawn) })]
    public static class Patch_IsForbidden
    {
        public static bool Prefix(Thing t, Pawn pawn, ref bool __result)
        {
            if (pawn.IsSlaveOfColony && t is Building_Bed bed && SRI_GameComponent.Instance?.IsHybrid(bed) == true)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn), "CanAssignTo")]
    public static class Patch_CanAssignTo
    {
        public static void Postfix(CompAssignableToPawn __instance, Pawn pawn, ref AcceptanceReport __result)
        {
            if (!__result.Accepted && __instance.parent is Building_Bed bed && SRI_GameComponent.Instance?.IsHybrid(bed) == true)
            {
                if (pawn.IsSlaveOfColony || pawn.IsColonist)
                    __result = AcceptanceReport.WasAccepted;
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
                Building_Bed bed1 = pawn1.ownership.OwnedBed;
                Building_Bed bed2 = pawn2.ownership.OwnedBed;
                if (bed1 != null && bed2 != null && bed1 == bed2 && SRI_GameComponent.Instance?.IsHybrid(bed1) == true)
                    __result = true;
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
                Building_Bed bed1 = first.ownership.OwnedBed;
                Building_Bed bed2 = second.ownership.OwnedBed;
                if (bed1 != null && bed2 != null && bed1 == bed2 && SRI_GameComponent.Instance?.IsHybrid(bed1) == true)
                    __result = true;
            }
        }
    }

    #endregion
}
