using System;
using System.Collections.Generic;

namespace Paladin.Framework.Utilities
{
    public static class StringUtility
    {
        private const string HungarianPrefix = "m_";
        private const string Underscore = "_";
        private const string HumanReadableSeparator = " ";

        /// <summary>
        /// Returns the first index of any of the specified strings
        /// in the specified text or -1 if none of them occurred at all.
        /// </summary>
        /// <param name="text">The text to search in.</param>
        /// <param name="anyOf">The texts to search for.</param>
        /// <returns>-1 if none of the strings occurred,
        /// otherwise the earliest zer-based index.</returns>
        public static int IndexOfAny(string text, string[] anyOf)
        {
            int result = int.MaxValue;
            int index;

            // Find the earliest occurrence of any of the specified strings.
            for (int i = 0; i < anyOf.Length; i++)
            {
                index = text.IndexOf(anyOf[i]);

                if (index != -1 && index < result)
                {
                    result = index;
                }
            }

            // If we didn't find any of the specified strings at all.
            if (result == int.MaxValue)
            {
                return -1;
            }
            else
            {
                return result;
            }
        }

        /// <summary>
        /// Returns all the indentation characters preceding the specified
        /// position or an empty string if there was none.
        /// </summary>
        /// <param name="text">The text within which to search.</param>
        /// <param name="position">The position to retrieve the preceding indentation of.</param>
        /// <returns>A string containing all preceding indentation characters.</returns>
        public static string GetIndentationAt(string text, int position)
        {
            string result = "";
            text = text.Substring(0, position);
            while (text.EndsWith(CodeUtility.IndentationString))
            {
                result += CodeUtility.IndentationString;
                text = text.Remove(text.Length - CodeUtility.IndentationString.Length);
            }
            return result;
        }

        /// <summary>
        /// Gets the number of indentations preceding the specified position.
        /// </summary>
        /// <param name="text">Text within which to search.</param>
        /// <param name="position">The position to retrieve the preceding indentation count of.</param>
        /// <returns>The number of indentations preceding the specified position.</returns>
        public static int GetIndentationCountAt(string text, int position)
        {
            // Get the indentation string.
            string indentation = GetIndentationAt(text, position);

            // If it's empty, there's no indentation.
            if (string.IsNullOrEmpty(indentation))
                return 0;

            // Otherwise count the indentation symbols.
            else
                return indentation.Split(
                    new string[] { CodeUtility.IndentationString },
                    StringSplitOptions.None).Length - 1;
        }

        public static string ReplaceAndKeepIndentation(string text, string oldString, string newString)
        {
            int oldStringIndex = text.IndexOf(oldString);
            int indentation;
            while (oldStringIndex != -1)
            {
                // Get the level of indentation at the occurrence.
                indentation = GetIndentationCountAt(text, oldStringIndex);

                // Remove the occurrence of the old string.
                text = text.Remove(oldStringIndex, oldString.Length);

                // Insert the new string indented in the same manner as the occurrence.
                text = text.Insert(oldStringIndex,
                    CodeUtility.Indent(newString, indentation, true)
                    );

                // Search for the next occurrence.
                oldStringIndex = text.IndexOf(oldString);
            }
            return text;
        }

        /// <summary>
        /// Gets the human readable version of  programmer text like a variable name.
        /// </summary>
        /// <param name="programmerText">The programmer text.</param>
        /// <returns>The human readable equivalent of the programmer text.</returns>
        public static string GetHumanReadableText(string programmerText)
        {
            bool wasLetter = false;
            bool wasUpperCase = false;
            bool addedSpace = false;
            string result = "";

            // First remove the  prefix if it exists.
            if (programmerText.StartsWith(HungarianPrefix))
                programmerText = programmerText.Substring(HungarianPrefix.Length);

            // Deal with any miscellanneous underscores.
            programmerText = programmerText.Replace(Underscore, string.Empty);

            // Go through the original string and copy it with some modifications.
            for (int i = 0; i < programmerText.Length; i++)
            {
                // If there was a change in caps add spaces.
                if (char.IsUpper(programmerText[i]) != wasUpperCase
                    && i > 0 && !addedSpace)
                {
                    // Upper case to lower case.
                    // I added this so that something like 'GUIItem' turns into 'GUI Item',
                    // but that means we have to make sure that no symbols are involved.
                    if (wasUpperCase && i > 1)
                    {
                        // From letter to letter means we have to insert a space one character back.
                        // Otherwise it's going from a letter to a symbol and we can just add a space.
                        if (wasLetter && char.IsLetter(programmerText[i]))
                            result = result.Insert(result.Length - 1, HumanReadableSeparator);
                        else
                            result += HumanReadableSeparator;
                        addedSpace = true;
                    }
                    // Lower case to upper case.
                    if (!wasUpperCase)
                    {
                        result += HumanReadableSeparator;
                        addedSpace = true;
                    }
                }
                else
                {
                    // No case change.
                    addedSpace = false;
                }

                // Add the character.
                result += programmerText[i];

                // Capitalize the first character.
                if (i == 0)
                    result = result.ToUpper();

                // Remember things about the previous letter.
                wasLetter = char.IsLetter(programmerText[i]);
                wasUpperCase = char.IsUpper(programmerText[i]);
            }
            return result;
        }
    }
}