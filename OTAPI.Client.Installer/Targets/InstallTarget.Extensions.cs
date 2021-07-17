﻿using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using ModFramework.Modules.ClearScript.Typings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using static ModFramework.Modules.CSharp.CSharpLoader;

namespace OTAPI.Client.Installer.Targets
{
    public static class InstallTargetExtensions
    {
        // @todo use modfw's - needs nuget update
        static IEnumerable<CompilationFile> ParseFiles(IEnumerable<string> files, IEnumerable<string> constants, string type)
        {
            foreach (var file in files)
            {
                var folder = Path.GetFileName(Path.GetDirectoryName(file));

                var encoding = System.Text.Encoding.UTF8;
                var parse_options = CSharpParseOptions.Default
                    .WithKind(SourceCodeKind.Regular)
                    .WithPreprocessorSymbols(constants.Select(s => s.Replace("#define ", "")))
                    .WithDocumentationMode(DocumentationMode.Parse)
                    .WithLanguageVersion(LanguageVersion.Preview); // allows toplevel functions

                var src = File.ReadAllText(file);
                var source = SourceText.From(src, encoding);
                var encoded = CSharpSyntaxTree.ParseText(source, parse_options, file);
                var embedded = EmbeddedText.FromSource(file, source);

                yield return new CompilationFile()
                {
                    File = file,
                    SyntaxTree = encoded,
                    EmbeddedText = embedded,
                };
            }
        }

        public static void CopyOTAPI(this IInstallTarget target, string otapiFolder, IEnumerable<string> packagePaths)
        {
            Console.WriteLine(target.Status = "Copying OTAPI...");
            foreach (var packagePath in packagePaths)
                target.CopyFiles(packagePath, otapiFolder);

            // copy installer

            File.WriteAllText(Path.Combine(otapiFolder, "Terraria.runtimeconfig.json"), @"{
  ""runtimeOptions"": {
    ""tfm"": ""net5.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""5.0.0""
    }
  }
}");

            target.TransferFile("FNA.dll", Path.Combine(otapiFolder, "FNA.dll"));
            target.TransferFile("FNA.dll.config", Path.Combine(otapiFolder, "FNA.dll.config"));
            target.TransferFile("FNA.pdb", Path.Combine(otapiFolder, "FNA.pdb"));

            target.TransferFile("ModFramework.dll", Path.Combine(otapiFolder, "ModFramework.dll"));

            target.TransferFile("NLua.dll", Path.Combine(otapiFolder, "NLua.dll"));
            target.TransferFile("KeraLua.dll", Path.Combine(otapiFolder, "KeraLua.dll"));

            target.TransferFile("ImGui.NET.dll", Path.Combine(otapiFolder, "ImGui.NET.dll"));

            if (File.Exists("lua54.dll")) target.TransferFile("lua54.dll", Path.Combine(otapiFolder, "lua54.dll"));
            if (File.Exists("cimgui.dll")) target.TransferFile("cimgui.dll", Path.Combine(otapiFolder, "cimgui.dll"));

            target.TransferFile("SteelSeriesEngineWrapper.dll", Path.Combine(otapiFolder, "SteelSeriesEngineWrapper.dll"));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) target.TransferFile("OTAPI.Client.Installer.exe", Path.Combine(otapiFolder, "OTAPI.Client.Installer.exe"));
            else target.TransferFile("OTAPI.Client.Installer", Path.Combine(otapiFolder, "OTAPI.Client.Installer"));
            target.TransferFile("OTAPI.Client.Installer.runtimeconfig.json", Path.Combine(otapiFolder, "OTAPI.Client.Installer.runtimeconfig.json"));
            target.TransferFile(Path.Combine(otapiFolder, "Terraria.exe"), Path.Combine(otapiFolder, "OTAPI.Client.Installer.dll"));
            target.TransferFile(Path.Combine(otapiFolder, "Terraria.pdb"), Path.Combine(otapiFolder, "OTAPI.Client.Installer.pdb"));

            target.TransferFile("OTAPI.exe", Path.Combine(otapiFolder, "OTAPI.exe"));
            target.TransferFile("OTAPI.Runtime.dll", Path.Combine(otapiFolder, "OTAPI.Runtime.dll"));
            target.TransferFile("OTAPI.Common.dll", Path.Combine(otapiFolder, "OTAPI.Common.dll"));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "OTAPI.Patcher*"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "Mono*.dll"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "ClearScript*.dll"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "System.*.dll"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "ms*.dll"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "Microsoft*.dll"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "api-ms-*.dll"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory, "Avalonia*.dll"))
                target.TransferFile(file, Path.Combine(otapiFolder, Path.GetFileName(file)));

            target.TransferFile("netstandard.dll", Path.Combine(otapiFolder, "netstandard.dll"));

            if (Directory.Exists("runtimes"))
            {
                target.CopyFiles("runtimes", Path.Combine(otapiFolder, "runtimes"));

                target.CopyFiles(Path.Combine("runtimes", "osx", "native"), Path.Combine(otapiFolder, "osx"));
                target.CopyFiles(Path.Combine("runtimes", "osx-x64", "native"), Path.Combine(otapiFolder, "osx"));
                target.CopyFiles(Path.Combine("runtimes", "win-x64", "native"), Path.Combine(otapiFolder, "x64"));
                target.CopyFiles(Path.Combine("runtimes", "linux-x64", "native"), Path.Combine(otapiFolder, "lib64"));
            }
        }

        public static IEnumerable<string> PublishHostGame(this IInstallTarget target)
        {
            Console.WriteLine(target.Status = "Building host game...");
            var hostDir = "hostgame";

            var output = "host_game";
            if (Directory.Exists(output)) Directory.Delete(output, true);
            Directory.CreateDirectory(output);

            //var hostDir = "../../../../OTAPI.Client.Host/";

            //var package = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            //    ? "win-x64" : (
            //    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx.10.11-x64" : "ubuntu.16.04-x64"
            //);

            //using var process = new System.Diagnostics.Process()
            //{
            //    StartInfo = new System.Diagnostics.ProcessStartInfo()
            //    {
            //        FileName = Path.Combine(Environment.CurrentDirectory, "MSBuild.dll"),
            //        Arguments = $"publish {hostDir}/OTAPI.Client.Host.csproj -r " + package,
            //        //Arguments = "msbuild -restore -t:PublishAllRids",
            //        WorkingDirectory = Environment.CurrentDirectory
            //    },
            //};
            //process.Start();
            //process.WaitForExit();

            //var output = Path.Combine(hostDir, "bin", "Debug", "net5.0", package, "publish");
            //if (Directory.Exists(output)) return new[] { output };
            //else return Enumerable.Empty<string>();

            const string constants_path = "AutoGenerated.cs";
            var constants = File.Exists(constants_path)
                ? File.ReadAllLines(constants_path) : Enumerable.Empty<string>(); // bring across the generated constants

            var compile_options = new CSharpCompilationOptions(OutputKind.WindowsApplication)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.X64)
                .WithAllowUnsafe(true);

            var files = Directory.EnumerateFiles(hostDir, "*.cs");
            var parsed = ParseFiles(files, constants, null);

            var syntaxTrees = parsed.Select(x => x.SyntaxTree);

            var compilation = CSharpCompilation
                .Create("Terraria", syntaxTrees, options: compile_options)
            ;

            var libs = ((String)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Where(x => !x.StartsWith(Environment.CurrentDirectory));
            foreach (var lib in libs)
            {
                compilation = compilation.AddReferences(MetadataReference.CreateFromFile(lib));
            }

#if RELEASE
            foreach (var lib in System.IO.Directory.GetFiles(Environment.CurrentDirectory, "System.*.dll"))
            {
                compilation = compilation.AddReferences(MetadataReference.CreateFromFile(lib));
            }
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile("mscorlib.dll"));
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile("netstandard.dll"));
#endif

            compilation = compilation.AddReferences(MetadataReference.CreateFromFile("FNA.dll"));
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile("ImGui.NET.dll"));
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile("OTAPI.exe"));
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile("OTAPI.Runtime.dll"));
            //compilation = compilation.AddReferences(MetadataReference.CreateFromFile(Path.Combine(hostDir, @"..\OTAPI.Client.Installer\bin\Debug\net5.0\OTAPI.exe")));
            //compilation = compilation.AddReferences(MetadataReference.CreateFromFile(Path.Combine(hostDir, @"..\OTAPI.Client.Installer\bin\Debug\net5.0\OTAPI.Runtime.dll")));

            var outPdbPath = Path.Combine(output, "Terraria.pdb");
            var emitOptions = new EmitOptions(
                debugInformationFormat: DebugInformationFormat.PortablePdb,
                pdbFilePath: outPdbPath
            );


            var dllStream = new MemoryStream();
            var pdbStream = new MemoryStream();
            var xmlStream = new MemoryStream();
            var result = compilation.Emit(
                peStream: dllStream,
                pdbStream: pdbStream,
                xmlDocumentationStream: xmlStream,
                embeddedTexts: parsed.Select(x => x.EmbeddedText),
                options: emitOptions
            );

            if (!result.Success)
            {
                throw new Exception($"Compilation failed: " + String.Join("\n", result.Diagnostics.Select(x => x.ToString())));
            }

            File.WriteAllBytes(Path.Combine(output, "Terraria.exe"), dllStream.ToArray());
            File.WriteAllBytes(outPdbPath, pdbStream.ToArray());
            File.WriteAllBytes(Path.Combine(output, "Terraria.xml"), xmlStream.ToArray());

            Console.WriteLine("Published");

            return new[] { output };
        }

        class GHArtifactResponse
        {
            [JsonProperty("artifacts")]
            public IEnumerable<GHArtifact> Artifacts { get; set; }
        }
        class GHArtifact
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("archive_download_url")]
            public string ArchiveDownloadUrl { get; set; }
        }

        public static string PublishHostLauncher(this IInstallTarget target)
        {
            var url = "https://api.github.com/repos/DeathCradle/Open-Terraria-API/actions/artifacts?per_page=20";

            if (!Directory.Exists("launcher_files"))
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("OTAPI3-Installer");
                var data = client.GetStringAsync(url).Result;
                var resp = Newtonsoft.Json.JsonConvert.DeserializeObject<GHArtifactResponse>(data);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    url = resp.Artifacts.First(a => a.Name == "Windows Launcher").ArchiveDownloadUrl;
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    url = resp.Artifacts.First(a => a.Name == "MacOS Launcher").ArchiveDownloadUrl;
                else // linux
                    url = resp.Artifacts.First(a => a.Name == "Linux Launcher").ArchiveDownloadUrl;

                Console.WriteLine(target.Status = "Downloading launcher, this may take a long time...");
                using var launcher = client.GetStreamAsync(url).Result;

                if (File.Exists("launcher.zip")) File.Delete("launcher.zip");
                using var fs = File.OpenWrite("launcher.zip");

                var buffer = new byte[512];
                int read;
                while (launcher.CanRead)
                {
                    if ((read = launcher.Read(buffer, 0, buffer.Length)) == 0)
                        break;

                    fs.Write(buffer, 0, read);
                }
                fs.Flush();

                Directory.CreateDirectory("launcher_files");
                ZipFile.ExtractToDirectory("launcher.zip", "launcher_files");
            }

            return "launcher_files";

            //Console.WriteLine(target.Status = "Building launcher, this may take a long time...");
            //var hostDir = "../../../../OTAPI.Client.Launcher/";

            //var package = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            //    ? "win-x64" : (
            //    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx.10.11-x64" : "ubuntu.16.04-x64"
            //);

            //using var process = new System.Diagnostics.Process()
            //{
            //    StartInfo = new System.Diagnostics.ProcessStartInfo()
            //    {
            //        FileName = "dotnet",
            //        Arguments = $"publish -r {package} --framework net5.0 -p:PublishTrimmed=true -p:PublishSingleFile=true -p:PublishReadyToRun=true --self-contained true -c Release",
            //        //Arguments = "msbuild -restore -t:PublishAllRids",
            //        WorkingDirectory = hostDir
            //    },
            //};
            //process.Start();
            //process.WaitForExit();

            //Console.WriteLine("Published");

            //return Path.Combine(hostDir, "bin", "Release", "net5.0", package, "publish");
        }

        public static void CopyFiles(this IInstallTarget target, string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }

        public static void TransferFile(this IInstallTarget target, string src, string dst)
        {
            if (!File.Exists(src))
                throw new FileNotFoundException("Source binary not found, was it rebuilt before running the installer?\n" + src);

            if (File.Exists(dst))
                File.Delete(dst);

            File.Copy(src, dst);
        }

        public static string DownloadZip(this IInstallTarget target, string url)
        {
            Console.WriteLine($"Downloading {url}");
            var uri = new Uri(url);
            string filename = Path.GetFileName(uri.AbsolutePath);
            if (!String.IsNullOrWhiteSpace(filename))
            {
                var savePath = Path.Combine(Environment.CurrentDirectory, filename);
                var info = new FileInfo(savePath);

                //if (info.Exists) info.Delete();

                if (!info.Exists || info.Length == 0)
                {
                    using (var wc = new System.Net.WebClient())
                    {
                        int lastPercentage = -1;
                        wc.DownloadProgressChanged += (s, e) =>
                        {
                            if (lastPercentage != e.ProgressPercentage)
                            {
                                lastPercentage = e.ProgressPercentage;
                                Console.WriteLine($"Downloading fnalibs...{e.ProgressPercentage}%");
                            }
                        };
                        wc.DownloadFileTaskAsync(new Uri(url), savePath).Wait();
                    }
                }

                return savePath;
            }
            else throw new NotSupportedException();
        }

        public static string ExtractBZip2(this IInstallTarget target, string zipPath, string dest = null)
        {
            using var raw = File.OpenRead(zipPath);
            using var ms = new MemoryStream();
            BZip2.Decompress(raw, ms, false);
            ms.Seek(0, SeekOrigin.Begin);

            using var tarArchive = TarArchive.CreateInputTarArchive(ms, System.Text.Encoding.UTF8);

            var abs = Path.GetFullPath(dest);
            tarArchive.ExtractContents(abs);
            tarArchive.Close();

            return dest;
        }

        public static void GenerateTypings(this IInstallTarget target, string rootFolder)
        {
            var patcherDir = "../../../../OTAPI.Patcher/";

            using (var typeGen = new TypingsGenerator())
            {
                AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
                {
                    var asr = new AssemblyName(e.Name);
                    var exe = Path.Combine(rootFolder, $"{asr.Name}.exe");
                    var dll = Path.Combine(rootFolder, $"{asr.Name}.dll");

                    if (File.Exists(exe))
                        return Assembly.LoadFile(exe);

                    if (File.Exists(dll))
                        return Assembly.LoadFile(dll);

                    exe = Path.Combine(patcherDir, "bin", "Debug", "net5.0", "EmbeddedResources", $"{asr.Name}.exe");
                    dll = Path.Combine(patcherDir, "bin", "Debug", "net5.0", "EmbeddedResources", $"{asr.Name}.dll");

                    if (File.Exists(exe))
                        return Assembly.LoadFile(exe);

                    if (File.Exists(dll))
                        return Assembly.LoadFile(dll);

                    return null;
                };

                //typeGen.AddAssembly(typeof(Mono.Cecil.AssemblyDefinition).Assembly);

                var otapi = Path.Combine(rootFolder, "OTAPI.exe");
                var otapiRuntime = Path.Combine(rootFolder, "OTAPI.Runtime.dll");

                if (File.Exists(otapi))
                    typeGen.AddAssembly(Assembly.LoadFile(otapi));

                //if (File.Exists(otapiRuntime))
                //    typeGen.AddAssembly(Assembly.LoadFile(otapiRuntime));

                //var outDir = Path.Combine(rootFolder, "clearscript", "typings");
                var outDir = Path.Combine(rootFolder, "clearscript", "test", "src", "typings");
                typeGen.Write(outDir);

                File.WriteAllText(Path.Combine(outDir, "index.js"), "// typings only\n");
            }
        }

        public static void InstallClearScript(this IInstallTarget target, string otapiInstallPath)
        {
            var modificationsDir = Path.Combine(otapiInstallPath, "modifications");
            Directory.CreateDirectory(modificationsDir);
            target.TransferFile("ModFramework.Modules.ClearScript.dll", Path.Combine(modificationsDir, "ModFramework.Modules.ClearScript.dll"));
        }

        public static void InstallLua(this IInstallTarget target, string otapiInstallPath)
        {
            var modificationsDir = Path.Combine(otapiInstallPath, "modifications");
            Directory.CreateDirectory(modificationsDir);
            target.TransferFile("ModFramework.Modules.Lua.dll", Path.Combine(modificationsDir, "ModFramework.Modules.Lua.dll"));
        }

        public static void CopyInstallFiles(this IInstallTarget target, string otapiInstallPath)
        {
            target.CopyFiles("install", otapiInstallPath);
        }

        public static void InstallLibs(this IInstallTarget target, string installPath)
        {
            var zipPath = target.DownloadZip("http://fna.flibitijibibo.com/archive/fnalibs.tar.bz2");
            target.ExtractBZip2(zipPath, installPath);
        }

        public static void InstallSteamworks64(this IInstallTarget target, string installPath, string steam_appid_folder)
        {
            var zipPath = target.DownloadZip("https://github.com/rlabrecque/Steamworks.NET/releases/download/15.0.1/Steamworks.NET-Standalone_15.0.1.zip");
            var folderName = Path.GetFileNameWithoutExtension(zipPath);
            if (Directory.Exists(folderName)) Directory.Delete(folderName, true);
            ZipFile.ExtractToDirectory(zipPath, folderName);

            var osx_lin = Path.Combine(folderName, "OSX-Linux-x64");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                target.CopyFiles(Path.Combine(folderName, "Windows-x64"), installPath);
            else
                target.CopyFiles(osx_lin, installPath);

            // ensure to use terrarias steam appid
            target.TransferFile(Path.Combine(steam_appid_folder, "steam_appid.txt"), Path.Combine(installPath, "steam_appid.txt"));

            target.TransferFile(Path.Combine(osx_lin, "libsteam_api.so"), Path.Combine(installPath, "lib64", "libsteam_api.so"));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                target.TransferFile(Path.Combine(installPath, "steam_api.bundle", "Contents", "MacOS", "libsteam_api.dylib"), Path.Combine(installPath, "osx", "libsteam_api.dylib"));
            }
        }

        public static void PatchOSXLaunch(this IInstallTarget target, string installPath)
        {
            var launch_script = Path.Combine(installPath, "MacOS/Terraria");
            var backup_launch_script = Path.Combine(installPath, "MacOS/Terraria.bak.otapi");
            var otapi_launcher = Path.Combine(installPath, "otapi_launcher");

            if (!File.Exists(backup_launch_script))
            {
                File.Copy(launch_script, backup_launch_script);
            }
            File.WriteAllText(launch_script, @"
#!/bin/bash
# MonoKickstart Shell Script
# Written by Ethan ""flibitijibibo"" Lee

cd ""`dirname ""$0""`""

UNAME=`uname`
ARCH=`uname -m`

if [ ""$UNAME"" == ""Darwin"" ]; then
	./fixDylibs.sh
	export DYLD_LIBRARY_PATH=$DYLD_LIBRARY_PATH:./osx/
	if [ ""$STEAM_DYLD_INSERT_LIBRARIES"" != """" ] && [ ""$DYLD_INSERT_LIBRARIES"" == """" ]; then
		export DYLD_INSERT_LIBRARIES=""$STEAM_DYLD_INSERT_LIBRARIES""
	fi

	echo ""Starting launcher""
	cd ../otapi_launcher
	
	./Terraria
	
	status=$?

	if [ $status -eq 210 ]; then
		echo ""Launch vanilla""

		cd ../MacOS
		./Terraria.bin.osx $@
	elif [ $status -eq 200 ]; then
		echo ""Launching OTAPI""
		cd ../otapi
		./Terraria $@
	else
		echo ""Exiting""
	fi
fi
");

            // publish and copy OTAPI.Client.Launcher
            {
                Console.WriteLine("Publishing and creating launcher...this will take a while.");
                var output = target.PublishHostLauncher();
                var launcher = Path.Combine(output, "Terraria");
                //var otapi = Path.Combine(installPath, "OTAPI.Client.Launcher");

                if (!File.Exists(launcher))
                    throw new Exception($"Failed to produce launcher to: {launcher}");

                Directory.CreateDirectory(otapi_launcher);
                target.CopyFiles(output, otapi_launcher);
            }
        }

        public static void PatchWindowsLaunch(this IInstallTarget target, string installPath)
        {
            var launch_file = Path.Combine(installPath, "Terraria.exe");

            // backup Terraria.exe to Terraria.orig.exe
            {
                var backup_launch_file = Path.Combine(installPath, "Terraria.orig.exe");

                if (!File.Exists(backup_launch_file))
                {
                    File.Copy(launch_file, backup_launch_file);
                }
            }

            // publish and copy OTAPI.Client.Launcher
            {
                var output = target.PublishHostLauncher();
                var launcher = Path.Combine(output, "Terraria.exe");

                if (!File.Exists(launcher))
                    throw new Exception($"Failed to produce launcher to: {launcher}");

                if (File.Exists(launch_file))
                    File.Delete(launch_file);

                File.Copy(Path.Combine(output, "Terraria.exe"), launch_file);
            }
        }

        public static void PatchLinuxLaunch(this IInstallTarget target, string installPath)
        {
            var otapi_launcher = Path.Combine(installPath, "otapi_launcher");
            var launch_script = Path.Combine(installPath, "Terraria");
            var backup_launch_script = Path.Combine(installPath, "Terraria.bak.otapi");

            if (!File.Exists(backup_launch_script))
            {
                File.Copy(launch_script, backup_launch_script);
            }
            File.WriteAllText(launch_script, @"
#!/bin/bash
# MonoKickstart Shell Script
# Written by Ethan ""flibitijibibo"" Lee

# Move to script's directory
cd ""`dirname ""$0""`""

# Get the system architecture
UNAME=`uname`
ARCH=`uname -m`
BASENAME=`basename ""$0""`

# MonoKickstart picks the right libfolder, so just execute the right binary.
if [ ""$UNAME"" == ""Darwin"" ]; then
	ext=osx
else
	ext=x86_64
fi

export MONO_IOMAP=all

echo ""Starting launcher""
cd otapi_launcher

./Terraria

status=$?

if [ $status -eq 210 ]; then
    echo ""Launch vanilla""

    cd ../
    ./${BASENAME}.bin.${ext} $@
elif [ $status -eq 200 ]; then
    echo ""Launching OTAPI""
    cd ../otapi
    ./Terraria $@
else
    echo ""Exiting""
fi
");

            // publish and copy OTAPI.Client.Launcher
            {
                Console.WriteLine("Publishing and creating launcher...this will take a while.");
                var output = target.PublishHostLauncher();
                var launcher = Path.Combine(output, "Terraria");
                //var otapi = Path.Combine(installPath, "OTAPI.Client.Launcher");

                if (!File.Exists(launcher))
                    throw new Exception($"Failed to produce launcher to: {launcher}");

                Directory.CreateDirectory(otapi_launcher);
                target.CopyFiles(output, otapi_launcher);
            }
        }
    }
}
