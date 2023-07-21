using Arcen.AIW2.Core;
using Arcen.Universal;
using System;

using System.Text;

namespace Arcen.AIW2.External
{
    public class DireHarvesterDescriptionAppender : GameEntityDescriptionAppenderBase
    {
        public override void AddToDescriptionBuffer( GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenCharacterBufferBase Buffer )
        {
            if ( RelatedEntityOrNull == null )
                return;
            DireMacrophageFactionBaseInfoCore infestation = null;
            Faction facOrNull = RelatedEntityOrNull.GetFactionOrNull_Safe();
            if ( facOrNull != null )
                infestation = facOrNull.TryGetExternalBaseInfoAs<DireMacrophageFactionBaseInfoCore>();
            if ( infestation == null )
            {
                Buffer.Add( "DireMacrophageFactionBaseInfo could not be found here. This is a DireBUG" );
                return;
            }
            DireMacrophagePerHarvesterBaseInfo hData = RelatedEntityOrNull.TryGetExternalBaseInfoAs<DireMacrophagePerHarvesterBaseInfo>();
            if ( hData == null )
            {
                Buffer.Add( "hData for this Dire Harvester is null. If you have just loaded a game then please unpause it and it should fill in" );
                return;
            }
            Buffer.Add( "This Dire Harvester has collected " + hData.CurrentMetal + " metal " );
            if ( hData.ReturningToTelium )
                Buffer.Add( "and is currently returning to its Dire Telium to deposit metal. " );
            else
            {
                int totalMetalHarvesterCanHold = infestation.MetalHarvesterCanHold + RelatedEntityOrNull.CurrentMarkLevel * infestation.DireMacrophageMaxMetalHarvesterCanHoldPerMark;
                Buffer.Add("and will return to its Dire Telium when it has collected at least " + totalMetalHarvesterCanHold + ". ");
            }
                
        }
    }
}
