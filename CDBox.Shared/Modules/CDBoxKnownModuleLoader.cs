using System;
using System.IO;
using System.Reflection;
using CDBox.Shared.Services;

namespace CDBox.Shared.Modules
{
    public sealed class CDBoxModuleLoadResult
    {
        internal CDBoxModuleLoadResult(
            ICDBoxWorkspaceModule module,
            string assemblyPath,
            Exception error)
        {
            Module = module;
            AssemblyPath = assemblyPath ?? string.Empty;
            Error = error;
        }

        public bool Success { get { return Module != null && Error == null; } }
        public ICDBoxWorkspaceModule Module { get; private set; }
        public string AssemblyPath { get; private set; }
        public Exception Error { get; private set; }
    }

    /// <summary>
    /// 只加载宿主明确指定的模块文件和类型，不扫描目录或第三方插件。
    /// </summary>
    public static class CDBoxKnownModuleLoader
    {
        public static CDBoxModuleLoadResult LoadAndInitialize(
            string assemblyPath,
            string moduleTypeName,
            ICDBoxServices services)
        {
            string resolvedPath = string.IsNullOrWhiteSpace(assemblyPath)
                ? string.Empty
                : Path.GetFullPath(assemblyPath);
            ICDBoxWorkspaceModule module = null;

            try
            {
                if (services == null)
                    throw new ArgumentNullException("services");
                if (string.IsNullOrWhiteSpace(resolvedPath)
                    || !File.Exists(resolvedPath))
                    throw new FileNotFoundException(
                        "未找到 CDBox 业务模块。", resolvedPath);
                if (string.IsNullOrWhiteSpace(moduleTypeName))
                    throw new ArgumentException(
                        "模块类型名称不能为空。", "moduleTypeName");

                Assembly assembly = Assembly.LoadFrom(resolvedPath);
                Type moduleType = assembly.GetType(
                    moduleTypeName.Trim(), true, false);
                module = Activator.CreateInstance(moduleType)
                    as ICDBoxWorkspaceModule;
                if (module == null)
                    throw new InvalidCastException(
                        "指定类型没有实现 ICDBoxWorkspaceModule："
                        + moduleType.FullName);

                module.Initialize(services);
                return new CDBoxModuleLoadResult(
                    module, resolvedPath, null);
            }
            catch (Exception ex)
            {
                if (module != null)
                {
                    try { module.Shutdown(); }
                    catch { }
                }
                return new CDBoxModuleLoadResult(
                    null, resolvedPath, ex);
            }
        }
    }
}
