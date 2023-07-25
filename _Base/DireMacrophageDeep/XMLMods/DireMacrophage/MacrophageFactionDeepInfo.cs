using Arcen.AIW2.Core;
using System;
using UnityEngine;
using System.Text;
using Arcen.Universal;

namespace Arcen.AIW2.External
{
    // Regular Macrophage
    public class DireMacrophageFactionDeepInfo : DireMacrophageFactionDeepInfoBase //not sealed since the tamed version needs to inherit from them, too
    {
        public DireMacrophageFactionBaseInfoCore BaseInfo;
        public override void SubDoAnyInitializationImmediatelyAfterFactionAssigned()
        {
            this.BaseInfo = this.AttachedFaction.GetExternalBaseInfoAs<DireMacrophageFactionBaseInfo>();
        }

        protected override void SubCleanup() 
        {
            this.BaseInfo = null;
        }

        protected override int MinimumSecondsBetweenLongRangePlannings => 3;

        private void GetGalaxyRadius(out int radius, out ArcenPoint centroid)
        {
            var galaxy = World_AIW2.Instance.CurrentGalaxy;

            int totalX = 0;
            int totalY = 0;
            int numPlanets = 0;
            galaxy.DoForPlanetsSingleThread(false,
                (p) =>
                {
                    totalX += p.GalaxyLocation.X;
                    totalY += p.GalaxyLocation.Y;
                    numPlanets++;
                    return DelReturn.Continue;
                });

            var center = ArcenPoint.Create(totalX / numPlanets, totalY / numPlanets);

            long maxLenSqr = 0;
            galaxy.DoForPlanetsSingleThread(false,
                (p) =>
                {
                    var d = p.GalaxyLocation.GetSquareDistanceTo(center);
                    if (d > maxLenSqr)
                        maxLenSqr = d;
                    return DelReturn.Continue;
                });

            radius = (int)Math.Sqrt(maxLenSqr);
            centroid = center;
        }

        ArcenPoint RandomPointInRadius(RandomGenerator rand, ArcenPoint pos, int radius)
        {
            var vec = Vector2.zero;

            for (int i = 0; i < 100; i++)
            {
                vec = new Vector2()
                {
                    x = rand.NextFloat(radius * 2) - radius,
                    y = rand.NextFloat(radius * 2) - radius,
                };

                var len = vec.magnitude;
                if (len > radius)
                    continue;

                vec.x += pos.X;
                vec.y += pos.Y;

                break;
            }

            return vec.ToArcenPoint();
        }

        private Planet CreateSpawnPlanet(ArcenHostOnlySimContext hostCtx, Planet nearbyPlanet)
        {
            //ArcenDebugging.ArcenDebugLogSingleLine( string.Format("[nano] CreateSpawnPlanet"), Verbosity.DoNotShow );

            var galaxy = World_AIW2.Instance.CurrentGalaxy;
            var rand = hostCtx.RandomToUse;

            int galaxy_radius;
            ArcenPoint galaxy_centroid;
            GetGalaxyRadius(out galaxy_radius, out galaxy_centroid);

            ArcenPoint planet_pnt = ArcenPoint.ZeroZeroPoint;
            int cur_radius = galaxy_radius / 4;
            int radius_step = galaxy_radius / 4;
            while (true)
            {
                // try 10 times at this radius
                // then increase the radius
                bool success = false;
                for (int i = 0; i < 10; i++)
                {
                    var pnt = RandomPointInRadius(rand, nearbyPlanet.GalaxyLocation, cur_radius);
                    if (!galaxy.CheckForTooCloseToExistingNodes(pnt, PlanetType.Normal, true))
                    {
                        planet_pnt = pnt;
                        success = true;
                        break;
                    }
                }

                if (success)
                    break;

                cur_radius += radius_step;
            }

            var planet = galaxy.AddPlanet(PlanetType.Normal, planet_pnt, PlanetGravWellSizeTable.Instance.DefaultSize);
            var playerLocalFaction = planet.GetFirstFactionOfType(FactionType.Player);
            playerLocalFaction.AIPLeftFromCommandStation = 0;
            playerLocalFaction.AIPLeftFromWarpGate = 0;

            var cmd = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.LinkPlanets], GameCommandSource.AnythingElse);
            cmd.RelatedIntegers.Add(planet.Index);
            cmd.RelatedIntegers.Add(nearbyPlanet.Index);
            World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, cmd, false);

            return planet;
        }

        #region SeedStartingEntities_EarlyMajorFactionClaimsOnly
        private readonly List<Planet> workingAllowedSpawnPlanets = List<Planet>.Create_WillNeverBeGCed(30, "DireMacrophageFactionDeepInfo-workingAllowedSpawnPlanets");

        /*public override void SeedStartingEntities_EarlyMajorFactionClaimsOnly( Galaxy galaxy, ArcenHostOnlySimContext Context, MapTypeData mapType )
        {
            Tutorial tutorialData = World_AIW2.Instance.TutorialOrNull;
            if ( tutorialData != null && tutorialData.SkipMacrophageTelium )
                return;

            Planet spawnPlanet = null;
            workingAllowedSpawnPlanets.Clear();

            int preferredHomeworldDistance = 5;
            do
            {
                //debugCode = 600;
                World_AIW2.Instance.DoForPlanetsSingleThread(false, delegate (Planet planet)
                {
                    //debugCode = 700;
                    if (AttachedFaction.GetBoolValueForCustomFieldOrDefaultValue("SpawnNearPlayer", true) && planet.GetControllingFactionType() == FactionType.Player)
                    {
                        workingAllowedSpawnPlanets.Add(planet);
                        return DelReturn.Continue;
                    }
                    if (planet.GetControllingFactionType() == FactionType.Player)
                        return DelReturn.Continue;
                    if (planet.GetFactionWithSpecialInfluenceHere().Type != FactionType.NaturalObject && preferredHomeworldDistance >= 6) //don't seed over a minor faction if we are finding good spots
                    {
                        return DelReturn.Continue;
                    }
                    if (planet.IsPlanetToBeDestroyed || planet.HasPlanetBeenDestroyed)
                        return DelReturn.Continue;
                    if (planet.PopulationType == PlanetPopulationType.AIBastionWorld ||
                            planet.IsZenithArchitraveTerritory)
                    {
                        return DelReturn.Continue;
                    }
                    //debugCode = 800;
                    if (planet.OriginalHopsToAIHomeworld >= preferredHomeworldDistance &&
                            (planet.OriginalHopsToHumanHomeworld == -1 ||
                            planet.OriginalHopsToHumanHomeworld >= preferredHomeworldDistance + 2))
                        workingAllowedSpawnPlanets.Add(planet);

                    return DelReturn.Continue;
                });

                preferredHomeworldDistance--;
                if (preferredHomeworldDistance == 0)
                    break;
            } while (workingAllowedSpawnPlanets.Count == 0);
            //debugCode = 900;
            if (workingAllowedSpawnPlanets.Count == 0)
                throw new Exception("Unable to find a place to spawn the Dire Macrophage");

            // This is not actually random unless we set the seed ourselves.
            // Since other processing happening before us tends to set the seed to the same value repeatedly.
            Context.RandomToUse.ReinitializeWithSeed(Engine_Universal.PermanentQualityRandom.Next() + AttachedFaction.FactionIndex);
            spawnPlanet = workingAllowedSpawnPlanets[Context.RandomToUse.Next(0, workingAllowedSpawnPlanets.Count)];

            //instead of spawning on this planet, create a new planet linked to it
            if(AttachedFaction.GetStringValueForCustomFieldOrDefaultValue("SpawningOptions", true) == "Invasion")
            {
                spawnPlanet = CreateSpawnPlanet(Context, spawnPlanet);
            }
            GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireTeliumTag);

            PlanetFaction pFaction = spawnPlanet.GetPlanetFactionForFaction(AttachedFaction);
            ArcenPoint spawnLocation = spawnPlanet.GetSafePlacementPointAroundPlanetCenter(Context, entityData, FInt.FromParts(0, 200), FInt.FromParts(0, 600));

            GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, entityData.MarkFor(pFaction),
                                            pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Dire Macrophage Telium");

        }*/
        #endregion

        //Chris says: I honestly don't understand the meaning of this helper method, because I don't know what "valid" means in this context.
        //Based on a comment elsewhere, it seems like maybe this was just a way to deal with old data from old saves?  If so, we can remove it, yes?
        private static bool Helper_DoBasicEntityLRPValidityChecks( GameEntity_Squad entity )
        {
            //if ( entity.TypeData.GetHasTag( MacrophageFactionBaseInfo.TeliumTag ) || entity.TypeData.GetHasTag( MacrophageFactionBaseInfo.SpireTeliumTag ) )
            //    return DelReturn.Continue; Chris notes: this does not seem needed!  BADGER_TODO: please review
            if ( entity.CalculateFinalDestinationPlanetIndex_Safe() != -1 &&
                 entity.CalculateFinalDestinationPlanetIndex_Safe() != entity.GetPlanetIndexSafe() )
                return false; // this unit is en route to another planet
            return true;
        }

        //only used in this one thread, and is cleared there, so great!
        public readonly List<SafeSquadWrapper> ConstructorsLRP = List<SafeSquadWrapper>.Create_WillNeverBeGCed(150, "DireMacrophageDeepInfo-ConstructorsLRP");
        private static readonly List<ArcenPoint> WorkingMetalGeneratorList_Full = List<ArcenPoint>.Create_WillNeverBeGCed( 300, "DireMacrophageFactionDeepInfo-WorkingMetalGeneratorList_Full" );
        private static readonly List<ArcenPoint> WorkingMetalGeneratorList_ThatIAmNotNear = List<ArcenPoint>.Create_WillNeverBeGCed( 300, "DireMacrophageFactionDeepInfo-WorkingMetalGeneratorList_ThatIAmNotNear" );
        public override void DoLongRangePlanning_OnBackgroundNonSimThread_Subclass( ArcenLongTermIntermittentPlanningContext Context )
        {
            int debugStage = 0;
            PerFactionPathCache pathingCacheData = PerFactionPathCache.GetCacheForTemporaryUse_MustReturnToPoolAfterUseOrLeaksMemory();
            try
            {
                debugStage = 100;
                ConstructorsLRP.Clear();
                debugStage = 1000;

                List<Planet> workingPlanets = Planet.GetTemporaryPlanetList( "DireMacroph-DoLongRangePlanning_OnBackgroundNonSimThread_Subclass-workingPlanets", 10f );

                debugStage = 2000;

                Planet.ReleaseTemporaryPlanetList( workingPlanets );
                List<Planet> possiblePlanets = Planet.GetTemporaryPlanetList( "Macroph-DoLongRangePlanning_OnBackgroundNonSimThread_Subclass-possiblePlanets", 10f );
                debugStage = 3000;
                //MacrophageFactionBaseInfo.HarvesterTag
                //Both for the carrier and the harvester, we use the same tags
                AttachedFaction.DoForEntities(DireMacrophageFactionBaseInfo.DireHarvesterTag, delegate ( GameEntity_Squad entity )
                {
                    debugStage = 3100;
                    if ( !Helper_DoBasicEntityLRPValidityChecks( entity ) )
                        return DelReturn.Continue;
                    debugStage = 3200;
                    DireMacrophagePerHarvesterBaseInfo hData = entity.TryGetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                    debugStage = 3300;
                    //if we aren't doing something important, see if we're allowed a break
                    if ( !hData.IsHeadingTowardMetalGeneratorOnPlanet && !hData.ReturningToTelium )
                    {
                        int markModifier = (entity.CurrentMarkLevel - 1) * BaseInfo.SnackTimeIncreasePerMark;
                        if ( World_AIW2.Instance.GameSecond - entity.GameSecondCreated < Context.RandomToUse.Next( BaseInfo.MinimumSnackTimeOnSpawn + markModifier, BaseInfo.MaximumSnackTimeOnSpawn + markModifier ) )
                            return DelReturn.Continue; // this unit was just spawned in, and should hang out and chomp anything on its own planet for a while
                        if ( entity.PlanetFaction.DataByStance[FactionStance.Hostile].ThreatStrength > 10000 &&
                            World_AIW2.Instance.GameSecond - entity.GameSecondEnteredThisPlanet < Context.RandomToUse.Next( BaseInfo.MinimumSnackTimeOnNewPlanet + markModifier, BaseInfo.MaximumSnackTimeOnNewPlanet + markModifier ) )
                            return DelReturn.Continue; // this unit should rage out on its planet for a while
                        markModifier = (entity.CurrentMarkLevel - 1) * BaseInfo.BreakTimeIncreasePerMark;
                        //Stays on planets only if there is sufficent ennemy strength to notice
                        if ( World_AIW2.Instance.GameSecond - entity.GameSecondEnteredThisPlanet < Context.RandomToUse.Next( BaseInfo.MinimumBreakTime + markModifier, BaseInfo.MaximumBreakTime + markModifier ) 
                            && entity.PlanetFaction.DataByStance[FactionStance.Hostile].TotalStrength > 10000)
                            return DelReturn.Continue; // this unit should wait for a short time to see if anything it can eat arrives before moving on
                    }
                    debugStage = 3400;
                    if ( hData.ReturningToTelium )
                    {
                        //Go to the Telium's planet if we aren't there
                        //If we are on the Telium's planet, go right to the telium
                        GameEntity_Squad telium = GetTeliumForHarvester( entity, hData );
                        if ( telium == null )
                        {
                            ArcenDebugging.ArcenDebugLogSingleLine( "BUG: no telium for harvester on " + entity.GetPlanetName_Safe(), Verbosity.DoNotShow );
                            return DelReturn.Continue;
                        }
                        if ( entity.Planet == telium.Planet )
                        {
                            if ( DireMacrophageFactionBaseInfo.debug )
                                ArcenDebugging.ArcenDebugLogSingleLine( "Dire Harvester " + entity.PrimaryKeyID + " heading to telium on this planet", Verbosity.DoNotShow );
                            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCVisitTargetOnPlanet], GameCommandSource.AnythingElse );
                            command.ToBeQueued = false;
                            command.RelatedPoints.Add( telium.WorldLocation );
                            command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                            bool playAudioEffectForCommand = false;
                            World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, playAudioEffectForCommand );
                            return DelReturn.Continue;
                        }
                        else
                        {
                            if ( DireMacrophageFactionBaseInfo.debug )
                                ArcenDebugging.ArcenDebugLogSingleLine( "Dire Harvester " + entity.PrimaryKeyID + " heading to telium on " + telium.GetPlanetName_Safe(), Verbosity.DoNotShow );

                            PathBetweenPlanetsForFaction pathCache = PathingHelper.FindPathFreshOrFromCache( AttachedFaction, "MacrophageLRP2", entity.Planet, telium.Planet, PathingMode.Default, Context, pathingCacheData );
                            if ( pathCache != null && pathCache.PathToReadOnly.Count > 0 )
                            {
                                GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                                command.ToBeQueued = false;
                                command.RelatedString = "Phage_ToTelium";
                                command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                for ( int k = 0; k < pathCache.PathToReadOnly.Count; k++ )
                                    command.RelatedIntegers.Add( pathCache.PathToReadOnly[k].Index );
                                World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
                            }
                            return DelReturn.Continue;
                        }
                    }
                    debugStage = 3500;
                    if ( hData.HasVisitedMetalGeneratorOnPlanet )
                    {
                        // Allow Harvesters to traverse X/2 hops from their Telium, where X is equal the number of harvesters that telium owns
                        int hopLimit = 0;
                        Planet teliumPlanet = null;

                        GameEntity_Squad telium = GetTeliumForHarvester( entity, hData );
                        if ( telium != null )
                        {
                            DireMacrophagePerTeliumBaseInfo tData = telium.TryGetExternalBaseInfoAs<DireMacrophagePerTeliumBaseInfo>();
                            int divisor = 2;
                            hopLimit = Math.Max( 0, tData.CurrentHarvesters / divisor );
                            teliumPlanet = telium.Planet;
                        }

                        if ( hopLimit == 0 && telium != null )
                        {
                            if ( entity.Planet == telium.Planet )
                            {
                                // If we are restricted to our telium planet, and are already on said planet, reset our mine booleans to continue moving around our current planet.
                                hData.HasVisitedMetalGeneratorOnPlanet = false;
                                if ( DireMacrophageFactionBaseInfo.debug )
                                    ArcenDebugging.ArcenDebugLogSingleLine( "Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " is the only harvester for its telium, and is remaining on its telium's planet", Verbosity.DoNotShow );
                                return DelReturn.Continue;
                            }
                            else
                            {
                                // If we are restricted to our telium planet, and are not on our telium's planet, move to it.
                                hData.ReturningToTelium = true;
                                if ( DireMacrophageFactionBaseInfo.debug )
                                    ArcenDebugging.ArcenDebugLogSingleLine( "Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " is the only harvester for its telium, and is returning to its telium's planet", Verbosity.DoNotShow );
                                return DelReturn.Continue;
                            }
                        }
                        else
                        {
                            // We're free from our Telium's home planet. Attempt to find an adjacent planet thats within our hop limit.

                            // ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has visited a metal generator, so head toward a new planet", Verbosity.DoNotShow );
                            //Go to the next adjacent planet
                            Planet destination = null;
                            possiblePlanets.Clear();
                            KeyValuePair<Planet, int> weakestPlanet = new KeyValuePair<Planet, int>( null, 0 );
                            entity.Planet.DoForLinkedNeighbors( false, delegate ( Planet neighbor )
                            {
                                if ( telium == null || telium.Planet.GetHopsTo( neighbor ) <= hopLimit && !hData.IsEvolved)
                                {
                                    bool isPlayerHome = false;
                                    // Dire Macrophages are stronger thus braver (4 vs 3 for regular macrophages)
                                    if ( entity.CurrentMarkLevel >= 3 )
                                        // If we're at least mark 3, we're brave enough to traverse anywhere.
                                        possiblePlanets.Add( neighbor );
                                    else
                                    {
                                        int markOfPlanet = 0;
                                        switch ( neighbor.GetControllingFactionType() )
                                        {
                                            case FactionType.Player:
                                                if ( neighbor.GetCommandStationOrNull() != null && neighbor.GetCommandStationOrNull().TypeData.SpecialType == SpecialEntityType.HumanHomeCommand )
                                                {
                                                    markOfPlanet = 7;
                                                    isPlayerHome = true;
                                                }
                                                else
                                                    markOfPlanet = 4;
                                                break;
                                            case FactionType.AI:
                                                markOfPlanet = neighbor.MarkLevelForAIOnly.Ordinal;
                                                break;
                                            default:
                                                markOfPlanet = 0;
                                                break;
                                        }
                                        //Dire Macrophages can go to planets of higher mark faster overall, for example with mark 2 ships being able to go to mark 5 planets
                                        if ( (entity.CurrentMarkLevel * 2 + 1) >= markOfPlanet )
                                            possiblePlanets.Add( neighbor );
                                    }
                                    // If we don't have any possible planets, update our weakest planet.
                                    if ( possiblePlanets.Count == 0 && !isPlayerHome )
                                    {
                                        int threat = neighbor.GetDataByStanceForFaction( AttachedFaction, FactionStance.Hostile ).TotalStrength;
                                        if ( weakestPlanet.Key == null )
                                        {
                                            // No entry yet; default to this no matter what.
                                            weakestPlanet = new KeyValuePair<Planet, int>( neighbor, threat );
                                        }
                                        else if ( threat < weakestPlanet.Value )
                                        {
                                            // This planet is weaker than our current weakest. Update it.
                                            weakestPlanet = new KeyValuePair<Planet, int>( neighbor, threat );
                                        }
                                    }
                                }
                                return DelReturn.Continue;
                            } );

                            // If no valid planets, pick the lowest strength adjacent planet instead.
                            if ( possiblePlanets.Count == 0 )
                            {
                                // In the very rare case where we do not have a valid target, default to our telium.
                                if ( weakestPlanet.Key == null )
                                    destination = telium.Planet;
                                else
                                    destination = weakestPlanet.Key;
                            }
                            else
                                destination = possiblePlanets[Context.RandomToUse.Next(possiblePlanets.Count)];

                            if ( destination == null )
                            {
                                if ( DireMacrophageFactionBaseInfo.debug )
                                {
                                    ArcenDebugging.ArcenDebugLogSingleLine( "Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " just chill, we are only flipping coins to move", Verbosity.DoNotShow );
                                }
                                return DelReturn.Continue;
                            }
                            if ( DireMacrophageFactionBaseInfo.debug )
                                ArcenDebugging.ArcenDebugLogSingleLine( "Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " choosing random planet " + destination.Name, Verbosity.DoNotShow );

                            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                            command.RelatedString = "Phage_Harvest";
                            command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                            command.RelatedIntegers.Add( destination.Index );
                            World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
                            hData.HasVisitedMetalGeneratorOnPlanet = false; //for the new planet
                            return DelReturn.Continue;
                        }
                    }
                    debugStage = 3600;
                    if ( !hData.HasVisitedMetalGeneratorOnPlanet && hData.IsHeadingTowardMetalGeneratorOnPlanet == false )
                    {
                        //    ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has not visited a metal generator yet, so head toward one", Verbosity.DoNotShow );
                        //Head toward a random metal harvester. TODO: replace this with a call to GetRandomMetalGeneratorOnPlanet()
                        WorkingMetalGeneratorList_Full.Clear();
                        WorkingMetalGeneratorList_ThatIAmNotNear.Clear();
                        entity.Planet.DoForEntities( "MetalGenerator", //note: this DOES work for both distributed economy mode and regular
                            generator =>
                        {
                            WorkingMetalGeneratorList_Full.Add( generator.WorldLocation );
                            if ( generator.WorldLocation.GetExtremelyRoughDistanceTo( entity.WorldLocation ) >= 5000 )
                                WorkingMetalGeneratorList_ThatIAmNotNear.Add( generator.WorldLocation );
                            return DelReturn.Continue;
                        } );
                        if ( WorkingMetalGeneratorList_Full.Count == 0 )
                        {
                            //ArcenDebugging.ArcenDebugLogSingleLine( "Potential bug: planet " + entity.GetPlanetName_Safe() + " has no metal generators. This is unexpected", Verbosity.DoNotShow );
                            hData.HasVisitedMetalGeneratorOnPlanet = true;
                            return DelReturn.Continue;
                        }
                        if ( WorkingMetalGeneratorList_ThatIAmNotNear.Count == 0 ) //I'm satisfied, stay near stuff
                        {
                            hData.HasVisitedMetalGeneratorOnPlanet = true;
                            return DelReturn.Continue;
                        }
                        ArcenPoint chosenPoint = WorkingMetalGeneratorList_ThatIAmNotNear[Context.RandomToUse.Next( 0, WorkingMetalGeneratorList_ThatIAmNotNear.Count )];
                        GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCWander], GameCommandSource.AnythingElse );
                        command.ToBeQueued = true;
                        command.RelatedPoints.Add( chosenPoint );
                        command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                        World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
                        hData.IsHeadingTowardMetalGeneratorOnPlanet = true;
                        return DelReturn.Continue;
                    }
                    debugStage = 3700;
                    if ( !hData.HasVisitedMetalGeneratorOnPlanet && hData.IsHeadingTowardMetalGeneratorOnPlanet == true )
                    {
                        //check if we have reached that metal harvester
                        int rangeForMetalGenerator = 400;
                        if ( Mat.DistanceBetweenPointsImprecise( entity.WorldLocation, entity.CalculateDestinationPoint_Safe() ) < rangeForMetalGenerator )
                        {
                            //        ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has just reached a metal generator", Verbosity.DoNotShow );
                            hData.HasVisitedMetalGeneratorOnPlanet = true;
                            hData.IsHeadingTowardMetalGeneratorOnPlanet = false;
                            hData.CurrentMetal += BaseInfo.MetalGainedFromVisitingMine;
                        }
                        return DelReturn.Continue;
                    }
                    return DelReturn.Continue;
                } ); //endMacrophageFactionBaseInfo.HarvesterTag

                //EvolvedDireMacrophageHarvester
                AttachedFaction.DoForEntities(DireMacrophageFactionBaseInfo.EvolvedDireHarvesterTag, delegate (GameEntity_Squad entity)
                {
                    debugStage = 3100;
                    if (!Helper_DoBasicEntityLRPValidityChecks(entity))
                        return DelReturn.Continue;
                    debugStage = 3200;
                    DireMacrophagePerHarvesterBaseInfo hData = entity.TryGetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                    debugStage = 3300;
                    //if we aren't doing something important, see if we're allowed a break
                    if (!hData.IsHeadingTowardMetalGeneratorOnPlanet && !hData.ReturningToTelium)
                    {
                        int markModifier = (entity.CurrentMarkLevel - 1) * BaseInfo.SnackTimeIncreasePerMark;
                        if (World_AIW2.Instance.GameSecond - entity.GameSecondCreated < Context.RandomToUse.Next(BaseInfo.MinimumSnackTimeOnSpawn + markModifier, BaseInfo.MaximumSnackTimeOnSpawn + markModifier))
                            return DelReturn.Continue; // this unit was just spawned in, and should hang out and chomp anything on its own planet for a while
                        if (entity.PlanetFaction.DataByStance[FactionStance.Hostile].ThreatStrength > 1000 &&
                            World_AIW2.Instance.GameSecond - entity.GameSecondEnteredThisPlanet < Context.RandomToUse.Next(BaseInfo.MinimumSnackTimeOnNewPlanet + markModifier, BaseInfo.MaximumSnackTimeOnNewPlanet + markModifier))
                            return DelReturn.Continue; // this unit should rage out on its planet for a while
                        markModifier = (entity.CurrentMarkLevel - 1) * BaseInfo.BreakTimeIncreasePerMark;
                        //The Evolved Dire Harvester stays a long time on planets, but only if there are ennemies otherwise it bails
                        if (World_AIW2.Instance.GameSecond - entity.GameSecondEnteredThisPlanet < Context.RandomToUse.Next((BaseInfo.MinimumBreakTime + markModifier), (BaseInfo.MaximumBreakTime + markModifier)) * 2
                            && entity.PlanetFaction.DataByStance[FactionStance.Hostile].TotalStrength > 25000)
                            return DelReturn.Continue; // this unit should wait for a short time to see if anything it can eat arrives before moving on
                    }
                    debugStage = 3400;
                    if (hData.ReturningToTelium)
                    {
                        //Go to the Telium's planet if we aren't there
                        //If we are on the Telium's planet, go right to the telium
                        GameEntity_Squad telium = GetTeliumForHarvester(entity, hData);
                        if (telium == null)
                        {
                            ArcenDebugging.ArcenDebugLogSingleLine("BUG: no telium for harvester on " + entity.GetPlanetName_Safe(), Verbosity.DoNotShow);
                            return DelReturn.Continue;
                        }
                        if (entity.Planet == telium.Planet)
                        {
                            if (DireMacrophageFactionBaseInfo.debug)
                                ArcenDebugging.ArcenDebugLogSingleLine("Evolved Dire Harvester " + entity.PrimaryKeyID + " heading to telium on this planet", Verbosity.DoNotShow);
                            GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCVisitTargetOnPlanet], GameCommandSource.AnythingElse);
                            command.ToBeQueued = false;
                            command.RelatedPoints.Add(telium.WorldLocation);
                            command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                            bool playAudioEffectForCommand = false;
                            World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, playAudioEffectForCommand);
                            return DelReturn.Continue;
                        }
                        else
                        {
                            if (DireMacrophageFactionBaseInfo.debug)
                                ArcenDebugging.ArcenDebugLogSingleLine("Evolved Dire Harvester " + entity.PrimaryKeyID + " heading to telium on " + telium.GetPlanetName_Safe(), Verbosity.DoNotShow);

                            PathBetweenPlanetsForFaction pathCache = PathingHelper.FindPathFreshOrFromCache(AttachedFaction, "MacrophageLRP2", entity.Planet, telium.Planet, PathingMode.Default, Context, pathingCacheData);
                            if (pathCache != null && pathCache.PathToReadOnly.Count > 0)
                            {
                                GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse);
                                command.ToBeQueued = false;
                                command.RelatedString = "Phage_ToTelium";
                                command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                                for (int k = 0; k < pathCache.PathToReadOnly.Count; k++)
                                    command.RelatedIntegers.Add(pathCache.PathToReadOnly[k].Index);
                                World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
                            }
                            return DelReturn.Continue;
                        }
                    }
                    debugStage = 3500;
                    if (hData.HasVisitedMetalGeneratorOnPlanet)
                    {
                        // Allow Harvesters to traverse X/2 hops from their Telium, where X is equal the number of harvesters that telium owns
                        int hopLimit = 0;
                        Planet teliumPlanet = null;

                        GameEntity_Squad telium = GetTeliumForHarvester(entity, hData);
                        if (telium != null)
                        {
                            DireMacrophagePerTeliumBaseInfo tData = telium.TryGetExternalBaseInfoAs<DireMacrophagePerTeliumBaseInfo>();
                            int divisor = 2;
                            hopLimit = Math.Max(0, tData.CurrentHarvesters / divisor);
                            teliumPlanet = telium.Planet;
                        }

                        if (hopLimit == 0 && telium != null)
                        {
                            if (entity.Planet == telium.Planet)
                            {
                                // If we are restricted to our telium planet, and are already on said planet, reset our mine booleans to continue moving around our current planet.
                                hData.HasVisitedMetalGeneratorOnPlanet = false;
                                if (DireMacrophageFactionBaseInfo.debug)
                                    ArcenDebugging.ArcenDebugLogSingleLine("Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " is the only harvester for its telium, and is remaining on its telium's planet", Verbosity.DoNotShow);
                                return DelReturn.Continue;
                            }
                            else
                            {
                                // If we are restricted to our telium planet, and are not on our telium's planet, move to it.
                                hData.ReturningToTelium = true;
                                if (DireMacrophageFactionBaseInfo.debug)
                                    ArcenDebugging.ArcenDebugLogSingleLine("Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " is the only harvester for its telium, and is returning to its telium's planet", Verbosity.DoNotShow);
                                return DelReturn.Continue;
                            }
                        }
                        else
                        {
                            // We're free from our Telium's home planet. Attempt to find an adjacent planet thats within our hop limit.

                            // ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has visited a metal generator, so head toward a new planet", Verbosity.DoNotShow );
                            //Go to the next adjacent planet
                            Planet destination = null;
                            possiblePlanets.Clear();
                            KeyValuePair<Planet, int> weakestPlanet = new KeyValuePair<Planet, int>(null, 0);
                            entity.Planet.DoForLinkedNeighbors(false, delegate (Planet neighbor)
                            {
                                if (telium == null || telium.Planet.GetHopsTo(neighbor) <= hopLimit)
                                {
                                    // The Evolved version will happily go suicide on any planet
                                    possiblePlanets.Add(neighbor);
                                }
                                return DelReturn.Continue;
                            });

                            // The Evolved variant will ideally go on planets that have ennemies
                            Planet destinationFallback = possiblePlanets[Context.RandomToUse.Next(possiblePlanets.Count)];
                            destination = destinationFallback;
                            if (!(destinationFallback.GetDataByStanceForFaction(AttachedFaction, FactionStance.Hostile).TotalStrength > 30000))
                            {
                                for (int i = 0; i < possiblePlanets.Count; i++)
                                {
                                    //Note that strength numbers are in the code 1000 times higher than in the actual game
                                    if (possiblePlanets[i].GetDataByStanceForFaction(AttachedFaction, FactionStance.Hostile).TotalStrength > 30000)
                                    {
                                        destination = possiblePlanets[i];
                                        break;
                                    }
                                }
                            }
                            if (destination == null)
                            {
                                if (DireMacrophageFactionBaseInfo.debug)
                                {
                                    ArcenDebugging.ArcenDebugLogSingleLine("Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " just chill, we are only flipping coins to move", Verbosity.ShowAsError);
                                }
                                return DelReturn.Continue;
                            }
                            if (DireMacrophageFactionBaseInfo.debug)
                                ArcenDebugging.ArcenDebugLogSingleLine("Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " choosing random planet " + destination.Name, Verbosity.DoNotShow);

                            GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse);
                            command.RelatedString = "Phage_Harvest";
                            command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                            command.RelatedIntegers.Add(destination.Index);
                            World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
                            hData.HasVisitedMetalGeneratorOnPlanet = false; //for the new planet
                            return DelReturn.Continue;
                        }
                    }
                    debugStage = 3600;
                    if (!hData.HasVisitedMetalGeneratorOnPlanet && hData.IsHeadingTowardMetalGeneratorOnPlanet == false)
                    {
                        //    ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has not visited a metal generator yet, so head toward one", Verbosity.DoNotShow );
                        //Head toward a random metal harvester. TODO: replace this with a call to GetRandomMetalGeneratorOnPlanet()
                        WorkingMetalGeneratorList_Full.Clear();
                        WorkingMetalGeneratorList_ThatIAmNotNear.Clear();
                        entity.Planet.DoForEntities("MetalGenerator", //note: this DOES work for both distributed economy mode and regular
                            generator =>
                            {
                                WorkingMetalGeneratorList_Full.Add(generator.WorldLocation);
                                if (generator.WorldLocation.GetExtremelyRoughDistanceTo(entity.WorldLocation) >= 5000)
                                    WorkingMetalGeneratorList_ThatIAmNotNear.Add(generator.WorldLocation);
                                return DelReturn.Continue;
                            });
                        if (WorkingMetalGeneratorList_Full.Count == 0)
                        {
                            //ArcenDebugging.ArcenDebugLogSingleLine( "Potential bug: planet " + entity.GetPlanetName_Safe() + " has no metal generators. This is unexpected", Verbosity.DoNotShow );
                            hData.HasVisitedMetalGeneratorOnPlanet = true;
                            return DelReturn.Continue;
                        }
                        if (WorkingMetalGeneratorList_ThatIAmNotNear.Count == 0) //I'm satisfied, stay near stuff
                        {
                            hData.HasVisitedMetalGeneratorOnPlanet = true;
                            return DelReturn.Continue;
                        }
                        ArcenPoint chosenPoint = WorkingMetalGeneratorList_ThatIAmNotNear[Context.RandomToUse.Next(0, WorkingMetalGeneratorList_ThatIAmNotNear.Count)];
                        GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCWander], GameCommandSource.AnythingElse);
                        command.ToBeQueued = true;
                        command.RelatedPoints.Add(chosenPoint);
                        command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                        World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
                        hData.IsHeadingTowardMetalGeneratorOnPlanet = true;
                        return DelReturn.Continue;
                    }
                    debugStage = 3700;
                    if (!hData.HasVisitedMetalGeneratorOnPlanet && hData.IsHeadingTowardMetalGeneratorOnPlanet == true)
                    {
                        //check if we have reached that metal harvester
                        int rangeForMetalGenerator = 400;
                        if (Mat.DistanceBetweenPointsImprecise(entity.WorldLocation, entity.CalculateDestinationPoint_Safe()) < rangeForMetalGenerator)
                        {
                            //        ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has just reached a metal generator", Verbosity.DoNotShow );
                            hData.HasVisitedMetalGeneratorOnPlanet = true;
                            hData.IsHeadingTowardMetalGeneratorOnPlanet = false;
                            hData.CurrentMetal += BaseInfo.MetalGainedFromVisitingMine * 2; //Evolved Dire Harvester gains twice as much
                        }
                        return DelReturn.Continue;
                    }
                    return DelReturn.Continue;
                }); //endMacrophageFactionBaseInfo.HarvesterTag

                AttachedFaction.DoForEntities(DireMacrophageFactionBaseInfo.DireMacrophageBorer, delegate (GameEntity_Squad entity)
                {
                    debugStage = 3100;
                    if (!Helper_DoBasicEntityLRPValidityChecks(entity))
                        return DelReturn.Continue;
                    debugStage = 3200;
                    DireMacrophagePerHarvesterBaseInfo hData = entity.TryGetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                    debugStage = 3300;
                    //if we aren't doing something important, see if we're allowed a break
                    if (World_AIW2.Instance.GameSecond - entity.GameSecondCreated < Context.RandomToUse.Next(BaseInfo.MinimumSnackTimeOnSpawn, BaseInfo.MaximumSnackTimeOnSpawn))
                        return DelReturn.Continue; // this unit was just spawned in, and should hang out and chomp anything on its own planet for a while
                    //The Borer does things for a bit before going away, and usually fast
                    if (World_AIW2.Instance.GameSecond - entity.GameSecondEnteredThisPlanet < Context.RandomToUse.Next((BaseInfo.MinimumBreakTime), (BaseInfo.MaximumBreakTime)))
                        return DelReturn.Continue; // this unit should wait for a short time to see if anything it can eat arrives before moving on
                    debugStage = 3400;
                    // Allow Harvesters to traverse X/2 hops from their Telium, where X is equal the number of harvesters that telium owns
                    int hopLimit = 0;
                    Planet teliumPlanet = null;

                    GameEntity_Squad telium = GetTeliumForHarvester(entity, hData);
                    if (telium != null)
                    {
                        DireMacrophagePerTeliumBaseInfo tData = telium.TryGetExternalBaseInfoAs<DireMacrophagePerTeliumBaseInfo>();
                        int divisor = 2;
                        hopLimit = Math.Max(0, tData.CurrentHarvesters / divisor);
                        teliumPlanet = telium.Planet;
                    }

                    if (hopLimit == 0 && telium != null)
                    {
                            // If we are restricted to our telium planet, and are not on our telium's planet, move to it.
                            hData.ReturningToTelium = true;
                            if (DireMacrophageFactionBaseInfo.debug)
                                ArcenDebugging.ArcenDebugLogSingleLine("Harvester " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " is the only harvester for its telium, and is returning to its telium's planet", Verbosity.DoNotShow);
                            return DelReturn.Continue;
                    }
                    else
                    {
                        // We're free from our Telium's home planet. Attempt to find an adjacent planet thats within our hop limit.

                        // ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has visited a metal generator, so head toward a new planet", Verbosity.DoNotShow );
                        //Go to the next adjacent planet
                        Planet destination = null;
                        possiblePlanets.Clear();
                        entity.Planet.DoForLinkedNeighbors(false, delegate (Planet neighbor)
                        {
                            if (telium == null || telium.Planet.GetHopsTo(neighbor) <= hopLimit)
                            {
                                // The Borer roams around randomly
                                possiblePlanets.Add(neighbor);
                            }
                            return DelReturn.Continue;
                        });

                        //The Borer roams around randomly
                        destination = possiblePlanets[Context.RandomToUse.Next(possiblePlanets.Count)];
                        if (destination == null)
                        {
                            if (DireMacrophageFactionBaseInfo.debug)
                            {
                                ArcenDebugging.ArcenDebugLogSingleLine("Borer " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " just chill, we are only flipping coins to move", Verbosity.ShowAsError);
                            }
                            return DelReturn.Continue;
                        }
                        if (DireMacrophageFactionBaseInfo.debug)
                            ArcenDebugging.ArcenDebugLogSingleLine("Borer " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " choosing random planet " + destination.Name, Verbosity.DoNotShow);

                        GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse);
                        command.RelatedString = "Phage_Harvest";
                        command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                        command.RelatedIntegers.Add(destination.Index);
                        World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
                        hData.HasVisitedMetalGeneratorOnPlanet = false; //for the new planet
                        return DelReturn.Continue;
                    }
                    return DelReturn.Continue;
                }); //endMacrophageFactionBaseInfo.HarvesterTag

                //Macrophage Constructors
                //Roams around randomly until it finds a planet without three bastions and spawns one.
                //ABSOLUTELY DISASTROUS CODE!!!!!!!!!!!!!!!!!!!!!!!
                AttachedFaction.DoForEntities(DireMacrophageFactionBaseInfo.DireMacrophageConstructor, delegate (GameEntity_Squad entity)
                {
                    DireMacrophagePerConstructorBaseInfo cData = entity.TryGetExternalBaseInfoAs<DireMacrophagePerConstructorBaseInfo>();
                    List<SafeSquadWrapper> macrophageBastionsInGalaxy = this.BaseInfo.MacrophageBastionsInGalaxy.GetDisplayList();
                    List<SafeSquadWrapper> macrophageFortificationsInGalaxy = this.BaseInfo.MacrophageFortificationsInGalaxy.GetDisplayList();
                    int numberBastions = 0;
                    int numberFortifications = 0;
                    if (!Helper_DoBasicEntityLRPValidityChecks(entity))
                        return DelReturn.Continue;
                    if (!cData.SpotFound) //we check if we have found our spot
                    { 
                        //Check for how many bastions on current planet, then fortifications
                        for (int i = 0; i < macrophageBastionsInGalaxy.Count; i++)
                        {
                            GameEntity_Squad bastion = macrophageBastionsInGalaxy[i].GetSquad();
                            if (bastion == null)
                                continue;
                            if (bastion.Planet == entity.Planet)
                            {
                                numberBastions++;
                            }
                            if(numberBastions >= 3) 
                            {
                                cData.SpotFound = false;
                                break; //Begone performance drain
                            }
                        }
                        if(numberBastions < 3) //If there are less than 2 bastions then start pathing towards the selected location
                        {                       //Similar to scourge decision making for placing fortresses when there's nothing important on the planet
                            Planet planet = entity.Planet;
                            GameEntityTypeData DefenseEntityData = GameEntityTypeDataTable.Instance.GetRowByName("DireMacrophageDefense"); //This annoys me a lot but it can't be helped
                            ArcenPoint baseLocation = Engine_AIW2.Instance.CombatCenter; //default location
                            cData.transformationLocation = planet.GetSafePlacementPoint_AroundDesiredPointVicinity(Context, DefenseEntityData, baseLocation, FInt.FromParts(0, 050), FInt.FromParts(0, 200));
                            GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCWander], GameCommandSource.AnythingElse);
                            command.ToBeQueued = false; //hopefully this works the way I want
                            command.RelatedPoints.Add(cData.transformationLocation);
                            command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                            World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
                            cData.SpotFound = true;
                            cData.timeSupposedlyGoingTowardsTheSpot = World_AIW2.Instance.GameSecond;
                            cData.TransformingIntoThis = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireMacrophageDefenseTag);
                            return DelReturn.Continue;
                        }
                        //Now onto fortifications if we can't try to spawn as a bastion
                        for (int i = 0; i < macrophageFortificationsInGalaxy.Count; i++)
                        {
                            GameEntity_Squad fortification = macrophageFortificationsInGalaxy[i].GetSquad();
                            if (fortification == null)
                                continue;
                            if (fortification.Planet == entity.Planet)
                            {
                                numberFortifications++;
                            }
                            if (numberFortifications >= this.BaseInfo.MaximumFortifications)
                            {
                                cData.SpotFound = false;
                                break; //Begone performance drain
                            }
                        }
                        if (numberFortifications < this.BaseInfo.MaximumFortifications) //Less than an arbitrary amount of fortifications? Become one on this planet
                        {                       //Similar to scourge decision making for placing fortresses when there's nothing important on the planet
                            Planet planet = entity.Planet;
                            GameEntityTypeData DefenseEntityData = GameEntityTypeDataTable.Instance.GetRowByName("DireMacrophageLesserDefense"); //This annoys me a lot but it can't be helped
                            ArcenPoint baseLocation = Engine_AIW2.Instance.CombatCenter; //default location
                            cData.transformationLocation = planet.GetSafePlacementPoint_AroundDesiredPointVicinity(Context, DefenseEntityData, baseLocation, FInt.FromParts(0, 050), FInt.FromParts(0, 200));
                            GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCWander], GameCommandSource.AnythingElse);
                            command.ToBeQueued = false; //hopefully this works the way I want
                            command.RelatedPoints.Add(cData.transformationLocation);
                            command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                            World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
                            cData.SpotFound = true;
                            cData.timeSupposedlyGoingTowardsTheSpot = World_AIW2.Instance.GameSecond;
                            cData.TransformingIntoThis = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireMacrophageLesserDefenseTag);
                            return DelReturn.Continue;
                        }
                    }
                    if (cData.SpotFound)
                    {
                        if(cData.transformationLocation == entity.WorldLocation)
                        {
                            TransformIntoDefense(cData, entity, Context, DireMacrophageFactionBaseInfo.debug);
                            entity.Despawn(Context, true, InstancedRendererDeactivationReason.IAmTransforming);
                        }
                        //We check every 20 minutes to see whether or not we are there, if not then there's a problem with the spot or we decollided before transforming
                        //Larger grav well can cause issues with lower times hence why the large time limit
                        if (cData.transformationLocation != entity.WorldLocation && (World_AIW2.Instance.GameSecond - cData.timeSupposedlyGoingTowardsTheSpot) >= 1200) 
                        {
                            cData.SpotFound = false;
                        }
                    }
                    if(!cData.SpotFound) //Consider the next planet to go to if we aren't gonna turn into a bastion just yet
                    {
                        int hopLimit = 0;
                        Planet teliumPlanet = null;

                        GameEntity_Squad telium = GetTeliumForHarvester(entity, cData);
                        if (telium != null)
                        {
                            DireMacrophagePerTeliumBaseInfo tData = telium.TryGetExternalBaseInfoAs<DireMacrophagePerTeliumBaseInfo>();
                            int divisor = 2;
                            hopLimit = Math.Max(0, tData.CurrentHarvesters / divisor);
                            teliumPlanet = telium.Planet;
                        }

                        if (hopLimit == 0 && telium != null)
                        {
                            // If we are restricted to our telium planet, and are not on our telium's planet, move to it.
                            cData.ReturningToTelium = true;
                            if (DireMacrophageFactionBaseInfo.debug)
                                ArcenDebugging.ArcenDebugLogSingleLine("Constructor " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " is returning to its telium's planet, for its telium only has one harvester.", Verbosity.DoNotShow);
                            return DelReturn.Continue;
                        }
                        else
                        {
                            // We're free from our Telium's home planet. Attempt to find an adjacent planet thats within our hop limit.

                            // ArcenDebugging.ArcenDebugLogSingleLine("The macrophage on " + entity.GetPlanetName_Safe() + " has visited a metal generator, so head toward a new planet", Verbosity.DoNotShow );
                            //Go to the next adjacent planet
                            Planet destination = null;
                            possiblePlanets.Clear();
                            entity.Planet.DoForLinkedNeighbors(false, delegate (Planet neighbor)
                            {
                                if (telium == null || telium.Planet.GetHopsTo(neighbor) <= hopLimit)
                                {
                                    // The Constructor roams around randomly
                                    possiblePlanets.Add(neighbor);
                                }
                                return DelReturn.Continue;
                            });

                            //The Constructor roams around randomly
                            destination = possiblePlanets[Context.RandomToUse.Next(possiblePlanets.Count)];
                            if (destination == null)
                            {
                                if (DireMacrophageFactionBaseInfo.debug)
                                {
                                    ArcenDebugging.ArcenDebugLogSingleLine("Borer " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " just chill, we are only flipping coins to move", Verbosity.ShowAsError);
                                }
                                return DelReturn.Continue;
                            }
                            if (DireMacrophageFactionBaseInfo.debug)
                                ArcenDebugging.ArcenDebugLogSingleLine("Borer " + entity.PrimaryKeyID + " on " + entity.GetPlanetName_Safe() + " choosing random planet " + destination.Name, Verbosity.DoNotShow);

                            GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse);
                            command.RelatedString = "Phage_Harvest";
                            command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                            command.RelatedIntegers.Add(destination.Index);
                            World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
                            return DelReturn.Continue;
                        }
                    }
                    return DelReturn.Continue;
                });
                //ngl this code is quite a bit better than the one above, although they are not meant to fullfill the same functions
                AttachedFaction.DoForEntities(DireMacrophageFactionBaseInfo.DireMacrophageColoniser, delegate (GameEntity_Squad entity)
                {
                    int debugCode = 0;
                    int BuildRange = 150;
                    ConstructorsLRP.Add(entity);
                    try
                    {
                        debugCode = 100;
                        List<SafeSquadWrapper> colonisers = this.BaseInfo.MacrophageColonisers.GetDisplayList();
                            debugCode = 200;
                            if (entity == null)
                                return DelReturn.Continue;
                            TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                            if (data.UnitToBuild == null)//If we forgot to set which unit to build
                            {
                                //this shouldn't be possible, but just in case
                                data.UnitToBuild = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, "TemplarLowTierStructure");
                                return DelReturn.Continue;
                            }

                            if (data.PlanetIdx == -1)
                            {
                                //This shouldn't be possible, since we only create the constructor if there's a planet
                                //to build on, but just in case
                                Planet newCastlePlanet = GetNewTeliumLocation(entity, Context);
                                if (newCastlePlanet != null)
                                {
                                    data.PlanetIdx = newCastlePlanet.Index;
                                    return DelReturn.Continue;
                                }
                            //we couldn't find a planet to build on, so just self-destruct
                            entity.Despawn(Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut); //we've created our new castle
                            }

                            if (entity.Planet.Index != data.PlanetIdx)
                                return DelReturn.Continue;
                            debugCode = 300;
                            PlanetFaction pFaction = entity.PlanetFaction;
                            if (data.LocationToBuild == ArcenPoint.ZeroZeroPoint)
                            {
                                //if we have just arrived at our planet but don't have a location to build, pick one
                                data.LocationToBuild = entity.Planet.GetSafePlacementPoint_AroundEntity(Context, data.UnitToBuild, entity, FInt.FromParts(0, 200), FInt.FromParts(0, 650));
                                return DelReturn.Continue;
                            }
                            debugCode = 400;
                            if (Mat.DistanceBetweenPointsImprecise(entity.WorldLocation, data.LocationToBuild) > BuildRange)
                                return DelReturn.Continue;
                            debugCode = 500;
                            ArcenPoint finalPoint = entity.Planet.GetSafePlacementPoint_AroundDesiredPointVicinity(Context, data.UnitToBuild, data.LocationToBuild, FInt.FromParts(0, 10), FInt.FromParts(0, 50));
                            debugCode = 600;
                            //Creating the new telium
                            GameEntity_Squad newEntity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, data.UnitToBuild, (byte)1,
                                                                                                          pFaction.Faction.LooseFleet, 0, finalPoint, Context, "DireMacrophage-Coloniser");

                            debugCode = 700;
                            //Setting up its variables
                            DireMacrophagePerTeliumBaseInfo newData = newEntity.CreateExternalBaseInfo<DireMacrophagePerTeliumBaseInfo>("DireMacrophagePerTeliumBaseInfo");
                            newData.CurrentHarvesters = 0;
                            newData.ExtraMetalGenerationPerSecond = 0; //Make something in the constants
                            newData.MetalGainedFromHarvesters = 5000000 * entity.CurrentMarkLevel;
                            newData.MetalIncomeFromDefenses = 0;
                            newData.TotalMetalEverCollected = 0;
                            newData.CurrentMetal = 0;
                            newEntity.ShouldNotBeConsideredAsThreatToHumanTeam = true;

                            debugCode = 800;
                            debugCode = 900;
                            entity.Despawn(Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut); //we've created our new castle
                            debugCode = 1000;
                            ConstructorsLRP.Remove(entity);
                    }
                    catch (Exception e)
                    {
                        ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in HandleConstructorsSim debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow);
                    }
                    return DelReturn.Continue;
                });
                HandleColonisersLRP(Context, pathingCacheData);
                Planet.ReleaseTemporaryPlanetList( possiblePlanets );

                debugStage = 4000;
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Error in Macrophage long range planning at debugStage " + debugStage + " Error: " + e, Verbosity.ShowAsError );
            }
            finally
            {
                pathingCacheData.ReturnToPool();
            }
            FactionUtilityMethods.Instance.FlushUnitsFromReinforcementPointsOnAllRelevantPlanets(AttachedFaction, Context, 5f);
        }

        public override void DoOnAnyDeathLogic_FromCentralLoop_NotJustMyOwnShips_HostOnly( ref int debugStage, GameEntity_Squad entity, DamageSource Damage, EntitySystem FiringSystemOrNull,
              Faction factionThatKilledEntity, Faction entityOwningFaction, int numExtraStacksKilled, ArcenHostOnlySimContext Context )
        {
            if ( this.BaseInfo.DireHarvesters.Count <= 0 && entity == null )
                return; // Do not process if we have no consumers active.
            try
            {
                //Handle Macrophage for any ship that dies, not just ours.  But make the super basic check very quick
                if ( FiringSystemOrNull != null && DireMacrophageFactionBaseInfo.AllDireMacrophageFactions.Count > 0 )
                {
                    debugStage = 5900;
                    //if the Macrophage is enabled and the dying unit is a ship
                    GameEntity_Squad EntityThatKilledTarget = FiringSystemOrNull.ParentEntity;
                    //if WE were the faction who killed this ship that is dying, then check further.  Otherwise ignore this death.
                    if ( EntityThatKilledTarget != null && EntityThatKilledTarget.GetFactionOrNull_Safe() == AttachedFaction )
                    {
                        debugStage = 6000;
                        if ( EntityThatKilledTarget.TypeData.GetHasTag( DireMacrophageFactionBaseInfo.DireHarvesterTag ) || EntityThatKilledTarget.TypeData.GetHasTag(DireMacrophageFactionBaseInfo.EvolvedDireHarvesterTag))
                        {
                            debugStage = 6100;
                            DireMacrophagePerHarvesterBaseInfo pData = EntityThatKilledTarget.GetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                            if ( pData != null )
                            {
                                debugStage = 6110;
                                int metalGained = entity.GetStrengthPerSquad() + entity.GetStrengthPerSquad() * numExtraStacksKilled;
                                metalGained *= BaseInfo.HarvesterMetalFromKillMultiplier;
                                pData.CurrentMetal += metalGained;
                                pData.TotalMetalEverCollected += metalGained;
                            }
                        }
                        if (EntityThatKilledTarget != null) //this is useless but good practice
                        {
                            if (EntityThatKilledTarget.TypeData.GetHasTag(DireMacrophageFactionBaseInfo.CarrierDrone))
                            {
                                debugStage = 6200;
                                {
                                    debugStage = 6210;
                                    try
                                    {
                                        if((EntityThatKilledTarget.GetFleetCenterpieceOrNull_Safe().TypeData.GetHasTag(DireMacrophageFactionBaseInfo.DireHarvesterTag) || EntityThatKilledTarget.GetFleetCenterpieceOrNull_Safe().TypeData.GetHasTag(DireMacrophageFactionBaseInfo.EvolvedDireHarvesterTag)))
                                        {
                                            DireMacrophagePerHarvesterBaseInfo pData = EntityThatKilledTarget.GetFleetCenterpieceOrNull_Safe().GetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                                            if (pData != null)
                                            {
                                                debugStage = 6220;
                                                int metalGained = entity.GetStrengthPerSquad() + entity.GetStrengthPerSquad() * numExtraStacksKilled;
                                                metalGained *= BaseInfo.HarvesterMetalFromKillMultiplier;
                                                pData.CurrentMetal += metalGained;
                                                pData.TotalMetalEverCollected += metalGained;
                                            }
                                        }     
                                    }
                                    catch (Exception e)
                                    {   //Skillfully make the error not show up, since I can't do anything about it and is harmless, ESPECIALLY for bombs
                                        ArcenDebugging.ArcenDebugLog("Drones bugged again? Skill issue " + debugStage + "\n" + e, Verbosity.DoNotShow);
                                    }

                                }
                            }
                        }
                        
                        if (EntityThatKilledTarget.TypeData.GetHasTag(DireMacrophageFactionBaseInfo.DireMacrophageDefenseTag) || EntityThatKilledTarget.TypeData.GetHasTag(DireMacrophageFactionBaseInfo.DireMacrophageLesserDefenseTag))
                        {
                            debugStage = 6300;
                            DireMacrophagePerDefenseBaseInfo dData = EntityThatKilledTarget.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
                            if (dData != null)
                            {
                                debugStage = 6310;
                                int metalGained = entity.GetStrengthPerSquad() + entity.GetStrengthPerSquad() * numExtraStacksKilled;
                                metalGained *= BaseInfo.HarvesterMetalFromKillMultiplier;
                                dData.CurrentMetal += metalGained;
                                dData.TotalMetalEverCollected += metalGained;
                                //Send the metal to the telium as well
                                
                                //This part is for the income that the bastion gives to the telium, which you can destroy and thus reduce
                                //After some testing, now gives double to make them more valuable to destroy as before they were quite pathetic
                                dData.ExtraMetalGenerationPerSecondForHomeTelium = ((BaseInfo.EffectiveIntensity / 2) * (dData.TotalMetalEverCollected / BaseInfo.MetalHarvesterCanHold)) * 2;

                                //This part is for sending metal directly to the telium, turning it into unremovable income down the line
                                List<SafeSquadWrapper> telia = this.BaseInfo.DireTelia.GetDisplayList();
                                for (int i = 0; i < telia.Count; i++) //I hope this doesn't get laggy if you were to decide to put multiple telia, not an issue right now though
                                {
                                    if (dData.TeliumID == telia[i].PrimaryKeyID)
                                    {
                                        GameEntity_Squad telium = telia[i].GetSquad();
                                        DireMacrophagePerTeliumBaseInfo tData = telium.CreateExternalBaseInfo<DireMacrophagePerTeliumBaseInfo>("DireMacrophagePerTeliumBaseInfo");
                                        tData.MetalGainedFromHarvesters += metalGained; //For permanent income
                                        tData.CurrentMetal += metalGained; //Also direct metal gain
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLog( "Exception in SpecialFaction_Macrophage.DoOnAnyDeathLogic_HostOnly_FromCentralLoop_NotJustMyOwnShips stage " + debugStage + "\n" + e, Verbosity.ShowAsError );
            }
        }
        public override void DoOnAnyDeathLogic_MyFactionUnitsOnly_HostOnly( GameEntity_Squad entity, DamageSource Damage, EntitySystem FiringSystemOrNull, ArcenHostOnlySimContext Context )
        {
            if ( entity == null )
                return;
        }
        public override void DoPerSimStepLogic_OnMainThreadAndPartOfSim_HostOnly( ArcenHostOnlySimContext Context )
        {
        }

        public override void DoPerSecondLogic_Stage3Main_OnMainThreadAndPartOfSim_HostOnly( ArcenHostOnlySimContext Context )
        {

            if (!AttachedFaction.HasDoneInvasionStyleAction &&
                     (AttachedFaction.InvasionTime > 0 && AttachedFaction.InvasionTime <= World_AIW2.Instance.GameSecond))
            {
                //debugCode = 500;
                //Lets default to just putting the nanocaust hive on a completely random non-player non-ai-king planet
                //TODO: improve this
                GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireTeliumTag);

                Planet spawnPlanet = null;
                {
                    workingAllowedSpawnPlanets.Clear();
                    int preferredHomeworldDistance = 6;
                    if (this.BaseInfo.SeedNearPlayer && World_AIW2.Instance.PlayerOwnedPlanets != 0)
                    {
                        World_AIW2.Instance.DoForPlanetsSingleThread(false, delegate (Planet planet)
                        {
                            if (planet.GetControllingFactionType() == FactionType.Player)
                            {
                                workingAllowedSpawnPlanets.Add(planet);
                                return DelReturn.Continue;
                            }

                            return DelReturn.Continue;
                        });
                    }
                    else if(!this.BaseInfo.SeedNearPlayer)
                    {
                        do
                        {
                            //debugCode = 600;
                            World_AIW2.Instance.DoForPlanetsSingleThread(false, delegate (Planet planet)
                            {
                                if (planet.GetControllingFactionType() == FactionType.Player)
                                    return DelReturn.Continue;
                                if (planet.GetFactionWithSpecialInfluenceHere().Type != FactionType.NaturalObject && preferredHomeworldDistance >= 4) //don't seed over a minor faction if we are finding good spots
                                {
                                    return DelReturn.Continue;
                                }
                                if (planet.IsPlanetToBeDestroyed || planet.HasPlanetBeenDestroyed)
                                    return DelReturn.Continue;
                                if (planet.PopulationType == PlanetPopulationType.AIBastionWorld ||
                                        planet.IsZenithArchitraveTerritory)
                                {
                                    return DelReturn.Continue;
                                }
                                //debugCode = 800;
                                if (planet.OriginalHopsToAIHomeworld >= preferredHomeworldDistance &&
                                        (planet.OriginalHopsToHumanHomeworld == -1 ||
                                        planet.OriginalHopsToHumanHomeworld >= preferredHomeworldDistance))
                                    workingAllowedSpawnPlanets.Add(planet);
                                return DelReturn.Continue;
                            });

                            preferredHomeworldDistance--;
                            //No need to go past the first loop if we are to seed near the player
                            if (preferredHomeworldDistance == 0)
                                break;
                        } while (workingAllowedSpawnPlanets.Count < 6);
                    }
                    //debugCode = 900;
                    if(workingAllowedSpawnPlanets.Count != 0)
                    {
                        Context.RandomToUse.ReinitializeWithSeed(Engine_Universal.PermanentQualityRandom.Next() + AttachedFaction.FactionIndex);
                        spawnPlanet = workingAllowedSpawnPlanets[Context.RandomToUse.Next(0, workingAllowedSpawnPlanets.Count)];

                        //instead of spawning on this planet, create a new planet linked to it
                        if (AttachedFaction.GetStringValueForCustomFieldOrDefaultValue("SpawningOptions", true) == "Invasion")
                        {
                            spawnPlanet = CreateSpawnPlanet(Context, spawnPlanet);
                        }
                        PlanetFaction pFaction = spawnPlanet.GetPlanetFactionForFaction(AttachedFaction);
                        ArcenPoint spawnLocation = spawnPlanet.GetSafePlacementPointAroundPlanetCenter(Context, entityData, FInt.FromParts(0, 200), FInt.FromParts(0, 600));

                        var originalDireTelium = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, entityData.MarkFor(pFaction),
                                                        pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Dire Macrophage Telium");

                        AttachedFaction.HasDoneInvasionStyleAction = true;

                        SquadViewChatHandlerBase chatHandlerOrNull = ChatClickHandler.CreateNewAs<SquadViewChatHandlerBase>("ShipGeneralFocus");
                        if (chatHandlerOrNull != null)
                            chatHandlerOrNull.SquadToView = LazyLoadSquadWrapper.Create(originalDireTelium);

                        string planetStr = "";
                        if (spawnPlanet.GetDoHumansHaveVision())
                        {
                            planetStr = " from " + spawnPlanet.Name;
                        }

                        var str = string.Format("<color=#{0}>{1}</color> are invading{2}!", AttachedFaction.FactionCenterColor.ColorHexBrighter, AttachedFaction.GetDisplayName(), planetStr);
                        World_AIW2.Instance.QueueChatMessageOrCommand(str, ChatType.LogToCentralChat, chatHandlerOrNull);
                    }
                    //if (workingAllowedSpawnPlanets.Count == 0)
                    //   throw new Exception("Unable to find a place to spawn the Dire Macrophage");

                    // This is not actually random unless we set the seed ourselves.
                    // Since other processing happening before us tends to set the seed to the same value repeatedly.
                    
                }
                
            }
            // If we're effectively dead, stop processing.
            //I need to still process it even if no telia, as in this function lies the deletion of rogue macrophage elements
            //This does include colonisers however, who do not get deleted
            if ( BaseInfo.DireTelia.Count <= 0 && BaseInfo.DireHarvesters.Count <= 0 && BaseInfo.MacrophageBastionsInGalaxy.Count <= 0 && BaseInfo.MacrophageFortificationsInGalaxy.Count <= 0 && BaseInfo.MacrophageColonisers.Count <= 0)
                return;

            //we only want to call SetExternalData when something has changed
            //bool harvesterExternalDataUpdateRequired = false;
            //bool sporeExternalDataUpdateRequired = false;
            int rangeForMetalDeposit = 600;
            int totalMetalIncomeAccrossAllTelia = 0;

            if ( BaseInfo.MetalHarvesterCanHold <= 0 )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "BUG: failed to parse MetalHarvesterCanHold: " + BaseInfo.MetalHarvesterCanHold + " forevent " + BaseInfo.GetEventCost( 0 ) + " this indicates something wrong with the ExternalConstants xml", Verbosity.DoNotShow );
                return;
            }

            // Reset all of our Harvester data.
            List<SafeSquadWrapper> harvesters = this.BaseInfo.DireHarvesters.GetDisplayList();
            for (int i = 0; i < harvesters.Count; i++ )
            {
                GameEntity_Squad entity = harvesters[i].GetSquad();
                if (entity == null)
                   continue;
                DireMacrophagePerHarvesterBaseInfo hData = entity.GetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();

                int totalMetalHarvesterCanHold = BaseInfo.MetalHarvesterCanHold + entity.CurrentMarkLevel * BaseInfo.DireMacrophageMaxMetalHarvesterCanHoldPerMark;
                int totalMetalEvolvedHarvesterCanHold = totalMetalHarvesterCanHold * BaseInfo.EvolvedDireMacrophageMetalHoldMult + ((entity.CurrentMarkLevel - 1) * BaseInfo.DireMacrophageMaxMetalHarvesterCanHoldPerMark * BaseInfo.EvolvedDireMacrophageMetalHoldMult);
                //We must set whether the harvester is Evolved and a Carrier or not, courtesy of loading shenanigans
                //tbf if I'm doing this like this why am I even bothering to have isEvolved and isCarrier? This is what I would have done if I knew how to do it in the first
                //But alas we now have a huge pile of spaghetti
                if (entity.TypeData.GetHasTag(DireMacrophageFactionBaseInfoCore.RegularDireCarrierTag))
                {
                    hData.IsCarrier = true;
                    hData.IsEvolved = false;
                }
                if (entity.TypeData.GetHasTag(DireMacrophageFactionBaseInfoCore.EvolvedDireHarvesterTag))
                {
                    hData.IsCarrier = false;
                    hData.IsEvolved = true;
                }
                if (entity.TypeData.GetHasTag(DireMacrophageFactionBaseInfoCore.RegularEvolvedDireCarrierTag))
                {
                    hData.IsCarrier = false;
                    hData.IsEvolved = true;
                }
                if (hData.CurrentMetal >= totalMetalHarvesterCanHold && !hData.IsEvolved)
                {
                    if (DireMacrophageFactionBaseInfo.debug && !hData.ReturningToTelium)
                       ArcenDebugging.ArcenDebugLogSingleLine("Dire Harvester " + entity.PrimaryKeyID + " has " + hData.CurrentMetal + " which is >= " + totalMetalHarvesterCanHold + " head back to telium", Verbosity.DoNotShow);
                    hData.ReturningToTelium = true;
                }
                if (hData.CurrentMetal >= totalMetalEvolvedHarvesterCanHold && hData.IsEvolved)
                {
                    if (DireMacrophageFactionBaseInfo.debug && !hData.ReturningToTelium)
                        ArcenDebugging.ArcenDebugLogSingleLine("Evolved Dire Harvester " + entity.PrimaryKeyID + " has " + hData.CurrentMetal + " which is >= " + totalMetalEvolvedHarvesterCanHold + " head back to telium", Verbosity.DoNotShow);
                    hData.ReturningToTelium = true;
                }
            }
            // Reset all of our bastion data
            List<SafeSquadWrapper> bastions = this.BaseInfo.MacrophageBastionsInGalaxy.GetDisplayList();
            for (int i = 0; i < bastions.Count; i++)
            {
                GameEntity_Squad bastion = bastions[i].GetSquad();
                DireMacrophagePerDefenseBaseInfo dData = bastion.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
                if (bastion == null)
                    continue;
            }
            // Reset all of our forti data
            List<SafeSquadWrapper> fortifications = this.BaseInfo.MacrophageFortificationsInGalaxy.GetDisplayList();
            for (int i = 0; i < fortifications.Count; i++)
            {
                GameEntity_Squad fortification = fortifications[i].GetSquad();
                DireMacrophagePerDefenseBaseInfo fData = fortification.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
                if (fortification == null)
                    continue;
            }
            // Reset all of our colonisers data
            List<SafeSquadWrapper> colonisers = this.BaseInfo.MacrophageColonisers.GetDisplayList();
            for (int i = 0; i < colonisers.Count; i++)
            {
                GameEntity_Squad entity = colonisers[i].GetSquad();
                if (entity == null)
                    continue;
            }

            //Now that the Telia list is set, handle its harvesters
            List<SafeSquadWrapper> telia = this.BaseInfo.DireTelia.GetDisplayList();
            for ( int i = 0; i < telia.Count; i++ )
            {
                GameEntity_Squad telium = telia[i].GetSquad();
                if ( telium == null )
                    continue;
                DireMacrophagePerTeliumBaseInfo tData = telium.CreateExternalBaseInfo<DireMacrophagePerTeliumBaseInfo>( "DireMacrophagePerTeliumBaseInfo" );
                int numHarvestersForThisTelium = 0;
                tData.MetalIncomeFromDefenses = 0;//Reset this otherwise infinite stack

                for ( int j = 0; j < harvesters.Count; j++ )
                {
                    GameEntity_Squad harvester = harvesters[j].GetSquad();
                    if ( harvester == null )
                        continue;

                    DireMacrophagePerHarvesterBaseInfo hData = harvester.GetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                    //Metal Generation Bonus
                    int totalMetalHarvesterCanHold = BaseInfo.MetalHarvesterCanHold + harvester.CurrentMarkLevel * BaseInfo.DireMacrophageMaxMetalHarvesterCanHoldPerMark - 1;
                    int totalMetalEvolvedHarvesterCanHold = totalMetalHarvesterCanHold * BaseInfo.EvolvedDireMacrophageMetalHoldMult + ((harvester.CurrentMarkLevel - 1) * BaseInfo.DireMacrophageMaxMetalHarvesterCanHoldPerMark * BaseInfo.EvolvedDireMacrophageMetalHoldMult);
                    if (hData.TeliumID == telium.PrimaryKeyID)
                    {
                        //If this harvester belongs to this Telium, remove it from the general list
                        //and add it to the specific list for this telium
                        if (Mat.DistanceBetweenPointsImprecise(harvester.WorldLocation, telium.WorldLocation) < rangeForMetalDeposit)
                        {
                            // Macrophage has returned to its Telium with enough metal. Transfer metal, attempt to mark up, and restore shields.
                            tData.MetalGainedFromHarvesters += hData.CurrentMetal;
                            tData.TotalMetalEverCollected += hData.CurrentMetal;
                            tData.CurrentMetal += hData.CurrentMetal;
                            if (DireMacrophageFactionBaseInfo.debug && hData.CurrentMetal > 0)
                                ArcenDebugging.ArcenDebugLogSingleLine("FLAGFLAGFLAG Dire harvester " + harvester.PrimaryKeyID + " has arrived to the telium at " + harvester.GetPlanetName_Safe() + " to deposit " + hData.CurrentMetal + ". The telium now has " + tData.CurrentMetal, Verbosity.DoNotShow);

                            // If a full delivery, restore shields and let them have a chance to mark up. Default chance of 50%, reduced by 8% for every mark above 1.
                            if (hData.CurrentMetal >= totalMetalHarvesterCanHold && !hData.IsEvolved)
                            {
                                if (harvester.CurrentMarkLevel < 7)
                                {
                                    // Roll our virtual 100 sided die. Also has a small scaling with intensity
                                    byte chance = (byte)(BaseInfo.BasePercentChanceToMarkUp - ((harvester.CurrentMarkLevel - 1) * BaseInfo.ReductionPerMarkForPercentChanceToMarkUp) + (BaseInfo.EffectiveIntensity / 2));
                                    if (Context.RandomToUse.Next(0, 100) < chance)
                                    {
                                        harvester.SetCurrentMarkLevel((byte)(harvester.CurrentMarkLevel + 1));
                                        if (DireMacrophageFactionBaseInfo.debug)
                                            ArcenDebugging.ArcenDebugLogSingleLine("Dire Harvester " + harvester.PrimaryKeyID + " has marked up with a chance of " + chance + "%", Verbosity.DoNotShow);
                                    }
                                    else if (DireMacrophageFactionBaseInfo.debug)
                                        ArcenDebugging.ArcenDebugLogSingleLine("Dire Harvester " + harvester.PrimaryKeyID + " has failed to mark up with a chance of " + chance + "%", Verbosity.DoNotShow);
                                }
                                if(harvester.CurrentMarkLevel == 7)
                                {
                                    byte chance = (byte)((BaseInfo.BasePercentChanceToMarkUp / 2) - 15 + BaseInfo.EffectiveIntensity);//25% chance on base settings
                                    if (Context.RandomToUse.Next(0, 100) < chance && !hData.IsCarrier)
                                    {
                                        harvester.Despawn(Context, true, InstancedRendererDeactivationReason.IAmTransforming);
                                        SpawnNewEvolvedHarvester(Context, telium, DireMacrophageFactionBaseInfo.debug);
                                    }
                                    if (Context.RandomToUse.Next(0, 100) < chance && hData.IsCarrier)
                                    {
                                        harvester.Despawn(Context, true, InstancedRendererDeactivationReason.IAmTransforming);
                                        SpawnNewEvolvedCarrier(Context, telium, DireMacrophageFactionBaseInfo.debug);
                                    }
                                }

                                // Restore its shields
                                harvester.TakeShieldRepair(harvester.TypeData.GetForMark(harvester.CurrentMarkLevel).BaseShieldPoints);
                            }
                            if(hData.CurrentMetal >= totalMetalEvolvedHarvesterCanHold && hData.IsEvolved)
                            {
                                if (harvester.CurrentMarkLevel < 7)
                                {
                                    // Roll our virtual 100 sided die.
                                    byte chance = (byte)(BaseInfo.BasePercentChanceToMarkUp - ((harvester.CurrentMarkLevel - 1) * BaseInfo.ReductionPerMarkForPercentChanceToMarkUp) + (BaseInfo.EffectiveIntensity / 2));
                                    if (Context.RandomToUse.Next(0, 100) < chance)
                                    {
                                        harvester.SetCurrentMarkLevel((byte)(harvester.CurrentMarkLevel + 1));
                                        if (DireMacrophageFactionBaseInfo.debug)
                                            ArcenDebugging.ArcenDebugLogSingleLine("Evolved Dire Harvester " + harvester.PrimaryKeyID + " has marked up with a chance of " + chance + "%", Verbosity.DoNotShow);
                                    }
                                    else if (DireMacrophageFactionBaseInfo.debug)
                                        ArcenDebugging.ArcenDebugLogSingleLine("Evolved Dire Harvester " + harvester.PrimaryKeyID + " has failed to mark up with a chance of " + chance + "%", Verbosity.DoNotShow);
                                }
                                harvester.TakeShieldRepair(harvester.TypeData.GetForMark(harvester.CurrentMarkLevel).BaseShieldPoints);
                            }

                            hData.CurrentMetal = 0;
                            hData.ReturningToTelium = false;
                        }
                        numHarvestersForThisTelium++;
                    }
                }
                //Mark to build stuff. It gets sus after 3 but I still put it just in case there are some humongus sized fights happening for a reason or another
                //Also a bit less marks for the coloniser, they start spawning at 20k metal but you'd already be mark 4, which wouldn't really be cool
                byte markLeveltoSpawn = 1;
                byte markLeveltoSpawnColoniser = 1;
                if (tData.ExtraMetalGenerationPerSecond < 2000)
                {
                    markLeveltoSpawn = 1;
                    markLeveltoSpawnColoniser = 1;
                }
                else if (tData.ExtraMetalGenerationPerSecond < 6000)
                {
                    markLeveltoSpawn = 2;
                    markLeveltoSpawnColoniser = 1;
                }
                else if (tData.ExtraMetalGenerationPerSecond < 12000)
                {
                    markLeveltoSpawn = 3;
                    markLeveltoSpawnColoniser = 2;
                }
                else if (tData.ExtraMetalGenerationPerSecond < 24000)
                {
                    markLeveltoSpawn = 4;
                    markLeveltoSpawnColoniser = 3;
                }
                else if (tData.ExtraMetalGenerationPerSecond < 56000)
                {
                    markLeveltoSpawn = 5;
                    markLeveltoSpawnColoniser = 4;
                }
                else if (tData.ExtraMetalGenerationPerSecond < 100000)
                {
                    markLeveltoSpawn = 6;
                    markLeveltoSpawnColoniser = 5;
                }
                else
                {
                    markLeveltoSpawn = 7;
                    markLeveltoSpawnColoniser = 6;
                }
                if(tData.ExtraMetalGenerationPerSecond > 200000)
                {
                    markLeveltoSpawnColoniser = 7;
                }
                //Checking our bastions shit
                for (int j = 0; j < bastions.Count; j++)
                {
                    GameEntity_Squad bastion = bastions[j].GetSquad();
                        if (bastion == null)
                        continue;

                    DireMacrophagePerDefenseBaseInfo dData = bastion.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
                    //Metal Generation Bonus
                    if (dData.TeliumID == telia[i].PrimaryKeyID)
                    { 
                        //If this bastion belongs to this Telium, remove it from the general list
                        //and add it to the specific list for this telium
                        if (dData.CurrentMetal >= this.BaseInfoCore.GetDefenseMarkupCost(bastion))
                        {
                            if (bastion.CurrentMarkLevel < 7)
                            {
                                bastion.SetCurrentMarkLevel((byte)(bastion.CurrentMarkLevel + 1));
                                dData.CurrentMetal = 0;
                                if (DireMacrophageFactionBaseInfo.debug)
                                    ArcenDebugging.ArcenDebugLogSingleLine("Macrophage Bastion " + bastion.PrimaryKeyID + " has marked up", Verbosity.DoNotShow);
                            }
                            if(bastion.CurrentMarkLevel == 7)//Instead of marking up spawn constructors
                            {
                                SpawnNewConstructor(Context, dData.TeliumID, bastion, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                                dData.CurrentMetal = 0;
                                if (DireMacrophageFactionBaseInfo.debug)
                                    ArcenDebugging.ArcenDebugLogSingleLine("Macrophage Bastion " + bastion.PrimaryKeyID + " is spawning a constructor", Verbosity.DoNotShow);
                            }
                        }
                        tData.MetalIncomeFromDefenses += dData.ExtraMetalGenerationPerSecondForHomeTelium;
                    }
                }
                //checking fortifications shit
                for (int j = 0; j < fortifications.Count; j++)
                {
                    GameEntity_Squad fortification = fortifications[j].GetSquad();
                    if (fortification == null)
                        continue;

                    DireMacrophagePerDefenseBaseInfo fData = fortification.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
                    //Metal Generation Bonus
                    if (fData.TeliumID == telia[i].PrimaryKeyID)
                    {
                        //If this fortification belongs to this Telium, remove it from the general list
                        //and add it to the specific list for this telium
                        if (fData.CurrentMetal >= this.BaseInfoCore.GetDefenseMarkupCost(fortification))
                        {
                            if (fortification.CurrentMarkLevel < 7)
                            {
                                fortification.SetCurrentMarkLevel((byte)(fortification.CurrentMarkLevel + 1));
                                fData.CurrentMetal = 0;
                                if (DireMacrophageFactionBaseInfo.debug)
                                    ArcenDebugging.ArcenDebugLogSingleLine("Macrophage Fortification " + fortification.PrimaryKeyID + " has marked up", Verbosity.DoNotShow);
                            }
                            if (fortification.CurrentMarkLevel == 7)//Instead of marking up spawn constructors
                            {
                                SpawnNewConstructor(Context, fData.TeliumID, fortification, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                                fData.CurrentMetal = 0;
                                if (DireMacrophageFactionBaseInfo.debug)
                                    ArcenDebugging.ArcenDebugLogSingleLine("Macrophage Fortification " + fortification.PrimaryKeyID + " is spawning a constructor", Verbosity.DoNotShow);
                            }
                        }
                        tData.MetalIncomeFromDefenses += fData.ExtraMetalGenerationPerSecondForHomeTelium;
                    }
                }

                tData.CurrentHarvesters = numHarvestersForThisTelium;
                tData.ExtraMetalGenerationPerSecond = (BaseInfo.EffectiveIntensity / 2) * (tData.MetalGainedFromHarvesters / BaseInfo.MetalHarvesterCanHold) + tData.MetalIncomeFromDefenses;
                totalMetalIncomeAccrossAllTelia += tData.ExtraMetalGenerationPerSecond; //For fortifications stuff
                if ( numHarvestersForThisTelium > 0 )
                    tData.CurrentMetal += (BaseInfo.MetalGenerationPerSecond * BaseInfo.MetalGenerationMultiplierWithLivingHarvesters).IntValue + tData.ExtraMetalGenerationPerSecond;
                else
                    tData.CurrentMetal += BaseInfo.MetalGenerationPerSecond + tData.ExtraMetalGenerationPerSecond;
                // Get our event cost, and save it to our telium for display purposes.
                int cost = BaseInfo.GetEventCost( numHarvestersForThisTelium );
                tData.MetalForNextBuild = cost;

                //If we have enough metal at the Telium, spawn spores or a new Harvester
                if ( tData.CurrentMetal >= cost )
                {
                    int random = Context.RandomToUse.Next(0, 100);
                    //Higher intensities get more borers, +0.5% per intensity
                    if (random <= (10 + BaseInfo.EffectiveIntensity/2) && numHarvestersForThisTelium >= 2)
                    {
                        SpawnNewBorer(Context, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                    } //10% to spawn a carrier always, they have FAR better survivability in PVE
                    else if (random <= (20 + BaseInfo.EffectiveIntensity/2) && numHarvestersForThisTelium >= 2)
                    {
                        SpawnNewCarrier(Context, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                    } //20% to spawn a constructor, +1% per intensity
                    else if (random <= (40 + BaseInfo.EffectiveIntensity/2 + BaseInfo.EffectiveIntensity) && numHarvestersForThisTelium >= 2)
                    {
                        SpawnNewConstructor(Context, telium.PrimaryKeyID, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                        //Spawn an extra at intensity 7 and above
                        if(BaseInfo.EffectiveIntensity >= 7)
                        {
                            SpawnNewConstructor(Context, telium.PrimaryKeyID, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                        }
                    }
                    else
                    {
                        SpawnNewHarvester(Context, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                        if(numHarvestersForThisTelium == 0) //Prevents getting softlocked like an idiot when at gamestart
                        {
                            SpawnNewHarvester(Context, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                        }
                    }
                    if ( DireMacrophageFactionBaseInfo.debug )
                        ArcenDebugging.ArcenDebugLogSingleLine( "We have " + tData.CurrentMetal + " let's spawn " + 1 + " harvester", Verbosity.DoNotShow );

                    tData.CurrentMetal = 0;

                }
                //Raise cost by 10k everytime one is spawned
                int newColoniserRequirements = (20000 + (5000 * tData.NumberOfTimeSpawnedColoniser)) * (tData.NumberOfTimeSpawnedColoniser + 1);
                if (tData.ExtraMetalGenerationPerSecond > newColoniserRequirements)
                {
                    SpawnNewColoniser(Context, telium.PrimaryKeyID, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawnColoniser);
                    tData.NumberOfTimeSpawnedColoniser++;
                }
                //If we want to spawn a harvester before the accumulation of metal would typically allow, handle that  here
                //this is set in the XML
                if ( World_AIW2.Instance.GameSecond == BaseInfo.EarlyHarvesterSpawnTime )
                {
                    SpawnNewHarvester( Context, telium, DireMacrophageFactionBaseInfo.debug, markLeveltoSpawn);
                }
            }
            if(totalMetalIncomeAccrossAllTelia < 20000) //Will want to define such a thing inside the external constants, one day
            {
                this.BaseInfo.MaximumFortifications = 0;
            }
            else
            {
                this.BaseInfo.MaximumFortifications = totalMetalIncomeAccrossAllTelia / 20000;
            }


            //The code below is for handling the case where they don't find their telium, meaning it died.
            //Despawns the associated entities
            //Constructors not included, they would delete themselves anyways after turning into a Bastion
            for (int j = 0; j < harvesters.Count; j++)
            {
                GameEntity_Squad harvester = harvesters[j].GetSquad();
                if(harvester == null)
                {
                    continue;
                }
                DireMacrophagePerHarvesterBaseInfo hData = harvester.GetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                GameEntity_Squad telium = GetTeliumForHarvester(harvester, hData);

                if (telium == null) //You should kill yourself NOW
                {
                    harvester.Despawn(Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut);
                }
            }
            //Check to delete bastions
            for (int i = 0; i < bastions.Count; i++)
            {
                GameEntity_Squad bastion = bastions[i].GetSquad();
                if (bastion == null)
                    continue;

                DireMacrophagePerDefenseBaseInfo dData = bastion.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
                GameEntity_Squad telium = GetTeliumForDefenses(bastion, dData);

                if (telium == null) //You should kill yourself NOW
                {
                    bastion.Despawn(Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut);
                }
            }
            //Check to delete fortifications
            for (int i = 0; i < fortifications.Count; i++)
            {
                GameEntity_Squad fortification = fortifications[i].GetSquad();
                if (fortification == null)
                    continue;

                DireMacrophagePerDefenseBaseInfo fData = fortification.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
                GameEntity_Squad telium = GetTeliumForDefenses(fortification, fData);
                if (telium == null) //You should kill yourself NOW
                {
                    fortification.Despawn(Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut);
                }
            }
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has(ArcenTracingFlags.HumanMarauders);//That means enabling this shit alows us to have tracing as well epic
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate("Macrophage-DoPerSecondLogic_Stage3Main_OnMainThreadAndPartOfSim_HostOnly-trace", 10f) : null;
            this.updateWaveData(AttachedFaction, Context, tracingBuffer);
            /*for ( int j = 0; j < harvesters.Count; j++ )
            {
                GameEntity_Squad harvester = harvesters[j].GetSquad();
                if ( harvester == null )
                    continue;
                DireMacrophagePerHarvesterBaseInfo hData = harvester.GetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
                if ( hData.HasLivingTelium == false )
                {
                    //try to despawn everyone here
                }
            }*/
        }

        
        public void SpawnNewHarvester( ArcenHostOnlySimContext Context, GameEntity_Squad telium, bool debug, byte markLevel)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, DireMacrophageFactionBaseInfo.RegularDireHarvesterTag );
            ArcenPoint spawnLocation = telium.WorldLocation;
            PlanetFaction pFaction = telium.Planet.GetPlanetFactionForFaction( AttachedFaction );
            if ( entityData == null )
                ArcenDebugging.ArcenDebugLogSingleLine( "BUG: no MacrophageHarvestern tag found", Verbosity.DoNotShow );
            GameEntity_Squad harvester = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, entityData, markLevel,
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Harvester" );
            if ( harvester == null )
                return;

            harvester.Orders.SetBehaviorDirectlyInSim( EntityBehaviorType.Attacker_Full );
            DireMacrophagePerHarvesterBaseInfo hData = harvester.CreateExternalBaseInfo<DireMacrophagePerHarvesterBaseInfo>( "DireMacrophagePerHarvesterBaseInfo" );
            hData.TeliumID = telium.PrimaryKeyID;
            hData.TotalMetalEverCollected = 0;
            hData.CurrentMetal = 0;
            hData.IsEvolved = false;
            hData.IsCarrier = false;
            harvester.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if ( DireMacrophageFactionBaseInfo.debug )
                ArcenDebugging.ArcenDebugLogSingleLine( "Spawned a new harvester " + harvester.PrimaryKeyID + " for telium " + telium.PrimaryKeyID, Verbosity.DoNotShow );
        }

        public void SpawnNewCarrier(ArcenHostOnlySimContext Context, GameEntity_Squad telium, bool debug, byte markLevel)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.RegularDireCarrierTag);
            ArcenPoint spawnLocation = telium.WorldLocation;
            PlanetFaction pFaction = telium.Planet.GetPlanetFactionForFaction(AttachedFaction);
            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no MacrophageCarrier tag found", Verbosity.DoNotShow);
            GameEntity_Squad harvester = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, markLevel,
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Carrier");
            if (harvester == null)
                return;

            harvester.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            DireMacrophagePerHarvesterBaseInfo hData = harvester.CreateExternalBaseInfo<DireMacrophagePerHarvesterBaseInfo>("DireMacrophagePerHarvesterBaseInfo");
            hData.TeliumID = telium.PrimaryKeyID;
            hData.TotalMetalEverCollected = 0;
            hData.CurrentMetal = 0;
            hData.IsEvolved = false;
            hData.IsCarrier = true;
            harvester.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new carrier " + harvester.PrimaryKeyID + " for telium " + telium.PrimaryKeyID, Verbosity.DoNotShow);
        }

        public void SpawnNewEvolvedHarvester(ArcenHostOnlySimContext Context, GameEntity_Squad telium, bool debug, byte markLevel = 1)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.RegularEvolvedDireHarvesterTag);
            ArcenPoint spawnLocation = telium.WorldLocation;
            PlanetFaction pFaction = telium.Planet.GetPlanetFactionForFaction(AttachedFaction);
            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no MacrophageHarvestern tag found", Verbosity.DoNotShow);
            GameEntity_Squad harvester = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, markLevel,
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Harvester");
            if (harvester == null)
                return;

            harvester.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            DireMacrophagePerHarvesterBaseInfo hData = harvester.CreateExternalBaseInfo<DireMacrophagePerHarvesterBaseInfo>("DireMacrophagePerHarvesterBaseInfo");
            hData.TeliumID = telium.PrimaryKeyID;
            hData.TotalMetalEverCollected = 0;
            hData.CurrentMetal = 0;
            hData.IsEvolved = true;
            hData.IsCarrier = false;
            harvester.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new harvester " + harvester.PrimaryKeyID + " for telium " + telium.PrimaryKeyID, Verbosity.DoNotShow);
        }

        public void SpawnNewEvolvedCarrier(ArcenHostOnlySimContext Context, GameEntity_Squad telium, bool debug, byte markLevel = 1)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.RegularEvolvedDireCarrierTag);
            ArcenPoint spawnLocation = telium.WorldLocation;
            PlanetFaction pFaction = telium.Planet.GetPlanetFactionForFaction(AttachedFaction);
            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no MacrophageHarvestern tag found", Verbosity.DoNotShow);
            GameEntity_Squad harvester = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, markLevel,
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Carrier");
            if (harvester == null)
                return;

            harvester.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            DireMacrophagePerHarvesterBaseInfo hData = harvester.CreateExternalBaseInfo<DireMacrophagePerHarvesterBaseInfo>("DireMacrophagePerHarvesterBaseInfo");
            hData.TeliumID = telium.PrimaryKeyID;
            hData.TotalMetalEverCollected = 0;
            hData.CurrentMetal = 0;
            hData.IsEvolved = true;
            hData.IsCarrier = true;
            harvester.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new carrier " + harvester.PrimaryKeyID + " for telium " + telium.PrimaryKeyID, Verbosity.DoNotShow);
        }

        public void SpawnNewBorer(ArcenHostOnlySimContext Context, GameEntity_Squad telium, bool debug, byte markLevel)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireMacrophageBorer);
            ArcenPoint spawnLocation = telium.WorldLocation;
            PlanetFaction pFaction = telium.Planet.GetPlanetFactionForFaction(AttachedFaction);
            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no DireMacrophageBorer tag found", Verbosity.DoNotShow);
            GameEntity_Squad borer = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, markLevel,
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Harvester");
            if (borer == null)
                return;
            borer.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            //We can use the DireMacrophage Base Info, it doesn't matter much since the goal is to suicide
            DireMacrophagePerHarvesterBaseInfo bData = borer.CreateExternalBaseInfo<DireMacrophagePerHarvesterBaseInfo>("DireMacrophagePerHarvesterBaseInfo");
            bData.TeliumID = telium.PrimaryKeyID;
            bData.TotalMetalEverCollected = 0;
            bData.CurrentMetal = 0;
            bData.IsEvolved = false;
            bData.IsCarrier = false;
            borer.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new borer " + borer.PrimaryKeyID + " for telium " + telium.PrimaryKeyID, Verbosity.DoNotShow);
        }

        public void SpawnNewConstructor(ArcenHostOnlySimContext Context, int teliumID, GameEntity_Squad entityFromWhichToSpawn, bool debug, byte markLevel)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireMacrophageConstructor);
            ArcenPoint spawnLocation = entityFromWhichToSpawn.WorldLocation;
            PlanetFaction pFaction = entityFromWhichToSpawn.Planet.GetPlanetFactionForFaction(AttachedFaction);
            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no DireMacrophageConstructor tag found", Verbosity.DoNotShow);
            GameEntity_Squad constructor = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, markLevel,
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Constructor");
            if (constructor == null)
                return;

            constructor.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            DireMacrophagePerConstructorBaseInfo cData = constructor.CreateExternalBaseInfo<DireMacrophagePerConstructorBaseInfo>("DireMacrophagePerConstructorBaseInfo");
            cData.TeliumID = teliumID;
            cData.SpotFound = false;
            constructor.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new constructor " + constructor.PrimaryKeyID + " for telium " + teliumID, Verbosity.DoNotShow);
        }

        //Uses the TemplarPerUnitBaseInfo. No I do not care at all
        public void SpawnNewColoniser(ArcenHostOnlySimContext Context, int teliumID, GameEntity_Squad entityFromWhichToSpawn, bool debug, byte markLevel)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireMacrophageColoniser);
            ArcenPoint spawnLocation = entityFromWhichToSpawn.WorldLocation;
            PlanetFaction pFaction = entityFromWhichToSpawn.Planet.GetPlanetFactionForFaction(AttachedFaction);
            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no DireMacrophageColoniser tag found", Verbosity.DoNotShow);
            //Creating the coloniser
            GameEntity_Squad coloniser = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, markLevel,
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Coloniser");
            if (coloniser == null)
                return;
            coloniser.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            TemplarPerUnitBaseInfo cData = coloniser.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>("TemplarPerUnitBaseInfo");
            //Telling the coloniser what we're gonna build
            cData.UnitToBuild = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireTeliumTag);
            coloniser.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            Planet newTeliumPlanet = GetNewTeliumLocation(coloniser, Context);
            cData.PlanetIdx = newTeliumPlanet.Index;
            cData.LocationToBuild = ArcenPoint.ZeroZeroPoint;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new coloniser " + coloniser.PrimaryKeyID + " for telium " + teliumID, Verbosity.DoNotShow);
        }

        private GameEntity_Squad GetTeliumForHarvester( GameEntity_Squad harvester, DireMacrophagePerHarvesterBaseInfo hData )
        {
            if ( harvester == null )
                return null;
            DireMacrophagePerHarvesterBaseInfo harvesterInfo = harvester.GetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
            return World_AIW2.Instance.GetEntityByID_Squad( harvesterInfo.TeliumID );
        }

        //Override for constructors
        private GameEntity_Squad GetTeliumForHarvester(GameEntity_Squad harvester, DireMacrophagePerConstructorBaseInfo hData)
        {
            if (harvester == null)
                return null;
            DireMacrophagePerConstructorBaseInfo harvesterInfo = harvester.GetExternalBaseInfoAs<DireMacrophagePerConstructorBaseInfo>();
            return World_AIW2.Instance.GetEntityByID_Squad(harvesterInfo.TeliumID);
        }

        private GameEntity_Squad GetTeliumForDefenses(GameEntity_Squad harvester, DireMacrophagePerDefenseBaseInfo hData)
        {
            if (harvester == null)
                return null;
            DireMacrophagePerDefenseBaseInfo harvesterInfo = harvester.GetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();
            return World_AIW2.Instance.GetEntityByID_Squad(harvesterInfo.TeliumID);
        }

        //This makes it so we get influence if we have bastions on a planet. Also adds AIP based on current planets owned.
        public override void UpdatePlanetInfluence_HostOnly(ArcenHostOnlySimContext Context)
        {
            //reset the faction Influences for this one
            List<Planet> planetsInfluenced = Planet.GetTemporaryPlanetList("DireMacrophage-UpdatePlanetInfluence_HostOnly-planetsInfluenced", 10f);

            List<SafeSquadWrapper> BastionsInGalaxy = this.BaseInfo.MacrophageBastionsInGalaxy.GetDisplayList();
            for (int i = 0; i < BastionsInGalaxy.Count; i++)
            {
                GameEntity_Squad entity = BastionsInGalaxy[i].GetSquad();
                planetsInfluenced.AddIfNotAlreadyIn(entity.Planet);
            }
            //Every influenced planet adds 20 AIP
            MinorFactionAIPEquivalentSet((FInt)(planetsInfluenced.Count * 20));
            AttachedFaction.SetInfluenceForPlanetsToList(planetsInfluenced);
            Planet.ReleaseTemporaryPlanetList(planetsInfluenced);
        }

        //Deprecated, use TransformIntoDefense instead
        public void TransformIntoBastion(DireMacrophagePerConstructorBaseInfo cData, GameEntity_Squad cEntity, ArcenHostOnlySimContext Context, bool debug)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireMacrophageDefenseTag);

            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no DireMacrophageDefense tag found", Verbosity.DoNotShow);

            PlanetFaction pFaction = cEntity.PlanetFaction; //Check whether this works or not, faction should be the one of the constructor but not sure
            GameEntity_Squad bastion = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, cEntity.CurrentMarkLevel,
                pFaction.FleetUsedAtPlanet, 0, cEntity.WorldLocation, Context, "Macrophage-Harvester");
            if (bastion == null)
                return;
            bastion.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            //Using the DireMacrophagePerDefenseBaseInfo
            DireMacrophagePerDefenseBaseInfo dData = bastion.CreateExternalBaseInfo<DireMacrophagePerDefenseBaseInfo>("DireMacrophagePerDefenseBaseInfo");
            dData.TeliumID = cData.TeliumID;
            dData.TotalMetalEverCollected = 0;
            dData.CurrentMetal = 0;
            bastion.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new bastion " + bastion.PrimaryKeyID + " from constructor " + cEntity.PrimaryKeyID, Verbosity.DoNotShow);
        }

        public void TransformIntoDefense(DireMacrophagePerConstructorBaseInfo cData, GameEntity_Squad cEntity, ArcenHostOnlySimContext Context, bool debug)
        {
            if (cData.TransformingIntoThis == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no GameEntityData assigned", Verbosity.DoNotShow);

            PlanetFaction pFaction = cEntity.PlanetFaction; //Check whether this works or not, faction should be the one of the constructor but not sure
            GameEntity_Squad defense = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, cData.TransformingIntoThis, cEntity.CurrentMarkLevel,
                pFaction.FleetUsedAtPlanet, 0, cEntity.WorldLocation, Context, "Macrophage-Harvester");
            if (defense == null)
                return;
            defense.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            //Using the DireMacrophagePerDefenseBaseInfo
            DireMacrophagePerDefenseBaseInfo dData = defense.CreateExternalBaseInfo<DireMacrophagePerDefenseBaseInfo>("DireMacrophagePerDefenseBaseInfo");
            dData.TeliumID = cData.TeliumID;
            dData.TotalMetalEverCollected = 0;
            dData.CurrentMetal = 0;
            defense.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new bastion " + defense.PrimaryKeyID + " from constructor " + cEntity.PrimaryKeyID, Verbosity.DoNotShow);
        }

        //Code below will likely stay useless for its whole life, since now I'm using templar shit
        /*public void TransformIntoTelium(DireMacrophagePerConstructorBaseInfo cData, GameEntity_Squad cEntity, ArcenHostOnlySimContext Context, bool debug)
        {
            GameEntityTypeData entityData;
            entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, DireMacrophageFactionBaseInfo.DireTeliumTag);

            if (entityData == null)
                ArcenDebugging.ArcenDebugLogSingleLine("BUG: no DireTeliumTag tag found", Verbosity.DoNotShow);

            PlanetFaction pFaction = cEntity.PlanetFaction; //Check whether this works or not, faction should be the one of the constructor but not sure
            GameEntity_Squad telium = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, entityData, cEntity.CurrentMarkLevel,
                pFaction.FleetUsedAtPlanet, 0, cEntity.WorldLocation, Context, "Macrophage-Telium");
            if (telium == null)
                return;
            telium.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full);
            //Using the DireMacrophagePerDefenseBaseInfo
            DireMacrophagePerTeliumBaseInfo tData = telium.CreateExternalBaseInfo<DireMacrophagePerTeliumBaseInfo>("DireMacrophagePerTeliumBaseInfo");
            tData.CurrentHarvesters = 0;
            tData.ExtraMetalGenerationPerSecond = 2000; //Make something in the constants
            tData.MetalGainedFromHarvesters = 0;
            tData.MetalIncomeFromDefenses = 0;
            tData.TotalMetalEverCollected = 0;
            tData.CurrentMetal = 0;
            telium.ShouldNotBeConsideredAsThreatToHumanTeam = true;
            if (DireMacrophageFactionBaseInfo.debug)
                ArcenDebugging.ArcenDebugLogSingleLine("Spawned a new telium " + telium.PrimaryKeyID + " from constructor " + cEntity.PrimaryKeyID, Verbosity.DoNotShow);
        }*/

        private void updateWaveData(Faction faction, ArcenHostOnlySimContext Context, ArcenCharacterBufferBase tracingBuffer)
        {
            //update the information about the next wave coming toward this faction
            bool debug = false;
            bool tracing = tracingBuffer != null;
            if (BaseInfo.MacrophageBastionsInGalaxy.Count <= 0)
            {
                //no bastions no problems
                return;
            }
            bool hasHostileAi = false;
            World_AIW2.Instance.AIFactions.ForEach((f) =>
            {
                if (f.GetIsHostileTowards(AttachedFaction))
                {
                    hasHostileAi = true;
                }
            });
            if (!hasHostileAi)
            {
                //if allied to AI, no problems
                return;
            }
            int WaveInterval = 5; //Waves every 5 minutes
            FInt minimumWaveSize = FInt.FromParts(1200, 000);
            if (World_AIW2.Instance.GameSecond % 60 == 0)
            {
                //always use macrophage AIP
                FInt aipToUse = BaseInfo.MacrophageSpecificAIP; //wat, why do we have such things 2 times, deosn't seem to matter though
                FInt BaseWaveBudgetPerMinute = FInt.FromParts(4, 200);
                if(World_AIW2.Instance.AIFactions.Count == 1) //Less income when there are more AIs
                {
                    BaseWaveBudgetPerMinute = FInt.FromParts(3, 100);
                }
                if (World_AIW2.Instance.AIFactions.Count > 3) //Less income when there are more AIs
                {
                    BaseWaveBudgetPerMinute = FInt.FromParts(2, 100);
                }
                //Higher intensities have bigger waves (note that the budget above is ridiculously small)
                BaseWaveBudgetPerMinute *= BaseInfo.EffectiveIntensity * 3;
                if (BaseInfo.MacrophageSpecificAIP > 200)//More budget at more AIP
                {
                    BaseWaveBudgetPerMinute += BaseWaveBudgetPerMinute/2;   //So for some reason I can do this but not multiply by 0.5 fml
                }
                if (BaseInfo.MacrophageSpecificAIP > 600)//More budget at more AIP
                {
                    BaseWaveBudgetPerMinute += BaseWaveBudgetPerMinute / 2;
                }
                if (BaseInfo.MacrophageSpecificAIP > 1400)//Rare case where we own most of the galaxy, don't know why I put this there but here we are
                {
                    BaseWaveBudgetPerMinute += BaseWaveBudgetPerMinute / 2;
                }
                aipToUse = BaseInfo.MacrophageSpecificAIP;//always use the macros AIP
                //Prevent the increase if at more than 20 minutes of budget, I've seen some really unpleasant things
                
                FInt increase = aipToUse * BaseWaveBudgetPerMinute;
                if (this.BaseInfo.WaveData.currentWaveBudget <= (increase * 10))
                {
                    this.BaseInfo.WaveData.currentWaveBudget += increase;
                }
                if (tracing) tracingBuffer.Add("Macrophage anti-self wave budget is " + this.BaseInfo.WaveData.currentWaveBudget + " and most recent increase was " + increase + "\n");
            }
            if (this.BaseInfo.WaveData.timeForNextWave <= World_AIW2.Instance.GameSecond && this.BaseInfo.WaveData.currentWaveBudget >= minimumWaveSize)
            {
                //int budgetSpent = AntiMinorFactionWaveData.QueueWave(faction, Context, this.BaseInfo.WaveData.currentWaveBudget.GetNearestIntPreferringHigher(), true);
                PlannedWaveOptions options = PlannedWaveOptions.CreateWithBasics(
                    overrideEntityToSpawnAt: null, overrideSpawnUnits: null, targetFaction: faction, allowGuardians: true, allowDireGuardians: false);
                options.isReconquestWave = true;
                options.requiredWaveToDefenseRatio = FInt.Zero; //Necessary otherwise the AI only spends a small portion of the budget
                int budgetMemory = this.BaseInfo.WaveData.currentWaveBudget.ToInt(); //Otherwise the other AIs are gonna send 0 ships
                bool alreadyReducedWaveBudget = false;
                World_AIW2.Instance.AIFactions.ForEach((f) =>
                {
                    //ArcenDebugging.ArcenDebugLogSingleLine("Got inside", Verbosity.ShowAsError);
                    var sentinalData = f.GetAISentinelsCoreData();
                    if(sentinalData == null)
                    {
                        //ArcenDebugging.ArcenDebugLogSingleLine("Failed to get sentinal data", Verbosity.ShowAsError);
                        return; //AI might be dead, go next (need to see if we actually go next)
                    }
                    PlannedWave wave = sentinalData.PlanWave_OrGetNull(Context, budgetMemory, options);
                    //PlannedWave.GetFromPoolOrCreate();
                    int budgetSpent;
                    //wave.FinalComposition.Add()
                    if (wave == null) //dont forget to think about whether or not planets are connected grandmaster idiot instead of debugging uselessly
                    {
                        //the wave wasn't actually sent; presumably there are no valid targets
                        //start checking every minute
                        this.BaseInfo.WaveData.timeForNextWave = World_AIW2.Instance.GameSecond + 60;
                        //ArcenDebugging.ArcenDebugLogSingleLine("Failed to spawn wave", Verbosity.ShowAsError);
                        //ArcenDebugging.ArcenDebugLogSingleLine("We have " + budgetMemory + " budget to spend", Verbosity.ShowAsError);
                        return;
                    }
                    else
                    {
                        //ArcenDebugging.ArcenDebugLogSingleLine("Got past the else", Verbosity.ShowAsError);
                        sentinalData.WaveList.Add(wave);
                        this.BaseInfo.WaveData.timeForNextWave = World_AIW2.Instance.GameSecond + WaveInterval * 60;
                        budgetSpent = wave.aiCostBudgetForWave;
                        if (!alreadyReducedWaveBudget)
                        {
                            this.BaseInfo.WaveData.currentWaveBudget -= budgetSpent; //This means that if at least one wave goes through, remove all budget for others
                            alreadyReducedWaveBudget = true;
                        }
                    }
                    if (debug)
                        ArcenDebugging.ArcenDebugLogSingleLine("launching wave at " + World_AIW2.Instance.GameSecond + " amd spent budget " + budgetSpent + " next wave at " + this.BaseInfo.WaveData.timeForNextWave, Verbosity.DoNotShow);

                    if (tracing) tracingBuffer.Add("Macrophage requesting wave at " + World_AIW2.Instance.GameSecond + " with budget " + this.BaseInfo.WaveData.currentWaveBudget + " and next wave scheduled for " + this.BaseInfo.WaveData.timeForNextWave + "\n");
                });
                options.ReturnToPool();
            }
        }
        public void MinorFactionAIPEquivalentSet(FInt AIPEquivalent)
        {
            this.BaseInfo.MacrophageSpecificAIP = AIPEquivalent;
        }

        //Shamelessly stolen from templar
        private static readonly List<Planet> WorkingPlanetsList = List<Planet>.Create_WillNeverBeGCed(100, "DireMacrophageDeepInfo-WorkingPlanetsList");

        private Planet GetNewTeliumLocation( GameEntity_Squad entity, ArcenHostOnlySimContext Context )
        {
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "DireMacrophage-GetNewTeliumLocation-trace", 10f ) : null;

            List<SafeSquadWrapper> telia = this.BaseInfo.DireTelia.GetDisplayList();
            List<SafeSquadWrapper> constructors = this.BaseInfo.MacrophageColonisers.GetDisplayList();
            bool verboseDebug = false;
            WorkingPlanetsList.Clear();
            int debugCode = 0;
            try
            {
                entity.Planet.DoForPlanetsWithinXHops_NoFilters(4, delegate ( Planet planet, Int16 Distance)//
                {
                    debugCode = 400;
                    var pFaction = planet.GetStanceDataForFaction( AttachedFaction );
                    if ( tracing && verboseDebug )
                        tracingBuffer.Add( "\tChecking whether we can build on " + planet.Name ).Add( "\n" );
                    //I don't care at all whether there are ennemies or not
                    /*if ( pFaction[FactionStance.Hostile].TotalStrength > 0 )
                    {
                        if ( tracing && verboseDebug )
                            tracingBuffer.Add( "\t\tNo, hostile enemies" ).Add( "\n" );

                        return DelReturn.Continue;
                    }*/
                    //I don't care either if we owned a planet recently or something
                    /*if ( this.BaseInfo.LastTimePlanetWasOwned[planet] > 0 &&
                         ( World_AIW2.Instance.GameSecond - this.BaseInfo.LastTimePlanetWasOwned[planet] ) < BaseInfo.Difficulty.MinTimeBeforeRebuildingCastle )
                    {
                        if ( tracing && verboseDebug )
                            tracingBuffer.Add( "\t\tNo, we've recently owned this planet" ).Add( "\n" );

                        return DelReturn.Continue;
                    }*/
                    //This I care about, don't wanna have infinite teliums around
                    for ( int i = 0; i < telia.Count; i++ )
                    {
                        if (telia[i].Planet == planet )
                        {
                            if ( tracing && verboseDebug )
                                tracingBuffer.Add( "\t\tNo, we have a telium already" ).Add( "\n" );

                            return DelReturn.Continue;
                        }
                    }
                    for ( int i = 0; i < constructors.Count; i++ )
                    {
                        GameEntity_Squad ship = constructors[i].GetSquad();
                        if ( ship == null )
                            continue;
                        //make sure we don't have a constructor going here
                        TemplarPerUnitBaseInfo constructorData = ship.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                        if ( constructorData.PlanetIdx == planet.Index )
                        {
                            if ( tracing && verboseDebug )
                                tracingBuffer.Add( "\t\tNo, we have a constructor en route already" ).Add( "\n" );

                            return DelReturn.Continue;
                        }
                    }

                    WorkingPlanetsList.Add( planet );
                    if ( WorkingPlanetsList.Count > 10 ) //More choices, there aren't that many colonisers around so I want to have them spread to increase the faction's range
                        return DelReturn.Break;
                    return DelReturn.Continue;
                });

                if ( WorkingPlanetsList.Count == 0 )
                    return null;
                //We prefer further away planets (To make sure the Templar expand rapidly, and also
                //to give the player more chances to kill Constructors
                WorkingPlanetsList.Sort ( delegate( Planet L, Planet R )
                {
                    //sort from "furthest away" to "closest"
                    int lHops = L.GetHopsTo( entity.Planet );
                    int rHops = R.GetHopsTo( entity.Planet );
                    return rHops.CompareTo( lHops );
                } );

                for ( int i = 0; i < WorkingPlanetsList.Count; i++ )
                {
                    if ( Context.RandomToUse.Next( 0, 100 ) < 40 )
                        return WorkingPlanetsList[i];
                }
                return WorkingPlanetsList[0];
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit exception in GetNewTTeliumLocation debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
            return null;
        }

        public void HandleColonisersLRP(ArcenLongTermIntermittentPlanningContext Context, PerFactionPathCache PathCacheData)
        {
            int debugCode = 0;
            try
            {
                debugCode = 100;
                for (int i = 0; i < ConstructorsLRP.Count; i++)
                {
                    debugCode = 200;
                    GameEntity_Squad entity = ConstructorsLRP[i].GetSquad();
                    if (entity == null)
                        continue;
                    TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                    if (entity.HasQueuedOrders())
                        continue;
                    Planet destPlanet = World_AIW2.Instance.GetPlanetByIndex((short)data.PlanetIdx);
                    if (destPlanet == null)
                        continue; //should only be by race with sim
                    if (destPlanet != entity.Planet)
                        SendShipToPlanet(entity, destPlanet, Context, PathCacheData);//go to the planet
                    if (data.LocationToBuild == ArcenPoint.ZeroZeroPoint)
                        continue; //should only be by race with sim
                    SendShipToLocation(entity, data.LocationToBuild, Context);
                }
            }
            catch (Exception e)
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in HandleConstructorsLRP debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow);
            }
        }

        public void SendShipToPlanet(GameEntity_Squad entity, Planet destination, ArcenLongTermIntermittentPlanningContext Context, PerFactionPathCache PathCacheData)
        {
            PathBetweenPlanetsForFaction pathCache = PathingHelper.FindPathFreshOrFromCache(entity.PlanetFaction.Faction, "DireMacrophageSendShipToPlanet", entity.Planet, destination, PathingMode.Shortest, Context, PathCacheData);
            if (pathCache != null && pathCache.PathToReadOnly.Count > 0)
            {
                GameCommand command = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse);
                command.RelatedString = "DireMacrophage_Dest";
                command.RelatedEntityIDs.Add(entity.PrimaryKeyID);
                for (int k = 0; k < pathCache.PathToReadOnly.Count; k++)
                    command.RelatedIntegers.Add(pathCache.PathToReadOnly[k].Index);
                World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, command, false);
            }
        }

        public void SendShipToLocation(GameEntity_Squad entity, ArcenPoint dest, ArcenLongTermIntermittentPlanningContext Context)
        {
            GameCommand moveCommand = GameCommand.Create(BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCVisitTargetOnPlanet], GameCommandSource.AnythingElse);
            moveCommand.PlanetOrderWasIssuedFrom = entity.Planet.Index;
            moveCommand.RelatedPoints.Add(dest);
            moveCommand.RelatedEntityIDs.Add(entity.PrimaryKeyID);
            World_AIW2.Instance.QueueGameCommand(this.AttachedFaction, moveCommand, false);
        }
    }
}
