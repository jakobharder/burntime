using System;
using System.Collections.Generic;
using System.Threading;

namespace Burntime.Framework.States
{
    //public class StateClient
    //{
    //    StateServer server;
    //    int id;
    //    bool active = false;
    //    internal EventWaitHandle FinishedEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
    //    internal bool isServerStopped;
    //    public int MachineCode = 0;

    //    public StateClient(StateServer Server)
    //    {
    //        server = Server;
    //        id = Server.AddClient();
    //    }

    //    public bool IsReady
    //    {
    //        get { return active; }
    //    }

    //    public bool IsServerStopped
    //    {
    //        get { return isServerStopped; }
    //    }

    //    public void Process()
    //    {
    //        Finish();
    //    }

    //    public void Finish()
    //    {
    //        active = false;
    //        FinishedEvent.Set();
    //    }

    //    internal void Activate()
    //    {
    //        active = true;
    //    }
    //}
}
