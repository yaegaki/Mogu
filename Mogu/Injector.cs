using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mogu
{
    public partial class Injector
    {
        public InjectorOption Option { get; set; } = InjectorOption.Default;

        public ValueTask<Connection> InjectAsync(uint pid, Expression<Action<Connection>> entryPoint)
            => InjectAsync(pid, entryPoint, CancellationToken.None);

        public ValueTask<Connection> InjectAsync(uint pid, Expression<Action<Connection>> entryPoint, CancellationToken cancellationToken)
            => InjectAsyncInternal(pid, entryPoint, cancellationToken);

        public ValueTask<Connection> InjectAsync(uint pid, Expression<Func<Connection, Task>> entryPoint)
            => InjectAsync(pid, entryPoint, CancellationToken.None);

        public ValueTask<Connection> InjectAsync(uint pid, Expression<Func<Connection, Task>> entryPoint, CancellationToken cancellationToken)
            => InjectAsyncInternal(pid, entryPoint, cancellationToken);

        public ValueTask<Connection> InjectAsync(uint pid, Expression<Func<Connection, ValueTask>> entryPoint)
            => InjectAsync(pid, entryPoint, CancellationToken.None);

        public ValueTask<Connection> InjectAsync(uint pid, Expression<Func<Connection, ValueTask>> entryPoint, CancellationToken cancellationToken)
            => InjectAsyncInternal(pid, entryPoint, cancellationToken);

        private ValueTask<Connection> InjectAsyncInternal(uint pid, Expression entryPoint, CancellationToken cancellationToken)
        {
            var methodInfo = entryPoint switch
            {
                LambdaExpression lambda => lambda.Body is MethodCallExpression m ? m.Method : null,
                _ => null,
            };

            if (methodInfo == null)
            {
                throw new ArgumentException("Invalid EntryPoint. EntryPoint must consist of Method call.");
            }

            if (!methodInfo.IsStatic)
            {
                throw new ArgumentException("Invalid EntryPoint. EntryPoint must be static");
            }

            return InjectAsync(pid, methodInfo, cancellationToken);
        }

        public ValueTask<Connection> InjectAsync(uint pid, MethodInfo entryPoint)
            => InjectAsync(pid, entryPoint, CancellationToken.None);

        public async ValueTask<Connection> InjectAsync(uint pid, MethodInfo entryPoint, CancellationToken cancellationToken)
        {
            var option = this.Option;

            var declaringType = entryPoint.DeclaringType;
            var assemblyLocation = declaringType.Assembly.Location;
            var typeName = declaringType.FullName;
            var methodName = entryPoint.Name;


            var process = NativeFunctions.OpenProcess(NativeFunctions.Consts.PROCESS_ALL_ACCESS, false, pid);
            if (process == IntPtr.Zero)
            {
                throw new MoguException($"Can not open process '{pid}'");
            }

            var loadLibraryW = ProcessUtil.GetProcAddressFromProcessMemory(process, "kernel32", "LoadLibraryW");
            if (loadLibraryW == IntPtr.Zero)
            {
                throw new MoguException($"Can not get 'LoadLibraryW' address");
            }

            var pipeName = $"MoguPipe-{Guid.NewGuid()}";
            var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
            try
            {
                var (nethostDllPath, moguhostDllPath) = GetNativeDllPath(process, assemblyLocation);

                // TODO: Get mutex for create MemoryMappedFile.
                var memorySize = 3 * 4 + (assemblyLocation.Length + typeName.Length + methodName.Length) * 2;
                using (var sharedMemory = MemoryMappedFile.CreateNew(GetMemoryMappedFileName(pid), memorySize))
                using (var accessor = sharedMemory.CreateViewAccessor())
                {
                    int position = 0;
                    accessor.Write(position, assemblyLocation, out position);
                    accessor.Write(position, typeName, out position);
                    accessor.Write(position, methodName, out position);
                    accessor.Write(position, pipeName, out position);


                    if (!(await InjectNativeDllAsync(process, nethostDllPath, loadLibraryW, false)))
                    {
                        throw new MoguException($"Can not inject nethost.dll to process({pid})");
                    }

                    var waitForConnectionTask = pipe.WaitForConnectionAsync(cancellationToken);

                    if (!(await InjectNativeDllAsync(process, moguhostDllPath, loadLibraryW, false)))
                    {
                        throw new MoguException($"Can not inject MoguHost.dll to process({pid})");
                    }

                    await Task.WhenAny(
                        waitForConnectionTask,
                        Task.Delay(option.InjectDllTimeOut)
                    );
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!waitForConnectionTask.IsCompleted)
                    {
                        throw new MoguException("TimeOut inject dll");
                    }
                }
            }
            catch
            {
                pipe.Dispose();
                throw;
            }
            finally
            {
                NativeFunctions.CloseHandle(process);
            }

            return new Connection(pipe);
        }

        private static (string nethostDllPath, string moguhostDllPath) GetNativeDllPath(IntPtr process, string assemblyLocation)
        {
            string target;
            if (NativeFunctions.IsWow64Process(process, out var isWow64) && !isWow64)
            {
                target = "x64";
            }
            else
            {
                target = "x86";
            }

            var dir = Path.GetDirectoryName(assemblyLocation);
            var nethostDllPath = Path.Combine(dir, $"nethost_{target}.dll");
            var moguhostDllPath = Path.Combine(dir, $"MoguHost_{target}.dll");

            if (!File.Exists(nethostDllPath))
            {
                throw new MoguException($"Not found '{nethostDllPath}'");
            }

            if (!File.Exists(moguhostDllPath))
            {
                throw new MoguException($"Not found '{moguhostDllPath}'");
            }

            return (nethostDllPath, moguhostDllPath);
        }

        public ValueTask<bool> InjectNativeDllAsync(IntPtr process, string dllPath, bool safe = false)
        {
            var loadLibraryW = ProcessUtil.GetProcAddressFromProcessMemory(process, "kernel32.dll", "LoadLibraryW");
            if (loadLibraryW == IntPtr.Zero)
            {
                throw new MoguException($"Can not get 'LoadLibraryW' address");
            }

            return InjectNativeDllAsync(process, dllPath, loadLibraryW, safe);
        }

        private ValueTask<bool> InjectNativeDllAsync(IntPtr process, string dllPath, IntPtr loadLibraryW, bool safe)
            => safe ? InjectNativeDllSafeAsync(process, dllPath, loadLibraryW) : new ValueTask<bool>(InjectNativeDllUnsafe(process, dllPath, loadLibraryW));

        private bool InjectNativeDllUnsafe(IntPtr process, string dllPath, IntPtr loadLibraryW)
        {
            var buffer = new byte[dllPath.Length * 2 + 1];
            var memory = NativeFunctions.VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)(buffer.Length), NativeFunctions.Consts.MEM_COMMIT, NativeFunctions.Consts.PAGE_READWRITE);
            if (memory == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                Encoding.Unicode.GetBytes(dllPath, 0, dllPath.Length, buffer, 0);
                UIntPtr protect;
                if (!NativeFunctions.WriteProcessMemory(process, memory, buffer, (UIntPtr)buffer.Length, out protect))
                {
                    return false;
                }

                IntPtr threadId;
                var thread = NativeFunctions.CreateRemoteThread(process, IntPtr.Zero, (UIntPtr)0, loadLibraryW, memory, 0, out threadId);
                if (thread == IntPtr.Zero)
                {
                    return false;
                }

                NativeFunctions.WaitForSingleObject(thread, NativeFunctions.Consts.INFINITE);
            }
            finally
            {
                NativeFunctions.VirtualFreeEx(process, memory, (UIntPtr)0, NativeFunctions.Consts.MEM_RELEASE);
            }

            return true;
        }

        // TODO: Impl.
        private ValueTask<bool> InjectNativeDllSafeAsync(IntPtr process, string dllPath, IntPtr loadLibraryW)
            => new ValueTask<bool>(false);

        private static string GetMemoryMappedFileName(uint pid)
            => $"MoguMemoryMappedFile-{pid}";
    }
}
