﻿// Copyright (C) 2020-2021 DeathCradle
//
// This file is part of Open Terraria API v3 (OTAPI)
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.
// using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModFramework;
using ModFramework.Modules.CSharp;
using ModFramework.Plugins;
using Mono.Cecil;
using OTAPI.Common;

namespace OTAPI.Patcher.Targets
{
    [MonoMod.MonoModIgnore]
    public class OTAPIClientLightweightTarget : IPatchTarget
    {
        public string DisplayText { get; } = "OTAPI Client (lightweight)";

        bool CanLoadFile(string filepath)
        {
            // only load "server" or "both" variants
            var filename = Path.GetFileNameWithoutExtension(filepath);
            return !filename.EndsWith(".Server", StringComparison.CurrentCultureIgnoreCase);
        }

        public void Patch()
        {
            Console.WriteLine($"Open Terraria API v{Common.GetVersion()} [lightweight]");

            PluginLoader.AssemblyFound += CanLoadFile;
            ModFramework.Modules.CSharp.CSharpLoader.AssemblyFound += CanLoadFile;

            var installPath = ClientHelpers.DetermineClientInstallPath();

            var input_regular = Path.Combine(installPath, "Resources/Terraria.exe");
            var input_orig = Path.Combine(installPath, "Resources/Terraria.orig.exe");

            var input = File.Exists(input_regular) ? input_regular : input_orig;

            //var freshAssembly = "../../../../OTAPI.Setup/bin/Debug/net5.0/Terraria.exe";
            var localPath = "Terraria.exe";

            if (File.Exists(localPath)) File.Delete(localPath);
            File.Copy(input, localPath);

            foreach (var lib in new[]
            {
                "FNA.dll",
                "SteelSeriesEngineWrapper.dll",
                //"../MacOS/osx/CSteamworks",
            })
            {
                var name = Path.GetFileName(lib);
                var src = Path.Combine(installPath, "Resources", lib);
                if (File.Exists(src))
                {
                    if (File.Exists(name)) File.Delete(name);
                    File.Copy(src, name);
                }
            }

            // load into the current app domain for patch refs
            var asm = Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, localPath));
            //var asmFNA = Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, FNA));
            var assemblies = new Dictionary<string, Assembly>()
            {
                {asm.FullName, asm },
            };
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                Console.WriteLine("[Patch Resolve] " + args.Name);
                //if (args.Name.IndexOf("Terraria") > -1)
                //{
                //    return asm;
                //}

                var match = assemblies.FirstOrDefault(a => a.Key == args.Name);
                if (match.Key != null)
                    return match.Value;

                var asn = new AssemblyName(args.Name);
                var filename = $"{asn.Name}.dll";
                if (File.Exists(filename))
                {
                    try
                    {
                        var abs = Path.GetFullPath(filename);
                        var loaded = Assembly.LoadFile(abs);
                        assemblies.Add(args.Name, loaded);
                        return loaded;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                return null;
            };

            Console.WriteLine("[OTAPI] Extracting embedded binaries for assembly resolution...");
            var extractor = new ResourceExtractor();
            var embeddedResourcesDir = extractor.Extract(localPath);


            // build shims
            var ldr = new CSharpLoader().SetAutoLoadAssemblies(false);
            var md = ldr.CreateMetaData();
            var shims = ldr.LoadModules(md, "shims").ToArray();

            using (var public_mm = new ModFwModder()
            {
                InputPath = localPath,
                OutputPath = "OTAPI.dll",
                MissingDependencyThrow = false,
                //LogVerboseEnabled = true,
                PublicEverything = true,

                GACPaths = new string[] { } // avoid MonoMod looking up the GAC, which causes an exception on .netcore
            })
            {
                (public_mm.AssemblyResolver as DefaultAssemblyResolver)!.AddSearchDirectory(embeddedResourcesDir);
                (public_mm.AssemblyResolver as DefaultAssemblyResolver)!.AddSearchDirectory(Path.Combine(installPath, "Resources"));
                public_mm.Read();
                public_mm.MapDependencies();
                public_mm.ReadMod(this.GetType().Assembly.Location);
                public_mm.ReadMod(Path.Combine(embeddedResourcesDir, "ReLogic.dll"));

                foreach (var path in shims)
                {
                    public_mm.ReadMod(path);
                }

                // relink / merge into the output
                public_mm.RelinkModuleMap["ReLogic"] = public_mm.Module;
                //public_mm.RelinkModuleMap["System.Windows.Forms"] = public_mm.Module;

                public_mm.AutoPatch();
                public_mm.Write();

                const string script_refs = "refs.dll";
                if (File.Exists(script_refs)) File.Delete(script_refs);
                File.Copy("OTAPI.dll", script_refs);


                var inputName = Path.GetFileNameWithoutExtension(localPath);
                var initialModuleName = public_mm.Module.Name;

                var const_major = $"{inputName}_V{public_mm.Module.Assembly.Name.Version.Major}_{public_mm.Module.Assembly.Name.Version.Minor}";
                var const_fullname = $"{inputName}_{public_mm.Module.Assembly.Name.Version.ToString().Replace(".", "_")}";

                File.WriteAllText("AutoGenerated.target", @$"<!-- DO NOT EDIT THIS FILE! It was auto generated by the setup project  -->
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <DefineConstants>{inputName};{const_major};{const_fullname}</DefineConstants>
  </PropertyGroup>
</Project>");
                File.WriteAllText("AutoGenerated.cs", @$"#define {inputName}
#define {const_major}
#define {const_fullname}
");
            }

            //var installPath = ClientHelpers.DetermineClientInstallPath();
            var resources = Path.Combine(installPath, "Resources");
            var assembly_output = Path.Combine(installPath, "Resources/OTAPI.exe");
            //var runtime_output = Path.Combine(installPath, "Resources/Terraria.Runtime.dll");
            //var mfw_output = Path.Combine(installPath, "Resources/ModFramework.dll");

            // load modfw plugins. this will load ModFramework.Modules and in turn top level c# scripts
            ModFramework.Modules.CSharp.CSharpLoader.GlobalAssemblies.Add("OTAPI.dll");
            ModFramework.Modules.CSharp.CSharpLoader.GlobalAssemblies.Add(Path.Combine(resources, "FNA.dll"));
            //ModFramework.Modules.CSharp.CSharpLoader.GlobalAssemblies.Add(Path.Combine(Path.GetDirectoryName(typeof(Object).Assembly.Location), "mscorlib.dll"));
            PluginLoader.TryLoad();

            using var mm = new ModFwModder()
            {
                InputPath = "OTAPI.dll",
                OutputPath = "OTAPI.exe",
                MissingDependencyThrow = false,
                //LogVerboseEnabled = true,
                //PublicEverything = true,

                GACPaths = new string[] { } // avoid MonoMod looking up the GAC, which causes an exception on .netcore
            };
            (mm.AssemblyResolver as DefaultAssemblyResolver)!.AddSearchDirectory(embeddedResourcesDir);
            (mm.AssemblyResolver as DefaultAssemblyResolver)!.AddSearchDirectory(Path.Combine(installPath, "Resources"));
            mm.Read();

            //// prechange the assembly name to a dll
            //// monomod will also reference this when relinking so it must be correct
            //// in order for shims within this dll to work (relogic)
            //mm.Module.Name = "TerrariaServer.dll";
            //mm.Module.Assembly.Name.Name = "TerrariaServer";

            //// merge in ModFramework
            //{
            //    mm.OnReadMod += (m, module) =>
            //    {
            //        if (module.Assembly.Name.Name.StartsWith("ModFramework"))
            //            mm.RelinkAssembly(module);
            //    };
            //    mm.ReadMod(Path.Combine(System.Environment.CurrentDirectory, "ModFramework.dll"));
            //}

            mm.MapDependencies();

            mm.RelinkModuleMap["System.Windows.Forms"] = mm.Module;

            mm.AutoPatch();

#if tModLoaderServer_V1_3
                mm.WriterParameters.SymbolWriterProvider = null;
                mm.WriterParameters.WriteSymbols = false;
#endif

            //{
            //    var sac = mm.Module.ImportReference(typeof(AssemblyInformationalVersionAttribute).GetConstructors()[0]);
            //    var sa = new CustomAttribute(sac);
            //    sa.ConstructorArguments.Add(new CustomAttributeArgument(mm.Module.TypeSystem.String, GetVersion()));
            //    mm.Module.Assembly.CustomAttributes.Add(sa);
            //}

            foreach (var asmref in mm.Module.AssemblyReferences.ToArray())
            {
                if (asmref.Name.Contains("System.Private.CoreLib") || asmref.Name.Contains("netstandard")
                    || asmref.Name.Contains("System.Windows.Forms"))
                {
                    mm.Module.AssemblyReferences.Remove(asmref);
                }
            }

            foreach (var mmt in mm.Module.Types.Where(x => x.Namespace == "MonoMod").ToArray())
            {
                mm.Module.Types.Remove(mmt);
            }

            mm.Write();

            //if (File.Exists(assembly_output)) File.Delete(assembly_output);
            //File.Copy("OTAPI.exe", assembly_output);

            //mm.Log("[OTAPI] Generating Terraria.Runtime.dll");
            //var gen = new MonoMod.RuntimeDetour.HookGen.HookGenerator(mm, "Terraria.Runtime.dll");
            //using (ModuleDefinition mOut = gen.OutputModule)
            //{
            //    gen.Generate();


            //    foreach (var asmref in mOut.AssemblyReferences.ToArray())
            //    {
            //        if (asmref.Name.Contains("System.Private.CoreLib") || asmref.Name.Contains("netstandard"))
            //        {
            //            mOut.AssemblyReferences.Remove(asmref);
            //        }
            //    }

            //    mOut.Write("Terraria.Runtime.dll");
            //    if (File.Exists(runtime_output)) File.Delete(runtime_output);
            //    File.Copy("Terraria.Runtime.dll", runtime_output);
            //}

            //if (File.Exists(mfw_output)) File.Delete(mfw_output);
            //File.Copy("ModFramework.dll", mfw_output);

            CreateRuntimeEvents();

            mm.Log("[OTAPI] Done.");
        }

        static void CreateRuntimeEvents()
        {
            Console.WriteLine("[OTAPI] Creating runtime events");
            var root = Environment.CurrentDirectory;

            PluginLoader.Clear();

            using (var mm = new ModFwModder()
            {
                InputPath = Path.Combine(root, "OTAPI.exe"),
                //OutputPath = "OTAPI.dll",
                MissingDependencyThrow = false,
                //LogVerboseEnabled = true,
                //PublicEverything = true,

                GACPaths = new string[] { } // avoid MonoMod looking up the GAC, which causes an exception on .netcore
            })
            {
                (mm.AssemblyResolver as DefaultAssemblyResolver)!.AddSearchDirectory(Path.Combine(root, "EmbeddedResources"));
                //(mm.AssemblyResolver as DefaultAssemblyResolver)!.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
                mm.Read();
                mm.MapDependencies();

                //mm.Log("[OTAPI Client Install] Generating OTAPI.Runtime.dll");
                var gen = new MonoMod.RuntimeDetour.HookGen.HookGenerator(mm, "OTAPI.Runtime.dll");
                using (ModuleDefinition mOut = gen.OutputModule)
                {
                    gen.Generate();

                    foreach (var asmref in mOut.AssemblyReferences.ToArray())
                    {
                        if (asmref.Name.Contains("System.Private.CoreLib") || asmref.Name.Contains("netstandard"))
                        {
                            //mOut.AssemblyReferences.Remove(asmref);
                        }
                    }

                    Directory.CreateDirectory("outputs");
                    mOut.Write("outputs/OTAPI.Runtime.dll");
                    ModFramework.Relinker.MscorlibRelinker.PostProcessMscorLib("outputs/OTAPI.Runtime.dll");
                }
            }
        }
    }
}