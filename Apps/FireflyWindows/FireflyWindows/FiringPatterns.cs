using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FireflyWindows
{
    class FiringPattern
    {
        bool stopped;
        IList<Tube> allTubes;

        bool complete;
        FireCommands cmds;

        public FiringPattern(FireCommands cmds)
        {
            this.cmds = cmds;
        }

        public void Start(IList<Tube> allTubes)
        {
            this.allTubes = allTubes;
            Resume();    
        }

        public int Count {  get { return allTubes.Count; } }

        public Tube GetTube(int i) { return allTubes[i]; }

        public virtual void Stop()
        {
            stopped = true;
        }

        public void Resume()
        {
            stopped = false;
            if (!threadRunning)
            {
                Task.Run(new Action(RunThread));
            }
        }

        bool threadRunning;

        private void RunThread()
        {
            threadRunning = true;
            while (!stopped)
            {
                Tube tube = Next();
                if (tube == null)
                {
                    complete = true;
                    break;
                }
                tube.Firing = true;
                cmds.FireTube(tube);
                Delay();
                tube.Firing = false;
            }
            threadRunning = false;
        }

        public virtual Tube Next()
        {
            return null;
        }

        public virtual void Delay()
        {

        }

        public bool Complete { get { return complete; } }
    }

    class SequentialPattern : FiringPattern
    {
        TimeSpan delay;
        int pos;

        public SequentialPattern(FireCommands cmds, TimeSpan delay) : base(cmds)
        {
            this.delay = delay;
        }

        public override Tube Next()
        {
            if (pos < Count)
            {
                return GetTube(pos++);
            }
            return null;
        }
        public override void Delay()
        {
            Thread.Sleep(delay);
        }

    }

    class CrescendoPattern : FiringPattern
    {
        int pos;
        int sleepStep;
        int nextSleep;
        int acceleration = 10;

        public CrescendoPattern(FireCommands cmds) : base(cmds)
        {
        }

        public override Tube Next()
        {
            if (nextSleep == 0)
            {
                sleepStep = 50;
                for (int i = 0; i < Count; i++)
                {
                    nextSleep += sleepStep;
                    sleepStep += acceleration;
                }
            }

            if (pos < Count)
            {
                nextSleep -= sleepStep;
                sleepStep -= acceleration;
                return GetTube(pos++);
            }
            return null;
        }

        public override void Delay()
        {
            System.Diagnostics.Debug.WriteLine("Sleeping " + nextSleep);
            Thread.Sleep(nextSleep);
        }
    }
}
