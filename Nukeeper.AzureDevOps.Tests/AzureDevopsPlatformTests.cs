using NSubstitute;
using NUnit.Framework;
using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Logging;
using NuKeeper.AzureDevOps;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Linq;
using NuKeeper.Abstractions;

namespace NuKeeper.AzureDevOps.Tests
{
    public class AzureDevOpsPlatformTests
    {
        const string MyRepository = "MyRepository";
        const string KnownUserId = "26238A59-0F9F-4E03-9BD2-DA419E003FB0";
        const string KnownUser = "nukeeper@nukeeper.nukeeper";
        private INuKeeperLogger _logger;

        [SetUp]
        public void Setup()
        {
            _logger = Substitute.For<INuKeeperLogger>();
        }

        [Test]
        public async Task OpenPullRequest_WithReviewers_CreatesPullRequestWithReviewers()
        {
            var platform = MakeAzureDevopsPlatform();
            var forkData = MakeForkData();
            var pullrequest = MakePullRequestRequest();
            var client = platform.Client as AzureDevOpsRestClientMock;
            pullrequest.Reviewers.Add(new Reviewer { Name = KnownUser});

            await platform.OpenPullRequest(forkData, pullrequest, new List<string>());

            Assert.AreEqual(1, client.AttemptedPullRequest.reviewers.Count());
        }

        [Test]
        public async Task OpenPullRequest_WithReviewers_CreatesPullRequestWithoutReviewersIfIdentitiesCannotBeFetched()
        {
            var platform = MakeAzureDevopsPlatform();
            var forkData = MakeForkData();
            var pullrequest = MakePullRequestRequest();
            var client = platform.Client as AzureDevOpsRestClientMock;
            client.ThrowOnFetchIdentities = true;
            pullrequest.Reviewers.Add(new Reviewer { Name = KnownUser});

            await platform.OpenPullRequest(forkData, pullrequest, new List<string>());

            Assert.AreEqual(0, client.AttemptedPullRequest.reviewers?.Count() ?? 0);
        }

        [Test]
        public async Task OpenPullRequest_WithKnownAndUnknownReviewers_CreatesPullRequestForKnownReviewers()
        {
            var platform = MakeAzureDevopsPlatform();
            var forkData = MakeForkData();
            var pullrequest = MakePullRequestRequest();
            pullrequest.Reviewers.Add(new Reviewer { Name = KnownUser});
            pullrequest.Reviewers.Add(new Reviewer { Name = "unknown@nukeeper.nukeeper" });

            await platform.OpenPullRequest(forkData, pullrequest, new List<string>());

            var attemptedPr = (platform.Client as AzureDevOpsRestClientMock).AttemptedPullRequest;

            Assert.AreEqual(1, attemptedPr.reviewers.Count());
        }

        private AzureDevOpsPlatformStub MakeAzureDevopsPlatform(
            string content = "",
            HttpStatusCode statusCode = HttpStatusCode.OK
        )
        {
            var platform = new AzureDevOpsPlatformStub(_logger, content, statusCode);
            platform.Initialise(
                new AuthSettings(
                    new Uri("https://tfs.mycompany.com/tfs/DefaultCollection"),
                    "PAT"
                )
            );
            return platform;
        }

        private static ForkData MakeForkData()
        {
            return new ForkData(
                new Uri("https://tfs.mycompany.com/tfs/DefaultCollection/MyProject/_git/MyRepository"),
                "MyProject",
                MyRepository
            );
        }

        private static PullRequestRequest MakePullRequestRequest()
        {
            return new PullRequestRequest("", "", "", false);
        }

        class AzureDevOpsPlatformStub : AzureDevOpsPlatform
        {
            private INuKeeperLogger _logger;

            public AzureDevOpsPlatformStub(
                INuKeeperLogger logger,
                string content,
                HttpStatusCode statusCode = HttpStatusCode.OK
            ) : base(logger)
            {
                Content = content;
                StatusCode = statusCode;
                _logger = logger;
            }

            public string Content { get; }
            public HttpStatusCode StatusCode { get; }
            public AzureDevOpsRestClient Client { get; protected set; }

            protected override AzureDevOpsRestClient GetClient(AuthSettings settings)
            {
                var fakeHttpMessageHandler = new FakeHttpMessageHandler(
                    new HttpResponseMessage
                    {
                        StatusCode = StatusCode,
                        Content = new StringContent(Content, Encoding.UTF8, "application/json")
                    }
                );
                var fakeHttpClient = new HttpClient(fakeHttpMessageHandler)
                {
                    BaseAddress = settings.ApiBase
                };

                return Client = new AzureDevOpsRestClientMock(fakeHttpClient, _logger, settings.Token);
            }
        }

        class AzureDevOpsRestClientMock : AzureDevOpsRestClient
        {
            public AzureDevOpsRestClientMock(
                HttpClient client,
                INuKeeperLogger logger,
                string personalAccessToken
            ) : base(client, logger, personalAccessToken) { }

            public bool ThrowOnFetchIdentities { get; set; }

            public PRRequest AttemptedPullRequest { get; private set; }

            public override Task<IEnumerable<AzureRepository>> GetGitRepositories(string projectName)
            {
                return Task.FromResult(
                    new List<AzureRepository>
                    {
                        new AzureRepository { name = MyRepository }
                    }.AsEnumerable()
                );
            }

            public override Task<IEnumerable<WebApiTeam>> GetTeamsAsync(string projectName)
            {
                return Task.FromResult(
                    new List<WebApiTeam>
                    {
                        new WebApiTeam()
                    }.AsEnumerable()
                );
            }

            public override Task<IEnumerable<TeamMember>> GetTeamMembersAsync(string projectName, string teamName)
            {
                return Task.FromResult(
                    new List<TeamMember>
                    {
                        new TeamMember { identity = new IdentityRef { id = KnownUserId } }
                    }.AsEnumerable()
                );
            }

            public override Task<Identity> GetUserAsync(string id)
            {
                if (ThrowOnFetchIdentities)
                    throw new NuKeeperException("Could not fetch identity");

                if (id == KnownUserId)
                {
                    return Task.FromResult(
                        new Identity
                        {
                            properties = new Dictionary<string, object>
                            {
                                { "Mail", KnownUser }
                            }
                        }
                   );
                }

                return Task.FromResult<Identity>(null);
            }

            public override Task<PullRequest> CreatePullRequest(PRRequest request, string projectName, string azureRepositoryId)
            {
                AttemptedPullRequest = request;
                return Task.FromResult(new PullRequest());
            }
        }
    }
}
