namespace DiscordMultiBot.PollService.Data.Dto;

public class ResultDto
{
    protected ResultDto(bool isOk, string error)
    {
        IsOK = isOk;
        Error = error;
    }
    
    public static ResultDto CreateOK()
    {
        return new ResultDto(true, "");
    }

    public static ResultDto<T> CreateOK<T>(T result)
    {
        return new ResultDto<T>(result, true, "");
    }
    
    public static ResultDto CreateError(string error)
    {
        return new ResultDto(false, error);
    }
    
    public static ResultDto<T> CreateError<T>(string error)
    {
        return new ResultDto<T>(default, false, error);
    }
   
    
    public bool IsOK { get; }
    public string Error { get; }
}

public sealed class ResultDto<T> : ResultDto
{
    internal ResultDto(T? result, bool isOk, string error)
        : base(isOk, error)
    {
        Result = result;
    }
    
    public T? Result { get; }
}