using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace Burntime.Framework.States
{
    class StateFormatter
    {
        BinaryFormatter formatter;

        public StateFormatter()
        {
            formatter = new BinaryFormatter();

            //formatter.AssemblyFormat = FormatterAssemblyStyle.Simple;
            //formatter.TypeFormat = FormatterTypeStyle.TypesWhenNeeded;
            formatter.Binder = new GenericSerializationBinder();
        }

        public void Serialize(Stream serializationStream, object graph)
        {
#pragma warning disable SYSLIB0011
            formatter.Serialize(serializationStream, graph);
#pragma warning restore SYSLIB0011
        }

        public object Deserialize(Stream serializationStream)
        {
#pragma warning disable SYSLIB0011
            return formatter.Deserialize(serializationStream);
#pragma warning restore SYSLIB0011
        }

        internal sealed class GenericSerializationBinder : System.Runtime.Serialization.SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                Type typeToDeserialize = null;

                List<Type> tmpTypes = new List<Type>();
                Type genType = null;

                try
                {
                    if (typeName.Contains("System.Collections.Generic") && typeName.Contains("[["))
                    {
                        string[] splitTyps = typeName.Split(new char[] { '[' });

                        foreach (string typ in splitTyps)
                        {
                            if (typ.Contains("Version"))
                            {
                                string asmTmp = typ.Substring(typ.IndexOf(',') + 1);
                                string asmName = asmTmp.Remove(asmTmp.IndexOf(']')).Trim();
                                string typName = typ.Remove(typ.IndexOf(','));
                                tmpTypes.Add(BindToType(asmName, typName));
                            }
                            else if (typ.Contains("Generic"))
                            {
                                genType = BindToType(assemblyName, typ);
                            }
                        }
                        if (genType != null && tmpTypes.Count > 0)
                        {
                            return genType.MakeGenericType(tmpTypes.ToArray());
                        }
                    }

                    string ToAssemblyName = assemblyName.Split(',')[0];
                    Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (Assembly asm in Assemblies)
                    {
                        if (asm.FullName.Split(',')[0] == ToAssemblyName)
                        {
                            typeToDeserialize = asm.GetType(typeName);
                            break;
                        }
                    }

                }
                catch (System.Exception exception)
                {
                    throw exception;
                }
                return typeToDeserialize;
            }
        }
    }
}
