using BleLights.SharedControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireflyWindows.ViewModels
{
    public class HubProgram
    {
        DelayedActions delayedActions = new DelayedActions();

        public HubProgram()
        {
            Numbers = new List<int>();
            playPos = -1;
        }

        public event EventHandler PlayComplete;

        /// <summary>
        ///  Contains array of numbers listing how many tubes should be fired on each
        ///  step of the Play.
        /// </summary>
        public List<int> Numbers { get; set; }

        public bool HasProgram
        {
            get
            {
                return Numbers != null && Numbers.Count > 0;
            }
        }

        int playPos;
        bool paused;

        internal void Pause()
        {
            paused = true;
        }

        internal void Play()
        {
            if (HasProgram)
            {
                delayedActions.StartDelayedAction("PlayProgram", () => { PlayProgram(); }, TimeSpan.FromMilliseconds(0));
            }
            else
            {
                delayedActions.StartDelayedAction("PlayNext", () => { PlaySequence(); }, TimeSpan.FromSeconds(0));
            }
        }

        List<int> leftBank = null;
        List<int> rightBank = null;


        private void PlaySequence()
        {
            // batch size tells us how many tubes we want to fire from each hub at a time.
            // If it is greater than 1 then we also want to balance the number across the
            // left and right sides of the hub to help split the power draw, plus it makes
            // the explosions more balanced (assuming hub configuration 5 x 2).
            int batchSize = Settings.Instance.BatchSize;

            HubManager hubs = ((App)App.Current).Hubs;

            int maxTubes = (from h in hubs.Hubs select h.Hub.Tubes).Max();

            if (playPos == -1)
            {
                if (batchSize > 1)
                {
                    int half = (maxTubes / 2);
                    leftBank = new List<int>(Enumerable.Range(0, half));
                    rightBank = new List<int>(Enumerable.Range(half, maxTubes - half));
                }
                else
                {
                    leftBank = new List<int>(Enumerable.Range(0, maxTubes));
                }
                playPos = 0;
            }

            // now select next batchSize tubes from eacn bank
            int leftCount = (int)Math.Ceiling((double)batchSize / 2.0);
            int rightCount = batchSize - leftCount;
            if (playPos % 2 > 0 && batchSize > 1)
            {
                // switch them
                int temp = leftCount;
                leftCount = rightCount;
                rightCount = temp;
            }

            List<int> tubesToFire = new List<int>();
            for (int i = 0; i < leftCount; i++)
            {
                if (leftBank.Count > 0)
                {
                    int tube = leftBank[0];
                    leftBank.RemoveAt(0);
                    tubesToFire.Add(tube);
                }
            }
            for (int i = 0; i < rightCount; i++)
            {
                if (rightBank.Count > 0)
                {
                    int tube = rightBank[0];
                    rightBank.RemoveAt(0);
                    tubesToFire.Add(tube);
                }
            }

            foreach (var hub in hubs.Hubs.ToArray())
            {
                FireflyHub fh = hub.Hub;
                hub.Hub.FireTubes(tubesToFire, Settings.Instance.BurnTime);
            }

            playPos += batchSize;

            if (playPos >= maxTubes)
            {
                OnPlayComplete();
            }
            else
            {
                OnPlayContinue();
            }
        }

        private void OnPlayContinue()
        {
            if (!paused)
            {
                int speed = Settings.Instance.PlaySpeed;
                if (HasProgram) {
                    delayedActions.StartDelayedAction("PlayProgram", () => { PlayProgram(); }, TimeSpan.FromMilliseconds(speed));
                }
                else
                {
                    delayedActions.StartDelayedAction("PlaySequence", () => { PlaySequence(); }, TimeSpan.FromMilliseconds(speed));
                }
            }
        }

        private void OnPlayComplete()
        {
            playPos = -1;
            if (PlayComplete != null)
            {
                PlayComplete(this, EventArgs.Empty);
            }
        }

        internal void Refresh()
        {
            playPos = -1;
        }
 
        public void PlayProgram()
        {
            List<int> program = this.Numbers;
            if (playPos == -1)
            {
                playPos = 0;
            }

            int n = program[playPos];

            // fire this many tubes by selecting them in round robin style from each hub.
            // this uses the firing state of each tube to keep track of which tubes are still loaded.

            HubManager hubmgr = ((App)App.Current).Hubs;
            var hubs = hubmgr.Hubs.ToArray();
            List<Tuple<HubModel,List<int>>> firingPattern = new List<Tuple<HubModel,List<int>>>();
            foreach (var h in hubs)
            {
                firingPattern.Add(new Tuple<HubModel, List<int>>(h, new List<int>()));
            }

            bool added = true;
            while (n > 0 && added)
            {
                added = false;
                for (int h = 0; h < hubs.Length && n > 0; h++)
                {
                    List<int> tubes = firingPattern[h].Item2;
                    var hub = hubs[h];
                    for (int i = 0; i < hub.Tubes.Count; i++)
                    {
                        if (!tubes.Contains(i) && hub.Hub.GetTubeState(i) == 1)
                        {
                            tubes.Add(i);
                            n--;
                            added = true;
                            break;
                        }
                    }
                }
            }

            int burnTime = Settings.Instance.BurnTime;

            // send firing patterns
            System.Threading.Tasks.Parallel.ForEach(firingPattern, (tuple) => {
                HubModel model = tuple.Item1;
                List<int> tubes = tuple.Item2;
                model.Hub.FireTubes(tubes, burnTime);
            });


            playPos++;
            if (playPos >= program.Count)
            {
                OnPlayComplete();
            }
            else
            {
                OnPlayContinue();
            }
        }
    }
}
