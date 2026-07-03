namespace ISO11820.Models;

/// <summary>
/// 试验状态枚举
/// </summary>
public enum TestStatus
{
    Idle,        // 空闲
    Preparing,   // 升温中
    Ready,       // 就绪
    Recording,   // 记录中
    Complete     // 完成
}

/// <summary>
/// 用户角色
/// </summary>
public enum UserType
{
    Admin,
    Operator
}

/// <summary>
/// 试验时长模式
/// </summary>
public enum TestDurationMode
{
    Standard60Min,  // 标准60分钟
    Custom          // 自定义时长
}
