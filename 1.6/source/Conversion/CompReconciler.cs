using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Mechanoid_Sentience
{
    public static class CompReconciler
    {
        public static List<ThingComp> Reconcile(IList<ThingComp> live, IList<CompProperties> target, Func<CompProperties, ThingComp> create, out List<ThingComp> created)
        {
            created = new List<ThingComp>();
            var pending = new Dictionary<CompSignature, Queue<ThingComp>>();
            for (int i = 0; i < (live?.Count ?? 0); i++)
            {
                ThingComp comp = live[i];
                if (comp == null || comp.props == null)
                {
                    throw new InvalidOperationException("Live comp " + i + " (" + (comp?.GetType().FullName ?? "null") + ") has no comp properties.");
                }
                CompSignature signature = CompSignature.Of(comp.props);
                if (!pending.TryGetValue(signature, out Queue<ThingComp> queue))
                {
                    pending[signature] = queue = new Queue<ThingComp>();
                }
                queue.Enqueue(comp);
            }

            var result = new List<ThingComp>();
            for (int i = 0; i < (target?.Count ?? 0); i++)
            {
                CompProperties props = target[i];
                if (props == null)
                {
                    throw new InvalidOperationException("Target comp " + i + " is null.");
                }
                CompSignature signature = CompSignature.Of(props);
                if (pending.TryGetValue(signature, out Queue<ThingComp> queue) && queue.Count > 0)
                {
                    result.Add(queue.Dequeue());
                    continue;
                }
                if (props.compClass == null || !typeof(ThingComp).IsAssignableFrom(props.compClass))
                {
                    throw new InvalidOperationException("Target comp " + i + " (" + signature + ") has no usable ThingComp class.");
                }
                ThingComp comp = create(props);
                if (comp == null || comp.props != props)
                {
                    throw new InvalidOperationException("Creating target comp " + i + " (" + signature + ") did not produce a comp carrying that property.");
                }
                created.Add(comp);
                result.Add(comp);
            }

            List<string> leftovers = (live ?? new List<ThingComp>()).Where(c => pending[CompSignature.Of(c.props)].Contains(c)).Select(c => CompSignature.Of(c.props).ToString()).ToList();
            if (leftovers.Count > 0)
            {
                throw new InvalidOperationException("Live comps with no counterpart in the sentient def: " + string.Join(", ", leftovers) + ".");
            }
            return result;
        }
    }
}
