using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Common.Providers
{
    public interface IProvider
    {
        /// <summary>
        /// Provider ID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// User friendly name
        /// </summary>
        string Name { get; }

        bool IsInitialized { get; }

        Type ConfigurationType { get; }

        Task Configure(object config);

        void Unconfigure();
    }
}
