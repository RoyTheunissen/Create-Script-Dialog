using System;

namespace RoyTheunissen.CreateScriptDialog.Utilities
{
    public static class PathUtility
    {
        public const char FolderSymbol = '\\';
        public const char AlternateFolderSymbol = '/';
        public const char ExtensionSymbol = '.';

        public static string RemoveFileNameFromPath(string path)
        {
            int lastFolderIndex = path.LastIndexOf(FolderSymbol);
            int alternateLastFolderIndex = path.LastIndexOf(AlternateFolderSymbol);
            if (alternateLastFolderIndex > lastFolderIndex)
            {
                lastFolderIndex = alternateLastFolderIndex;
            }

            // If there are no subfolders in the path or the last section of the path
            // does not contain an extension, do not trim anything.
            if (lastFolderIndex == -1
                || !path.Substring(lastFolderIndex).Contains(ExtensionSymbol.ToString()))
            {
                return path;
            }
            else
            {
                return path.Substring(0, lastFolderIndex);
            }
        }

        /// <summary>
        /// Removes up until the specified subpath.
        /// </summary>
        /// <param name="path">The path to modify.</param>
        /// <param name="subPath">The subpath up until which we need to remove the path.</param>
        /// <returns>The unchanged string if the sub path didn't even occur,
        /// otherwise the path with everything up until the sub path removed.</returns>
        public static string RemovePathUpUntil(string path, string subPath, int startIndex = 0)
        {
            int subPathIndex = path.IndexOf(subPath, startIndex);

            // The specified path name did not contain the subpath.
            if (subPathIndex == -1)
            {
                return path;
            }

            // Remove everything up until the sub path.
            path = path.Substring(0, startIndex) + path.Substring(subPathIndex + subPath.Length);
            return path;
        }
    }
}
