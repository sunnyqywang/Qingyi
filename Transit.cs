using Datastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using XTMF;

namespace Qingyi
{
    public class Transit : ITashaMode, IIterationSensitive
    {
        [RootModule]
        public ITashaRuntime Root;

        public IVehicleType RequiresVehicle => null;

        public double VarianceScale { get => 1.0; set { } }

        [RunParameter("Network Type", "Auto", "The name of the network to use.")]
        public string NetworkType { get; set; }

        public bool NonPersonalVehicle => throw new NotImplementedException();

        public float CurrentlyFeasible { get => 1.0f;  set { } }

        [RunParameter("Mode Name", "Auto", "The name of the mode")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        public float Progress => 0.0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        private ITripComponentData _network;

        private SparseArray<IZone> _zones;

        [RunParameter("Constant", 0.0f, "The modal constant")]
        public float BConst;

        [RunParameter("IVTT", 0.0f, "The in vehicle travel time.")]
        public float BIvtt;

        [RunParameter("Wait", 0.0f, "The wait time.")]
        public float BWait;

        [RunParameter("Walk", 0.0f, "The walk time.")]
        public float BWalk;

        [RunParameter("Cost", 0.0f, "The cost of the trip.")]
        public float BCost;

        public double CalculateV(ITrip trip)
        {
            var start = trip.ActivityStartTime;
            var origin = _zones.GetFlatIndex(trip.OriginalZone.ZoneNumber);
            var dest = _zones.GetFlatIndex(trip.DestinationZone.ZoneNumber);
            float ivtt, cost, walk, wait, boarding;
            var travelData = _network.GetAllData(origin, dest, start, out ivtt, out walk, out wait, out boarding, out cost);
            return BConst + BIvtt * ivtt + BCost * cost + BWait * wait + BWalk * walk;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return float.NegativeInfinity;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public bool Feasible(ITrip trip)
        {
            return true;
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach(var network in Root.NetworkData)
            {
                if(network.NetworkType == NetworkType)
                {
                    _network = network as ITripComponentData;
                    break;
                }
            }
            if(_network == null)
            {
                error = $"In {Name} we were unable to find a network with the name {NetworkType} to gather travel information.";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
            
        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            _zones = Root.ZoneSystem.ZoneArray;
        }
    }
}
