using System;

using System.Linq;
using System.Text;
using Arcen.AIW2.Core;
using Arcen.Universal;

namespace Arcen.AIW2.External
{
    /*
      Some Telia are seeded on the map at the beginning. Each Telium starts with one Harvester that harvests metal.
      When enough metal gets harvested the Telium will either release a bunch of spores or make another Harvester.
      Spores are cloaked and travel at random around the map. When Spores from multiple Telia wind up on the same planet
      they are consumed to create a Telium.

      Spores have a limited lifetime.

      If a Telium builds enough Harvester then some of them will Enrage and go attack either a player or ai homeworld.
      If a Telium dies then all its harvesters enrage.

      Each Spore, Telium and Macrophage track a bunch of data.
    */

    // Base Macrophage Class
    public abstract class DireMacrophageFactionDeepInfoBase : ExternalFactionDeepInfoRoot
    {
        //TEACHING_MOMENT: I have sealed this property in order to avoid any classes that inherit from this from making a mistake
        //It's imperative that the normal, enraged, and tamed macrophage all share the same name, which will mean they never run
        //at the same time.  The CalculateGatheringPoints_NotThreadsafe() meethod is run from the LRP, which is great, but we cannot
        //have two kinds of macrophage running that at once.  They must take turns, and this ensures that they do.

        public DireMacrophageFactionBaseInfoCore BaseInfoCore;
        public sealed override void DoAnyInitializationImmediatelyAfterFactionAssigned()
        {
            this.BaseInfoCore = this.AttachedFaction.GetExternalBaseInfoAs<DireMacrophageFactionBaseInfoCore>();
            this.SubDoAnyInitializationImmediatelyAfterFactionAssigned();
        }
        public abstract void SubDoAnyInitializationImmediatelyAfterFactionAssigned();

        // Whenever we load a save; we want to reset our above statics so we aren't carrying data we don't want between saves.
        protected sealed override void Cleanup()
        {
            BaseInfoCore = null;

            workingTeliumPlanets.Clear();

            this.SubCleanup();
        }
        protected abstract void SubCleanup();

        //This method can't be threadsafe, because it's updating a double-buffered list.
        //What do I mean by threadsafe?  It can't be called by two threads at the same time.
        //It can be called by any thread, but it should never be called from two different threads,
        //since they might overlap each other.
        //
        //Update: these used to be static, which was a lot more problematic.  Now they are instance methods and variables,
        //so actually they probably ARE threadsafe except when there are multiple factions of the exact same type.
        //Still best not to tempt fate, though.
        private readonly List<Planet> workingTeliumPlanets = List<Planet>.Create_WillNeverBeGCed( 500, "MacrophageFactionDeepInfoBase-workingTeliumPlanets" );
        public void CalculateGatheringPoints_NotThreadsafe( Faction faction, ArcenLongTermIntermittentPlanningContext Context, PerFactionPathCache PathCacheData )
        {
            workingTeliumPlanets.Clear();

            // Find any planet that is midway between our existing Telia.
            // Start by getting all of our planets with Telia into a list.
            // This list of factions includes enraged, tamed, normal, etc.

            // Finally, add every single midway point between our planets together (wow this is a bit expensive, but okay)

            //now we have our list, flip it from construction to display.  Any thread can use it now.
            //Any thread in the middle of using the old list will be able to finish using that until we next call this method, so no worries.
            //If you call this every sim frame, that means the other thread needs to finish using the list in 100ms.  If it's every second,
            //then it has between 200ms and 1 second to complete (5x speed means 200ms per second)
        }

        public void SpawnTeliumOnPlanet( ArcenHostOnlySimContext Context, Planet planet )
        {
            bool debug = false;
            GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, MacrophageFactionBaseInfo.TeliumTag );
            if ( entityData == null )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "BUG: no macrophagetelium defined for spawning", Verbosity.DoNotShow );
                return;
            }
            if ( debug )
                ArcenDebugging.ArcenDebugLogSingleLine( "FLAGFLAG Spawning a new telium on " + planet.Name, Verbosity.DoNotShow );
            ArcenPoint spawnLocation = planet.GetSafePlacementPointAroundPlanetCenter( Context, entityData, FInt.FromParts( 0, 150 ), FInt.FromParts( 0, 300 ) );
            PlanetFaction pFaction = planet.GetPlanetFactionForFaction( AttachedFaction );
            GameEntity_Squad entity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, entityData, entityData.MarkFor( pFaction ),
                pFaction.FleetUsedAtPlanet, 0, spawnLocation, Context, "Macrophage-Telium" );
            entity.CreateExternalBaseInfo<MacrophagePerTeliumBaseInfo>( "MacrophagePerTeliumBaseInfo" );
            //TEACHING_MOMENT: the MacrophagePerTeliumBaseInfo does need to be created, but nothing needs to be initialized.
            //The Cleanup() command has already set everything to the defaults.  We don't need to track a "UniqueID," because
            //the entity that is the telium has a PrimaryKeyID that will do just fine.  On the MacrophagePerTeliumBaseInfo object,
            //you can always call something like tData.AttachedSquad and get the ID, just like you see happening here for the faction AttachedFaction.
            //There's never a need to index or store relationships between external data and the thing it is attached to -- they are bidirectionally linked until death.
            //When the objects die, they clear themselves and go back to their respective pools, now separated.

            if ( planet.IntelLevel > PlanetIntelLevel.Unexplored ) //no warning if you don't have vision
            {
                PlanetViewChatHandlerBase chatHandlerOrNull = ChatClickHandler.CreateNewAs<PlanetViewChatHandlerBase>( "PlanetGeneralFocus" );
                if ( chatHandlerOrNull != null )
                    chatHandlerOrNull.PlanetToView = planet;

                World_AIW2.Instance.QueueChatMessageOrCommand( AttachedFaction.StartFactionColourForLog() + "Macrophage</color> Telium spawned on " + planet.Name + "!",
                    ChatType.LogToCentralChat, "ArkChiefOfStaff_TeliumSpawn", chatHandlerOrNull );
            }
        }
    }
}
