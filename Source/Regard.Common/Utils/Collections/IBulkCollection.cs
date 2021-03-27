using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.Utils.Collections
{
    public interface IBulkCollection<T>
    {
        public void AddRange(IEnumerable<T> range);

        public void BeginBatch();

        public void EndBatch();
    }
}
