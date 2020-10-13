using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Git;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.RepositoryInspection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuKeeper.Engine.Packages
{
    public class ExistingCommitFilter : IExistingCommitFilter
    {
        private readonly CommitUpdateMessageTemplate _commitTemplate;
        private readonly IEnrichContext<PackageUpdateSet, UpdateMessageTemplate> _enricher;
        private readonly INuKeeperLogger _logger;

        // IGitDriver should be taken as constructor dependency
        public ExistingCommitFilter(
            CommitUpdateMessageTemplate commitTemplate,
            IEnrichContext<PackageUpdateSet, UpdateMessageTemplate> enricher,
            INuKeeperLogger logger
        )
        {
            _commitTemplate = commitTemplate;
            _enricher = enricher;
            _logger = logger;
        }

        public async Task<IReadOnlyCollection<PackageUpdateSet>> Filter(IGitDriver git, IReadOnlyCollection<PackageUpdateSet> updates, string baseBranch, string headBranch)
        {
            if (git == null)
            {
                throw new ArgumentNullException(nameof(git));
            }

            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            try
            {
                var filtered = new List<PackageUpdateSet>();
                // commit messages are compared without whitespace because the system tends to add ws.
                var commitMessages = await git.GetNewCommitMessages(baseBranch, headBranch);
                var compactCommitMessages = commitMessages.Select(m => new string(m.Where(c => !char.IsWhiteSpace(c)).ToArray()));

                foreach (var update in updates)
                {
                    _enricher.Enrich(update, _commitTemplate);
                    var updateCommitMessage = _commitTemplate.Output();
                    //TODO: bug was not discovered in tests
                    _commitTemplate.Clear();
                    var compactUpdateCommitMessage = new string(updateCommitMessage.Where(c => !char.IsWhiteSpace(c)).ToArray());

                    // use equality comparer that ignores whitespace instead
                    if (!compactCommitMessages.Contains(compactUpdateCommitMessage))
                    {
                        filtered.Add(update);
                    }

                }
                return filtered;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.Error($"Failed on existing Commit check for {baseBranch} <= {headBranch}", ex);

                return updates;
            }
        }
    }
}
