using Arcen.AIW2.Core;
using Arcen.Universal;
using System;

using System.Text;

namespace Arcen.AIW2.External
{
    public class DireBorerDescriptionAppender : GameEntityDescriptionAppenderBase
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
                Buffer.Add( "hData for this Dire Borer is null. If you have just loaded a game then please unpause it and it should fill in" );
                return;
            }                
        }
    }
}
