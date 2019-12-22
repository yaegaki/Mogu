using System;
using System.Runtime.InteropServices;
using Mogu.NativeStructs;

namespace Mogu
{
    public static class NativeFunctions
    {
        public static class Consts
        {
            public const uint STANDARD_RIGHT_REQUIRED = 0x000F0000;
            public const uint SYNCHRONIZE = 0x00100000;
            public const uint PROCESS_ALL_ACCESS = STANDARD_RIGHT_REQUIRED | SYNCHRONIZE | 0xFFFF;

            public const uint MEM_COMMIT   = 0x00001000;
            public const uint MEM_DECOMMIT = 0x00004000;
            public const uint MEM_RELEASE  = 0x00008000;

            public const uint PAGE_READWRITE         = 0x04;
            public const uint PAGE_EXECUTE           = 0x10;
            public const uint PAGE_EXECUTE_READWRITE = 0x40;

            public const uint INFINITE = 0xFFFFFFFF;

            public const uint LIST_MODULES_32BIT   = 0x01;
            public const uint LIST_MODULES_64BIT   = 0x02;
            public const uint LIST_MODULES_ALL     = LIST_MODULES_32BIT | LIST_MODULES_64BIT;
            public const uint LIST_MODULES_DEFAULT = 0x00;
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32", SetLastError = true)]
        public static extern uint GetCurrentProcessId();

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, out UIntPtr flProtect);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, UIntPtr dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32", EntryPoint = "K32EnumProcessModulesEx", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumProcessModulesEx(IntPtr hProcess, byte[]? lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        [DllImport("kernel32", CharSet = CharSet.Unicode, EntryPoint = "K32GetModuleBaseName", SetLastError = true)]
        public static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, byte[] lpBaseName, uint size);

        [DllImport("kernel32", CharSet = CharSet.Unicode, EntryPoint = "K32GetModuleFileNameEx", SetLastError = true)]
        public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, byte[] lpBaseName, uint size);

        [DllImport("kernel32", EntryPoint = "K32GetModuleInformation", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint size);

        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool IsWow64ProcessDelegate(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool Wow64Process);

        private static IsWow64ProcessDelegate isWow64Process = CreateIsWow64ProcessDelegate();

        private static IsWow64ProcessDelegate CreateIsWow64ProcessDelegate()
        {
            var isWow64ProcessPointer = NativeFunctions.GetProcAddress(GetModuleHandle("kernel32"), "IsWow64Process");

            if (isWow64ProcessPointer == null)
            {
                bool IsWow64ProcessOn32Process(IntPtr hProcess, out bool Wow64Process)
                {
                    Wow64Process = false;
                    return false;
                }

                return IsWow64ProcessOn32Process;
            }
            else
            {
                return Marshal.GetDelegateForFunctionPointer<IsWow64ProcessDelegate>(isWow64ProcessPointer);
            }
        }

        public static bool IsWow64Process(IntPtr hProcess, out bool Wow64Process)
            => isWow64Process(hProcess, out Wow64Process);
        
        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);



        [DllImport("kernel32")]
        public static extern int GetLastError();
    }
}