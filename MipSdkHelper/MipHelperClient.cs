using ConfigApiSharp;
using ConfigApiSharp.ServerCommandService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using VideoOS.ConfigurationAPI;
using VideoOS.Platform;
using VideoOS.Platform.ConfigurationItems;
using VideoOS.Platform.Login;
using IConfigurationService = ConfigApiSharp.ConfigurationApiService.IConfigurationService;

namespace MipSdkHelper
{
    public class MipHelperClient : IDisposable
    {
        private readonly Uri _uri;
        private readonly Dictionary<Guid, Item> _recorderIdToItem = new Dictionary<Guid, Item>();
        private LoginSettings _loginSettings;
        private bool _currentUserAuth;

        public string CurrentToken => _loginSettings.Token;

        private IConfigurationService _configServiceClient;
        public IConfigurationService ConfigApiClient
        {
            get
            {
                if (_serverCommandServiceClient != null) return _configServiceClient;
                var result = ClientFactory.BuildConfigApiClient(_uri.Host, _uri.Port, _currentUserAuth ? UserType.CurrentUser : UserType.Windows, _loginSettings.NetworkCredential.UserName, _loginSettings.NetworkCredential.Password);
                if (!result.Success)
                    throw result.Exception;
                _configServiceClient = result.Client;
                return _configServiceClient;
            }
        }

        private IServerCommandService _serverCommandServiceClient;
        public IServerCommandService ServerCommandServiceClient
        {
            get
            {
                if (_serverCommandServiceClient != null) return _serverCommandServiceClient;
                var result = ClientFactory.BuildServerCommandServiceClient(_uri.Host, _uri.Port, _currentUserAuth ? UserType.CurrentUser : UserType.Windows, _loginSettings.NetworkCredential.UserName, _loginSettings.NetworkCredential.Password);
                if (!result.Success)
                    throw result.Exception;
                _serverCommandServiceClient = result.Client;
                return _serverCommandServiceClient;
            }
        }

        public MipHelperClient(Uri uri)
        {
            _uri = uri;
        }

        public async Task<LoginResult> LoginAsync(NetworkCredential nc = null)
        {
            return await InitAndLogin(_uri, nc);
        }

        public LoginResult Login(NetworkCredential nc = null)
        {
            return LoginAsync(nc).Result;
        }

        private async Task<LoginResult> InitAndLogin(Uri uri, NetworkCredential nc = null)
        {
            if (nc == null) _currentUserAuth = true;
            VideoOS.Platform.SDK.Environment.Initialize();
            VideoOS.Platform.SDK.Environment.AddServer(uri, nc ?? CredentialCache.DefaultNetworkCredentials);

            try
            {
                await Task.Run(() => VideoOS.Platform.SDK.Environment.Login(uri));
            }
            catch (VideoOS.Platform.SDK.Platform.InvalidCredentialsMIPException ex)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "Login failed. Invalid username or password.",
                    Exception = ex
                };
            }
            catch (VideoOS.Platform.SDK.Platform.ServerNotFoundMIPException ex)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "Login failed. Server not found.",
                    Exception = ex
                };
            }
            catch (VideoOS.Platform.SDK.Platform.LoginFailedInternalMIPException ex)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "Login failed. Internal error on Management Server.",
                    Exception = ex
                };
            }
            catch (Exception ex)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = $"An error occurred during login. Error \"{ex.Message}\".",
                    Exception = ex
                };
            }
            if (VideoOS.Platform.SDK.Environment.IsLoggedIn(uri))
            {
                _loginSettings = LoginSettingsCache.GetLoginSettings(EnvironmentManager.Instance.MasterSite);
                return new LoginResult
                {
                    Success = true,
                    Message = "Logged in"
                };
            }
            else
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "Not logged in"
                };
            }
        }

        public IEnumerable<RecordingServer> GetRecordingServers()
        {
            var rsFolder = new RecordingServerFolder(Configuration.Instance.ServerFQID.ServerId,
                $"/{ItemTypes.RecordingServerFolder}");

            // probably shouldn't do this foreach here, but I want to pre-cache
            // the Item objects for the Recording Servers in the background to
            // minimize the amount of time between receiving an event and generating
            // an alarm based on the source Recording Server. I need the FQID.
            foreach (var rs in rsFolder.RecordingServers)
            {
                Task.Run(() => GetRecordingServerItemByGuid(new Guid(rs.Id)));
            }
            
            return rsFolder.RecordingServers.ToList();
        }

        public Item GetRecordingServerItemByGuid(Guid id)
        {
            lock (_recorderIdToItem)
            {
                if (_recorderIdToItem.ContainsKey(id))
                    return _recorderIdToItem[id];

                var item = Configuration.Instance.GetItem(id, Kind.Server);
                _recorderIdToItem.Add(id, item);
                return item;
            }
        }

        public async Task<IEnumerable<MarkedData>> GetMarkedData()
        {
            var markedData = new List<MarkedData>();
            var pageIndex = 0;
            while (true)
            {
                MarkedData[] result = null;
                await Task.Run(() => 
                    result = ServerCommandServiceClient.MarkedDataSearch(CurrentToken, new Guid[] { }, null, null,
                    DateTime.MinValue, DateTime.UtcNow.AddSeconds(-10), DateTime.MinValue,
                    DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue,
                    pageIndex, 100, SortOrderOption.CreateTime, true)
                    );
                markedData.AddRange(result);
                if (result.Length >= 100)
                {
                    pageIndex++;
                    continue;
                }

                break;
            }

            return markedData;
        }

        public IEnumerable<MarkedData> EnumerateMarkedData()
        {
            var pageIndex = 0;
            while (true)
            {
                var result = ServerCommandServiceClient.MarkedDataSearch(_loginSettings.Token, new Guid[] { }, null, null,
                    DateTime.MinValue, DateTime.UtcNow.AddSeconds(-10), DateTime.MinValue,
                    DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue,
                    pageIndex, 100, SortOrderOption.RetentionExpireTime, false);
                foreach (var t in result)
                {
                    yield return t;
                }
                if (result.Length >= 100)
                {
                    pageIndex++;
                    continue;
                }

                break;
            }
        }

        public void Dispose()
        {
            try
            {
                _serverCommandServiceClient?.Logout(_loginSettings.InstanceGuid, _loginSettings.Token);
                VideoOS.Platform.SDK.Environment.Logout(_uri);
                VideoOS.Platform.SDK.Environment.RemoveAllServers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}
