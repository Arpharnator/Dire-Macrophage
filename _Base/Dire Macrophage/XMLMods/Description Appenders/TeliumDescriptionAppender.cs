using Arcen.AIW2.Core;
using Arcen.Universal;
using System;

using System.Text;

namespace Arcen.AIW2.External
{
    public class DireTeliumDescriptionAppender : GameEntityDescriptionAppenderBase
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
                Buffer.Add( "MacrophageFactionBaseInfo could not be found here. This is a BUG" );
                return;
            }
            DireMacrophagePerTeliumBaseInfo tData = RelatedEntityOrNull.TryGetExternalBaseInfoAs< DireMacrophagePerTeliumBaseInfo>();
            if ( tData == null )
            {
                Buffer.Add( "tData for this Dire Telium is null. This is a bug." );
                return;
            }
            int cost = tData.MetalForNextBuild;
            Buffer.Add( tData.CurrentMetal.ToString( "0.##" ) + "/" + cost.ToString( "0.##" ) + " metal until next build event. \n" );
            Buffer.Add( "This Telium currently supports " + tData.CurrentHarvesters + " Harvesters. \n");
            Buffer.Add("This Telium has " + tData.ExtraMetalGenerationPerSecond + " extra metal generation from Bastions and successful hauls combined.\n");
            Buffer.Add("Income you could reduce due to coming from defenses is " + tData.MetalIncomeFromDefenses + ".");
        }
    }
}
