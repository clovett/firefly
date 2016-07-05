using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FireflyWindows
{
    /// <summary>
    /// A simple helper class that gives a way to run things on the UI thread.  The app must call Initialize once during app start, using inside OnLaunch.
    /// </summary>
    public class UiDispatcher
    {
        static UiDispatcher instance;
        Dispatcher dispatcher;

        public static void Initialize(Dispatcher d)
        {
            instance = new UiDispatcher()
            {
                dispatcher = d
            };
        }

        public static void RunOnUIThread(Action a)
        {
            if (instance == null)
            {
                // we must be in headless background task
                a();
            }
            else
            {
                var nowait = instance.dispatcher.BeginInvoke(a);
            }
        }
    }
}
