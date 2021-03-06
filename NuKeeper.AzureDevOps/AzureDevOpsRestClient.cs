using Newtonsoft.Json;
using NuKeeper.Abstractions;
using NuKeeper.Abstractions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NuKeeper.AzureDevOps
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class AzureDevOpsRestClient
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly HttpClient _fallbackClient;
        private readonly HttpClient _client;
        private readonly INuKeeperLogger _logger;

        public AzureDevOpsRestClient(HttpClient client, INuKeeperLogger logger, string personalAccessToken)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _fallbackClient = new HttpClient();
            _logger = logger;
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{string.Empty}:{personalAccessToken}")));
            _fallbackClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _fallbackClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{string.Empty}:{personalAccessToken}")));
        }

        private async Task<T> PostResource<T>(string url, HttpContent content, bool previewApi = false, [CallerMemberName] string caller = null)
        {
            var fullUrl = BuildAzureDevOpsUri(url, previewApi);
            _logger.Detailed($"{caller}: Requesting {fullUrl}");

            var response = await _client.PostAsync(fullUrl, content);
            return await HandleResponse<T>(response, caller);
        }

        private async Task<T> GetResource<T>(string url, bool previewApi = false, bool isAbsoluteUrl = false, [CallerMemberName] string caller = null)
        {
            var fullUrl = BuildAzureDevOpsUri(url, previewApi, isAbsoluteUrl);
            _logger.Detailed($"{caller}: Requesting {fullUrl}");

            HttpResponseMessage response;

            if (isAbsoluteUrl)
            {
                response = await _fallbackClient.GetAsync(fullUrl);
            }
            else
            {
                response = await _client.GetAsync(fullUrl);
            }

            return await HandleResponse<T>(response, caller);
        }

        private async Task<T> HandleResponse<T>(HttpResponseMessage response, [CallerMemberName] string caller = null)
        {
            string msg;

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.Detailed($"Response {response.StatusCode} is not success, body:\n{responseBody}");

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        msg = $"{caller}: Unauthorised, ensure PAT has appropriate permissions";
                        _logger.Error(msg);
                        throw new NuKeeperException(msg);

                    case HttpStatusCode.Forbidden:
                        msg = $"{caller}: Forbidden, ensure PAT has appropriate permissions";
                        _logger.Error(msg);
                        throw new NuKeeperException(msg);

                    default:
                        msg = $"{caller}: Error {response.StatusCode}";
                        _logger.Error($"{caller}: Error {response.StatusCode}");
                        throw new NuKeeperException(msg);
                }
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(responseBody);
            }
            catch (JsonException ex)
            {
                msg = $"{caller} failed to parse json to {typeof(T)}: {ex.Message}";
                _logger.Error(msg);
                throw new NuKeeperException($"Failed to parse json to {typeof(T)}", ex);
            }
        }

        public static Uri BuildAzureDevOpsUri(string path, bool previewApi = false, bool isAbsolute = false)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var separator = path.Contains("?") ? "&" : "?";
            return previewApi
                ? new Uri($"{path}{separator}api-version=4.1-preview.1", isAbsolute ? UriKind.Absolute : UriKind.Relative)
                : new Uri($"{path}{separator}api-version=4.1", isAbsolute ? UriKind.Absolute : UriKind.Relative);
        }

        // documentation is confusing, I think this won't work without memberId or ownerId
        // https://docs.microsoft.com/en-us/rest/api/azure/devops/account/accounts/list?view=azure-devops-rest-6.0
        public Task<Resource<Account>> GetCurrentUser()
        {
            return GetResource<Resource<Account>>("/_apis/accounts");
        }

        public Task<Resource<Account>> GetUserByMail(string email)
        {
            var encodedEmail = HttpUtility.UrlEncode(email);
            return GetResource<Resource<Account>>($"/_apis/identities?searchFilter=MailAddress&filterValue={encodedEmail}");
        }

        public async virtual Task<IEnumerable<Project>> GetProjects()
        {
            var response = await GetResource<ProjectResource>("/_apis/projects");
            return response?.value.AsEnumerable();
        }

        public async virtual Task<IEnumerable<WebApiTeam>> GetTeamsAsync(string projectName)
        {
            var response = await GetResource<Resource<WebApiTeam>>($"_apis/projects/{projectName}/teams");
            return response?.value.AsEnumerable();
        }

        public async virtual Task<IEnumerable<TeamMember>> GetTeamMembersAsync(string projectName, string teamName)
        {
            var response = await GetResource<Resource<TeamMember>>($"_apis/projects/{projectName}/teams/{teamName}/members");
            return response?.value.AsEnumerable();
        }

        public async virtual Task<Identity> GetUserAsync(string id)
        {
            var url = $"_apis/identities/{id}";
            var isAbsoluteUrl = false;

            if (_client.BaseAddress.Host.Contains("dev.azure.com"))
            {
                url = _client.BaseAddress.AbsoluteUri.Replace("dev.azure.com", "vssps.dev.azure.com") + url;
                isAbsoluteUrl = true;
            }

            return await GetResource<Identity>(url, previewApi: false, isAbsoluteUrl: isAbsoluteUrl);
        }

        public async virtual Task<IEnumerable<AzureRepository>> GetGitRepositories(string projectName)
        {
            var response = await GetResource<GitRepositories>($"{projectName}/_apis/git/repositories");
            return response?.value.AsEnumerable();
        }

        public async virtual Task<IEnumerable<GitRefs>> GetRepositoryRefs(string projectName, string repositoryId)
        {
            var response = await GetResource<GitRefsResource>($"{projectName}/_apis/git/repositories/{repositoryId}/refs");
            return response?.value.AsEnumerable();
        }

        //https://docs.microsoft.com/en-us/rest/api/azure/devops/git/pull%20requests/get%20pull%20requests?view=azure-devops-rest-5.0
        public async virtual Task<IEnumerable<PullRequest>> GetPullRequests(
             string projectName,
             string azureRepositoryId,
             string headBranch,
             string baseBranch)
        {
            var encodedBaseBranch = HttpUtility.UrlEncode(baseBranch);
            var encodedHeadBranch = HttpUtility.UrlEncode(headBranch);

            var response = await GetResource<PullRequestResource>($"{projectName}/_apis/git/repositories/{azureRepositoryId}/pullrequests?searchCriteria.sourceRefName={encodedHeadBranch}&searchCriteria.targetRefName={encodedBaseBranch}");

            return response?.value.AsEnumerable();
        }

        public async virtual Task<IEnumerable<PullRequest>> GetPullRequests(string projectName, string repositoryName, string user)
        {
            var response = await GetResource<PullRequestResource>(
                $"{projectName}/_apis/git/repositories/{repositoryName}/pullrequests?searchCriteria.creatorId={user}"
            );

            return response?.value.AsEnumerable();
        }

        public async virtual Task<PullRequest> CreatePullRequest(PRRequest request, string projectName, string azureRepositoryId)
        {
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            return await PostResource<PullRequest>($"{projectName}/_apis/git/repositories/{azureRepositoryId}/pullrequests", content);
        }

        public async virtual Task<LabelResource> CreatePullRequestLabel(LabelRequest request, string projectName, string azureRepositoryId, int pullRequestId)
        {
            var labelContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            return await PostResource<LabelResource>($"{projectName}/_apis/git/repositories/{azureRepositoryId}/pullRequests/{pullRequestId}/labels", labelContent, true);
        }

        public async virtual Task<IEnumerable<string>> GetGitRepositoryFileNames(string projectName, string azureRepositoryId)
        {
            var response = await GetResource<GitItemResource>($"{projectName}/_apis/git/repositories/{azureRepositoryId}/items?recursionLevel=Full");
            return response?.value.Select(v => v.path).AsEnumerable();
        }
    }
}
