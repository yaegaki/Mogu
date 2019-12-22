using System;

namespace Mogu
{
    internal abstract class ArchitectureBase : IArchitecture
    {
        public abstract bool IsX64 { get; }
        public abstract byte[] GetRelativeJump(IntPtr from, int offset);
        public abstract byte[] GetAbsoluteJump(IntPtr from, IntPtr to);
    }
}