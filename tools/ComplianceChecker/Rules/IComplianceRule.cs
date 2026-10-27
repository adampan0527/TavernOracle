using ComplianceChecker.Models;
using Microsoft.CodeAnalysis;

namespace ComplianceChecker.Rules;

/// <summary>
/// 源码级合规审查规则接口（基于 Roslyn AST 分析）
/// </summary>
public interface IComplianceRule
{
    string RuleId { get; }
    string Title { get; }
    string Description { get; }
    ComplianceSeverity Severity { get; }

    IEnumerable<ComplianceViolation> Analyze(SyntaxTree tree, SemanticModel? semanticModel, string filePath);
}

/// <summary>
/// 工程配置/包依赖级合规审查规则接口
/// </summary>
public interface IProjectComplianceRule
{
    string RuleId { get; }
    string Title { get; }
    string Description { get; }
    ComplianceSeverity Severity { get; }

    IEnumerable<ComplianceViolation> AnalyzeProject(string projectFilePath, string projectFileContent);
}
