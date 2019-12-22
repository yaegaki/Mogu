using System.IO.MemoryMappedFiles;

namespace Mogu
{
    internal static class MemoryMappedFileExtensions
    {
        public static string ReadString(this MemoryMappedViewAccessor accessor, int position, out int nextPosition)
        {
            var len = accessor.ReadInt32(position);
            var array = new char[len];
            accessor.ReadArray<char>(position + 4, array, 0, array.Length);
            nextPosition = position + 4 + array.Length * 2;
            return new string(array, 0, array.Length);
        }

        public static void Write(this MemoryMappedViewAccessor accessor, int position, string value, out int nextPosition)
        {
            var array = value.ToCharArray();
            accessor.Write(position, array.Length);
            accessor.WriteArray(position + 4, array, 0, array.Length);
            nextPosition = position + 4 + array.Length * 2;
        }
    }
}