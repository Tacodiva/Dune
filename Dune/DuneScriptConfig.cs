
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Dune;

public sealed class DuneScriptConfig {

    public NullableContextOptions NullableContextOptions { get; set; } = NullableContextOptions.Enable;

    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Release;

    public bool CheckIntegerMath { get; set; } = false;

    public Type? ReturnType { get; set; } = null;

    public Type? GlobalsType { get; set; } = null;

    public List<string> ImplicitUsings { get; set; } = [];

    public List<string> PreprocessorSymbols { get; set; } = [];

    public bool GeneratePDB { get; set; } = true;

    public List<DuneAssembly> ReferencedAssemblies { get; set; } = [
        DuneAssembly.FromAssembly(typeof(object).Assembly),
        DuneAssembly.FromAssembly(typeof(Console).Assembly),
        DuneAssembly.FromAssembly(typeof(List<>).Assembly)
    ];

}