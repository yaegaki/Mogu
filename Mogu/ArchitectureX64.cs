using System;

namespace Mogu
{
    internal class ArchitectureX64 : ArchitectureBase
    {
        public override bool IsX64 => true;

        public override byte[] GetRelativeJump(IntPtr from, int offset)
        {
            var bin = new byte[5];
            bin[0] = 0xe9;
            BitConverter.GetBytes(offset).CopyTo(bin, 1);
            return bin;
        }

        public override byte[] GetAbsoluteJump(IntPtr from, IntPtr to)
        {
            var bin = new byte[14] { 0xff, 0x25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            BitConverter.GetBytes((ulong)to).CopyTo(bin, 6);
            return bin;
        }
    }
}
