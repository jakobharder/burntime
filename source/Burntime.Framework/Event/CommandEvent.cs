using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;

namespace Burntime.Framework
{
    public class CommandEvent : List<CommandHandler>
    {
        public static CommandEvent operator +(CommandEvent Left, CommandHandler Right)
        {
            if (Left == null)
                Left = new CommandEvent();
            Left.Add(Right);
            return Left;
        }

        public static CommandEvent operator +(CommandEvent Left, CommandMethodInterface Right)
        {
            if (Left == null)
                Left = new CommandEvent();
            Left.Add((CommandHandler)Right);
            return Left;
        }

        public static CommandEvent operator +(CommandEvent Left, CommandIndexMethodInterface Right)
        {
            if (Left == null)
                Left = new CommandEvent();
            Left.Add((CommandHandler)Right);
            return Left;
        }

        public static implicit operator CommandEvent(CommandHandler Right)
        {
            return new CommandEvent() + Right;
        }

        public static implicit operator CommandEvent(CommandMethodInterface Right)
        {
            return new CommandEvent() + (CommandHandler)Right;
        }

        public static implicit operator CommandEvent(CommandIndexMethodInterface Right)
        {
            return new CommandEvent() + (CommandHandler)Right;
        }

        public void Execute()
        {
            foreach (CommandHandler cmd in this)
                cmd.Execute();
        }

        public void Execute(int Index)
        {
            foreach (CommandHandler cmd in this)
                cmd.Execute(Index);
        }
    }

    public delegate void CommandMethodInterface();
    public delegate void CommandIndexMethodInterface(int Index);

    public class CommandHandler
    {
        CommandMethodInterface method1;
        CommandIndexMethodInterface method2;
        Action _action;
        int index;

        public CommandHandler(Action action)
        {
            _action = action;
        }

        public CommandHandler(CommandMethodInterface Method)
        {
            method1 = Method;
        }

        public CommandHandler(CommandIndexMethodInterface Method)
        {
            method2 = Method;
        }

        public CommandHandler(CommandIndexMethodInterface method, int directIndex)
        {
            index = directIndex;
            method2 = method;
        }

        public static implicit operator CommandHandler(CommandMethodInterface Right)
        {
            return new CommandHandler(Right);
        }

        public static implicit operator CommandHandler(CommandIndexMethodInterface Right)
        {
            return new CommandHandler(Right);
        }

        public void Execute()
        {
            if (_action is not null)
                _action.Invoke();
            else if (method1 != null)
                method1.Invoke();
            else
                method2.Invoke(index);
        }

        public void Execute(int Index)
        {
            if (_action is not null)
                _action.Invoke();
            else if (method1 != null)
                method1.Invoke();
            else
                method2.Invoke(Index);
        }
    }
}
