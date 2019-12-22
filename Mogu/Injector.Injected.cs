using System;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;

namespace Mogu
{
    public partial class Injector
    {
        /// <summary>
        /// EntryPoint for dotnet core hosting.
        /// This function is called by MoguHost.dll on target process.
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="len"></param>
        internal static int InjectedEntryPoint(IntPtr arg, int len)
        {
            var pid = NativeFunctions.GetCurrentProcessId();
            try
            {
                string assemblyLocation;
                string typeName;
                string methodName;
                string pipeName;

                using (var sharedMemory = MemoryMappedFile.OpenExisting(GetMemoryMappedFileName(pid)))
                using (var accessor = sharedMemory.CreateViewAccessor())
                {
                    var position = 0;
                    assemblyLocation = accessor.ReadString(position, out position);
                    typeName = accessor.ReadString(position, out position);
                    methodName = accessor.ReadString(position, out position);
                    pipeName = accessor.ReadString(position, out position);
                }

                var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

                try
                {
                    // timeout is 10sec
                    const int timeout = 10 * 1000;
                    pipe.Connect(timeout);

                    var assembly = Assembly.LoadFrom(assemblyLocation);

                    // var assembly = Assembly.LoadFrom(assemblyLocation);
                    if (assembly == null)
                    {
                        return -1;
                    }

                    var type = assembly.GetType(typeName);
                    if (type == null)
                    {
                        return -1;
                    }

                    var methodInfo = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (methodInfo == null)
                    {
                        return -1;
                    }

                    var parameters = methodInfo.GetParameters();
                    if (parameters.Length != 1)
                    {
                        return -1;
                    }

                    var connectionParameter = parameters[0];
                    var connectionType = connectionParameter.ParameterType;
                    if (connectionType.AssemblyQualifiedName != typeof(Connection).AssemblyQualifiedName)
                    {
                        return -1;
                    }

                    InvokeManagedEntryPoint(pipe, methodInfo);
                }
                catch (Exception)
                {
                    pipe.Dispose();
                    // TODO: log.
                }
            }
            catch
            {
                // Not target process if can not open MemoryMappedFile or NamedPipe.
            }

            return 0;
        }

        private static void InvokeManagedEntryPoint(NamedPipeClientStream pipe, MethodInfo entryPointMethod)
        {
            var connectionType = entryPointMethod.GetParameters()[0].ParameterType;
            if (connectionType.Assembly != typeof(Connection).Assembly)
            {
                // Avoid InvalidCastException.
                connectionType.Assembly
                    .GetType(typeof(Injector).FullName)
                    .GetMethod(nameof(InvokeManagedEntryPoint), BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { pipe, entryPointMethod });
                return;
            }

            var connection = new Connection(pipe);
            try
            {
                var result = entryPointMethod.Invoke(null, new object[] { connection });
                switch (result)
                {
                    case Task t:
                        t.Wait();
                        break;
                    case ValueTask t:
                        t.AsTask().Wait();
                        break;
                    default:
                        break;
                }
            }
            catch
            {
            }
            finally
            {
                if (connection.IsAutoClose)
                {
                    connection.Dispose();
                }
            }
        }
    }
}