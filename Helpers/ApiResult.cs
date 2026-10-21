namespace EZ_Rent.Helpers;

public static class ApiResult
{
    public static object Ok(string message = "Thành công", object? data = null)
        => new { success = true, message, data };

    public static object Fail(string message)
        => new { success = false, message };
}