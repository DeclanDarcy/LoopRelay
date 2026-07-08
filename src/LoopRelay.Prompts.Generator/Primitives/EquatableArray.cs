// <auto-authored/> Part of Lib.Prompts.
// An immutable, value-equatable array wrapper.
//
// WHY THIS EXISTS: Incremental source generators cache pipeline outputs by
// VALUE EQUALITY. A raw T[] uses reference equality, so a model containing a
// T[] is treated as "changed" on every edit even when its contents are
// identical, defeating caching and tanking IDE responsiveness. Wrapping array
// fields in EquatableArray<T> restores structural equality so unchanged
// templates are skipped on incremental rebuilds.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Lib.Prompts
{
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
        where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new EquatableArray<T>(Array.Empty<T>());

        private readonly T[] _array;

        public EquatableArray(T[] array) => _array = array ?? Array.Empty<T>();

        public int Count => _array?.Length ?? 0;

        public T this[int index] => _array[index];

        public bool Equals(EquatableArray<T> other)
        {
            var a = _array ?? Array.Empty<T>();
            var b = other._array ?? Array.Empty<T>();
            if (ReferenceEquals(a, b)) return true;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var a = _array;
            if (a is null) return 0;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < a.Length; i++)
                {
                    hash = hash * 31 + a[i].GetHashCode();
                }
                return hash;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            var a = _array ?? Array.Empty<T>();
            for (int i = 0; i < a.Length; i++) yield return a[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
