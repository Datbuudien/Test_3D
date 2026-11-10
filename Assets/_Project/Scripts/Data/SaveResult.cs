public readonly struct SaveResult
{
    public SaveStatus Status { get; }
    public string FilePath { get; }
    public string ErrorMessage { get; }

    public SaveResult(SaveStatus status, string filePath, string errorMessage)
    {
        Status = status;
        FilePath = filePath;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => Status == SaveStatus.Success;
}
