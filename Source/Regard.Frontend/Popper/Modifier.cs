using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Popper
{
    public abstract class Modifier
    {
        public abstract string Name { get; }

        public abstract object Options { get; }
    }
}
