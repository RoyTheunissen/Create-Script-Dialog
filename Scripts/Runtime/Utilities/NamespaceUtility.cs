using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyTheunissen.CreateScriptDialog.Utilities
{
    public static class NamespaceUtility
    {
        private const string ProjectRootPath = @"Assets/";
        private const string ScriptsFolderPath = @"Scripts";
        private const string EditorFolderName = "Editor";

        private const char SubNamespaceSymbol = '.';

        private const string ImplementationSubnamespace = "Implementation";

        private const string CompanyPrefix = "Paladin";

        /// <summary>
        /// The maximum namespace depth according to coding guidelines.
        /// Currently we've settled upon 3 so you can have Paladin.System.Audio
        /// and we don't differentiate between code that's any more specific than that.
        /// </summary>
        private const int DefaultMaxNameSpaceDepth = 4;


        public static string GetNamespaceForPath(string path, bool includePrefix)
        {
            path = path.Replace(PathUtility.FolderSymbol, PathUtility.AlternateFolderSymbol);
            
            // Strip it up until the project root.
            string result = PathUtility.RemovePathUpUntil(path, ProjectRootPath);

            int firstSubfolderIndex = result.IndexOf(PathUtility.AlternateFolderSymbol) + 1;

            // Strip it up until a scripts folder, leave the first folder (project name).
            // Also try to include the folder symbol, but if it was created in the scripts folder itself,
            // don't require it to be followed by a folder symbol.
            if (result.Contains(ScriptsFolderPath + PathUtility.AlternateFolderSymbol))
            {
                result = PathUtility.RemovePathUpUntil(
                    result, ScriptsFolderPath + PathUtility.AlternateFolderSymbol, firstSubfolderIndex);
            }
            else
            {
                result = PathUtility.RemovePathUpUntil(
                    result, ScriptsFolderPath, Mathf.Max(0, firstSubfolderIndex - 1));
            }

            // Remove the file name and extension from the path.
            result = PathUtility.RemoveFileNameFromPath(result);

            // Convert the folder path to a valid namespace.
            result = ConvertFolderPathToSubNamespaces(result);

            // Add the company prefix.
            if (includePrefix)
                result = AddNamespaceBefore(result, CompanyPrefix);

            // Figure out how deep namespaces should go. If the company prefix is omitted,
            // the first section is already a subnamespace.
            int depth = DefaultMaxNameSpaceDepth;
            if (!includePrefix)
                depth--;

            // Clamp the namespace to the specified depth.
            result = ClampNamespaceDepth(result, depth);
            
            // Remove "Editor" at the end if it's there. It would conflict with Unity's Editor class.
            List<string> sections = new List<string>(result.Split(SubNamespaceSymbol));
            if (sections.Count > 0 && sections[sections.Count - 1] == EditorFolderName)
                sections.RemoveAt(sections.Count - 1);
            result = string.Join(SubNamespaceSymbol, sections);

            return result;
        }

        /// <summary>
        /// Clamps the number of subnamespaces. 'System.ConfigLoader.Benchmark'
        /// has a depth of 3 namespaces. Clamping it to 2 would yield 'System.ConfigLoader'.
        /// Clamping to 1 would yield 'System' and clamping to 0 would yield ''.
        /// </summary>
        /// <param name="nameSpace">The namespace to clamp.</param>
        /// <param name="depth">The desired namespace depth.</param>
        /// <returns>The namespace clamped to the specified depth.</returns>
        public static string ClampNamespaceDepth(string nameSpace, int depth)
        {
            // Split the namespace up into sub namespaces.
            string[] subNameSpaces = nameSpace.Split(SubNamespaceSymbol);

            // A depth of 0 yields an empty namespace.
            if (depth == 0)
                return "";

            // If there's no sub namespaces just return the original name.
            if (subNameSpaces.Length == 0)
                return nameSpace;

            // Only take up until the specified depth.
            string result = subNameSpaces[0];
            for (int i = 1, I = Math.Min(depth, subNameSpaces.Length); i < I; i++)
            {
                // Add a separator.
                result += SubNamespaceSymbol;

                // Add the sub namespace.
                result += subNameSpaces[i];
            }

            return result;
        }

        public static string GetNamespaceForPath(string path)
        {
            return GetNamespaceForPath(path, true);
        }

        public static string AddNamespaceBefore(string nameSpace, string subNameSpace)
        {
            return subNameSpace + SubNamespaceSymbol + nameSpace;
        }

        public static string AddNamespaceAfter(string nameSpace, string subNameSpace)
        {
            return nameSpace + SubNamespaceSymbol + subNameSpace;
        }

        public static string ConvertFolderPathToSubNamespaces(string path)
        {
            // ROY: Some projects use square brackets to make the main project folder
            //      be alphabetically sorted as the first folder so ignore those symbols.
            path = path.Replace("[", "");
            path = path.Replace("]", "");

            // Remove the folder symbol and the alternate folder symbol because
            // Unity's own code seems to use it inconsistently.
            path = path.Replace(PathUtility.FolderSymbol, SubNamespaceSymbol);
            path = path.Replace(PathUtility.AlternateFolderSymbol, SubNamespaceSymbol);

            // Remove separators too!
            path = path.Replace(" ", "");
            path = path.Replace("-", "");
            path = path.Replace("_", "");
            
            return path;
        }
    }
}
