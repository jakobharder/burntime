/*
 *  Burntime Classic
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

using Burntime.Classic.Logic.Interaction;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Classic.ResourceProcessor
{
    class ConstructionsProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID id, ResourceManager resourceManager)
        {
            ConfigFile file = new ConfigFile();
            file.Open(id.File);
            return new Constructions(file);
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "constructions" }; }
        }
    }
}
