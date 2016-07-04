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
        TimeSpan delay;
        bool complete;
        FireCommands cmds;

        public FiringPattern(FireCommands cmds)
        {
            this.cmds = cmds;
        }

        public void Start(IList<Tube> allTubes, TimeSpan delay)
        {
            this.allTubes = allTubes;
            this.delay = delay;
            Resume();    
        }

        public int Count {  get { return allTubes.Count; } }

        public Tube GetTube(int i) { return allTubes[i]; }

        public virtual void Stop()
        {
            stopped = true;
        }

        public async void Resume()
        {
            stopped = false;
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
                await Task.Delay(200);
                tube.Firing = false;
            }
        }

        public virtual Tube Next()
        {
            return null;
        }

        public bool Complete { get { return complete; } }
    }

    class SequentialPattern : FiringPattern
    {
        int pos;

        public SequentialPattern(FireCommands cmds) : base(cmds)
        {
        }

        public override Tube Next()
        {
            if (pos < Count)
            {
                return GetTube(pos++);
            }
            return null;
        }
    }
}