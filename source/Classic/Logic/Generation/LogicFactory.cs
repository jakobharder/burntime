
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;

namespace Burntime.Classic.Logic.Generation
{
    public class LogicFactory
    {
        protected Dictionary<string, object> param = new Dictionary<string, object>();
        protected Dictionary<Type, Type> impl = new Dictionary<Type, Type>();

        public static IGameObjectCreator[] Creator { get; protected set; }

        public static LogicFactory Instance { get; protected set; }

        public object this[string name]
        {
            get
            {
                if (!param.ContainsKey(name))
                    return null;
                return param[name];
            }
            set
            {
                if (!param.ContainsKey(name))
                    param.Add(name, value);
                else
                    param[name] = value;
            }
        }

        public LogicFactory()
        {
            Instance = this;
        }

        public static T GetParameter<T>(string key)
        {
            return (T)Instance[key];
        }

        public static void SetParameter(string key, object obj)
        {
            Instance[key] = obj;
        }
    }
}
