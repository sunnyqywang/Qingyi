using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public void IterationFinished(int iteration, int totalIterations)
        {

        }

        public void IterationStarted(int iteration, int totalIterations)
        {

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
                foreach (var tripChain in person.TripChains)
                {
                    foreach (var trip in tripChain.Trips)
                    {
                        TripModeChoice(r, trip, v, temp);
                    }
                }
            }
            return true;
        }

        private bool TripModeChoice(Random random, ITrip trip, double[] pOfMode, bool[] temp)
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
