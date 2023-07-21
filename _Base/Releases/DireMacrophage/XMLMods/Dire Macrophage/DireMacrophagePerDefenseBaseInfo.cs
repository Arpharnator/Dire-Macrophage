using Arcen.AIW2.Core;
using Arcen.Universal;
using System;
using System.Text;
using UnityEngine;


namespace Arcen.AIW2.External
{
    public class DireMacrophagePerDefenseBaseInfo : ExternalSquadBaseInfo
    {
        //We use Metal collected and current Metal for marking up this structure
        public int TeliumID; //which telium gets metal delivered to it
        public int TotalMetalEverCollected;
        public int CurrentMetal;
        public int MetalForMarkup; //needed for the description
        public bool HasLivingTelium;

        public int ExtraMetalGenerationPerSecondForHomeTelium;
        public int MetalGainedFromHarvesters;
        public DireMacrophagePerDefenseBaseInfo()
        {
            Cleanup();
        }

        protected override void Cleanup()
        {
            this.TeliumID = -1;
            this.TotalMetalEverCollected = 0;
            this.CurrentMetal = 0;
            this.MetalForMarkup = 0; //not stored on disk, since we regenerate this each time
            this.HasLivingTelium = false; //this is set each PerSimStep, so don't sync to disk
            this.ExtraMetalGenerationPerSecondForHomeTelium = 0;
            this.MetalGainedFromHarvesters = 0;
        }

        public override void CopyTo(ExternalSquadBaseInfo CopyTarget)
        {
            if (CopyTarget == null)
                return;
            DireMacrophagePerDefenseBaseInfo target = CopyTarget as DireMacrophagePerDefenseBaseInfo;
            if (target == null)
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Could not copy from " + this.GetType() + " to " + CopyTarget.GetType() + " because of type mismatch.", Verbosity.ShowAsError);
                return;
            }
            target.TeliumID = this.TeliumID;
            target.TotalMetalEverCollected = this.TotalMetalEverCollected;
            target.CurrentMetal = this.CurrentMetal;
            target.MetalForMarkup = this.MetalForMarkup;
            target.ExtraMetalGenerationPerSecondForHomeTelium = this.ExtraMetalGenerationPerSecondForHomeTelium;
            target.MetalGainedFromHarvesters = this.MetalGainedFromHarvesters;
            target.HasLivingTelium = this.HasLivingTelium;
        }

        public override void DoAfterStackSplitAndCopy(int OriginalStackCount, int MyPersonalNewStackCount)
        {
            if (OriginalStackCount <= 0)
                return;

            //this method is called on both halves of the stack that are split,
            //and you can see the amount that was in the total stack originally, and the portion that is in this half of the new split stack

            //let each half only have part of the metal. We can use floats because this is only happening on the host anyway.
            float percentageMetalLeft = (float)MyPersonalNewStackCount / (float)OriginalStackCount;
            this.CurrentMetal = Mathf.RoundToInt(this.CurrentMetal * percentageMetalLeft);
        }

        public override void DoAfterSingleOtherShipMergedIntoOurStack(ExternalSquadBaseInfo OtherShipBeingDiscarded)
        {
            //the OtherShipBeingDiscarded is being discarded.  If there's any data we want to merge into this
            //(this being a part of the stack that is kept), then we can do it now.
            //if there are 10 squads being merged into one stack, this would be called 9 times on the
            //single squad that is remaining, with the parameter being the other 9 that are disappearing.
        }

        public override void SerializeTo(SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType)
        {
            Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, this.TeliumID, "TeliumID");
            Buffer.AddInt32(MetaData, ReadStyle.NonNeg, this.TotalMetalEverCollected);
            Buffer.AddInt32(MetaData, ReadStyle.NonNeg, this.CurrentMetal);
            Buffer.AddInt32(MetaData, ReadStyle.NonNeg, this.ExtraMetalGenerationPerSecondForHomeTelium);
            Buffer.AddInt32(MetaData, ReadStyle.NonNeg, this.MetalGainedFromHarvesters);
        }

        public override void DeserializeIntoSelf(SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType)
        {
            this.TeliumID = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "TeliumID");
            this.TotalMetalEverCollected = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
            this.CurrentMetal = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
            this.ExtraMetalGenerationPerSecondForHomeTelium = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
            this.MetalGainedFromHarvesters = Buffer.ReadInt32(MetaData, ReadStyle.NonNeg);
            this.HasLivingTelium = false; //this is set each PerSimStep, so don't sync to disk
        }
    }
}
