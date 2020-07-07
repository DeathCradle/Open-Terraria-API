﻿/*
Copyright (C) 2020 DeathCradle

This file is part of Open Terraria API v3 (OTAPI)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Terraria
{
    class patch_WindowsLaunch //: Terraria.WindowsLaunch
    {
        //public static AssemblyLoadContext Modifications = new AssemblyLoadContext("OTAPI", true);

        /** Begin Cross platform support - disable the kernel32 call when not on windows */
        public static extern bool orig_SetConsoleCtrlHandler(Terraria.WindowsLaunch.HandlerRoutine handler, bool add);
        public static bool SetConsoleCtrlHandler(Terraria.WindowsLaunch.HandlerRoutine handler, bool add)
        {
            if (ReLogic.OS.Platform.IsWindows)
            {
                return orig_SetConsoleCtrlHandler(handler, add);
            }
            return false;
        }
        /** End Cross platform support - disable the kernel32 call when not on windows */

        /** Begin OTAPI Startup */
        public static extern void orig_Main(string[] args);
        [System.STAThread]
        public static void Main(string[] args)
        {
            OTAPI.Plugins.PluginLoader.TryLoad();
            Console.WriteLine($"[OTAPI] Starting up.");
            OTAPI.Modifier.Apply(OTAPI.ModType.Runtime);
            orig_Main(args);
        }
        /** End OTAPI Startup */
    }
}