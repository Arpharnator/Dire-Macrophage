using Arcen.AIW2.Core;
using Arcen.Universal;
using System;

using System.Text;

namespace Arcen.AIW2.External
{
    public class DefenseMacrophageAppender : GameEntityDescriptionAppenderBase
    {
        public override void AddToDescriptionBuffer(GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenCharacterBufferBase Buffer)
        {
            if (RelatedEntityOrNull == null)
                return;
            DireMacrophageFactionBaseInfoCore infestation = null;
            Faction facOrNull = RelatedEntityOrNull.GetFactionOrNull_Safe();
            if (facOrNull != null)
                infestation = facOrNull.TryGetExternalBaseInfoAs<DireMacrophageFactionBaseInfoCore>();
            if (infestation == null)
            {
                Buffer.Add("MacrophageFactionBaseInfo could not be found here. This is a BUG");
                return;
            }
            DireMacrophagePerDefenseBaseInfo tData = RelatedEntityOrNull.TryGetExternalBaseInfoAs<DireMacrophagePerDefenseBaseInfo>();

            if (tData == null)
            {
                Buffer.Add("tData for this Dire Macrophage Bastion is null. This is a bug.");
                return;
            }
            GameEntity_Squad relatedTelium = World_AIW2.Instance.GetEntityByID_Squad(tData.TeliumID);
            Planet teliumPlanet = relatedTelium.Planet;
            // I'm so smart for finally learning how to use infestation epic, when it was right there the whole time
            int cost = infestation.GetDefenseMarkupCost(RelatedEntityOrNull);
            if(RelatedEntityOrNull.CurrentMarkLevel < 7)
            {
                Buffer.Add(tData.CurrentMetal.ToString("0.##") + "/" + cost.ToString("0.##") + " metal until next markup.\n");
            }
            if (RelatedEntityOrNull.CurrentMarkLevel == 7)
            {
                Buffer.Add(tData.CurrentMetal.ToString("0.##") + "/" + cost.ToString("0.##") + " metal next constructor spawn.\n");
            }
            Buffer.Add("This Bastion is giving " + tData.ExtraMetalGenerationPerSecondForHomeTelium + " extra metal generation to its home Dire Telium on " + teliumPlanet.Name);
        }
    }
}
