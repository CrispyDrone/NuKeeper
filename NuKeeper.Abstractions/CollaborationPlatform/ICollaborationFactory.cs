using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace NuKeeper.Abstractions.CollaborationPlatform
{
    public interface ICollaborationFactory
    {
        Task<ValidationResult> Initialise(
            Uri apiUri,
            string token,
            ForkMode? forkModeFromSettings,
            Platform? platformFromSettings,
            string commitTemplate = null,
            string pullrequestTitleTemplate = null,
            string pullrequestBodyTemplate = null,
            IDictionary<string, object> templateContext = null
        );
        CollaborationPlatformSettings Settings { get; }
        IForkFinder ForkFinder { get; }
        IRepositoryDiscovery RepositoryDiscovery { get; }
        ICollaborationPlatform CollaborationPlatform { get; }
        //TODO: Review this design
        UpdateMessageTemplate CommitTemplate { get; }
        UpdateMessageTemplate PullRequestTitleTemplate { get; }
        UpdateMessageTemplate PullRequestBodyTemplate { get; }
    }
}
