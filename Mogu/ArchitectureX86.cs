using System;

namespace Mogu
{
    internal class ArchitectureX86 : ArchitectureBase
    {
        public override bool IsX64 => false;

        public override byte[] GetRelativeJump(IntPtr from, int offset)
        {
            var bin = new byte[5];
            bin[0] = 0xe9;
            BitConverter.GetBytes(offset).CopyTo(bin, 1);
            return bin;
        }

        public override byte[] GetAbsoluteJump(IntPtr from, IntPtr to)
        {
            var bin = new byte[10];
            bin[0] = 0xff;
            bin[1] = 0x25;
            BitConverter.GetBytes((uint)(from + 6)).CopyTo(bin, 2);
            BitConverter.GetBytes((uint)to).CopyTo(bin, 6);
            return bin;
        }
    }
}
