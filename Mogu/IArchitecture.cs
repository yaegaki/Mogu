using System;

namespace Mogu
{
    internal interface IArchitecture
    {
        bool IsX64 { get; }
        byte[] GetRelativeJump(IntPtr from, int offset);
        byte[] GetAbsoluteJump(IntPtr from, IntPtr to);
    }
}
