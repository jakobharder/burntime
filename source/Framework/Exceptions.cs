/*
 *  Burntime Framework
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

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
