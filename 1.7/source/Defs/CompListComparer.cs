using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Mechanoid_Sentience
{
    public struct CompSignature : IEquatable<CompSignature>
    {
        public readonly Type PropsType;
        public readonly Type CompClass;

        public CompSignature(Type propsType, Type compClass)
        {
            PropsType = propsType;
            CompClass = compClass;
        }

        public static CompSignature Of(CompProperties props)
        {
            return props == null ? default(CompSignature) : new CompSignature(props.GetType(), props.compClass);
        }

        public bool IsVefAbilities => PropsType != null && CompClass != null
            && PropsType.FullName == CompListComparer.VefPropsTypeName && CompClass.FullName == CompListComparer.VefCompClassName;

        public bool IsHospitalityGuest => PropsType != null && CompClass != null
            && PropsType.FullName == CompListComparer.HospitalityPropsTypeName && CompClass.FullName == CompListComparer.HospitalityCompClassName;

        public bool Equals(CompSignature other)
        {
            return PropsType == other.PropsType && CompClass == other.CompClass;
        }

        public override bool Equals(object obj)
        {
            return obj is CompSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((PropsType?.GetHashCode() ?? 0) * 397) ^ (CompClass?.GetHashCode() ?? 0);
        }

        public override string ToString()
        {
            if (PropsType == null)
            {
                return "<null entry>";
            }
            return PropsType.FullName + " (compClass " + (CompClass?.FullName ?? "null") + ")";
        }
    }

    public static class CompListComparer
    {
        public const string VefPropsTypeName = "VEF.Apparels.CompProperties_ShieldBubble";
        public const string VefCompClassName = "VEF.Abilities.CompAbilities";
        public const string HospitalityPropsTypeName = "Hospitality.CompProperties_Guest";
        public const string HospitalityCompClassName = "Hospitality.CompGuest";

        public static string FirstDifference(IList<CompProperties> original, IList<CompProperties> sentient, out bool hasVefExtra)
        {
            return FirstDifference(original, sentient, null, out hasVefExtra);
        }

        public static string FirstDifference(IList<CompProperties> original, IList<CompProperties> sentient, Func<CompSignature, bool> isAllowedRemoval, out bool hasVefExtra)
        {
            List<CompSignature> expected = ExpectedBase(original, isAllowedRemoval);
            List<CompSignature> actual = (sentient ?? new List<CompProperties>()).Select(CompSignature.Of).ToList();
            string difference = Difference(expected, actual, out hasVefExtra);
            if (difference == null)
            {
                return null;
            }
            string label = isAllowedRemoval == null ? "Source without overseer subject" : "Source without overseer subject and allowed removals";
            return difference + " " + label + " (" + expected.Count + "): " + Format(expected)
                + ". Sentient (" + actual.Count + "): " + Format(actual) + ".";
        }

        public static List<CompSignature> ExpectedBase(IList<CompProperties> original)
        {
            return ExpectedBase(original, null);
        }

        public static List<CompSignature> ExpectedBase(IList<CompProperties> original, Func<CompSignature, bool> isAllowedRemoval)
        {
            return (original ?? new List<CompProperties>())
                .Where(c => !(c is CompProperties_OverseerSubject))
                .Select(CompSignature.Of)
                .Where(s => isAllowedRemoval == null || !isAllowedRemoval(s))
                .ToList();
        }

        public static string Format(IList<CompSignature> signatures)
        {
            return signatures.Count == 0 ? "(none)" : string.Join(", ", signatures.Select((s, i) => "[" + i + "] " + s));
        }

        private static string Difference(List<CompSignature> expected, List<CompSignature> actual, out bool hasVefExtra)
        {
            hasVefExtra = false;
            int common = Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < common; i++)
            {
                if (expected[i].Equals(actual[i]))
                {
                    continue;
                }
                int expectedCount = expected.Count(s => s.Equals(expected[i]));
                int actualCount = actual.Count(s => s.Equals(expected[i]));
                if (actualCount > 0 && expectedCount != actualCount)
                {
                    return "Entry " + i + " differs: expected " + expected[i] + " but found " + actual[i] + "; the source has " + expectedCount + " of the expected comp, the sentient def " + actualCount + ".";
                }
                bool actualInExpected = expected.Contains(actual[i]);
                bool expectedInActual = actual.Contains(expected[i]);
                if (actualInExpected && expectedInActual)
                {
                    return "Entry " + i + " is reordered: expected " + expected[i] + " but found " + actual[i] + ".";
                }
                if (!actualInExpected && expectedInActual)
                {
                    return "Entry " + i + " is an unexpected insertion: " + actual[i] + " (expected " + expected[i] + ").";
                }
                if (actualInExpected)
                {
                    return "Entry " + i + " is missing: expected " + expected[i] + " but found " + actual[i] + ".";
                }
                return "Entry " + i + " is changed: expected " + expected[i] + " but found " + actual[i] + ".";
            }
            if (actual.Count < expected.Count)
            {
                return "Entry " + common + " is missing: expected " + expected[common] + ".";
            }
            int vefCount = 0;
            int guestCount = 0;
            for (int i = expected.Count; i < actual.Count; i++)
            {
                if (actual[i].IsVefAbilities)
                {
                    vefCount++;
                    if (vefCount > 1)
                    {
                        return "Entry " + i + " is a duplicate of the VEF ability comp " + actual[i] + ".";
                    }
                    continue;
                }
                if (actual[i].IsHospitalityGuest)
                {
                    guestCount++;
                    if (guestCount > 1)
                    {
                        return "Entry " + i + " is a duplicate of the Hospitality guest comp " + actual[i] + ".";
                    }
                    continue;
                }
                return "Entry " + i + " is unexpected: " + actual[i] + ".";
            }
            hasVefExtra = vefCount == 1;
            return null;
        }
    }
}
