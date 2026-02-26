namespace EffiTex.Cli.Tests;

internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly StringWriter _outWriter = new();
    private readonly StringWriter _errorWriter = new();

    public string OutText => _outWriter.ToString();
    public string ErrorText => _errorWriter.ToString();

    public ConsoleCapture()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        Console.SetOut(_outWriter);
        Console.SetError(_errorWriter);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }
}
