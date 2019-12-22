using System;

namespace Mogu
{
    public readonly struct MemoryProtect : IDisposable
    {
        private readonly IntPtr process;
        private readonly IntPtr address;
        private readonly UIntPtr size;
        private readonly uint oldProtect;
        private readonly bool success;

        public MemoryProtect(IntPtr process, IntPtr address, int size, uint protect)
        {
            this.process = process;
            this.address = address;
            this.size = (UIntPtr)size;

            this.success = NativeFunctions.VirtualProtectEx(process, address, this.size, protect, out this.oldProtect);
        }

        public void Dispose()
        {
            if (!this.success) return;

            NativeFunctions.VirtualProtectEx(this.process, this.address, this.size, this.oldProtect, out _);
            NativeFunctions.FlushInstructionCache(this.process, this.address, this.size);
        }
    }
}
