using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDisasm;

namespace Mogu
{
    public class Hooker
    {
        private readonly IArchitecture arch;

        public Hooker()
        {
            if (IntPtr.Size == 8)
            {
                arch = new ArchitectureX64();
            }
            else
            {
                arch = new ArchitectureX86();
            }
        }

        public IHook<TDelegate> Hook<TDelegate>(string moduleName, string funcName, TDelegate hookFunc)
            where TDelegate : notnull
        {
            var module = NativeFunctions.GetModuleHandle(moduleName);
            if (module == IntPtr.Zero)
            {
                module = NativeFunctions.LoadLibrary(moduleName);
                if (module == IntPtr.Zero)
                {
                    throw new MoguException($"Not found module '{moduleName}'");
                }
            }

            var nativeFunc = NativeFunctions.GetProcAddress(module, funcName);
            if (nativeFunc == IntPtr.Zero)
            {
                throw new MoguException($"Not found function '{funcName}'");
            }

            return Hook(nativeFunc, hookFunc);
        }

        public IHook<TDelegate> Hook<TDelegate>(IntPtr nativeFuncPtr, TDelegate hookFunc)
            where TDelegate : notnull
        {
            try
            {
                var process = NativeFunctions.GetCurrentProcess();
                var hookFuncPtr = Marshal.GetFunctionPointerForDelegate(hookFunc);

                var jumpAsm = GetJumpAsm(nativeFuncPtr, hookFuncPtr);

                var readSize = jumpAsm.Length + 20;
                using var protect = new MemoryProtect(NativeFunctions.GetCurrentProcess(), nativeFuncPtr, readSize, NativeFunctions.Consts.PAGE_EXECUTE_READWRITE);

                var disassembler = new Disassembler(nativeFuncPtr, readSize, this.arch.IsX64 ? ArchitectureMode.x86_64 : ArchitectureMode.x86_32);
                var patchSize = GetPatchSize(disassembler.Disassemble(), jumpAsm.Length);

                var originalHead = new byte[jumpAsm.Length];
                Marshal.Copy(nativeFuncPtr, originalHead, 0, originalHead.Length);

                if (patchSize < 0)
                {
                    Marshal.Copy(jumpAsm, 0, nativeFuncPtr, jumpAsm.Length);
                    var nativeFunc = Marshal.GetDelegateForFunctionPointer<TDelegate>(nativeFuncPtr);
                    return new ThreadUnsafeHook<TDelegate>(process, nativeFuncPtr, nativeFunc, originalHead, jumpAsm);
                }
                else
                {
                    var patch = new byte[patchSize];
                    Marshal.Copy(nativeFuncPtr, patch, 0, patchSize);
                    var heapSize = patchSize + 20;
                    var heap = Marshal.AllocHGlobal(heapSize);
                    Marshal.Copy(patch, 0, heap, patchSize);
                    var patchToOriginalJumpAsm = GetJumpAsm(heap + patchSize, nativeFuncPtr + patchSize);
                    Marshal.Copy(patchToOriginalJumpAsm, 0, heap + patchSize, patchToOriginalJumpAsm.Length);
                    uint oldProtect;
                    var suc = NativeFunctions.VirtualProtectEx(process, heap, (UIntPtr)heapSize, NativeFunctions.Consts.PAGE_EXECUTE_READWRITE, out oldProtect);
                    NativeFunctions.FlushInstructionCache(process, heap, (UIntPtr)heapSize);
                    var nativeFunc = Marshal.GetDelegateForFunctionPointer<TDelegate>(heap);

                    Marshal.Copy(jumpAsm, 0, nativeFuncPtr, jumpAsm.Length);

                    return new ThreadSafeHook<TDelegate>(process, nativeFuncPtr, nativeFunc, originalHead, heap, (uint)heapSize, oldProtect);
                }
            }
            catch
            {
                throw new MoguException("Error occured while hooking");
            }
        }

        private byte[] GetJumpAsm(IntPtr fromPtr, IntPtr toPtr)
        {
            var from = (ulong)fromPtr;
            var to = (ulong)toPtr;
            const uint _32bitJumpSize = 5;
            var jumpBase = from + _32bitJumpSize;
            int sign;
            ulong offset;
            if (jumpBase < to)
            {
                sign = 1;
                offset = to - jumpBase;
            }
            else
            {
                sign = -1;
                offset = jumpBase - to;
            }
            var is64bitJump = offset >= 0x7f_ff_ff_ff;

            // TODO: attempt allocate memory near nativeFuncPtr if 64bit jump.

            return is64bitJump ? this.arch.GetAbsoluteJump(fromPtr, toPtr) : this.arch.GetRelativeJump(fromPtr, (int)offset * sign);
        }

        private int GetPatchSize(IEnumerable<Instruction> insns, int minimunSize)
        {
            var result = 0;
            foreach (var insn in insns)
            {
                var nemonic = (int)insn.Mnemonic;
                if (
                    // Call
                    nemonic == 36 ||
                    // Jump
                    (nemonic >= 244 && nemonic <= 263) ||
                    // Ret
                    (nemonic == 532)
                )
                {
                    return -1;
                }

                result += insn.Length;
                if (result >= minimunSize)
                {
                    return result;
                }
            }

            return -1;
        }
    }
}
