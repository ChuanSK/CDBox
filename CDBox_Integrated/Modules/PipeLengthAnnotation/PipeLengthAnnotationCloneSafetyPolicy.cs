namespace TCPipeAutoDraw.Modules.PipeLengthAnnotation
{
    /// <summary>
    /// 深拷贝事件中的源标识生命周期策略。
    /// 跨图纸复制的源数据库可能在命令结束前被 AutoCAD 释放，
    /// 因而只能把目标标识带出事件回调。
    /// </summary>
    internal static class PipeLengthAnnotationCloneSafetyPolicy
    {
        internal static bool MayRetainSourceIdentity(bool isCrossDatabase)
        {
            return !isCrossDatabase;
        }

        internal static T StableMapKey<T>(T source, T destination,
            bool isCrossDatabase)
        {
            return isCrossDatabase ? destination : source;
        }
    }
}
