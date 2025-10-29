using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RoyTheunissen.CreateScriptDialog.Utilities;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

public class NewScriptWindow : EditorWindow
{
    private const float kSidePanelWidth = 480f;
    private const int kButtonWidth = 120;
    private const int kLabelWidth = 85;
    private const string kLanguageEditorPrefName = "NewScriptLanguage";
    private const string kNamespacePrefixPrefName = "NewScriptNamespacePrefix";
    private const string kTemplateFolderName = "SmartScriptTemplates";
    private const string kResourcesTemplatePath = "Resources/" + kTemplateFolderName;
    private const string kMonoBehaviourName = "MonoBehaviour";
    private const string kPlainClassName = "Class";
    private const string kDefaultCustomEditorForMonoBehaviours = "Custom Editor";
    private const string kDefaultCustomEditorForClasses = "PropertyDrawer";
    private static readonly string[] kCustomEditorClassNames = { "Editor", "PropertyDrawer" };
    private const string kTempEditorClassPrefix = "E:";
    private const string kNoTemplateString = "No Template Found";
    private const string kEditorFolderName = "Editor";
    // char array can't be const for compiler reasons but this should still be treated as such.
    private char[] kInvalidPathChars = new char[] { '<', '>', ':', '"', '|', '?', '*', (char)0 };
    private char[] kPathSepChars = new char[] { '/', '\\' };
    private string[] kScopes = new string[]
    {
        "private",
        "protected",
        "public",
    };

    private ScriptPrescription m_ScriptPrescription;
    private string m_BaseClass;
    private string m_CustomEditorTargetClassName = string.Empty;
    private bool m_IsEditorClass = false;
    private bool m_IsCustomEditor = false;
    private bool m_FocusTextFieldNow = true;
    private string m_Directory = string.Empty;
    private Vector2 m_PreviewScroll;
    private Vector2 m_OptionsScroll;
    private bool m_ClearKeyboardControl = false;

    private int m_TemplateIndex;
    private string[] m_TemplateNames;

    private class Styles
    {
        public GUIStyle m_PreviewBox = new GUIStyle("OL Box");
        public GUIStyle m_PreviewTitle = new GUIStyle("OL Title");
        public GUIStyle m_LoweredBox = new GUIStyle("TextField");
        public GUIStyle m_HelpBox = new GUIStyle("helpbox");
        public Styles()
        {
            m_LoweredBox.padding = new RectOffset(1, 1, 1, 1);
        }
    }
    private static Styles m_Styles;
    
    private bool isInitialOpen;
    
    [NonSerialized] private static string cachedTemplatePath;
    [NonSerialized] private static bool didCacheTemplatePath;
    private static string TemplatePath
    {
        get
        {
            if (!didCacheTemplatePath)
            {
                string[] asmDefs = AssetDatabase.FindAssets("t:asmdef CreateScriptDialog.Editor");
                if (asmDefs.Length != 1)
                    return null;

                didCacheTemplatePath = true;
                string asmDefPath = AssetDatabase.GUIDToAssetPath(asmDefs[0]);
                string parentPath = asmDefPath.GetParentDirectory();
                parentPath = parentPath.GetParentDirectory();
                parentPath = parentPath.GetParentDirectory();

                cachedTemplatePath = parentPath + "/" + kTemplateFolderName;
            }
            return cachedTemplatePath;
        }
    }

    private string GetAbsoluteBuiltinTemplatePath()
    {
        return Path.Combine(EditorApplication.applicationContentsPath, kResourcesTemplatePath);
    }

    public static string GetAbsoluteCustomTemplatePath()
    {
        string projectPath = Application.dataPath.GetParentDirectory();
        string relativeTemplatePath = TemplatePath;
        return Path.Combine(projectPath, relativeTemplatePath).ToUnityPath();
    }

    private void SetTemplate(string name)
    {
        m_TemplateIndex = m_TemplateNames.ToList().IndexOf(name);
        AutomaticHandlingOnChangeTemplate();
    }

    private void UpdateTemplateNamesAndTemplate()
    {
        // Remember old selected template name
        string oldSelectedTemplateName = null;
        if (m_TemplateNames != null && m_TemplateNames.Length > 0)
            oldSelectedTemplateName = m_TemplateNames[m_TemplateIndex];

        // Get new template names
        m_TemplateNames = GetTemplateNames();

        // Select template
        if (m_TemplateNames.Length == 0)
        {
            m_ScriptPrescription.m_Template = kNoTemplateString
                + " @\nbuilt-in path: " + GetAbsoluteBuiltinTemplatePath()
                + "\nor custom path: " + GetAbsoluteCustomTemplatePath();
            m_BaseClass = null;
        }
        else
        {
            if (oldSelectedTemplateName != null && m_TemplateNames.Contains(oldSelectedTemplateName))
                m_TemplateIndex = m_TemplateNames.ToList().IndexOf(oldSelectedTemplateName);
            else
                m_TemplateIndex = 0;
            m_ScriptPrescription.m_Template = GetTemplate(m_TemplateNames[m_TemplateIndex]);
        }

        HandleBaseClass();
    }

    public bool IsEditorClass()
    {
        return m_IsEditorClass;
    }

    private void AutomaticHandlingOnChangeTemplate()
    {
        UpdateTemplateNamesAndTemplate();
        
        // Add or remove "Editor" from directory path
        if (m_IsEditorClass)
        {
            if (InvalidTargetPathForEditorScript())
                m_Directory = Path.Combine(m_Directory, "Editor");
        }
        else if (m_Directory.EndsWith("Editor"))
        {
            m_Directory = m_Directory.Substring(0, m_Directory.Length - 6).TrimEnd(kPathSepChars);
        }

        // Move keyboard focus to relevant field
        if (m_IsCustomEditor)
            m_FocusTextFieldNow = true;
        
        SetClassNameBasedOnTargetClassName();
    }

    private string GetBaseClass(string templateContent)
    {
        string firstLine = templateContent.Substring(0, templateContent.IndexOf("\n"));
        if (firstLine.Contains("BASECLASS"))
        {
            string baseClass = firstLine.Substring(10).Trim();
            if (baseClass != string.Empty)
                return baseClass;
        }
        return null;
    }

    private bool GetFunctionIsIncluded(string baseClassName, string functionName, bool includeByDefault)
    {
        string prefName = "FunctionData_" + (baseClassName != null ? baseClassName + "_" : string.Empty) + functionName;
        return EditorPrefs.GetBool(prefName, includeByDefault);
    }

    private void SetFunctionIsIncluded(string baseClassName, string functionName, bool include)
    {
        string prefName = "FunctionData_" + (baseClassName != null ? baseClassName + "_" : string.Empty) + functionName;
        EditorPrefs.SetBool(prefName, include);
    }

    private void HandleBaseClass()
    {
        if (m_TemplateNames.Length == 0)
        {
            m_BaseClass = null;
            return;
        }

        // Get base class
        m_BaseClass = GetBaseClass(m_ScriptPrescription.m_Template);

        // If base class was found, strip first line from template
        if (m_BaseClass != null)
            m_ScriptPrescription.m_Template =
                m_ScriptPrescription.m_Template.Substring(m_ScriptPrescription.m_Template.IndexOf("\n") + 1);

        m_IsEditorClass = IsEditorClass(m_BaseClass);
        m_IsCustomEditor = IsCustomEditorClass(m_BaseClass);
        m_ScriptPrescription.m_StringReplacements.Clear();

        // Try to find function file first in custom templates folder and then in built-in
        string functionDataFilePath = Path.Combine(GetAbsoluteCustomTemplatePath(), m_BaseClass + ".functions.txt");
        if (!File.Exists(functionDataFilePath))
            functionDataFilePath = Path.Combine(GetAbsoluteBuiltinTemplatePath(), m_BaseClass + ".functions.txt");

        if (!File.Exists(functionDataFilePath))
        {
            m_ScriptPrescription.m_Functions = null;
        }
        else
        {
            StreamReader reader = new StreamReader(functionDataFilePath);
            List<FunctionData> functionList = new List<FunctionData>();
            int lineNr = 1;
            while (!reader.EndOfStream)
            {
                string functionLine = reader.ReadLine();
                string functionLineWhole = functionLine;
                try
                {
                    if (functionLine.Substring(0, 7).ToLower() == "header ")
                    {
                        functionList.Add(new FunctionData(functionLine.Substring(7)));
                        continue;
                    }

                    FunctionData function = new FunctionData();

                    bool defaultInclude = false;
                    if (functionLine.Substring(0, 8) == "DEFAULT ")
                    {
                        defaultInclude = true;
                        functionLine = functionLine.Substring(8);
                    }

                    // ROY: Functions have a scope now so process that first
                    for (int i = 0; i < kScopes.Length; i++)
                    {
                        if (!functionLine.StartsWith(kScopes[i] + " "))
                            continue;

                        functionLine = functionLine.Substring(kScopes[i].Length + 1);
                        function.scope = kScopes[i] + " ";
                        break;
                    }
                    if (function.scope == null)
                        function.scope = "";
                    
                    if (functionLine.StartsWith("static "))
                    {
                        function.isStatic = true;
                        functionLine = functionLine.Substring("static ".Length);
                    }

                    if (functionLine.StartsWith("override "))
                    {
                        function.isVirtual = true;
                        functionLine = functionLine.Substring("override ".Length);
                    }

                    string returnTypeString = GetStringUntilSeperator(ref functionLine, " ");
                    function.returnType = (returnTypeString == "void" ? null : returnTypeString);
                    function.name = GetStringUntilSeperator(ref functionLine, "(");
                    string parameterString = GetStringUntilSeperator(ref functionLine, ")");
                    if (function.returnType != null)
                        function.returnDefault = GetStringUntilSeperator(ref functionLine, ";");
                    function.comment = functionLine;

                    string[] parameterStrings = parameterString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    List<ParameterData> parameterList = new List<ParameterData>();
                    for (int i = 0; i < parameterStrings.Length; i++)
                    {
                        string[] paramSplit = parameterStrings[i].Trim().Split(' ');
                        parameterList.Add(new ParameterData(paramSplit[1], paramSplit[0]));
                    }
                    function.parameters = parameterList.ToArray();

                    function.include = GetFunctionIsIncluded(m_BaseClass, function.name, defaultInclude);

                    functionList.Add(function);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Malformed function line: \"" + functionLineWhole + "\"\n  at " + functionDataFilePath + ":" + lineNr + "\n" + e);
                }
                lineNr++;
            }
            m_ScriptPrescription.m_Functions = functionList.ToArray();

        }
    }

    private string GetStringUntilSeperator(ref string source, string sep)
    {
        int index = source.IndexOf(sep);
        string result = source.Substring(0, index).Trim();
        source = source.Substring(index + sep.Length).Trim(' ');
        return result;
    }

    private string GetTemplate(string nameWithoutExtension)
    {
        string path = Path.Combine(GetAbsoluteCustomTemplatePath(), nameWithoutExtension + "." + extension + ".txt");
        if (File.Exists(path))
            return File.ReadAllText(path);

        path = Path.Combine(GetAbsoluteBuiltinTemplatePath(), nameWithoutExtension + "." + extension + ".txt");
        if (File.Exists(path))
            return File.ReadAllText(path);

        return kNoTemplateString;
    }

    private string GetTemplateName()
    {
        if (m_TemplateNames.Length == 0)
            return kNoTemplateString;
        return m_TemplateNames[m_TemplateIndex];
    }

    // Custom comparer to sort templates alphabetically,
    // but put MonoBehaviour and Plain Class as the first two
    private class TemplateNameComparer : IComparer<string>
    {
        private int GetRank(string s)
        {
            if (s == kMonoBehaviourName)
                return 0;
            if (s == kPlainClassName)
                return 1;
            if (s.StartsWith(kTempEditorClassPrefix))
                return 100;
            return 2;
        }

        public int Compare(string x, string y)
        {
            int rankX = GetRank(x);
            int rankY = GetRank(y);
            if (rankX == rankY)
                return x.CompareTo(y);
            else
                return rankX.CompareTo(rankY);
        }
    }

    private string[] GetTemplateNames()
    {
        List<string> templates = new List<string>();

        // Get all file names of custom templates
        if (Directory.Exists(GetAbsoluteCustomTemplatePath()))
            templates.AddRange(Directory.GetFiles(GetAbsoluteCustomTemplatePath()));

        // Get all file names of built-in templates
        if (Directory.Exists(GetAbsoluteBuiltinTemplatePath()))
            templates.AddRange(Directory.GetFiles(GetAbsoluteBuiltinTemplatePath()));

        if (templates.Count == 0)
            return new string[0];

        // Filter and clean up list
        templates = templates
            .Distinct()
            .Where(f => (f.EndsWith("." + extension + ".txt")))
            .Select(f => Path.GetFileNameWithoutExtension(f.Substring(0, f.Length - 4)))
            .ToList();

        // Determine which scripts have editor class base class
        for (int i = 0; i < templates.Count; i++)
        {
            string templateContent = GetTemplate(templates[i]);
            if (IsEditorClass(GetBaseClass(templateContent)))
                templates[i] = kTempEditorClassPrefix + templates[i];
        }

        // Order list
        templates = templates
            .OrderBy(f => f, new TemplateNameComparer())
            .ToList();

        // Insert separator before first editor script template
        bool inserted = false;
        for (int i = 0; i < templates.Count; i++)
        {
            if (templates[i].StartsWith(kTempEditorClassPrefix))
            {
                templates[i] = templates[i].Substring(kTempEditorClassPrefix.Length);
                if (!inserted)
                {
                    templates.Insert(i, string.Empty);
                    inserted = true;
                }
            }
        }

        // Return list
        return templates.ToArray();
    }

    private string extension => "cs";

    private static readonly EditorPreferenceString namespacePrefixEditorPref = new(
        kNamespacePrefixPrefName, string.Empty);

    [MenuItem("Assets/Create/Script...", false, 50)]
    private static void OpenFromAssetsMenu()
    {
        Init();
    }

    private static void Init()
    {
        GetWindow<NewScriptWindow>(true, "Create Script");
    }

    public NewScriptWindow()
    {
        // Large initial size
        position = new Rect(50, 50, 770, 500);
        // But allow to scale down to smaller size
        minSize = new Vector2(550, 400);

        m_ScriptPrescription = new ScriptPrescription();
    }

    private void OnEnable()
    {
        UpdateTemplateNamesAndTemplate();

        isInitialOpen = true;
        OnSelectionChange();
        isInitialOpen = false;
    }

    private void OnGUI()
    {
        if (m_Styles == null)
            m_Styles = new Styles();

        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 85;

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && CanCreate())
            Create();

        EditorGUILayout.BeginHorizontal();
        {
            GUILayout.Space(10);

            PreviewGUI();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();
            {
                OptionsGUI();

                GUILayout.Space(10);
                //GUILayout.FlexibleSpace ();

                CreateAndCancelButtonsGUI();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Clear keyboard focus if clicking a random place inside the dialog,
        // or if ClearKeyboardControl flag is set.
        if (m_ClearKeyboardControl || (Event.current.type == EventType.MouseDown && Event.current.button == 0))
        {
            GUIUtility.keyboardControl = 0;
            m_ClearKeyboardControl = false;
            Repaint();
        }

        EditorGUIUtility.labelWidth = originalLabelWidth;
    }

    private bool CanCreate()
    {
        return m_ScriptPrescription.m_ClassName.Length > 0 &&
            !File.Exists(TargetPath()) &&
            !ClassNameIsInvalid() &&
            !InvalidTargetPath() &&
            !InvalidTargetPathForEditorScript();
    }

    private void Create()
    {
        CreateScript();

        Close();
        GUIUtility.ExitGUI();
    }

    private void CreateAndCancelButtonsGUI()
    {
        bool canCreate = CanCreate();

        // Create string to tell the user what the problem is
        string blockReason = string.Empty;
        if (!canCreate && m_ScriptPrescription.m_ClassName != string.Empty)
        {
            if (File.Exists(TargetPath()))
                blockReason = "A script called \"" + m_ScriptPrescription.m_ClassName + "\" already exists at that path.";
            else if (ClassNameIsInvalid())
                blockReason = "The script name may only consist of a-z, A-Z, 0-9, _.";
            else if (TargetClassIsNotValidType())
                blockReason = "The class \"" + m_CustomEditorTargetClassName + "\" is not of type UnityEngine.Object.";
            else if (InvalidTargetPath())
                blockReason = "The folder path contains invalid characters.";
            else if (InvalidTargetPathForEditorScript())
                blockReason = "Editor scripts should be stored in a folder called Editor.";
        }

        // Warning about why the script can't be created
        if (blockReason != string.Empty)
        {
            GUILayout.BeginHorizontal(m_Styles.m_HelpBox);
            {
                EditorGUILayout.HelpBox(blockReason, MessageType.Warning);
            }
            GUILayout.EndHorizontal();
        }

        // Cancel and create buttons
        GUILayout.BeginHorizontal();
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(kButtonWidth)))
            {
                Close();
                GUIUtility.ExitGUI();
            }

            bool guiEnabledTemp = GUI.enabled;
            GUI.enabled = canCreate;
            if (GUILayout.Button("Create", GUILayout.Width(kButtonWidth)))
            {
                Create();
            }
            GUI.enabled = guiEnabledTemp;
        }
        GUILayout.EndHorizontal();
    }

    private void OptionsGUI()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        {
            GUILayout.BeginHorizontal();
            {
                NameGUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            TargetPathGUI();

            GUILayout.Space(20);

            TemplateSelectionGUI();

            GUILayout.Space(10);

            NamespaceGUI();

            if (m_IsCustomEditor)
            {
                GUILayout.Space(10);
                CustomEditorTargetClassNameGUI();
            }

            GUILayout.Space(10);

            FunctionsGUI();
        }
        EditorGUILayout.EndVertical();
    }

    private bool FunctionHeader(string header, bool expandedByDefault)
    {
        GUILayout.Space(5);
        bool expanded = GetFunctionIsIncluded(m_BaseClass, header, expandedByDefault);
        bool expandedNew = GUILayout.Toggle(expanded, header, EditorStyles.foldout);
        if (expandedNew != expanded)
            SetFunctionIsIncluded(m_BaseClass, header, expandedNew);
        return expandedNew;
    }

    private void FunctionsGUI()
    {
        if (m_ScriptPrescription.m_Functions == null)
        {
            GUILayout.FlexibleSpace();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        {
            GUILayout.Label("Functions", GUILayout.Width(kLabelWidth - 4));

            EditorGUILayout.BeginVertical(m_Styles.m_LoweredBox);
            m_OptionsScroll = EditorGUILayout.BeginScrollView(m_OptionsScroll);
            {
                bool expanded = FunctionHeader("General", true);

                for (int i = 0; i < m_ScriptPrescription.m_Functions.Length; i++)
                {
                    FunctionData func = m_ScriptPrescription.m_Functions[i];

                    if (func.name == null)
                    {
                        expanded = FunctionHeader(func.comment, false);
                    }
                    else if (expanded)
                    {
                        Rect toggleRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.toggle);
                        toggleRect.x += 15;
                        toggleRect.width -= 15;
                        bool include = GUI.Toggle(toggleRect, func.include, new GUIContent(func.name, func.comment));
                        if (include != func.include)
                        {
                            m_ScriptPrescription.m_Functions[i].include = include;
                            SetFunctionIsIncluded(m_BaseClass, func.name, include);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

        }
        EditorGUILayout.EndHorizontal();
    }

    private void SetClassNameBasedOnTargetClassName()
    {
        if (m_CustomEditorTargetClassName == string.Empty)
            m_ScriptPrescription.m_ClassName = string.Empty;
        else
            m_ScriptPrescription.m_ClassName = m_CustomEditorTargetClassName + m_BaseClass;
    }

    private void CustomEditorTargetClassNameGUI()
    {
        GUI.SetNextControlName("CustomEditorTargetClassNameField");

        string newName = EditorGUILayout.TextField("Editor for", m_CustomEditorTargetClassName);
        m_ScriptPrescription.m_StringReplacements["$TargetClassName"] = newName;
        if (newName != m_CustomEditorTargetClassName)
        {
            m_CustomEditorTargetClassName = newName;
            SetClassNameBasedOnTargetClassName();
        }

        if (m_FocusTextFieldNow && Event.current.type == EventType.Repaint)
        {
            GUI.FocusControl("CustomEditorTargetClassNameField");
            m_FocusTextFieldNow = false;
            Repaint();
        }

        HelpField("Script component to make an editor for.");
    }

    private void TargetPathGUI()
    {
        m_Directory = EditorGUILayout.TextField("Save in", m_Directory, GUILayout.ExpandWidth(true));

        HelpField("Click a folder in the Project view to select.");
    }

    private bool ClearButton()
    {
        return GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(40));
    }

    private void TemplateSelectionGUI()
    {
        m_TemplateIndex = Mathf.Clamp(m_TemplateIndex, 0, m_TemplateNames.Length - 1);
        int templateIndexNew = EditorGUILayout.Popup("Template", m_TemplateIndex, m_TemplateNames);
        if (templateIndexNew != m_TemplateIndex)
        {
            m_TemplateIndex = templateIndexNew;
            AutomaticHandlingOnChangeTemplate();
        }
    }

    private void NameGUI()
    {
        GUI.SetNextControlName("ScriptNameField");
        m_ScriptPrescription.m_ClassName = EditorGUILayout.TextField("Name", m_ScriptPrescription.m_ClassName);

        if (m_FocusTextFieldNow && !m_IsCustomEditor && Event.current.type == EventType.Repaint)
        {
            EditorGUI.FocusTextInControl("ScriptNameField");
            m_FocusTextFieldNow = false;
        }
    }

    private void NamespaceGUI()
    {
        // Name space prefix and body fields.
        if (m_ScriptPrescription.m_NamespaceApplyPrefix)
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Namespace", GUILayout.Width(kLabelWidth - 4));
                const float prefixWidth = 120;
                namespacePrefixEditorPref.Value = EditorGUILayout.TextField(
                    namespacePrefixEditorPref.Value, GUILayout.Width(prefixWidth));
                m_ScriptPrescription.m_NamespacePrefix = namespacePrefixEditorPref.Value;

                EditorGUILayout.LabelField(".", GUILayout.Width(7));
                m_ScriptPrescription.m_NamespaceBody = EditorGUILayout.TextField(m_ScriptPrescription.m_NamespaceBody);
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            m_ScriptPrescription.m_NamespaceBody = EditorGUILayout.TextField("Namespace", m_ScriptPrescription.m_NamespaceBody);
        }
        m_ScriptPrescription.m_NamespaceApplyPrefix = EditorGUILayout.Toggle(
            "Apply Prefix", m_ScriptPrescription.m_NamespaceApplyPrefix);
    }

    private void PreviewGUI()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(position.width * 0.4f, position.width - kSidePanelWidth)));
        {
            // Reserve room for preview title
            Rect previewHeaderRect = GUILayoutUtility.GetRect(new GUIContent("Preview"), m_Styles.m_PreviewTitle);

            // Secret! Toggle curly braces on new line when double clicking the script preview title
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.clickCount == 2 && previewHeaderRect.Contains(evt.mousePosition))
            {
                EditorPrefs.SetBool("CurlyBracesOnNewLine", !EditorPrefs.GetBool("CurlyBracesOnNewLine"));
                Repaint();
            }

            // Preview scroll view
            m_PreviewScroll = EditorGUILayout.BeginScrollView(m_PreviewScroll, m_Styles.m_PreviewBox);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    // Tiny space since style has no padding in right side
                    GUILayout.Space(5);

                    // Preview text itself
                    string previewStr = new NewScriptGenerator(m_ScriptPrescription).ToString();
                    Rect r = GUILayoutUtility.GetRect(
                        new GUIContent(previewStr),
                        EditorStyles.miniLabel,
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true));
                    EditorGUI.SelectableLabel(r, previewStr, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // Draw preview title after box itself because otherwise the top row
            // of pixels of the slider will overlap with the title
            GUI.Label(previewHeaderRect, new GUIContent("Preview"), m_Styles.m_PreviewTitle);

            GUILayout.Space(4);
        }
        EditorGUILayout.EndVertical();
    }

    private bool InvalidTargetPath()
    {
        if (m_Directory.IndexOfAny(kInvalidPathChars) >= 0)
            return true;
        if (TargetDir().Split(kPathSepChars, StringSplitOptions.None).Contains(string.Empty))
            return true;
        return false;
    }

    private bool InvalidTargetPathForEditorScript()
    {
        return m_IsEditorClass && !m_Directory.ToLower().Split(kPathSepChars).Contains("editor");
    }

    private bool IsFolder(Object obj)
    {
        return Directory.Exists(AssetDatabase.GetAssetPath(obj));
    }

    private void HelpField(string helpText)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Empty, GUILayout.Width(kLabelWidth - 4));
        GUILayout.Label(helpText, m_Styles.m_HelpBox);
        GUILayout.EndHorizontal();
    }

    private string TargetPath()
    {
        return Path.Combine(TargetDir(), m_ScriptPrescription.m_ClassName + "." + extension);
    }

    private string TargetDir()
    {
        return m_Directory.Trim(kPathSepChars);
    }

    private bool ClassNameIsInvalid()
    {
        return !System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier(
            m_ScriptPrescription.m_ClassName);
    }

    private Type GetType(string className)
    {
        return (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                          from type in assembly.GetTypes()
                          where type.Name == className
                          select type).FirstOrDefault();
    }

    private bool ClassExists(string className)
    {
        return GetType(className) != null;
    }

    private bool ClassAlreadyExists()
    {
        if (m_ScriptPrescription.m_ClassName == string.Empty)
            return false;
        return ClassExists(m_ScriptPrescription.m_ClassName);
    }

    private bool TargetClassDoesNotExist()
    {
        if (!m_IsCustomEditor)
            return false;
        if (m_CustomEditorTargetClassName == string.Empty)
            return true;
        return !ClassExists(m_CustomEditorTargetClassName);
    }

    private bool TargetClassIsNotValidType()
    {
        if (!m_IsCustomEditor)
            return false;
        if (m_CustomEditorTargetClassName == string.Empty)
            return true;
        Type type = GetType(m_CustomEditorTargetClassName);
        if (type == null)
            return true;
        return !typeof(UnityEngine.Object).IsAssignableFrom(type);
    }

    private void CreateScript()
    {
        NewScriptGenerator newScriptGenerator = new NewScriptGenerator(m_ScriptPrescription);
        
        string targetDirectory = TargetDir();
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
            
            // If we just created an Editor folder, let's do a quick check if we ought to create any asmdefs/asmrefs.
            if (targetDirectory.EndsWith(kEditorFolderName))
                TryCreateAsmDefsForEditorFolder(targetDirectory, newScriptGenerator.NamespaceLine);
        }

        string path = TargetPath();
        var writer = new StreamWriter(path);
        writer.Write(newScriptGenerator.ToString());
        writer.Close();
        writer.Dispose();
        AssetDatabase.Refresh();

        // Ping the newly created script so you don't lose it, especially if it's in a new Editor folder...
        Object script = AssetDatabase.LoadAssetAtPath<Object>(path);
        EditorGUIUtility.PingObject(script);
    }

    private void TryCreateAsmDefsForEditorFolder(string targetDirectory, string @namespace)
    {
        string parentDirectory = targetDirectory.GetParentDirectory();
        AssemblyDefinitionAsset asmDefInParentDirectory = AsmDefUtilities.GetAsmDefInFolder(parentDirectory);

        if (asmDefInParentDirectory != null)
        {
            // There was an asmdef next to the Editor folder. Let's create a new asmdef that references it.
            AsmDefUtilities.CreateEditorFolderAsmDef(targetDirectory, asmDefInParentDirectory);
            return;
        }

        asmDefInParentDirectory = AsmDefUtilities.GetAsmDefInFolderOrParent(parentDirectory);
        
        if (asmDefInParentDirectory == null)
        {
            // No asmdef found right next to the Editor folder nor in a parent folder.
            // Apparently asmdefs are not being used here, so just leave it...
            return;
        }

        AssemblyDefinitionAsset asmDefInParentEditorFolder = AsmDefUtilities.GetParentEditorAsmDef(targetDirectory);
        
        if (asmDefInParentEditorFolder != null)
        {
            // If there is an Editor folder above us already, just create an .asmref file that points to it.
            AsmDefUtilities.CreateAsmRef(targetDirectory, asmDefInParentEditorFolder);
        }
        else
        {
            // There was no Editor folder above us. We're going to have to create a new one with its own .asmdef and
            // also a dummy script, because .asmdefs can't be in an empty folder.
            
            // The dummy should be in a namespace that is just Author.Project
            string dummyNamespace = NamespaceUtility.ClampNamespaceDepth(@namespace, 2);
            
            // Create a new editor folder at the root with its own asmdef, then reference that with an asmref.
            AssemblyDefinitionAsset rootEditorFolderAsmDef =
                AsmDefUtilities.CreateEmptyEditorFolderForRuntimeAsmDef(asmDefInParentDirectory, dummyNamespace);
            AsmDefUtilities.CreateAsmRef(targetDirectory, rootEditorFolderAsmDef);
        }
    }

    private void UpdateNamespace()
    {
        // If no namespace is defined for this project, determine one based on the company name.
        if (string.IsNullOrEmpty(namespacePrefixEditorPref.Value))
        {
            namespacePrefixEditorPref.Value =
                NamespaceUtility.ConvertFolderPathToSubNamespaces(PlayerSettings.companyName);
        }

        string newPrefix = namespacePrefixEditorPref.Value;
        if (!string.Equals(newPrefix, m_ScriptPrescription.m_NamespacePrefix, StringComparison.Ordinal))
            m_ScriptPrescription.m_NamespacePrefix = newPrefix;

        string inferredNamespace = NamespaceUtility.GetNamespaceForPath(
            m_Directory, out bool shouldOverrideCompanyPrefix);
        
        m_ScriptPrescription.m_NamespaceApplyPrefix = !shouldOverrideCompanyPrefix;
        m_ScriptPrescription.m_NamespaceBody = inferredNamespace;
    }

    private void OnSelectionChange()
    {
        m_ClearKeyboardControl = true;

        if (Selection.activeObject == null)
            return;
        
        if (IsFolder(Selection.activeObject))
        {
            m_Directory = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (m_IsEditorClass && InvalidTargetPathForEditorScript())
                m_Directory = Path.Combine(m_Directory, "Editor");
        }
        else
        {
            m_Directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(Selection.activeObject));
            bool isScript = Selection.activeObject is MonoScript;
            MonoScript script = Selection.activeObject as MonoScript;
            
            // If you open the dialog with a MonoBehaviour selected, default to the custom editor.
            if (isInitialOpen && isScript)
            {
                Type scriptClass = script.GetClass();
                if (typeof(MonoBehaviour).IsAssignableFrom(scriptClass) ||
                    typeof(ScriptableObject).IsAssignableFrom(scriptClass))
                {
                    SetTemplate(kDefaultCustomEditorForMonoBehaviours);
                }
                else
                {
                    SetTemplate(kDefaultCustomEditorForClasses);
                }
            }
            
            if (m_IsCustomEditor && isScript)
            {
                m_CustomEditorTargetClassName = Selection.activeObject.name;
                SetClassNameBasedOnTargetClassName();
            }
        }

        UpdateNamespace();

        Repaint();
    }

    private string AssetPathWithoutAssetPrefix(Object obj)
    {
        return AssetDatabase.GetAssetPath(obj).Substring(7);
    }

    public static bool IsEditorClass(string className)
    {
        if (className == null)
            return false;
        return GetAllClasses("UnityEditor").Contains(className);
    }
    
    public static bool IsCustomEditorClass(string className)
    {
        if (string.IsNullOrEmpty(className))
            return false;
        
        return kCustomEditorClassNames.Contains(className);
    }

    /// Method to populate a list with all the class in the namespace provided by the user
    private static List<string> GetAllClasses(string nameSpace)
    {
        // Get the UnityEditor assembly
        Assembly asm = Assembly.GetAssembly(typeof(Editor));

        // Create a list for the namespaces
        List<string> namespaceList = new List<string>();

        // Create a list that will hold all the classes the suplied namespace is executing
        List<string> returnList = new List<string>();

        foreach (Type type in asm.GetTypes())
        {
            if (type.Namespace == nameSpace)
                namespaceList.Add(type.Name);
        }

        // Now loop through all the classes returned above and add them to our classesName list
        foreach (String className in namespaceList)
            returnList.Add(className);

        return returnList;
    }
}
