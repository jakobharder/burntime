using System;

namespace Burntime.Framework
{
    public class AssemblyNotFoundException : Exception
    {
        string name;

        public AssemblyNotFoundException(string name)
        {
            this.name = name;
        }

        public override string Message
        {
            get
            {
                return "assembly not found: " + name;
            }
        }
    }

    public class InvalidStateObjectConstruction : Exception
    {
        string name;

        public InvalidStateObjectConstruction(States.StateObject obj)
        {
            this.name = obj.GetType().Name;
        }

        public override string Message
        {
            get
            {
                return "Invalid StateObject construction of " + name;
            }
        }
    }

    public class CustomException : Exception
    {
        string message;

        public CustomException(string message)
        {
            this.message = message;
        }

        public override string Message
        {
            get
            {
                return message;
            }
        }
    }

    public class BurntimeLogicException : Exception
    {
    }

    public class BurntimeStateException : Exception
    {
    }
}
