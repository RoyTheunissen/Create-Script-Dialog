using System.Collections.Generic;
using System.IO;
using RoyTheunissen.CreateScriptDialog.Utilities;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    /// <summary>
    /// Contains useful utilities for organizing asmdefs.
    /// </summary>
    public static class AsmDefUtilities 
    {
        private static List<Object> GetSelectedEditorFolders()
        {
            List<Object> results = new List<Object>();
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                if (Selection.objects[i].name == "Editor")
                    results.Add(Selection.objects[i]);
            }

            return results;
        }
        
        [MenuItem("Assets/Create/Asm Refs To Editor Folder", false, 93)]
        public static void AddAsmRefsToTopLevelEditorFolders()
        {
            List<Object> selectedEditorFolders = GetSelectedEditorFolders();
            foreach (Object selectedEditorFolder in selectedEditorFolders)
            {
                AddAsmRefToTopLevelEditorFolder(selectedEditorFolder);
            }
        }

        private static AssemblyDefinitionAsset FindParentEditorAsmDef(string path)
        {
            string currentFolder = path.GetParentDirectory();

            while (currentFolder.HasParentDirectory())
            {
                string parent = currentFolder.GetParentDirectory();
                
                if (currentFolder == parent)
                {
                    // This directory is like Harry Potter because it has no more parents left!
                    break;
                }

                string editorFolderNextToParent = parent + Path.AltDirectorySeparatorChar + "Editor";

                // It existed! Let's check that it has an asmdef.
                if (AssetDatabase.IsValidFolder(editorFolderNextToParent))
                {
                    // Try to find asmdef files in this folder. Note that there can only be one or zero.
                    string[] asmdefFileResults = AssetDatabase.FindAssets("t:asmdef", new[] {editorFolderNextToParent});

                    // Check if one existed.
                    if (asmdefFileResults.Length == 1)
                    {
                        string asmdefFilePath = AssetDatabase.GUIDToAssetPath(asmdefFileResults[0]);
                        
                        // It existed! This folder is a valid asm def to reference!
                        return AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefFilePath);
                    }
                }
                
                currentFolder = parent;
            }

            return null;
        }

        private static string GetAsmRefFileName(string path, string asmDefPath)
        {
            // If the asmdef is at "Assets/ProjectName/Scripts/Editor" then we'd like to get the filename relative to
            // "Assets/ProjectName/Scripts". Then we can filter out the "Scripts" folder, add it to the name of the asmDef
            // we reference, and then we get a file in the same naming convention as the asmdef we reference. 
            string asmDefDirectory = Path.GetDirectoryName(asmDefPath).ToUnityPath();
            string asmDefParentDirectory = asmDefDirectory.GetParentDirectory();

            path = Path.GetRelativePath(asmDefParentDirectory, path);

            // The name should basically just be the folder relative to the asmdef that's referenced. 
            const char separator = '.';
            string asmFileName = path.Replace(Path.DirectorySeparatorChar, separator)
                .Replace(Path.AltDirectorySeparatorChar, separator);

            // Sometimes people add special characters so it shows up at the top. We don't want that for our filename,
            // so strip those out. Hyphens and spaces don't look nice either.
            string[] specialChars = { "_", "[", "]", "-", " " };
            foreach (string specialChar in specialChars)
            {
                asmFileName = asmFileName.Replace(specialChar, "");
            }
            
            // Remove any script folders from the name.
            string[] scriptFolderNames = {"Scripts", "Runtime"};
            List<string> segments = new List<string>(asmFileName.Split(separator));
            while (segments.Count > 0 && segments[0].StartsWithAny(scriptFolderNames))
            {
                segments.RemoveAt(0);
            }
            asmFileName = string.Join(separator, segments);

            string fileNameBase = Path.GetFileNameWithoutExtension(asmDefPath).RemoveSuffix(separator + "Editor");
            string fileNameFinal = fileNameBase + separator + asmFileName;
            return fileNameFinal;
        }

        private static void CreateAsmRef(string path, AssemblyDefinitionAsset asmDef)
        {
            string asmDefPath = AssetDatabase.GetAssetPath(asmDef);
            string fileName = GetAsmRefFileName(path, asmDefPath);
            string filePath = path.GetAbsolutePath() + Path.AltDirectorySeparatorChar + fileName + ".asmref";
            string asmDefGuid = AssetDatabase.AssetPathToGUID(asmDefPath);

            string text = "{\n";
            text += $"\t\"reference\": \"GUID:{asmDefGuid}\"\n";
            text += "}";
            
            File.WriteAllText(filePath, text);
            AssetDatabase.ImportAsset(filePath.GetProjectPath());
        }

        private static void AddAsmRefToTopLevelEditorFolder(Object selectedEditorFolder)
        {
            string path = AssetDatabase.GetAssetPath(selectedEditorFolder);
            AssemblyDefinitionAsset editorAsmDefToReference = FindParentEditorAsmDef(path);
            
            if (editorAsmDefToReference == null)
            {
                Debug.LogWarning($"Can't create asmref for folder {path} because we can't find an editor folder asmdef.");
                return;
            }
            
            CreateAsmRef(path, editorAsmDefToReference);
        }
        
        [MenuItem("Assets/Create/Asm Refs To Editor Folder", true, 93)]
        public static bool AddAsmRefsToTopLevelEditorFolderValidate()
        {
            List<Object> selectedEditorFolders = GetSelectedEditorFolders();
            return selectedEditorFolders.Count > 0;
        }
    }
}
