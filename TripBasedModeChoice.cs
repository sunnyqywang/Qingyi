using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using XTMF;
using Tasha.Common;

using TMG.Input;


namespace Qingyi
{
    public class TripBasedModeChoice : ITashaModeChoice
    {
        [RootModule]
        public ITashaRuntime Root;

        public string Name { get; set; }

        public float Progress => 0.0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        private ITashaMode[] _allModes;

        [SubModelInformation(Required = false, Description = "The location to save trip records to (Hhld,Person,Trip,[ModeProbabilities])")]
        public FileLocation SaveTripTo;

        private bool _writeThisIteration;

        struct DataToWrite
        {
            internal readonly int HhldID;
            internal readonly int PersonID;
            internal readonly int TripID;
            internal readonly int[] ModesChosen;

            public DataToWrite(ITashaMode[] allModes, int hhldID, int personID, int tripID, ITashaMode[] modesChosen)
            {
                HhldID = hhldID;
                PersonID = personID;
                TripID = tripID;
                ModesChosen = new int[allModes.Length];
                for (int i = 0; i < modesChosen.Length; i++)
                {
                    ModesChosen[Array.IndexOf(allModes, modesChosen[i])]++;
                }
            }
        }

        private BlockingCollection<DataToWrite> _tripStorageQueue;
        private Task _StoreTrips;

        public void IterationFinished(int iteration, int totalIterations)
        {
            _tripStorageQueue?.CompleteAdding();
            _StoreTrips?.Wait();            
            _tripStorageQueue?.Dispose();
            _tripStorageQueue = null;
        }

        public void IterationStarted(int iteration, int totalIterations)
        {
            _writeThisIteration = (iteration == totalIterations - 1) && SaveTripTo != null;
            if(_writeThisIteration)
            {
                _tripStorageQueue = new BlockingCollection<DataToWrite>();
                _StoreTrips = Task.Factory.StartNew(() =>
                {
                    using (var writer = new StreamWriter(SaveTripTo))
                    {
                        writer.Write("HhldID,PersonID,TripID");
                        for (int i = 0; i < _allModes.Length; i++)
                        {
                            writer.Write(',');
                            writer.Write(_allModes[i].ModeName);
                        }
                        writer.WriteLine();
                        foreach (var trip in _tripStorageQueue.GetConsumingEnumerable())
                        {
                            writer.Write(trip.HhldID);
                            writer.Write(',');
                            writer.Write(trip.PersonID);
                            writer.Write(',');
                            writer.Write(trip.TripID);
                            for (int i = 0; i < trip.ModesChosen.Length; i++)
                            {
                                writer.Write(',');
                                writer.Write(trip.ModesChosen[i]);
                            }
                            writer.WriteLine();
                        }
                    }
                }, TaskCreationOptions.LongRunning);
            }
        }

        public void LoadOneTimeLocalData()
        {
            _allModes = Root.AllModes.ToArray();
        }

        public bool Run(ITashaHousehold household)
        {
            var v = new double[_allModes.Length];
            var temp = new bool[_allModes.Length];
            Random r = new Random(household.HouseholdId * 128732489);
            foreach (var person in household.Persons)
            {
                int tripNumber = 1;
                foreach (var tripChain in person.TripChains)
                {
                    foreach (var trip in tripChain.Trips)
                    {
                        TripModeChoice(r, trip, v, temp, tripNumber++);
                    }
                }
            }
            return true;
        }

        private bool TripModeChoice(Random random, ITrip trip, double[] pOfMode, bool[] temp, int tripNumber)
        {

            for (int i = 0; i < _allModes.Length; i++)
            {
                if (_allModes[i].Feasible(trip))
                {
                    pOfMode[i] = Math.Exp(_allModes[i].CalculateV(trip));
                    temp[i] = !(double.IsNaN(pOfMode[i]) | double.IsInfinity(pOfMode[i]));
                }
                else
                {
                    pOfMode[i] = double.NegativeInfinity;
                    temp[i] = false;
                }
            }
            double sum = 0.0;
            for (int i = 0; i < pOfMode.Length; i++)
            {
                if(temp[i])
                {
                    sum += pOfMode[i];
                }
            }
            // if nothing was feasible
            var mc = trip.ModesChosen;
            if (sum == 0.0f)
            {
                for (int i = 0; i < mc.Length; i++)
                {
                    mc[i] = null;
                }
                return false;
            }
            double prev = 0.0;
            for (int i = 0; i < temp.Length; i++)
            {
                if(temp[i])
                {
                    prev = pOfMode[i] = (pOfMode[i] / sum) + prev;
                }
                else
                {
                    pOfMode[i] = 0.0;
                }
            }
            for (int i = 0; i < mc.Length; i++)
            {
                var pop = random.NextDouble();
                var acc = 0.0;
                for (int j = 0; j < pOfMode.Length; j++)
                {
                    acc += pOfMode[j];
                    if(acc >= pop)
                    {
                        mc[i] = _allModes[j];
                        break;
                    }
                }
            }
            if(_writeThisIteration)
            {
                var person = trip.TripChain.Person;
                var hhld = person.Household;
                _tripStorageQueue.Add(new DataToWrite(_allModes, hhld.HouseholdId, person.Id, tripNumber, mc));
            }
            return true;
        }

        private static void Zero(double[] calcSpace)
        {
            for (int i = 0; i < calcSpace.Length; i++)
            {
                calcSpace[i] = 0;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
