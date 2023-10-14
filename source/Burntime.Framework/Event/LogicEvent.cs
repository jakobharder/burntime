using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Framework.States;

namespace Burntime.Framework
{
    public class LogicEvent : List<LogicHandler>
    {
        public static LogicEvent operator +(LogicEvent Left, LogicHandler Right)
        {
            if (Left == null)
                Left = new LogicEvent();
            Left.Add(Right);
            return Left;
        }

        public static LogicEvent operator +(LogicEvent Left, LogicMethodInterface Right)
        {
            if (Left == null)
                Left = new LogicEvent();
            Left.Add((LogicHandler)Right);
            return Left;
        }

        public static implicit operator LogicEvent(LogicHandler Right)
        {
            return new LogicEvent() + Right;
        }

        public static implicit operator LogicEvent(LogicMethodInterface Right)
        {
            return new LogicEvent() + (LogicHandler)Right;
        }

        public void Execute(StateObject State)
        {
            foreach (LogicHandler cmd in this)
            {
                cmd.Execute(State);
            }
        }
    }

    public delegate void LogicMethodInterface(StateObject State);

    public class LogicHandler
    {
        LogicMethodInterface method;

        public LogicHandler(LogicMethodInterface Method)
        {
            method = Method;
        }

        public static implicit operator LogicHandler(LogicMethodInterface Right)
        {
            return new LogicHandler(Right);
        }

        public void Execute(StateObject State)
        {
            method.Invoke(State);
        }
    }
}
