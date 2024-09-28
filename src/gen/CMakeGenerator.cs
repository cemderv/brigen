using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace brigen.gen
{
    internal sealed class CMakeGenerator(Module module) : CodeGenerator(module)
    {
        private readonly Version _minRequiredCMakeVersion = new(3, 20);

        public override void Generate()
        {
            Paths paths = Module.Paths;
            string cmakeListsFilename = paths.CMakeListsFile;

            if (File.Exists(cmakeListsFilename))
            {
                return;
            }

            string cmakeSourceDir = Path.GetDirectoryName(cmakeListsFilename)!;
            Directory.CreateDirectory(cmakeSourceDir);

            var w = new Writer();

            w.WriteLine($"cmake_minimum_required(VERSION {_minRequiredCMakeVersion.Major}.{_minRequiredCMakeVersion.Minor})");
            w.WriteLine();

            w.WriteLine($"set(ProjectName {Module.Name})");
            w.WriteLine();

            w.WriteLine("project(");
            w.Indent();
            w.WriteLine("${ProjectName}");
            w.WriteLine($"VERSION {Module.Version.Major}.{Module.Version.Minor}");
            w.WriteLine("LANGUAGES CXX");
            w.Unindent();
            w.WriteLine(")");
            w.WriteLine();

            w.WriteLine("add_library(${ProjectName} SHARED)");
            w.WriteLine();

            w.WriteLine("target_include_directories(${ProjectName} PUBLIC include)");
            w.WriteLine();

            w.WriteLine("target_sources(${ProjectName} PRIVATE");
            w.Indent();

            foreach (var file in new[] {
                paths.CppSource,
                paths.CSource,
                paths.CppHelpersHeader,
                paths.CppImplSource,
                paths.CppImplHeader })
            {
                w.WriteLine(Path.GetRelativePath(cmakeSourceDir, file).CleanPath());
            }

            w.Unindent();
            w.WriteLine(')');
            w.WriteLine();

            w.SaveContentsToDisk(cmakeListsFilename);
        }
    }
}
