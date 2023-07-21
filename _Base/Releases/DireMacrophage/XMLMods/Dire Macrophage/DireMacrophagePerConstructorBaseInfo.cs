using Arcen.AIW2.Core;
using Arcen.Universal;
using System;
using System.Text;
using UnityEngine;

namespace Arcen.AIW2.External
{
    public class DireMacrophagePerConstructorBaseInfo : ExternalSquadBaseInfo
    {
        public int TeliumID; //which telium gets metal delivered to it
        public bool SpotFound;
        public bool HasLivingTelium;
        public bool ReturningToTelium;
        public ArcenPoint transformationLocation;
        public int timeSupposedlyGoingTowardsTheSpot;
        public GameEntityTypeData TransformingIntoThis;
        //if we aren't returning to the telium, each time a harvester visits a planet,
        //it must go to a metal harvester before it can go to the next planet.
        //This will keep the Macrophage harvesters movements dynamic and unpredictable

        public DireMacrophagePerConstructorBaseInfo()
        {
            Cleanup();
        }

        protected override void Cleanup()
        {
            this.TeliumID = -1;
            this.HasLivingTelium = false; //this is set each PerSimStep, so don't sync to disk
            this.SpotFound = false;
            this.ReturningToTelium = false;
            this.transformationLocation = Engine_AIW2.Instance.CombatCenter;
            this.timeSupposedlyGoingTowardsTheSpot = 0;
            this.TransformingIntoThis = null;
        }

        public override void CopyTo(ExternalSquadBaseInfo CopyTarget)
        {
            if (CopyTarget == null)
                return;
            DireMacrophagePerConstructorBaseInfo target = CopyTarget as DireMacrophagePerConstructorBaseInfo;
            if (target == null)
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Could not copy from " + this.GetType() + " to " + CopyTarget.GetType() + " because of type mismatch.", Verbosity.ShowAsError);
                return;
            }
            target.ReturningToTelium = this.ReturningToTelium;
            target.TeliumID = this.TeliumID;
            target.HasLivingTelium = this.HasLivingTelium;
            target.SpotFound = this.SpotFound;
            target.transformationLocation = this.transformationLocation;
            target.timeSupposedlyGoingTowardsTheSpot = this.timeSupposedlyGoingTowardsTheSpot;
            target.TransformingIntoThis = this.TransformingIntoThis;
        }

        public override void DoAfterStackSplitAndCopy(int OriginalStackCount, int MyPersonalNewStackCount)
        {
            if (OriginalStackCount <= 0)
                return;

            //this method is called on both halves of the stack that are split,
            //and you can see the amount that was in the total stack originally, and the portion that is in this half of the new split stack

            //let each half only have part of the metal. We can use floats because this is only happening on the host anyway.
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
            Buffer.AddBool(MetaData, this.ReturningToTelium, "ReturningToTelium");
        }

        public override void DeserializeIntoSelf(SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType)
        {
            this.TeliumID = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "TeliumID");
            this.SpotFound = false;
            this.ReturningToTelium = Buffer.ReadBool(MetaData, "ReturningToTelium");
            this.HasLivingTelium = false; //this is set each PerSimStep, so don't sync to disk
            this.transformationLocation = Engine_AIW2.Instance.CombatCenter;
            this.timeSupposedlyGoingTowardsTheSpot = 0;
            this.TransformingIntoThis = null;
        }
    }
}
