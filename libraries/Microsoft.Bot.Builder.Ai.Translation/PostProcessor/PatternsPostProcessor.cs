﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Bot.Builder.Ai.Translation.PostProcessor
{
    /// <summary>
    /// PatternsPostProcessor  is used to handle translation errors while translating numbers
    /// and to handle the words that need to be kept same as source language from provided template each line having a regex
    /// having first group matching the words that needs to be kept.
    /// </summary>
    public class PatternsPostProcessor : IPostProcessor
    {
        private readonly Dictionary<string, HashSet<string>> _processedPatterns;

        /// <summary>
        /// Constructor that indexes input template for the source language.
        /// </summary>
        /// <param name="patterns">No translate patterns for different languages.</param> 
        public PatternsPostProcessor(Dictionary<string, List<string>> patterns)
        {
            if (patterns == null)
            {
                throw new ArgumentNullException(nameof(patterns));
            }

            if (patterns.Count == 0)
            {
                throw new ArgumentException(MessagesProvider.EmptyPatternsErrorMessage);
            }

            _processedPatterns = new Dictionary<string, HashSet<string>>();
            foreach (KeyValuePair<string, List<string>> item in patterns)
            {
                _processedPatterns.Add(item.Key, new HashSet<string>());
                foreach (string pattern in item.Value)
                {
                    string processedLine = pattern.Trim();
                    //if (the pattern doesn't follow this format (pattern), add the braces around the pattern
                    if (!Regex.IsMatch(pattern, "(\\(.+\\))"))
                    {
                        processedLine = '(' + processedLine + ')';
                    }
                    _processedPatterns[item.Key].Add(processedLine);
                }
            }
        }

        /// <summary>
        /// Process the logic for patterns post processor used to handle numbers and no translate list.
        /// </summary>
        /// <param name="translatedDocument">Translated document.</param>
        /// <param name="languageId">Current source language id.</param>
        /// <returns>A <see cref="PostProcessedDocument"/> stores the original translated document state and the newly post processed message.</returns>
        public PostProcessedDocument Process(TranslatedDocument translatedDocument, string languageId)
        {
            //validate function arguments for null and incorrect format
            ValidateParameters(translatedDocument);

            //flag to indicate if the source message contains number , will used for 
            bool containsNum = Regex.IsMatch(translatedDocument.SourceMessage, @"\d");

            //output variable declaration
            string processedResult;

            //temporary pattern is used to contain two set of patterns :
            //  - the post processed patterns that was configured by the user ie : _processedPatterns and
            //  - the   liternal no translate pattern ie : translatedDocument.LiteranlNoTranslatePhrases , which takes the following regx "<literal>(.*)</literal>" , so the following code checks if this pattern exists in the translated document object to be added to the no translate list
            //  - ex : translatedDocument.sourceMessage = I like my friend <literal>happy</literal> , the literal tag here specifies that the word "happy" shouldn't be translated
            HashSet<string> temporaryPatterns = _processedPatterns[languageId];

            if (translatedDocument.LiteranlNoTranslatePhrases != null && translatedDocument.LiteranlNoTranslatePhrases.Count > 0)
            {
                temporaryPatterns.UnionWith((translatedDocument.LiteranlNoTranslatePhrases));
            }

            if (temporaryPatterns.Count == 0 && !containsNum)
            {
                processedResult = translatedDocument.TargetMessage;
            }

            if (string.IsNullOrWhiteSpace(translatedDocument.RawAlignment))
            {
                processedResult = translatedDocument.TargetMessage;
            }

            //loop for all the patterns and substitute each no translate pattern match with the original source words

            //ex : assuming the pattern = "mon nom est (.+)" 
            //and the phrase = "mon nom est l'etat"
            //the original translator output for this phrase would be "My name is the state", 
            //after applying the patterns post processor , the output would be : "My name is l'etat"
            foreach (string pattern in temporaryPatterns)
            {
                if (Regex.IsMatch(translatedDocument.SourceMessage, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase))
                {
                    SubstituteNoTranslatePattern(translatedDocument, pattern);
                }
            }
            SubstituteNumericPattern(translatedDocument);
            processedResult = PostProcessingUtilities.Join(" ", translatedDocument.TranslatedTokens);
            return new PostProcessedDocument(translatedDocument, processedResult);
        }

        /// <summary>
        /// Substitutes matched no translate pattern with the original token 
        /// </summary>
        /// <param name="translatedDocument">Translated document.</param>
        /// <param name="pattern">The no translate pattern.</param>
        private void SubstituteNoTranslatePattern(TranslatedDocument translatedDocument, string pattern)
        {
            //get the matched no translate pattern
            Match matchNoTranslate = Regex.Match(translatedDocument.SourceMessage, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            //calculate the boundaries of the pattern match
            //ex : "mon nom est l'etat
            // start index = 12
            //length = 6
            int noTranslateStartChrIndex = matchNoTranslate.Groups[1].Index;
            //the length of the matched pattern without spaces , which will be used in determining the translated tokens that will be replaced by their original values
            int noTranslateMatchLength = matchNoTranslate.Groups[1].Value.Replace(" ", "").Length;
            int wrdIndx = 0;
            int chrIndx = 0;
            int newChrLengthFromMatch = 0;
            int srcIndex = -1;
            int newNoTranslateArrayLength = 1;
            var sourceMessageCharacters = translatedDocument.SourceMessage.ToCharArray();


            foreach (string wrd in translatedDocument.SourceTokens)
            {

                //if the beginning of the current word equals the beginning of the matched no trasnalate word, then assign the current word index to srcIndex 
                if (chrIndx == noTranslateStartChrIndex)
                {
                    srcIndex = wrdIndx;
                }

                //the following code block does the folowing :
                //- checks if a match wsa found 
                //- checks if this match length equals the starting matching token length, if yes then this is the only token to process,
                //otherwise continue the loop and add the next token to the list of tokens to be processed
                //ex : "mon nom est l'etat"
                //tokens = {"mon", "nom", "est", "l'", "etat"}
                //when the loop reaches the token "l'" then srcIndex will = 3, but we don't want to consider only the token "l'" as the no translate token, 
                //instead we want to match the whole "l'etat" string regardless how many tokens it contains ie regardless that "l'etat" is actually composed of 2 tokens "l'" and "etat"
                //so what these condition is trying to do is make the necessary checks that we got all the matched pattern not just a part of it's tokens!

                //checks if match was found or not, because srcIndex value changes only in case a match was found !
                if (srcIndex != -1)
                {
                    //checks if we found all the tokens that matches the pattern
                    if (newChrLengthFromMatch + translatedDocument.SourceTokens[wrdIndx].Length >= noTranslateMatchLength)
                        break;

                    //if the previous condition fails it means that the next token is also matched in the pattern, so we increase the size of the no translate words array by 1
                    newNoTranslateArrayLength += 1;
                    //increment newChrLengthFromMatch with the found word size
                    newChrLengthFromMatch += translatedDocument.SourceTokens[wrdIndx].Length;
                }

                // the following block of code is used to calculate the next token starting index which could have two cases
                //the first case is that the current token is followed by a space in this case we increment the next chrIndx by 1 to get the next character after the space
                //the second case is that the token is followed by the next token without spaces , in this case we calculate chrIndx as chrIndx += wrd.Length without incrementing
                //assumption : The provided sourceMessage and sourceMessageCharacters doesn't contain any consecutive white spaces,
                //in our use case this handling is done using the translator output itself using the following line of code in PreprocessMessage function : 
                //textToTranslate = Regex.Replace(textToTranslate, @"\s+", " ");//used to remove multiple spaces in input user message
                if (chrIndx + wrd.Length < sourceMessageCharacters.Length && sourceMessageCharacters[chrIndx + wrd.Length] == ' ')
                {
                    chrIndx += wrd.Length + 1;
                }
                else
                {
                    chrIndx += wrd.Length;
                }
                wrdIndx++;
            }

            //if the loop ends and srcIndex then no match was found
            if (srcIndex == -1)
                return;
            //add the no translate words to a new array
            string[] wrdNoTranslate = new string[newNoTranslateArrayLength];
            Array.Copy(translatedDocument.SourceTokens, srcIndex, wrdNoTranslate, 0, newNoTranslateArrayLength);

            //loop for each of the no translate words and replace it's translation with it's origin
            foreach (string srcWrd in wrdNoTranslate)
            {
                translatedDocument.TranslatedTokens = PostProcessingUtilities.KeepSourceWordInTranslation(translatedDocument.IndexedAlignment, translatedDocument.SourceTokens, translatedDocument.TranslatedTokens, srcIndex);
                srcIndex++;
            }
        }

        /// <summary>
        /// Substitute the numeric numbers in translated message with their orignal format in source message.
        /// </summary>
        /// <param name="translatedDocument">Translated document.</param>
        private void SubstituteNumericPattern(TranslatedDocument translatedDocument)
        {
            MatchCollection numericMatches = Regex.Matches(translatedDocument.SourceMessage, @"\d+", RegexOptions.Singleline);
            foreach (Match numericMatch in numericMatches)
            {
                int srcIndex = Array.FindIndex(translatedDocument.SourceTokens, row => row == numericMatch.Groups[0].Value);
                translatedDocument.TranslatedTokens = PostProcessingUtilities.KeepSourceWordInTranslation(translatedDocument.IndexedAlignment, translatedDocument.SourceTokens, translatedDocument.TranslatedTokens, srcIndex);
            }
        }

        /// <summary>
        /// Validate <see cref="TranslatedDocument"/> object main parameters for null values.
        /// </summary>
        /// <param name="translatedDocument"></param>
        private void ValidateParameters(TranslatedDocument translatedDocument)
        {
            if (translatedDocument == null)
            {
                throw new ArgumentNullException(nameof(translatedDocument));
            }

            if (translatedDocument.SourceMessage == null)
            {
                throw new ArgumentNullException(nameof(translatedDocument.SourceMessage));
            }

            if (translatedDocument.TargetMessage == null)
            {
                throw new ArgumentNullException(nameof(translatedDocument.TargetMessage));
            }
        }
    }
}
