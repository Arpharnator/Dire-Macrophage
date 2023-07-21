using Arcen.AIW2.Core;
using Arcen.Universal;
using System;

using System.Text;

namespace Arcen.AIW2.External
{
    public class DireMacrophageFactionBaseInfo : DireMacrophageFactionBaseInfoCore
    {
        //serialized

        //not serialized
        public static List<DireMacrophageFactionBaseInfo> AllDireMacrophageFactions = List<DireMacrophageFactionBaseInfo>.Create_WillNeverBeGCed( 8, "DireMacrophageFactionBaseInfo-AllDireMacrophageFactions" );
        public int Intensity = 0;

        //constants        

        public DireMacrophageFactionBaseInfo()
        {
            Cleanup();
        }

        protected override void SubCleanup()
        {
            //not serialized
            AllDireMacrophageFactions.Clear();
        }

        protected override void SubSerializeFactionTo( SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
        }

        protected override void SubDeserializeFactionIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
        }

        public override int GetDifficultyOrdinal_OrNegativeOneIfNotRelevant()
        {
            return Intensity;
        }

        public override float CalculateYourPortionOfPredictedGameLoad_Where100IsANormalAI( ArcenCharacterBufferBase OptionalExplainCalculation )
        {
            DoRefreshFromFactionSettings();

            int load = 30 + (Intensity * 3);

            if ( OptionalExplainCalculation != null )
                OptionalExplainCalculation.Add( load ).Add( " Load From Dire Macrophage" );
            return load;
        }

        #region SubDoGeneralAggregationsPausedOrUnpaused
        protected override void SubDoGeneralAggregationsPausedOrUnpaused()
        {
            if ( !AllDireMacrophageFactions.Contains( this ) )
                AllDireMacrophageFactions.Add( this );
        }
        #endregion

        #region DoRefreshFromFactionSettings
        
        protected override void DoRefreshFromFactionSettings()
        {
            ConfigurationForFaction cfg = this.AttachedFaction.Config;
            Intensity = cfg.GetIntValueForCustomFieldOrDefaultValue( "Intensity", true );
        }
        #endregion

        #region SetStartingFactionRelationships
        public override void SetStartingFactionRelationships()
        {
            //these are hostile to everyone. Maybe they should be friendly with the Zenith Trader though?
            base.SetStartingFactionRelationships();
            Faction faction = this.AttachedFaction;
            for ( int i = 0; i < World_AIW2.Instance.Factions.Count; i++ )
            {
                Faction otherFaction = World_AIW2.Instance.Factions[i];
                if ( faction == otherFaction )
                    continue;
                if ( otherFaction.Type == FactionType.NaturalObject )
                    continue;
                switch ( otherFaction.Type )
                {
                    case FactionType.Player:
                    case FactionType.AI:
                    case FactionType.SpecialFaction:
                        faction.MakeHostileTo( otherFaction );
                        otherFaction.MakeHostileTo( faction );
                        break;
                }
            }
        }
        #endregion

        protected override void SubDoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context )
        {
            
        }
    }
}
