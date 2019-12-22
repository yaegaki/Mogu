using System;
using System.Runtime.InteropServices;

namespace Mogu
{
    public interface IHook<TDelegate> : IDisposable
        where TDelegate : notnull
    {
        /// <summary>
        /// Is original function call thread safe?
        /// </summary>
        /// <value></value>
        bool ThreadSafeOriginal { get; }

        void WithOriginal(Action<TDelegate> action);
        T WithOriginal<T>(Func<TDelegate, T> func);
    }

    public class ThreadSafeHook<TDelegate> : IHook<TDelegate>, IDisposable
        where TDelegate : notnull
    {
        public bool ThreadSafeOriginal => true;

        private IntPtr process;
        private IntPtr originalPtr;
        private TDelegate original;
        private byte[] originalHead;
        private IntPtr patchHeap;
        private uint heapSize;
        private uint heapOldProtect;

        public ThreadSafeHook(IntPtr process, IntPtr originalPtr, TDelegate original, byte[] originalHead, IntPtr patchHeap, uint heapSize, uint oldProtect)
        {
            this.process = process;
            this.originalPtr = originalPtr;
            this.original = original;
            this.originalHead = originalHead;
            this.patchHeap = patchHeap;
            this.heapSize = heapSize;
            this.heapOldProtect = oldProtect;
        }

        public void WithOriginal(Action<TDelegate> action)
            => action(this.original);

        public T WithOriginal<T>(Func<TDelegate, T> func)
            => func(this.original);

        private void Write(byte[] data)
        {
            using var protect = new MemoryProtect(this.process, this.originalPtr, this.originalHead.Length, NativeFunctions.Consts.PAGE_READWRITE);
            Marshal.Copy(data, 0, this.originalPtr, data.Length);
        }

        public void Dispose()
        {
            Write(this.originalHead);
            NativeFunctions.VirtualProtectEx(this.process, this.patchHeap, (UIntPtr)this.heapSize, this.heapOldProtect, out _);
            Marshal.FreeHGlobal(this.patchHeap);
        }
    }

    public class ThreadUnsafeHook<TDelegate> : IHook<TDelegate>, IDisposable
        where TDelegate : notnull
    {
        public bool ThreadSafeOriginal => false;

        private IntPtr process;
        private IntPtr originalPtr;
        private TDelegate original;
        private byte[] originalHead;
        private byte[] jumpAsm;

        public ThreadUnsafeHook(IntPtr process, IntPtr originalPtr, TDelegate original, byte[] originalHead, byte[] jumpAsm)
        {
            this.process = process;
            this.originalPtr = originalPtr;
            this.original = original;
            this.originalHead = originalHead;
            this.jumpAsm = jumpAsm;
        }

        public void WithOriginal(Action<TDelegate> action)
        {
            try
            {
                Write(this.originalHead);
                action(this.original);
            }
            finally
            {
                Write(this.jumpAsm);
            }
        }

        public T WithOriginal<T>(Func<TDelegate, T> func)
        {
            try
            {
                Write(this.originalHead);
                return func(this.original);
            }
            finally
            {
                Write(this.jumpAsm);
            }
        }

        private void Write(byte[] data)
        {
            using var protect = new MemoryProtect(this.process, this.originalPtr, this.originalHead.Length, NativeFunctions.Consts.PAGE_READWRITE);
            Marshal.Copy(data, 0, this.originalPtr, data.Length);
        }

        public void Dispose()
            => Write(this.jumpAsm);
    }
}
