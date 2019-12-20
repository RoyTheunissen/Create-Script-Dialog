using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using RoyTheunissen.CreateScriptDialog.Utilities;

namespace UnityEditor
{
	internal class NewScriptGenerator
	{
		private const int kCommentWrapLength = 35;
		
		private TextWriter m_Writer;
		private string m_Text;
		private ScriptPrescription m_ScriptPrescription;
		private string m_Indentation;
		private int m_IndentLevel = 0;
		
		private int IndentLevel
		{
			get
			{
				return m_IndentLevel;
			}
			set
			{
				m_IndentLevel = value;
				m_Indentation = String.Empty;
				for (int i=0; i<m_IndentLevel; i++)
					m_Indentation += CodeUtility.IndentationString;
			}
		}
		
		private string ClassName
		{
			get
			{
				if (m_ScriptPrescription.m_ClassName != string.Empty)
					return m_ScriptPrescription.m_ClassName;
				return "Example";
			}
		}

        private string Namespace
        {
            get
            {
                string line = "namespace ";

                // If there's no prefix provide an example.
                if (m_ScriptPrescription.m_NamespacePrefix == string.Empty
                    && m_ScriptPrescription.m_NamespaceBody == string.Empty)
                    line += "Company.Example";

                // If there's no body provide an example.
                else if (m_ScriptPrescription.m_NamespacePrefix == string.Empty)
                    line += "Company." + m_ScriptPrescription.m_NamespaceBody;

                // If there's neither provide an example.
                else if (m_ScriptPrescription.m_NamespaceBody == string.Empty)
                    line += m_ScriptPrescription.m_NamespacePrefix + ".Example";

                // Otherwise add the two together!
                else 
                    line += m_ScriptPrescription.m_NamespacePrefix + "." + m_ScriptPrescription.m_NamespaceBody;

                return line;
            }
        }
		
		public NewScriptGenerator (ScriptPrescription scriptPrescription)
		{
			m_ScriptPrescription = scriptPrescription;
		}
		
		public override string ToString ()
		{
			m_Text += m_ScriptPrescription.m_Template;
			m_Writer = new StringWriter ();
			m_Writer.NewLine = "\n";
			
			// Make sure all line endings are Unix (Mac OS X) format
            // TODO: Make it Windows style from the start
			m_Text = Regex.Replace (m_Text, @"\r\n?", delegate(Match m) { return "\n"; });

            // Add class summary template
            string path = Path.Combine(NewScriptWindow.GetAbsoluteCustomTemplatePath(), "Header.txt");
            if (File.Exists(path))
            {
                m_Text = StringUtility.ReplaceAndKeepIndentation(m_Text, "$ClassSummary", File.ReadAllText(path));
            }
			
			// Class Name
			m_Text = m_Text.Replace ("$ClassName", ClassName);
			m_Text = m_Text.Replace ("$NicifiedClassName", ObjectNames.NicifyVariableName (ClassName));

            // Namespace
            m_Text = m_Text.Replace("$Namespace", Namespace);
			
			// Other replacements
			foreach (KeyValuePair<string, string> kvp in m_ScriptPrescription.m_StringReplacements)
				m_Text = m_Text.Replace (kvp.Key, kvp.Value);
			
			// Functions
			// Find $Functions keyword including leading spaces
			Match match = Regex.Match (m_Text, @"( *)\$Functions");
			if (match.Success)
			{
				// Set indent level to number of spaces before $Functions keyword divided by the number of 
                // spaces per indentation level.
				IndentLevel = match.Groups[1].Value.Length / CodeUtility.IndentationString.Length;
				bool hasFunctions = false;
				if (m_ScriptPrescription.m_Functions != null)
				{
					foreach (var function in m_ScriptPrescription.m_Functions.Where (f => f.include))
					{
						WriteFunction (function);
						WriteBlankLine ();
						hasFunctions = true;
					}
					
					// Replace $Functions keyword plus newline with generated functions text
					if (hasFunctions)
						m_Text = m_Text.Replace (match.Value + "\n", m_Writer.ToString ());
				}
				
				if (!hasFunctions)
				{
					// Otherwise just remove $Functions keyword plus newline
					m_Text = m_Text.Replace (match.Value + "\n", string.Empty);
				}
			}
			
			// Put curly vraces on new line if specified in editor prefs
			if (EditorPrefs.GetBool ("CurlyBracesOnNewLine"))
				PutCurveBracesOnNewLine ();

            // Make the line endings either \r\n (Windows) or \n (OSX)
            m_Text = Regex.Replace(m_Text, @"\r\n|\n\r|\n|\r", delegate(Match m)
            {
#if UNITY_EDITOR_WIN
                return "\r\n";
#else
                return "\n";
#endif
            });
            m_Text = Regex.Replace(m_Text, @"\t", delegate(Match m) { return "    "; });
			
			// Return the text of the script
			return m_Text;
		}
		
		private void PutCurveBracesOnNewLine ()
		{
			m_Text = Regex.Replace (m_Text, @"(\t*)(.*) {\n((\t*)\n(\t*))?", delegate(Match match)
			{
				return match.Groups[1].Value + match.Groups[2].Value + "\n" + match.Groups[1].Value + "{\n" +
					(match.Groups[4].Value == match.Groups[5].Value ? match.Groups[4].Value : match.Groups[3].Value);
			});
		}
		
		private void WriteBlankLine ()
		{
			m_Writer.WriteLine (m_Indentation);
		}
		
		private void WriteComment (string comment)
		{
            //int index = 0;

            //m_Writer.WriteLine(m_Indentation + "/// <summary>");

            //while (true)
            //{
            //    if (comment.Length <= index + kCommentWrapLength)
            //    {
            //        m_Writer.WriteLine (m_Indentation + "/// " + comment.Substring (index));
            //        break;
            //    }
            //    else
            //    {
            //        int wrapIndex = comment.IndexOf (' ', index + kCommentWrapLength);
            //        if (wrapIndex < 0)
            //        {
            //            m_Writer.WriteLine (m_Indentation + "/// " + comment.Substring (index));
            //            break;
            //        }	
            //        else
            //        {
            //            m_Writer.WriteLine (m_Indentation + "/// " + comment.Substring (index, wrapIndex-index));
            //            index = wrapIndex + 1;
            //        }
            //    }
            //}

            //m_Writer.WriteLine(m_Indentation + "/// </summary>");
		}

		private string TranslateTypeToJavascript (string typeInCSharp)
		{
			return typeInCSharp.Replace ("bool", "boolean").Replace ("string", "String").Replace ("Object", "UnityEngine.Object");
		}
		
		private string TranslateTypeToBoo (string typeInCSharp)
		{
			return typeInCSharp.Replace ("float", "single");
		}
		
		private void WriteFunction (FunctionData function)
		{
			string paramString = string.Empty;
			string overrideString;
			string returnTypeString;
			string functionContentString;
			
			switch (m_ScriptPrescription.m_Lang)
			{
			    case Language.CSharp:
				    // Comment
                    if (!String.IsNullOrEmpty(function.comment))
				        WriteComment (function.comment);
				
				    // Function header
				    for (int i=0; i<function.parameters.Length; i++)
				    {
					    paramString += function.parameters[i].type + " " + function.parameters[i].name;
					    if (i < function.parameters.Length-1)
						    paramString += ", ";
				    }
				    overrideString = (function.isVirtual ? "override " : string.Empty);
				    returnTypeString = (function.returnType == null ? "void " : function.returnType + " ");
                    m_Writer.WriteLine(m_Indentation + function.scope + overrideString + returnTypeString + function.name + "(" + paramString + ")");
                    m_Writer.WriteLine (m_Indentation + "{");

				    // Function content
				    IndentLevel++;
				    functionContentString = (function.returnType == null ? string.Empty : function.returnDefault + ";");
				    m_Writer.WriteLine (m_Indentation + functionContentString);
				    IndentLevel--;
				    m_Writer.WriteLine (m_Indentation + "}");
				
				    break;
			}
		}
	}
}

