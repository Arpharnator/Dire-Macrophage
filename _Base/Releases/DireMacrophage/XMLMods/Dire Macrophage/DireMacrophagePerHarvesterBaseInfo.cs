using Arcen.AIW2.Core;
using Arcen.Universal;
using System;
using System.Text;
using UnityEngine;

namespace Arcen.AIW2.External
{
    public class DireMacrophagePerHarvesterBaseInfo : ExternalSquadBaseInfo
    {
        public int TeliumID; //which telium gets metal delivered to it
        public int TotalMetalEverCollected;
        public int CurrentMetal;
        public bool ReturningToTelium;
        public bool HasLivingTelium;
        //if we aren't returning to the telium, each time a harvester visits a planet,
        //it must go to a metal harvester before it can go to the next planet.
        //This will keep the Macrophage harvesters movements dynamic and unpredictable
        public bool HasVisitedMetalGeneratorOnPlanet;
        public bool IsHeadingTowardMetalGeneratorOnPlanet;
        public bool IsEvolved;
        public bool IsCarrier;
        public DireMacrophagePerHarvesterBaseInfo()
        {
            Cleanup();
        }

        protected override void Cleanup()
        {
            this.TeliumID = -1;
            this.TotalMetalEverCollected = 0;
            this.CurrentMetal = 0;
            this.ReturningToTelium = false;
            this.HasLivingTelium = false; //this is set each PerSimStep, so don't sync to disk
            this.HasVisitedMetalGeneratorOnPlanet = false;
            this.IsHeadingTowardMetalGeneratorOnPlanet = false;
            this.IsEvolved = false;
            this.IsCarrier = false;
        }

        public override void CopyTo( ExternalSquadBaseInfo CopyTarget )
        {
            if ( CopyTarget == null )
                return;
            DireMacrophagePerHarvesterBaseInfo target = CopyTarget as DireMacrophagePerHarvesterBaseInfo;
            if ( target == null )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Could not copy from " + this.GetType() + " to " + CopyTarget.GetType() + " because of type mismatch.", Verbosity.ShowAsError );
                return;
            }
            target.TeliumID = this.TeliumID;
            target.TotalMetalEverCollected = this.TotalMetalEverCollected;
            target.CurrentMetal = this.CurrentMetal;
            target.ReturningToTelium = this.ReturningToTelium;
            target.HasLivingTelium = this.HasLivingTelium;
            target.HasVisitedMetalGeneratorOnPlanet = this.HasVisitedMetalGeneratorOnPlanet;
            target.IsHeadingTowardMetalGeneratorOnPlanet = this.IsHeadingTowardMetalGeneratorOnPlanet;
            target.IsEvolved = this.IsEvolved;
            target.IsCarrier = this.IsCarrier;
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
            Buffer.AddInt32( MetaData, ReadStyle.PosExceptNeg1, this.TeliumID, "TeliumID" );
            Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.TotalMetalEverCollected, "TotalMetalEverCollected" );
            Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.CurrentMetal, "CurrentMetal" );
            Buffer.AddBool( MetaData, this.ReturningToTelium, "ReturningToTelium" );
            Buffer.AddBool( MetaData, this.HasVisitedMetalGeneratorOnPlanet, "HasVisitedMetalGeneratorOnPlanet" );
            Buffer.AddBool( MetaData, this.IsHeadingTowardMetalGeneratorOnPlanet, "IsHeadingTowardMetalGeneratorOnPlanet" );
            Buffer.AddBool(MetaData, this.IsEvolved, "IsEvolved");
            Buffer.AddBool(MetaData, this.IsCarrier, "IsCarrier");
        }

        public override void DeserializeIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            this.TeliumID = Buffer.ReadInt32( MetaData, ReadStyle.PosExceptNeg1, "TeliumID" );
            this.TotalMetalEverCollected = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg, "TotalMetalEverCollected" );
            this.CurrentMetal = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg, "CurrentMetal" );
            this.ReturningToTelium = Buffer.ReadBool( MetaData, "ReturningToTelium" );
            this.HasLivingTelium = false; //this is set each PerSimStep, so don't sync to disk
            this.HasVisitedMetalGeneratorOnPlanet = Buffer.ReadBool( MetaData, "HasVisitedMetalGeneratorOnPlanet" );
            this.IsHeadingTowardMetalGeneratorOnPlanet = Buffer.ReadBool( MetaData, "IsHeadingTowardMetalGeneratorOnPlanet" );
            this.IsEvolved = Buffer.ReadBool(MetaData, "IsEvolved");
            this.IsEvolved = Buffer.ReadBool(MetaData, "IsCarrier");
        }
    }
}
