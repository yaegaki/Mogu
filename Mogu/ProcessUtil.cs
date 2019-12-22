using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using dnlib.PE;

namespace Mogu
{
    public static class ProcessUtil
    {
        /// <summary>
        /// Get proc address from target process memory.
        /// This method can get address from 32bit and 64bit process if current process is 64bit.
        /// </summary>
        /// <param name="process"></param>
        /// <param name="dllName"></param>
        /// <param name="procName"></param>
        /// <returns></returns>
        public static IntPtr GetProcAddressFromProcessMemory(IntPtr process, string dllName, string procName)
        {
            uint needed;
            if (!NativeFunctions.EnumProcessModulesEx(process, null, 0, out needed, NativeFunctions.Consts.LIST_MODULES_ALL))
            {
                return IntPtr.Zero;
            }

            var buffer = new byte[needed];
            if (!NativeFunctions.EnumProcessModulesEx(process, buffer, (uint)buffer.Length, out needed, NativeFunctions.Consts.LIST_MODULES_ALL))
            {
                return IntPtr.Zero;
            }

            if (!dllName.Contains('.'))
            {
                dllName += ".dll";
            }

            var dllNameWithoutExtension = Path.GetFileNameWithoutExtension(dllName).ToLower();

            var nameBuffer = new byte[(dllName.Length + 1) * 2];
            for (var i = 0; i < buffer.Length; i += IntPtr.Size)
            {
                IntPtr module;
                if (IntPtr.Size == 4)
                {
                    module = new IntPtr(BitConverter.ToInt32(buffer, i));
                }
                else
                {
                    module = new IntPtr(BitConverter.ToInt64(buffer, i));
                }

                var len = NativeFunctions.GetModuleBaseName(process, module, nameBuffer, (uint)(nameBuffer.Length / 2));
                var moduleName = Encoding.Unicode.GetString(nameBuffer, 0, (int)(len * 2));
                if (dllNameWithoutExtension != Path.GetFileNameWithoutExtension(moduleName).ToLower())
                {
                    continue;
                }

                NativeStructs.MODULEINFO moduleInfo;
                if (!NativeFunctions.GetModuleInformation(process, module, out moduleInfo, (uint)Marshal.SizeOf<NativeStructs.MODULEINFO>()))
                {
                    return IntPtr.Zero;
                }

                using var pm = new ProcessMemoryMapper(process, moduleInfo.lpBaseOfDll, (int)moduleInfo.SizeOfImage);
                return GetProcAddressFromProcessMemory(pm, procName.ToLower());
            }

            return IntPtr.Zero;
        }

        private static IntPtr GetProcAddressFromProcessMemory(ProcessMemoryMapper processMemory, string procName)
        {
            var peBuffer = new byte[0x1000];
            if (!processMemory.Read(0, peBuffer, 0, peBuffer.Length))
            {
                return IntPtr.Zero;
            }

            try
            {
                var pe = new PEImage(peBuffer);

                var exportTableRVA = pe.ImageNTHeaders.OptionalHeader.DataDirectories[0].VirtualAddress;
                if (!processMemory.Read((int)(exportTableRVA + 20), peBuffer, 0, 20))
                {
                    return IntPtr.Zero;
                }

                var numberOfFunctions = BitConverter.ToInt32(peBuffer, 0);
                var numberOfNames = BitConverter.ToInt32(peBuffer, 4);
                var funcTableRVA = BitConverter.ToInt32(peBuffer, 8);
                var nameTableRVA = BitConverter.ToInt32(peBuffer, 12);
                var nameOrdinalTableRVA = BitConverter.ToInt32(peBuffer, 16);

                for (var i = 0; i < numberOfNames; i++)
                {
                    int nameRVA;
                    if (!processMemory.TryRead(nameTableRVA + i * 4, out nameRVA))
                    {
                        return IntPtr.Zero;
                    }

                    string name;
                    if (!processMemory.TryReadCStrA(nameRVA, out name))
                    {
                        return IntPtr.Zero;
                    }

                    if (name.ToLower() != procName)
                    {
                        continue;
                    }

                    short nameOrdinal;
                    if (!processMemory.TryRead(nameOrdinalTableRVA + i * 2, out nameOrdinal))
                    {
                        return IntPtr.Zero;
                    }

                    if (nameOrdinal < 0 || nameOrdinal >= numberOfFunctions)
                    {
                        return IntPtr.Zero;
                    }

                    int funcRVA;
                    if (!processMemory.TryRead(funcTableRVA + nameOrdinal * 4, out funcRVA))
                    {
                        return IntPtr.Zero;
                    }

                    return processMemory.BaseAddress + funcRVA;
                }
            }
            catch
            {
            }

            return IntPtr.Zero;
        }
    }
}
