using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FireflyWindows
{
    /// <summary>
    /// This class manages the queue of commands we are sending to the controller
    /// </summary>
    class FireCommands
    {
        Queue<FireMessage> queue = new Queue<FireMessage>();
        AutoResetEvent cmdAvailable = new AutoResetEvent(false);
        bool taskRunning;
        bool closed;
        FirePort port;

        public FireCommands()
        {

        }

        public void SendHeartbeat()
        {
            lock (queue)
            {
                queue.Enqueue(new FireMessage() { FireCommand = FireCommand.Heartbeat });
            }
            cmdAvailable.Set();
        }

        public void GetTubeInfo()
        {
            // get number of tubes
            lock (queue)
            {
                queue.Enqueue(new FireMessage() { FireCommand = FireCommand.Info });
            }
            cmdAvailable.Set();
        }
        public void FireTube(Tube tube)
        {
            lock (queue)
            {
                queue.Enqueue(new FireMessage() { FireCommand = FireCommand.Fire, Arg1 = (byte)tube.Number });
            }
            cmdAvailable.Set();
        }

        public FirePort Port
        {
            get { return this.port; }
            set { this.port = value; OnPortChanged(); }
        }

        private void OnPortChanged()
        {
            if (!taskRunning && this.port != null)
            {
                taskRunning = true;
                var nowait = Task.Run(() => { CommandQueue(); });
            }
        }

        public void Close()
        {
            closed = true;
            cmdAvailable.Set();
        }

        public event EventHandler<FireMessage> ResponseReceived;

        private void CommandQueue()
        {
            // background thread.
            while (!closed)
            {
                cmdAvailable.WaitOne(1000);

                FireMessage m = null;
                lock (queue)
                {
                    if (queue.Count > 0)
                    {
                        m = queue.Dequeue();
                    }
                }
                if (m != null)
                {
                    ProcessCommand(m);
                }
            }
            taskRunning = false;
        }

        int commandTimeout = 5000;

        private void ProcessCommand(FireMessage m)
        {
            try
            {
                CancellationTokenSource src = new CancellationTokenSource();
                Task<FireMessage> sendTask = port.Send(m, src.Token);
                Task delay = Task.Delay(commandTimeout);
                if (Task.WaitAny(sendTask, delay) == 1)
                {
                    src.Cancel();
                    if (ResponseReceived != null)
                    {
                        ResponseReceived(this, new FireflyWindows.FireMessage() { FireCommand = FireCommand.Timeout });
                    }
                }
                else
                {
                    FireMessage r = sendTask.Result;
                    if (ResponseReceived != null)
                    {
                        ResponseReceived(this, r);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ResponseReceived != null)
                {
                    ResponseReceived(this, new FireflyWindows.FireMessage() { FireCommand = FireCommand.Error, Error = ex });
                }
            }
        }

        public void Clear()
        {
            lock (queue)
            {
                queue.Clear();
            }
        }

    }

}
