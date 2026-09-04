namespace CDBox.Shared.Services
{
    /// <summary>
    /// 业务模块请求 AutoCAD 执行命令的宿主边界。
    /// </summary>
    public interface ICDBoxCadCommandService
    {
        void Execute(string commandName);
    }
}
