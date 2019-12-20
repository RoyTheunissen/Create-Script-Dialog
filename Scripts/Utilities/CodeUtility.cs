using System;
using System.Collections.Generic;

namespace RoyTheunissen.CreateScriptDialog.Utilities
{
    public static class CodeUtility
    {
        public const string CodeExtension = ".cs";

#if UNITY_EDITOR_WIN
        public const string LineBreakSymbol = "\r\n";
#else
        public const string LineBreakSymbol = "\n";
#endif

        public const string IndentationString = "    ";

        public const string UsingText = "using ";
        public static readonly string[] DeclarationWords = new string[] {
            "private", "protected", "public", "class", "struct", "enum"
        };

        public const char LineEndSymbol = ';';

        public const char MacroSymbol = '#';

        public static int GetIndexOfEarliestDeclaration(string contents)
        {
            return StringUtility.IndexOfAny(contents, DeclarationWords);
        }

        public static string Indent(string text, int numberOfIndentations, int fromIndex, int toIndex, bool skipFirstLine)
        {
            // Divide the text into the regions that should and should not be modified.
            string before = text.Substring(0, fromIndex);
            string middle = text.Substring(fromIndex, (toIndex - fromIndex));
            string after = text.Substring(toIndex);

            // Modify the middle part.
            string[] lines = middle.Split(new string[] { LineBreakSymbol }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                // Skip the first line if we're told to.
                if (skipFirstLine && i == 0)
                    continue;

                // Indent each line the specified number of times.
                for (int j = 0; j < numberOfIndentations; j++)
                    lines[i] = IndentationString + lines[i];
            }
            middle = string.Join(LineBreakSymbol.ToString(), lines);

            return before + middle + after;
        }

        public static string Indent(string text, int numberOfIndentations, int fromIndex, int toIndex)
        {
            return Indent(text, numberOfIndentations, fromIndex, toIndex, false);
        }

        public static string Indent(string text, int numberOfIndentations, bool skipFirstLine)
        {
            return Indent(text, numberOfIndentations, 0, text.Length, skipFirstLine);
        }

        public static string Indent(string text, int numberOfIndentations)
        {
            return Indent(text, numberOfIndentations, 0, text.Length, false);
        }

        /// <summary>
        /// Gets the index after the last using, or -1 if the usings were formatted invalidly.
        /// Usings need to be at the top of the file, before any declarations,
        /// and their lines need to be ended correctly, which in C# is with a semicolon.
        /// </summary>
        /// <param name="contents">The code to search through.</param>
        /// <returns>The index after the last using or -1 if usings were formatted invalidly.</returns>
        public static int GetIndexAfterLastUsing(string contents)
        {
            int earliestDeclaration = GetIndexOfEarliestDeclaration(contents);

            // If there's any declarations (there really should be).
            if (earliestDeclaration != -1)
            {
                contents = contents.Substring(0, earliestDeclaration);
            }

            // Find the last occurence of the using keyword before the first declaration.
            int lastOccurenceOfUsingKeyword = contents.LastIndexOf(UsingText);

            // No usings at all? What what?
            if (lastOccurenceOfUsingKeyword == -1)
            {
                return -1;
            }
            else
            {
                int firstNewLineEnd = contents.IndexOf(LineEndSymbol, lastOccurenceOfUsingKeyword);

                // The last using is not closed by a line end? What??
                if (firstNewLineEnd == -1)
                {
                    return -1;
                }
                else
                {
                    // Don't include the line end symbol (the semicolon in C#).
                    firstNewLineEnd++;

                    // Ignore any additional consecutive linebreaks, indentation or macro's.
                    while (firstNewLineEnd < contents.Length
                        && (contents[firstNewLineEnd] == '\n'
                        || contents[firstNewLineEnd] == '\r'
                        || contents[firstNewLineEnd] == '\t'
                        || contents[firstNewLineEnd] == ' '
                        || contents[firstNewLineEnd] == MacroSymbol))
                    {
                        // Skip to the end of the line.
                        if (contents[firstNewLineEnd] == MacroSymbol)
                        {
                            firstNewLineEnd = contents.IndexOf(LineBreakSymbol, firstNewLineEnd);
                        }
                        else
                        {
                            firstNewLineEnd++;
                        }
                    }

                    return firstNewLineEnd;
                }
            }
        }
    }
}
