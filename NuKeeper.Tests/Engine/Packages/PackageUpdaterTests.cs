using NSubstitute;
using NUnit.Framework;
using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Git;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.NuGet;
using NuKeeper.Abstractions.RepositoryInspection;
using NuKeeper.Engine.Packages;
using NuKeeper.Engine;
using NuKeeper.Update;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NuKeeper.Tests.Engine.Packages
{
    [TestFixture]
    public class PackageUpdaterTests
    {
        private ICollaborationFactory _collaborationFactory;
        private IExistingCommitFilter _existingCommitFilter;
        private IUpdateRunner _localUpdater;
        private INuKeeperLogger _logger;
        private IGitDriver _gitDriver;

        [SetUp]
        public void Initialize()
        {
            _collaborationFactory = Substitute.For<ICollaborationFactory>();
            _existingCommitFilter = Substitute.For<IExistingCommitFilter>();
            _localUpdater = Substitute.For<IUpdateRunner>();
            _logger = Substitute.For<INuKeeperLogger>();
            _gitDriver = Substitute.For<IGitDriver>();

            _existingCommitFilter
                .Filter(
                    Arg.Any<IGitDriver>(),
                    Arg.Any<IReadOnlyCollection<PackageUpdateSet>>(),
                    Arg.Any<string>(),
                    Arg.Any<string>()
                 )
                .Returns(MakePackageUpdateSet());
            _collaborationFactory.CollaborationPlatform
                .PullRequestExists(Arg.Any<ForkData>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(false);
        }

        [Test]
        public async Task MakeUpdatePullRequests_WithReviewers_CreatesPullRequestWithReviewers()
        {
            var packageUpdater = MakePackageUpdater();
            var expectedReviewers =
                new List<string> { "nukeeper@nukeeper.nukeeper", "nukeeper2@nukeeper.nukeeper" };
            var repositoryData = MakeRepositoryData();
            var nugetSources = MakeNugetSources();
            var packageUpdateSet = MakePackageUpdateSet();
            var settings = MakeSettings();
            settings.SourceControlServerSettings.Reviewers =
                new List<string> { "nukeeper@nukeeper.nukeeper", "nukeeper2@nukeeper.nukeeper" };

            var result = await packageUpdater.MakeUpdatePullRequests(
                _gitDriver,
                repositoryData,
                packageUpdateSet,
                nugetSources,
                settings
            );

            await _collaborationFactory
                .CollaborationPlatform
                .Received()
                .OpenPullRequest(
                    Arg.Any<ForkData>(),
                    Arg.Is<PullRequestRequest>(
                        pr => pr.Reviewers
                            .Select(r => r.Name)
                            .All(
                                r => expectedReviewers
                                    .Contains(
                                        r,
                                        StringComparer.InvariantCultureIgnoreCase
                                    )
                            )
                    ),
                    Arg.Any<IEnumerable<string>>()
                );
        }

        private static SettingsContainer MakeSettings()
        {
            return new SettingsContainer
            {
                UserSettings = new UserSettings(),
                BranchSettings = new BranchSettings(),
                SourceControlServerSettings = new SourceControlServerSettings()
            };
        }

        private static IReadOnlyCollection<PackageUpdateSet> MakePackageUpdateSet()
        {
            return new List<PackageUpdateSet> { PackageUpdates.MakeUpdateSet("foo.bar") };
        }

        private static NuGetSources MakeNugetSources()
        {
            return new NuGetSources("");
        }

        private static RepositoryData MakeRepositoryData()
        {
            return new RepositoryData(
                new ForkData(
                    new Uri("http://tfs.mycompany.com/tfs/DefaultCollection/MyProject/_git/MyRepository"),
                    "MyProject",
                    "MyRepository"
                ),
                new ForkData(
                    new Uri("http://tfs.mycompany.com/tfs/DefaultCollection/MyProject/_git/MyRepository"),
                    "MyProject",
                    "MyRepository"
                )
            );
        }

        private PackageUpdater MakePackageUpdater()
        {
            return new PackageUpdater(
                _collaborationFactory,
                _existingCommitFilter,
                _localUpdater,
                _logger
            );
        }
    }
}
