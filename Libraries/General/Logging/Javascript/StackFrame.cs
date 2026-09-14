using JetBrains.Annotations;

namespace DDS.General.Logging.Javascript;

[NoReorder]
internal sealed record StackFrame
{
	public required string OriginalFrame { get; init; }
	public int LineNumber { get; init; }
	public int ColumnNumber { get; init; }

	public string? FileName { get; init; }
	public string? FunctionName { get; init; }
	public string? Source { get; init; }

	/// <inheritdoc />
	public override string ToString()
	{
		if (string.IsNullOrEmpty(FunctionName) && string.IsNullOrEmpty(FileName))
		{
			return OriginalFrame;
		}

		//a()@file:///C:/Tools/CsCompiler/JsCoreLibrary/index.html:42:74
		return $"{FunctionName ?? "{anonymous}"}@{FileName ?? "{unknown location}"}:{LineNumber}:{ColumnNumber}";
	}
}
