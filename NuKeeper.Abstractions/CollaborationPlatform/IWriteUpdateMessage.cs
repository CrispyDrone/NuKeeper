using NuKeeper.Abstractions.RepositoryInspection;
using System.Collections.Generic;

namespace NuKeeper.Abstractions.CollaborationPlatform
{
    public interface IWriteUpdateMessage
    {
        // I don't want Context and CustomTemplate to be part of the interface because an implementation should not be required to use a context or template
        // However this is the least painful option.
        // Normally, the "SettingsContainer" or whatever, should be registered in the DI container so I can take a dependency on it in my constructor
        IDictionary<string, object> Context { get; }
        string CustomTemplate { get; set; }
    }
}
