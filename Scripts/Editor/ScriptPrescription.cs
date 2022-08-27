using System;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UnityEditor
{
	[Serializable]
	internal class ScriptPrescription
	{
		public string m_ClassName = string.Empty;
        public string m_NamespacePrefix = string.Empty;
        public string m_NamespaceBody = string.Empty;
		public Language m_Lang;
		public string m_Template;
		public FunctionData[] m_Functions;
		public Dictionary<string, string> m_StringReplacements = new Dictionary<string, string> ();
	}
	
	internal enum Language
	{
		CSharp = 1
	}

	internal struct FunctionData
	{
		public string name;
        public string scope;
		public string returnType;
		public string returnDefault;
		public bool isStatic;
        public bool isVirtual;
		public ParameterData[] parameters;
		public string comment;
		public bool include;
		
		public FunctionData (string headerName)
		{
			comment = headerName;
			name = null;
            scope = null;
			returnType = null;
			returnDefault = null;
			isStatic = false;
			isVirtual = false;
			parameters = null;
			include = false;
		}
	}
	
	internal struct ParameterData
	{
		public string name;
		public string type;
		
		public ParameterData (string name, string type)
		{
			this.name = name;
			this.type = type;
		}
	}
}
