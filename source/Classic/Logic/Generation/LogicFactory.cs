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
