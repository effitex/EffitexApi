namespace EffiTex.Core.Validation;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; } = new();
}

public class ValidationError
{
    public string Field { get; set; }
    public string Message { get; set; }
}
