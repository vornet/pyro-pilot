namespace Firefly.Cli;

/// <summary>Thrown for bad/missing CLI arguments; caught in Main to print usage instead of a stack trace.</summary>
internal sealed class CliUsageException(string message) : Exception(message);
