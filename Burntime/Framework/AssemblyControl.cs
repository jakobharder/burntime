
#region GNU General Public License - Burntime Deluxe
/*
 *  Burntime Deluxe
 *  Copyright (C) 2009-2011 Jakob Harder
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
*/
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Framework
{
    //class AssemblyInfo
    //{
    //    public String Name;
    //    public Module Module;
    //    public Assembly Asm;
    //}

    public class AssemblyControl
    {
        Dictionary<String, Module> modules = new Dictionary<string, Module>();
        Dictionary<String, Assembly> assemblies = new Dictionary<string, Assembly>();
        AppDomain domain;

        public AssemblyControl(AppDomain Domain)
        {
            domain = Domain;

            domain.AssemblyLoad += new AssemblyLoadEventHandler(AssemblyLoadEvent);
            domain.AssemblyResolve += new ResolveEventHandler(ResolveEvent);
        }

        public void LoadFile(String file)
        {
            domain.Load(file);
        }

        public Assembly GetAssembly(String Name)
        {
            if (assemblies.ContainsKey(Name))
                return assemblies[Name];
            foreach (Assembly asm in assemblies.Values)
            {
                if (asm.GetName().Name.ToLower() == Name.ToLower())
                    return asm;
            }

            throw new AssemblyNotFoundException(Name);
        }

        public object GetInstance(String Assembly, String TypeName)
        {
            Type type = assemblies[Assembly].GetType(TypeName);
            return Activator.CreateInstance(type);
        }

        void AssemblyLoadEvent(object sender, AssemblyLoadEventArgs args)
        {
            assemblies[args.LoadedAssembly.GetName().Name] = args.LoadedAssembly;
        }

        Assembly ResolveEvent(object sender, ResolveEventArgs args)
        {
            if (args.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return Assembly.LoadFile(args.Name);

            foreach (Assembly a in assemblies.Values)
            {
                if (a.FullName.IndexOf(args.Name, 0, StringComparison.OrdinalIgnoreCase) >= 0)
                    return a;
            }

            File file = FileSystem.GetFile(args.Name + ".dll");
            if (file == null)
                return null;
            
            // in order to use pdb debug files and visual studio debugging try loading from real file first
            if (file.HasFullPath)
                return Assembly.LoadFile(file.FullPath);
            
            // load assembly from memory
            return Assembly.Load(file.ReadAllBytes());
        }

        public void Load(String[] Modules, String Paket)
        {
            foreach (String mod in Modules)
                Load(mod, Paket);
        }

        public void Load(String moduleName, String package)
        {
            Log.Info("Load module: " + moduleName);

            Module module = null;

            domain.Load(moduleName);
            Assembly asm = GetAssembly(moduleName);
 
            List<Type> scenes = new List<Type>();
            List<Type> dataprocessor = new List<Type>();

            try
            {
                foreach (Type t in asm.GetTypes())
                {
                    if (t.IsSubclassOf(typeof(Module)))
                    {
                        module = Activator.CreateInstance(t) as Module;
                        Log.Info("   Load module class: " + t.Name);
                    }

                    if (t.IsSubclassOf(typeof(Scene)))
                        scenes.Add(t);

                    if (typeof(IDataProcessor).IsAssignableFrom(t))
                        dataprocessor.Add(t);
                }

                foreach (Type t in scenes)
                    Log.Info("   Load scene class: " + t.Name);
                foreach (Type t in dataprocessor)
                {
                    Log.Info("   Load data processor: " + t.Name);
                    module.AddProcessor((IDataProcessor)Activator.CreateInstance(t));
                }

            }
            catch (Exception e)
            {
                // marker for debugging purposes
                throw e;
            }

            module.Scenes = scenes;

            modules[FileSystem.GetUniqueName(moduleName, package)] = module;
        }

        public Module GetModule(String Module, String Paket)
        {
            if (modules.ContainsKey(FileSystem.GetUniqueName(Module, Paket)))
            {
                return modules[FileSystem.GetUniqueName(Module, Paket)];
            }

            return null;
        }

        public void InitAllModules(ResourceManager ResourceManager)
        {
            Log.Info("Initialize modules");

            foreach (Module module in modules.Values)
            {
                Log.Info(module.GetType().Name + "...");
                module.resourceManager = ResourceManager;
                module.Initialize();
            }
        }
    }
}
