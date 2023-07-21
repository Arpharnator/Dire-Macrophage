using Arcen.AIW2.Core;
using Arcen.Universal;
using System;
using System.Text;
using UnityEngine;

namespace Arcen.AIW2.External
{
    public class DireMacrophagePerTeliumBaseInfo : ExternalSquadBaseInfo
    {
        public int TotalMetalEverCollected;
        public int CurrentMetal;
        public int TimeLastHadHarvester;
        public int MetalForNextBuild; //needed for the description
        public int CurrentHarvesters; //needed for the description
        public int ExtraMetalGenerationPerSecond;
        public int MetalGainedFromHarvesters; //This includes bastions as well for the unremovable part, I'm lazy
        public int MetalIncomeFromDefenses; //This one is from destructible bastions, the part that can be lost
        public int NumberOfTimeSpawnedColoniser;
        public DireMacrophagePerTeliumBaseInfo()
        {
            Cleanup();
        }

        protected override void Cleanup()
        {
            this.TotalMetalEverCollected = 0;
            this.CurrentMetal = 0;
            this.TimeLastHadHarvester = 0;
            this.MetalForNextBuild = 0; //not stored on disk, since we regenerate this each time
            this.CurrentHarvesters = 0; //not stored on disk, since we regenerate this sim step
            this.ExtraMetalGenerationPerSecond = 0;
            this.MetalGainedFromHarvesters = 0;
            this.MetalIncomeFromDefenses = 0;
            this.NumberOfTimeSpawnedColoniser = 0;
    }

        public override void CopyTo( ExternalSquadBaseInfo CopyTarget )
        {
            if ( CopyTarget == null )
                return;
            DireMacrophagePerTeliumBaseInfo target = CopyTarget as DireMacrophagePerTeliumBaseInfo;
            if ( target == null )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Could not copy from " + this.GetType() + " to " + CopyTarget.GetType() + " because of type mismatch.", Verbosity.ShowAsError );
                return;
            }
            target.TotalMetalEverCollected = this.TotalMetalEverCollected;
            target.CurrentMetal = this.CurrentMetal;
            target.TimeLastHadHarvester = this.TimeLastHadHarvester;
            target.MetalForNextBuild = this.MetalForNextBuild;
            target.CurrentHarvesters = this.CurrentHarvesters;
            target.ExtraMetalGenerationPerSecond = this.ExtraMetalGenerationPerSecond;
            target.MetalGainedFromHarvesters = this.MetalGainedFromHarvesters;
            target.MetalIncomeFromDefenses = this.MetalIncomeFromDefenses;
            target.NumberOfTimeSpawnedColoniser = this.NumberOfTimeSpawnedColoniser;
        }

        public override void DoAfterStackSplitAndCopy( int OriginalStackCount, int MyPersonalNewStackCount )
        {
            if ( OriginalStackCount <= 0 )
                return;

            //this method is called on both halves of the stack that are split,
            //and you can see the amount that was in the total stack originally, and the portion that is in this half of the new split stack

            //let each half only have part of the metal. We can use floats because this is only happening on the host anyway.
            float percentageMetalLeft = (float)MyPersonalNewStackCount / (float)OriginalStackCount;
            this.CurrentMetal = Mathf.RoundToInt( this.CurrentMetal * percentageMetalLeft );
        }

        public override void DoAfterSingleOtherShipMergedIntoOurStack( ExternalSquadBaseInfo OtherShipBeingDiscarded )
        {
            //the OtherShipBeingDiscarded is being discarded.  If there's any data we want to merge into this
            //(this being a part of the stack that is kept), then we can do it now.
            //if there are 10 squads being merged into one stack, this would be called 9 times on the
            //single squad that is remaining, with the parameter being the other 9 that are disappearing.
        }

        public override void SerializeTo( SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.TotalMetalEverCollected );
            Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.CurrentMetal );
            Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.TimeLastHadHarvester );
            Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.ExtraMetalGenerationPerSecond);
            Buffer.AddInt32(MetaData, ReadStyle.NonNeg, this.MetalGainedFromHarvesters);
            Buffer.AddInt32(MetaData, ReadStyle.NonNeg, this.MetalIncomeFromDefenses);
            Buffer.AddInt32(MetaData, ReadStyle.NonNeg, this.NumberOfTimeSpawnedColoniser); 
        }

        public override void DeserializeIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            this.TotalMetalEverCollected = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg );
            this.CurrentMetal = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg );
            this.TimeLastHadHarvester = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg );
            this.ExtraMetalGenerationPerSecond = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
            this.MetalGainedFromHarvesters = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
            this.MetalIncomeFromDefenses = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
            this.NumberOfTimeSpawnedColoniser = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
        }
    }
}
