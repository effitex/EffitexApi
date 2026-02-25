using System.CommandLine;
using System.CommandLine.IO;

namespace EffiTex.Cli.Tests;

internal class TestOutputConsole : IConsole
{
    private readonly StringWriter _outWriter = new();
    private readonly StringWriter _errorWriter = new();

    public IStandardStreamWriter Out { get; }
    public IStandardStreamWriter Error { get; }
    public bool IsOutputRedirected => false;
    public bool IsErrorRedirected => false;
    public bool IsInputRedirected => false;

    public string OutText => _outWriter.ToString();
    public string ErrorText => _errorWriter.ToString();

    public TestOutputConsole()
    {
        Out = StandardStreamWriter.Create(_outWriter);
        Error = StandardStreamWriter.Create(_errorWriter);
    }
}
