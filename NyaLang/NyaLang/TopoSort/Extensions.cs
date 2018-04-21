using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NyaLang.TopoSort
{
    public static class Extensions
    {
        class DummyEnumerable<T> : IEnumerable<T>
        {
            private readonly Func<IEnumerator<T>> getEnumerator;

            public DummyEnumerable(Func<IEnumerator<T>> getEnumerator)
            {
                this.getEnumerator = getEnumerator;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return getEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public static IEnumerable<TItem> TopoSort<TItem, TKey>(this IEnumerable<TItem> source, Func<TItem, TKey> getKey, Func<TItem, IEnumerable<TKey>> getDependencies)
        {
            var enumerator = new TopoSortEnumerator<TItem, TKey>(source, getKey, getDependencies);
            return new DummyEnumerable<TItem>(() => enumerator);
        }
    }
}