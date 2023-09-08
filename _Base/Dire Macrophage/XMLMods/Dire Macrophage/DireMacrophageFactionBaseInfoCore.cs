using Arcen.AIW2.Core;
using Arcen.Universal;
using System;

using System.Text;

namespace Arcen.AIW2.External
{
    public abstract class DireMacrophageFactionBaseInfoCore : ExternalFactionBaseInfoRoot, IAntiMinorFactionWaveDataHolder
    {
        //serialized

        //not serialized
        public int EffectiveIntensity = -1;
        public bool SeedNearPlayer = false;

        public readonly DoubleBufferedList<SafeSquadWrapper> DireTelia = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "DireMacrophage-DireTelia" );
        public readonly DoubleBufferedDictionary<Planet, int> DireTeliaPlanets = DoubleBufferedDictionary<Planet, int>.Create_WillNeverBeGCed( 300, "DireMacrophage-DireTeliaPlanets" );
        public readonly DoubleBufferedList<SafeSquadWrapper> DireHarvesters = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "DireMacrophage-DireHarvesters" );
        public static List<DireMacrophageFactionBaseInfoCore> AllDireMacrophageFactionsOfAnyTypes = List<DireMacrophageFactionBaseInfoCore>.Create_WillNeverBeGCed( 10, "DireMacrophageFactionBaseInfoCore-AllDireMacrophageFactionsOfAnyTypes" );
        public readonly DoubleBufferedList<SafeSquadWrapper> MacrophageBastionsInGalaxy = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed(200, "DireMacrophage-MacrophageBastionsInGalaxy");
        public readonly DoubleBufferedList<SafeSquadWrapper> MacrophageFortificationsInGalaxy = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed(200, "DireMacrophage-MacrophageFortificationsInGalaxy");
        public readonly DoubleBufferedList<SafeSquadWrapper> MacrophageColonisers = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed(200, "DireMacrophage-MacrophageColonisers");


        public bool FinishedLoadingLogic = false; // Whenever we load, recalculate some things.
        public bool aiAllied = false;
        public bool humanAllied = false;
        public FInt MacrophageSpecificAIP;
        public readonly AntiMinorFactionWaveData WaveData = new AntiMinorFactionWaveData();
        public int MaximumFortifications = 0;

        //metal generation and planetary telium caps are done on a subfaction by subfaction basis
        public int DireTeliaPerPlanet = -1;
        public int MetalGenerationPerSecond = -1;

        //let the player know when we're berserk every now and then without spamming
        public int GameSecondForLastMessage;

        #region Constants
        //constants
        public const string DIREMACROPHAGE_TAG = "DireMacrophage";
        public const string DireHarvesterTag = "DireMacrophageHarvester"; // General tag for any Dire Harvester that needs our movement and collection logic.
        public const string RegularDireHarvesterTag = "RegularDireMacrophageHarvester";
        public const string RegularDireCarrierTag = "RegularDireMacrophageCarrier";
        public const string EvolvedDireHarvesterTag = "EvolvedDireMacrophageHarvester"; // General tag for any Evolved Dire Harvester that needs our movement and collection logic.
        public const string RegularEvolvedDireHarvesterTag = "RegularEvolvedDireMacrophageHarvester";
        public const string RegularEvolvedDireCarrierTag = "RegularEvolvedDireMacrophageCarrier";
        public const string DireMacrophageBorer = "DireMacrophageBorer";
        public const string DireTeliumTag = "DireMacrophageTelium";
        public const string CarrierDrone = "MacrophageCarrierDrone";
        public const string DireMacrophageDefenseTag = "DireMacrophageDefense";
        public const string DireMacrophageLesserDefenseTag = "DireMacrophageLesserDefense";
        public const string DireMacrophageConstructor = "DireMacrophageConstructor";
        public const string DireMacrophageColoniser = "DireMacrophageColoniser";
        #endregion

        public static readonly bool debug = false;

        #region From Xml
        //Here are some constants
        private bool hasInitializedConstants = false;
        public int MetalHarvesterCanHold;
        public int DireMacrophageMaxMetalHarvesterCanHoldPerMark;
        public int EarlyHarvesterSpawnTime; //Ordinarily it will take some time for Telia to get enough metal to spawn their first harvester. If we want them to just start out having a harvester early then set this value
        public int MetalForEvent;
        public int MetalForEventPlayerOnlyHostileMode; //this is an override
        public FInt EventCostMultiplierPerHarvesterBase; //multiply the above costs by this for every harvester
        public FInt EventCostMultiplierPerHarvesterDecreasePerIntensity; //lowers the above value by this for every intensity level
        public int MetalGenerationPerSecondLow;
        public int MetalGenerationPerSecondMed;
        public int MetalGenerationPerSecondHigh;
        public FInt MetalGenerationMultiplierWithLivingHarvesters = FInt.One; //we want to decrease the base metal income when the Macrophage has harvesters (since harvesting should be the primary means of income)
        public int HarvesterMetalFromKillMultiplier;
        public int MetalGainedFromVisitingMine;
        public int BasePercentChanceToMarkUp;
        public int ReductionPerMarkForPercentChanceToMarkUp;
        public int MinimumSnackTimeOnSpawn;
        public int MaximumSnackTimeOnSpawn;
        public int MinimumSnackTimeOnNewPlanet;
        public int MaximumSnackTimeOnNewPlanet;
        public int SnackTimeIncreasePerMark;
        public int MinimumBreakTime;
        public int MaximumBreakTime;
        public int BreakTimeIncreasePerMark;
        public int EvolvedDireMacrophageMetalHoldMult;
        public int BaseBastionMarkupCost;
        public int PerMarkBastionMarkupCost;

        private void InitializeConstantsIfNecessary()
        {
            if ( !hasInitializedConstants )
            {
                hasInitializedConstants = true;
                MetalForEvent = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MetalForSporeOrHarvesterSpawn" );
                EventCostMultiplierPerHarvesterBase = ExternalConstants.Instance.GetCustomFInt_Slow( "custom_FInt_DireMacrophage_SpawnCostMultiplierPerOwnedHarvester" );
                EventCostMultiplierPerHarvesterDecreasePerIntensity = ExternalConstants.Instance.GetCustomFInt_Slow( "custom_FInt_DireMacrophage_SpawnCostMultiplierDecreasePerIntensity" );
                MetalHarvesterCanHold = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MaxMetalHarvesterCanHold" );
                DireMacrophageMaxMetalHarvesterCanHoldPerMark = ExternalConstants.Instance.GetCustomInt32_Slow("custom_int_DireMacrophage_MaxMetalHarvesterCanHoldPerMark");
                EarlyHarvesterSpawnTime = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_EarlyHarvesterSpawnTime" );
                MetalGenerationPerSecondLow = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MetalGenerationPerSecondLow" );
                MetalGenerationPerSecondMed = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MetalGenerationPerSecondMed" );
                MetalGenerationPerSecondHigh = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MetalGenerationPerSecondHigh" );
                MetalForEventPlayerOnlyHostileMode = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MetalForSporeOrHarvesterSpawnPlayerOnlyHostileMode" );
                MetalGenerationMultiplierWithLivingHarvesters = ExternalConstants.Instance.GetCustomFInt_Slow( "custom_FInt_DireMacrophage_MetalGenerationMultiplierWithLivingHarvesters" );
                HarvesterMetalFromKillMultiplier = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_HarvesterMetalFromKillMultiplier" );
                MetalGainedFromVisitingMine = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MetalGainedFromVisitingMine" );
                BasePercentChanceToMarkUp = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_BasePercentChanceToMarkUp" );
                ReductionPerMarkForPercentChanceToMarkUp = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_ReductionPerMarkForPercentChanceToMarkUp" );
                MinimumSnackTimeOnSpawn = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MinimumSnackTimeOnSpawn" );
                MaximumSnackTimeOnSpawn = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MaximumSnackTimeOnSpawn" );
                MinimumSnackTimeOnNewPlanet = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MinimumSnackTimeOnNewPlanet" );
                MaximumSnackTimeOnNewPlanet = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MaximumSnackTimeOnNewPlanet" );
                MinimumBreakTime = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MinimumBreakTime" );
                MaximumBreakTime = ExternalConstants.Instance.GetCustomInt32_Slow( "custom_int_DireMacrophage_MaximumBreakTime" );
                BaseBastionMarkupCost = ExternalConstants.Instance.GetCustomInt32_Slow("custom_int_DireMacrophage_BaseBastionMarkupCost");
                PerMarkBastionMarkupCost = ExternalConstants.Instance.GetCustomInt32_Slow("custom_int_DireMacrophage_PerMarkBastionMarkupCost");
                EvolvedDireMacrophageMetalHoldMult = ExternalConstants.Instance.GetCustomInt32_Slow("custom_int_DireMacrophage_EvolvedDireMacrophageMetalHoldMult");
            }
        }
        #endregion


        public DireMacrophageFactionBaseInfoCore()
        {
            Cleanup();
        }

        #region Cleanup
        protected sealed override void Cleanup()
        {
            //serialized

            //not serialized
            SeedNearPlayer = false;
            EffectiveIntensity = -1;
            DireTelia.Clear();
            DireTeliaPlanets.Clear();
            DireHarvesters.Clear();
            MacrophageBastionsInGalaxy.Clear();
            MacrophageFortificationsInGalaxy.Clear();
            MacrophageColonisers.Clear();

            AllDireMacrophageFactionsOfAnyTypes.Clear();

            FinishedLoadingLogic = false;
            aiAllied = false;
            humanAllied = false;
            this.MacrophageSpecificAIP = FInt.Zero;
            this.MaximumFortifications = 0;

            DireTeliaPerPlanet = -1;
            MetalGenerationPerSecond = -1;
            GameSecondForLastMessage = 0;

            WaveData.Cleanup();

            hasInitializedConstants = false; //force reload

            this.SubCleanup();
        }

        protected abstract void SubCleanup();
        #endregion end Cleanup

        public sealed override void SerializeFactionTo( SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            Buffer.AddFInt(MetaData, MacrophageSpecificAIP); //Might fail horribly
            WaveData.SerializeTo(MetaData, Buffer, SerializationCmdType);
            this.SubSerializeFactionTo( MetaData, Buffer, SerializationCmdType );
        }
        protected abstract void SubSerializeFactionTo( SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType );

        public sealed override void DeserializeFactionIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            this.MacrophageSpecificAIP = Buffer.ReadFInt(MetaData); //Might fail horribly
            WaveData.DeserializeIntoSelf(MetaData, Buffer, SerializationCmdType);
            this.SubDeserializeFactionIntoSelf( MetaData, Buffer, SerializationCmdType );
        }
        protected abstract void SubDeserializeFactionIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType );

        protected sealed override void DoFactionGeneralAggregationsPausedOrUnpaused()
        {
            InitializeConstantsIfNecessary();
            DoFinishedLoadingLogicIfNeedBe();

            if ( !AllDireMacrophageFactionsOfAnyTypes.Contains( this ) )
                AllDireMacrophageFactionsOfAnyTypes.Add( this );

            this.SubDoGeneralAggregationsPausedOrUnpaused();
        }
        protected abstract void SubDoGeneralAggregationsPausedOrUnpaused();

        #region DoFinishedLoadingLogicIfNeedBe
        private void DoFinishedLoadingLogicIfNeedBe()
        {
            if ( !FinishedLoadingLogic )
            {
                this.EffectiveIntensity = this.AttachedFaction.GetIntValueForCustomFieldOrDefaultValue( "Intensity", true );

                if ( this.EffectiveIntensity > 7)
                {
                    MetalGenerationPerSecond = MetalGenerationPerSecondHigh;
                }
                else if ( this.EffectiveIntensity > 3 )
                {
                    MetalGenerationPerSecond = MetalGenerationPerSecondMed;
                }
                else
                {
                    MetalGenerationPerSecond = MetalGenerationPerSecondLow;
                }
                FinishedLoadingLogic = true;
                if ( debug )
                    ArcenDebugging.ArcenDebugLogSingleLine( "Finished Loading Logic for Dire Macrophage " + this.AttachedFaction.FactionIndex, Verbosity.DoNotShow );
            }
        }
        #endregion
        /*
        #region WriteTextToSecondLineOfLeftSidebarInLobby
        public override void WriteTextToSecondLineOfLeftSidebarInLobby( ArcenDoubleCharacterBuffer buffer )
        {
            int intensity = this.AttachedFaction.GetIntValueForCustomFieldOrDefaultValue( "Intensity", true );
            
            bool hasAdded = false;
            if ( intensity > 0 )
            {
                hasAdded = true;
                buffer.Add( "Strength: " ).Add( intensity );
            }
            string value = this.Allegiance;
            if ( value != null )
            {
                if ( hasAdded )
                    buffer.Add( "    " );
                else
                    hasAdded = true;
                buffer.Add( "Allegiance: " ).Add( value );
            }
        }
        #endregion
        */
        #region DoPerSecondLogic_Stage1Clearing_OnMainThreadAndPartOfSim_ClientAndHost
        public sealed override void DoPerSecondLogic_Stage1Clearing_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context )
        {

        }
        #endregion

        #region DoPerSecondLogic_Stage2APostAllFactionAggregating_OnMainThreadAndPartOfSim_ClientAndHost
        public sealed override void DoPerSecondLogic_Stage2APostAllFactionAggregating_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context )
        {

        }
        #endregion

        #region DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost
        public sealed override void DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context )
        {
            if ( !FinishedLoadingLogic )
                return; // Do not process if not entirely loaded.

            // Add to our main Macrophage class's statics.

            this.DireTelia.ClearConstructionListForStartingConstruction();
            this.DireTeliaPlanets.ClearConstructionDictForStartingConstruction();
            this.DireHarvesters.ClearConstructionListForStartingConstruction();
            this.MacrophageBastionsInGalaxy.ClearConstructionListForStartingConstruction();
            this.MacrophageFortificationsInGalaxy.ClearConstructionListForStartingConstruction();
            this.MacrophageColonisers.ClearConstructionListForStartingConstruction();

            this.AttachedFaction.DoForEntities( DireTeliumTag, delegate ( GameEntity_Squad entity )
            {
                if ( entity == null )
                    return DelReturn.Continue;
                DireTelia.AddToConstructionList( entity );

                Planet plan = entity.Planet;
                if ( plan == null )
                    return DelReturn.Continue;

                DireTeliaPlanets.Construction[plan]++;
                return DelReturn.Continue;
            } );
            this.AttachedFaction.DoForEntities( DireHarvesterTag, delegate ( GameEntity_Squad entity )
            {
                if ( entity == null )
                    return DelReturn.Continue;
                //Note that the Harvesters list is not constant, since we will remove
                //entries from it and add it to each Telium's list
                this.DireHarvesters.AddToConstructionList( entity );
                return DelReturn.Continue;
            } );
            this.AttachedFaction.DoForEntities(EvolvedDireHarvesterTag, delegate (GameEntity_Squad entity)
            {
                if (entity == null)
                    return DelReturn.Continue;
                //Note that the Harvesters list is not constant, since we will remove
                //entries from it and add it to each Telium's list
                this.DireHarvesters.AddToConstructionList(entity);
                return DelReturn.Continue;
            });
            this.AttachedFaction.DoForEntities(DireMacrophageDefenseTag, delegate (GameEntity_Squad entity)
            {
                if (entity == null)
                    return DelReturn.Continue;
                this.MacrophageBastionsInGalaxy.AddToConstructionList(entity);

                return DelReturn.Continue;
            });
            this.AttachedFaction.DoForEntities(DireMacrophageLesserDefenseTag, delegate (GameEntity_Squad entity)
            {
                if (entity == null)
                    return DelReturn.Continue;
                this.MacrophageFortificationsInGalaxy.AddToConstructionList(entity);

                return DelReturn.Continue;
            });
            this.AttachedFaction.DoForEntities(DireMacrophageColoniser, delegate (GameEntity_Squad entity)
            {
                if (entity == null)
                    return DelReturn.Continue;
                this.MacrophageColonisers.AddToConstructionList(entity);

                return DelReturn.Continue;
            });

            this.DireTelia.SwitchConstructionToDisplay();
            this.DireTeliaPlanets.SwitchConstructionToDisplay();
            this.DireHarvesters.SwitchConstructionToDisplay();
            this.MacrophageBastionsInGalaxy.SwitchConstructionToDisplay();
            this.MacrophageFortificationsInGalaxy.SwitchConstructionToDisplay();
            this.MacrophageColonisers.SwitchConstructionToDisplay();

            this.SubDoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost( Context );

            UpdateAllegiance();
        }
        protected abstract void SubDoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context );
        #endregion end DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost

        #region UpdateAllegiance
        protected virtual void UpdateAllegiance()
        {
            if ( ArcenStrings.Equals( this.Allegiance, "Hostile To All" ) ||
               ArcenStrings.Equals( this.Allegiance, "HostileToAll" ) ||
               string.IsNullOrEmpty( this.Allegiance ) )
            {
                this.humanAllied = false;
                this.aiAllied = false;
                if ( string.IsNullOrEmpty( this.Allegiance ) )
                    throw new Exception( "empty Dire Macrophage allegiance '" + this.Allegiance + "'" );
                if ( debug )
                    ArcenDebugging.ArcenDebugLogSingleLine( "This Dire Macrophage faction should be hostile to all (default)", Verbosity.DoNotShow );
                //make sure this isn't set wrong somehow
                AllegianceHelper.EnemyThisFactionToAll( AttachedFaction );
            }
            else if ( ArcenStrings.Equals( this.Allegiance, "Hostile To Players Only" ) ||
                    ArcenStrings.Equals( this.Allegiance, "HostileToPlayers" ) )
            {
                this.aiAllied = true;
                AllegianceHelper.AllyThisFactionToAI( AttachedFaction );
                if ( debug )
                    ArcenDebugging.ArcenDebugLogSingleLine( "This Dire Macrophage faction should be friendly to the AI and hostile to players", Verbosity.DoNotShow );

            }
            else if ( ArcenStrings.Equals( this.Allegiance, "Minor Faction Team Red" ) )
            {
                if ( debug )
                    ArcenDebugging.ArcenDebugLogSingleLine( "This Dire Macrophage faction is on team red", Verbosity.DoNotShow );
                AllegianceHelper.AllyThisFactionToMinorFactionTeam( AttachedFaction, "Minor Faction Team Red" );
            }
            else if ( ArcenStrings.Equals( this.Allegiance, "Minor Faction Team Blue" ) )
            {
                if ( debug )
                    ArcenDebugging.ArcenDebugLogSingleLine("This Dire Macrophage faction is on team blue", Verbosity.DoNotShow );

                AllegianceHelper.AllyThisFactionToMinorFactionTeam( AttachedFaction, "Minor Faction Team Blue" );
            }
            else if ( ArcenStrings.Equals( this.Allegiance, "Minor Faction Team Green" ) )
            {
                if ( debug )
                    ArcenDebugging.ArcenDebugLogSingleLine("This Dire Macrophage faction is on team green", Verbosity.DoNotShow );

                AllegianceHelper.AllyThisFactionToMinorFactionTeam( AttachedFaction, "Minor Faction Team Green" );
            }

            else if ( ArcenStrings.Equals( this.Allegiance, "HostileToAI" ) ||
                    ArcenStrings.Equals( this.Allegiance, "Friendly To Players" ) )
            {
                this.humanAllied = true;
                AllegianceHelper.AllyThisFactionToHumans( AttachedFaction );
                if ( debug )
                    ArcenDebugging.ArcenDebugLogSingleLine( "This Dire Macrophage faction should be hostile to the AI and friendly to players", Verbosity.DoNotShow );
            }
            else
            {
                throw new Exception( "unknown Dire Macrophage allegiance '" + this.Allegiance + "'" );
            }
        }
        #endregion

        #region GetEventCost
        public int GetEventCost( int harvesterCount )
        {
            // Get base cost based on allegience. If we only hate players, our events are more expensive.
            int cost;
            if ( aiAllied && !humanAllied )
                cost = MetalForEventPlayerOnlyHostileMode;
            else
                cost = MetalForEvent;

            // Multiply cost based on our harvester count, and our defined multiplier, treating Tamed as intensity 10.
            int intensity = this.GetDifficultyOrdinal_OrNegativeOneIfNotRelevant();
            FInt multiplier = EventCostMultiplierPerHarvesterBase - (EventCostMultiplierPerHarvesterDecreasePerIntensity * intensity);
            cost += (cost * (harvesterCount * multiplier)).ToInt();

            return cost;
        }
        #endregion

        //Gets defense costs. Not sure if I really needed a function for that
        #region GetDefenseMarkupCost
        public int GetDefenseMarkupCost(GameEntity_Squad entity)
        {
            // Get base cost based on allegience. If we only hate players, our events are more expensive.
            int cost;
            if (aiAllied && !humanAllied)
                cost = BaseBastionMarkupCost; //might change things later but that specific thing wouldn't really mark up if allied to the AI.
            else
                cost = BaseBastionMarkupCost;

            // Multiply by mark level
            cost += PerMarkBastionMarkupCost * entity.CurrentMarkLevel;

            return cost;
        }
        #endregion

        #region UpdatePowerLevel
        public override void UpdatePowerLevel()
        {
            FInt result = FInt.Zero;
            if(this.DireHarvesters.Count < 4)
            {
                result = FInt.FromParts(0, 00);
            }
            else if (this.DireHarvesters.Count < 10)
            {
                result = FInt.FromParts(0, 100);
            }
            else if (this.DireHarvesters.Count < 20)
            {
                result = FInt.FromParts(0, 250);
            }
            else if (this.DireHarvesters.Count < 40)
            {
                result = FInt.FromParts(0, 500);
            }
            else if (this.DireHarvesters.Count < 60)
            {
                result = FInt.FromParts(1, 000);
            }
            else if (this.DireHarvesters.Count < 100)
            {
                result = FInt.FromParts(1, 500);
            }
            else if (this.DireHarvesters.Count < 140)
            {
                result = FInt.FromParts(2, 000);
            }
            else if (this.DireHarvesters.Count < 200)
            {
                result = FInt.FromParts(2, 500);
            }
            this.AttachedFaction.OverallPowerLevel = result;
            //if ( World_AIW2.Instance.GameSecond % 60 == 0 )
            //    ArcenDebugging.ArcenDebugLogSingleLine("resulting power level: " + faction.OverallPowerLevel, Verbosity.DoNotShow );
        }
        #endregion

        #region GetShouldAttackNormallyExcludedTarget
        public override bool GetShouldAttackNormallyExcludedTarget(GameEntity_Squad Target)
        {
            try
            {
                Faction targetControllingFaction = Target.GetFactionOrNull_Safe();
                //bool planetHasWarpGate = false;
                //targetControllingFaction.DoForEntities( EntityRollupType.WarpEntryPoints, delegate ( GameEntity_Squad entity )
                //{
                //    if ( entity.Planet == Target.Planet )
                //        planetHasWarpGate = true;
                //    return DelReturn.Continue;
                //} );

                if (!ArcenStrings.Equals(this.Allegiance, "Friendly To Players"))
                {
                    //Human Allied factions will optionally leave an AI command station intact, so as to not drive AIP up so high
                    if (Target.TypeData.IsCommandStation)
                    {
                        return true;
                    }
                }
                if (Target.TypeData.GetHasTag("NormalPlanetNastyPick") || Target.TypeData.GetHasTag("DSAA"))
                    return true;

                return false;
            }
            catch (ArcenPleaseStopThisThreadException)
            {
                //this one is ok -- just means the thread is ending for some reason.  I guess we'll skip trying the normal handling here
                return false;
            }
            catch (Exception e)
            {
                ArcenDebugging.ArcenDebugLog("DireMacrophage GetShouldAttackNormallyExcludedTarget error:" + e, Verbosity.ShowAsError);
                return false;
            }
        }
        #endregion

        public AntiMinorFactionWaveData GetAntiMinorFactionWaveData()
        {
            return this.WaveData;
        }
    }
}
