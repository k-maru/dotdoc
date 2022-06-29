﻿using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotDoc.Core;

public class DotDocEngine
{
    private readonly ILogger logger;

    public DotDocEngine(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<DocItem>> ExecuteAsync(DotDocEngineOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        if (!ValidateOptions(options)) return Enumerable.Empty<DocItem>();
        
        MSBuildLocator.RegisterDefaults();
        var workspace = MSBuildWorkspace.Create();
        if (options.InputFileName!.EndsWith(Constants.SolutionFileExtension))
        {
            return await ReadSolutionFile(workspace, options);
        }
        else
        {
            var result = await ReadProjectFile(workspace, options);
            return result is null ? Enumerable.Empty<DocItem>() : new List<DocItem>() { result };
        }
    }

    private async Task<IEnumerable<DocItem>> ReadSolutionFile(MSBuildWorkspace workspace, DotDocEngineOptions options)
    {
        var solution = await workspace.OpenSolutionAsync(options.InputFileName!);
        var results = new List<DocItem>();
        foreach (var proj in solution.Projects)
        {
            var result = await ReadProject(proj, options);
            if(result is not null)
            {
                results.Add(result);
            }
        }
        return results;
    }
    
    private async Task<DocItem?> ReadProjectFile(MSBuildWorkspace workspace, DotDocEngineOptions options)
    {
        var proj = await workspace.OpenProjectAsync(options.InputFileName!);
        return await ReadProject(proj, options);
    }

    private async Task<DocItem?> ReadProject(Project proj, DotDocEngineOptions options)
    {
        var compilation = await proj.GetCompilationAsync();
        if (compilation is null) return null;

        return compilation.Assembly.Accept(new ProjectSymbolsVisitor(new DefaultFilter(options.ExcludeIdPatterns)));
    }
    

    private bool ValidateOptions(DotDocEngineOptions optins)
    {
        if (string.IsNullOrWhiteSpace(optins.InputFileName))
        {
            logger.Error($"入力パスが指定されていません。 {optins.InputFileName}");
            return false;
        }
        var file = new FileInfo(optins.InputFileName);
        if (!file.Exists)
        {
            logger.Error($"入力するファイルが見つかりません。 {optins.InputFileName}");
            return false;
        }
        if (file.Extension != Constants.SolutionFileExtension && file.Extension != Constants.CsProjectFileExtension)
        {
            logger.Error($"入力するファイルはソリューションファイル({Constants.SolutionFileExtension})" +
                         $"もしくはC#プロジェクトファイル({Constants.CsProjectFileExtension})を指定してください。 {optins.InputFileName}");
            return false;
        }

        return true;
    }
}

