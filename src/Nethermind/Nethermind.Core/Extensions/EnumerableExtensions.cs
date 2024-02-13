// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Linq;

namespace Nethermind.Core.Extensions
{
    public static class EnumerableExtensions
    {
        public static ISet<T> AsSet<T>(this IEnumerable<T> enumerable) =>
            enumerable is ISet<T> set ? set : enumerable.ToHashSet();

        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self) =>
            self.Select((item, index) => (item, index));
    }
}
