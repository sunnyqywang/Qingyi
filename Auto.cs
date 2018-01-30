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
    public class Auto : ITashaMode, IIterationSensitive
    {
        [RootModule]
        public ITashaRuntime Root;

        public IVehicleType RequiresVehicle => null;

        public double VarianceScale { get => 1.0; set { } }

        [RunParameter("Network Type", "Auto", "The name of the network to use.")]
        public string NetworkType { get; set; }

        public bool NonPersonalVehicle => throw new NotImplementedException();

        public float CurrentlyFeasible { get => 1.0f; set { } }

        [RunParameter("Mode Name", "Auto", "The name of the mode")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        public float Progress => 0.0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        private INetworkData _network;

        private SparseArray<IZone> _zones;

        private SparseArray<float> _waitTimes;

        [RunParameter("RequireLicense", true, "Does the mode require the driver to have a license?")]
        public bool RequireLicense;

        [RunParameter("Constant", 0.0f, "The modal constant")]
        public float BConst;

        [RunParameter("IVTT", 0.0f, "The in vehicle travel time.")]
        public float BIvtt;

        [RunParameter("Wait", 0.0f, "The wait time.")]
        public float BWait;

        [RunParameter("ToCentre", 0.0f, "Distance to city centre (represented by cost).")]
        public float BToCentre;

        [RunParameter("PurposeOther", 0.0f, "Whether this is an Other-related trip.")]
        public float BHO;

        [RunParameter("License", 0.0f, "Whether the trip-maker has a license.")]
        public float BLicense;

        [RunParameter("EmploymentStatus", 0.0f, "Whether the trip-maker is unemployed/work from home.")]
        public float BEmployment;

        [RunParameter("ONTrip", 0.0f, "Overnight trip. [24 - 6)")]
        public float BON;

        [RunParameter("EVTrip", 0.0f, "Evening trip. [19 - 24)")]
        public float BEV;

        [RunParameter("PMTrip", 0.0f, "PM trip. [15, 19)")]
        public float BPM;

        [RunParameter("MDTrip", 0.0f, "Midday trip. [9, 15)")]
        public float BMD;

        [RunParameter("AgeConstant1", 0.0f, "Age under 20")]
        public float BAge1;

        [RunParameter("AgeConstant2", 0.0f, "Age 20-29")]
        public float BAge2;

        [RunParameter("AgeConstant3", 0.0f, "Age 30-59")]
        public float BAge3;

        [RunParameter("AgeConstant4", 0.0f, "Age 60-69")]
        public float BAge4;

        [RunParameter("AgeConstant5", 0.0f, "Age 70-79")]
        public float BAge5;

        [RunParameter("AgeConstant6", 0.0f, "Age 80-89")]
        public float BAge6;

        [RunParameter("CityCentreZone", 54, "Zone number of City Centre.")]
        public int citycentre;

        [SubModelInformation(Required = false, Description = "The time waiting before starting a trip.")]
        public IDataSource<SparseArray<float>> WaitTime;

        [RunParameter("Cost", 0.0f, "The cost of the trip.")]
        public float BCost;

        /// <summary>
        /// The flat index for the city centre
        /// </summary>
        private int _centreIndex;

        public double CalculateV(ITrip trip)
        {
            var start = trip.ActivityStartTime;
            var origin = _zones.GetFlatIndex(trip.OriginalZone.ZoneNumber);
            var dest = _zones.GetFlatIndex(trip.DestinationZone.ZoneNumber);
            float ivtt, cost, ivtt_tc, cost_tc = 0;
            var travelData = _network.GetAllData(origin, dest, start, out ivtt, out cost);
            var v = BConst;
            if (!RequireLicense)
            {
                if(trip.Purpose == Activity.IndividualOther)
                {
                    v += BHO;
                }

                if (!trip.TripChain.Person.Licence)
                {
                    v += BLicense;
                }

                if (trip.TripChain.Person.EmploymentStatus == TTSEmploymentStatus.NotEmployed || trip.TripChain.Person.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_FullTime || trip.TripChain.Person.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_PartTime)
                {
                    v += BEmployment;
                }

                if(start.Hours >= 24 || start.Hours < 6)
                {
                    v += BON;
                }else if (start.Hours >= 19 || start.Hours < 24)
                {
                    v += BEV;
                } else if (start.Hours >= 15 || start.Hours < 19)
                {
                    v += BPM;
                }
                else if(start.Hours >= 9 || start.Hours < 15)
                {
                    v += BMD;
                }

                int age = trip.TripChain.Person.Age;
                if (age < 20)
                {
                    v += BAge1;
                }
                else if (age < 30)
                {
                    v += BAge2;
                }
                else if (age < 60)
                {
                    v += BAge3;
                }
                else if (age < 70)
                {
                    v += BAge4;
                }
                else if (age < 80)
                {
                    v += BAge5;
                }
                else if (age < 90)
                {
                    v += BAge6;
                }
                var toCentre = _network.GetAllData(origin, _centreIndex, start, out ivtt_tc, out cost_tc);
                v += BToCentre * cost_tc;
                cost = cost / (float)0.153 * (float)1.75 + (float)4.25;
            }

            return v + BIvtt * ivtt + BCost * cost + BWait * _waitTimes[origin];
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
            if (RequireLicense)
            {
                var person = trip.TripChain.Person;
                return person.Licence && person.Household.Vehicles.Length > 0;
            }
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
            foreach (var network in Root.NetworkData)
            {
                if (network.NetworkType == NetworkType)
                {
                    _network = network;
                    break;
                }
            }
            if (_network == null)
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
            WaitTime.UnloadData();
        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            _zones = Root.ZoneSystem.ZoneArray;
            _centreIndex = _zones.GetFlatIndex(citycentre);
            WaitTime.LoadData();
            _waitTimes = WaitTime.GiveData();
        }
    }
}
