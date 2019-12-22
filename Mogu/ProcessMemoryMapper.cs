using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Mogu
{
    public class ProcessMemoryMapper : IDisposable
    {
        public const int PageSize = 0x1000;
        private IntPtr process;
        private IntPtr baseAddress;
        private int baseAddressOffset;
        private bool[] pages;
        private IntPtr memory;
        private byte[] work = new byte[255];
        private StringBuilder sb = new StringBuilder();

        public IntPtr BaseAddress => baseAddress + baseAddressOffset;

        public ProcessMemoryMapper(IntPtr process, IntPtr baseAddress, int size)
        {
            this.process = process;

            var alignedBaseAddress = ((ulong)baseAddress / PageSize) * PageSize;
            if (alignedBaseAddress != (ulong)baseAddress)
            {
                this.baseAddress = (IntPtr)alignedBaseAddress;
                baseAddressOffset = (int)((ulong)baseAddressOffset - alignedBaseAddress);
                size += baseAddressOffset;
            }
            else
            {
                this.baseAddress = baseAddress;
            }

            var pageCount = size / PageSize;
            if (size % PageSize != 0)
            {
                pageCount += 1;
            }

            this.pages = new bool[pageCount];
            this.memory = Marshal.AllocHGlobal(size);
        }

        public bool Read(int rva, byte[] buffer, int offset, int count)
        {
            if (!Fetch(rva, count))
            {
                return false;
            }

            Marshal.Copy(this.memory + this.baseAddressOffset + rva, buffer, offset, count);
            return true;
        }

        public bool TryRead(int rva, out short value)
        {
            if (!Read(rva, this.work, 0, 2))
            {
                value = default;
                return false;
            }

            value = BitConverter.ToInt16(this.work);
            return true;
        }

        public bool TryRead(int rva, out int value)
        {
            if (!Read(rva, this.work, 0, 4))
            {
                value = default;
                return false;
            }

            value = BitConverter.ToInt32(this.work);
            return true;
        }

        public bool TryRead(int rva, out uint value)
        {
            if (!Read(rva, this.work, 0, 4))
            {
                value = default;
                return false;
            }

            value = BitConverter.ToUInt32(this.work);
            return true;
        }

        public bool TryReadCStrA(int rva, out string value)
        {
            this.sb.Clear();

            var address = this.baseAddressOffset + rva;
            var memorySize = this.pages.Length * PageSize;

            while (address < memorySize)
            {
                var read = this.work.Length;
                if ((address / PageSize) != ((address + this.work.Length) / PageSize))
                {
                    var nextPage = (address / PageSize) * PageSize + PageSize;
                    read = nextPage - address;
                }

                if (!Read(address - this.baseAddressOffset, this.work, 0, read))
                {
                    value = string.Empty;
                    return false;
                }

                for (var i = 0; i < read; i++)
                {
                    if (this.work[i] == 0)
                    {
                        value = this.sb.ToString();
                        this.sb.Clear();
                        return true;
                    }
                    
                    this.sb.Append((char)this.work[i]);
                }

                address += read;
            }

            value = string.Empty;
            return false;
        }

        private bool Fetch(int rva, int count)
        {
            var start = this.baseAddressOffset + rva;
            var end = start + (count - 1);
            var startPage = start / PageSize;
            var endPage = end / PageSize;
            if (endPage > this.pages.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (var i = startPage; i <= endPage; i++)
            {
                if (!this.pages[i])
                {
                    var pageOffset = i * PageSize;
                    UIntPtr read;
                    NativeFunctions.ReadProcessMemory(this.process, this.baseAddress + pageOffset , this.memory + pageOffset, (UIntPtr)PageSize, out read);
                    if ((int)read != PageSize)
                    {
                        return false;
                    }

                    this.pages[i] = true;
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (this.memory != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(this.memory);
                this.memory = IntPtr.Zero;
            }
        }
    }
}
