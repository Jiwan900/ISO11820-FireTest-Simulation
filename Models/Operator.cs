namespace ISO11820.Models;

/// <summary>
/// 操作员/用户
/// </summary>
public class Operator
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string UserType { get; set; } = "operator";
}
