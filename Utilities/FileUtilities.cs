using System;
using System.IO;

namespace Interstellar.Utilities
{
    public class FileUtilities
    {
        public static string GetGamePath() =>
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}
