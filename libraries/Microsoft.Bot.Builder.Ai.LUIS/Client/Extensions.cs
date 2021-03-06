﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Cognitive.LUIS.Models;

namespace Microsoft.Bot.Builder.Ai.LUIS
{
    /// <summary>
    /// LUIS extension methods.
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// Try to find an entity within the result.
        /// </summary>
        /// <param name="result">The LUIS result.</param>
        /// <param name="type">The entity type.</param>
        /// <param name="entity">The found entity.</param>
        /// <returns>True if the entity was found, false otherwise.</returns>
        public static bool TryFindEntity(this LuisResult result, string type, out EntityRecommendation entity)
        {
            entity = result.Entities?.FirstOrDefault(e => e.Type == type);
            return entity != null;
        }

        /// <summary>
        /// Parse all resolutions from a LUIS result.
        /// </summary>
        /// <param name="parser">The resolution parser.</param>
        /// <param name="entities">The LUIS entities.</param>
        /// <returns>The parsed resolutions.</returns>
        public static IEnumerable<Resolution> ParseResolutions(this IResolutionParser parser, IEnumerable<EntityRecommendation> entities)
        {
            if (entities != null)
            {
                foreach (var entity in entities)
                {
                    Resolution resolution;
                    if (parser.TryParse(entity.Resolution, out resolution))
                    {
                        yield return resolution;
                    }
                }
            }
        }

        /// <summary>
        /// Return the next <see cref="BuiltIn.DateTime.DayPart"/>. 
        /// </summary>
        /// <param name="part">The <see cref="BuiltIn.DateTime.DayPart"/> query.</param>
        /// <returns>The next <see cref="BuiltIn.DateTime.DayPart"/> after the query.</returns>
        public static BuiltIn.DateTime.DayPart Next(this BuiltIn.DateTime.DayPart part)
        {
            switch (part)
            {
                case BuiltIn.DateTime.DayPart.MO: return BuiltIn.DateTime.DayPart.MI;
                case BuiltIn.DateTime.DayPart.MI: return BuiltIn.DateTime.DayPart.AF;
                case BuiltIn.DateTime.DayPart.AF: return BuiltIn.DateTime.DayPart.EV;
                case BuiltIn.DateTime.DayPart.EV: return BuiltIn.DateTime.DayPart.NI;
                case BuiltIn.DateTime.DayPart.NI: return BuiltIn.DateTime.DayPart.MO;
                default: throw new NotImplementedException();
            }
        }

    }
}
