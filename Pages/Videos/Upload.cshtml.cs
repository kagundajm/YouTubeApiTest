using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Filters;
using Google.Apis.YouTube.v3.Data;
using System.Threading.Tasks;
using System.Text.Json;
using Google.Apis.Auth.OAuth2.Responses;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System.Reflection;
using Google.Apis.Upload;

namespace YouTubeApiTest.Pages.Videos
{
  public class UploadModel : PageModel
  {
    const string TOKEN_FILE = "youtube_token.json";

    private ILogger<UploadModel> _logger;
    private IHttpClientFactory _clientFactory;
    private IWebHostEnvironment _hostingEnvironment;
    private string _tempFilePath;

    [BindProperty]
    public VideoUploadModel VideoUpload { get; set; }

    public UploadModel(ILogger<UploadModel> logger, IHttpClientFactory clientFactory, IWebHostEnvironment hostingEnvironment)
    {
      _logger = logger;
      _clientFactory = clientFactory;
      _hostingEnvironment = hostingEnvironment;
    }

    public void OnGet()
    {
      string webRootPath = _hostingEnvironment.WebRootPath;
      string contentRootPath = _hostingEnvironment.ContentRootPath;

      _logger.LogInformation("||===>>> _hostingEnvironment.WebRootPath: {webRootPath}", webRootPath);
      _logger.LogInformation("||===>>> _hostingEnvironment.ContentRootPath: {contentRootPath}", contentRootPath);
    }
    public async void OnPostAsync()
    {
      var video = GetVideoData(VideoUpload.Title, VideoUpload.Description);

      var user = OAuthClientInfo.LoadClientSecretsInfo();

      if (!System.IO.File.Exists(TOKEN_FILE))
      {
        throw new SystemException("missing access token file. Request for authorization code before uploading a video");
      }

      var tokenResponse = await FetchToken(user);

      var youTubeService = FetchYouTubeService(tokenResponse, user.ClientId, user.ClientSecret);

      using (var fileStream = new FileStream(_tempFilePath, FileMode.Open))
      {
        var videosInsertRequest = youTubeService.Videos.Insert(video, "snippet, status", fileStream, "video/*");
        videosInsertRequest.ProgressChanged += VideoUploadProgressChanged;
        videosInsertRequest.ResponseReceived += VideoUploadResponseReceived;

        await videosInsertRequest.UploadAsync();
      }
    }
    void VideoUploadResponseReceived(Video video)
    {
      _logger.LogInformation("\n||==>Video id '{video.Id)}' was successfully uploaded.\n", video.Id);

      var file = new FileInfo(_tempFilePath);
      file.Delete();
    }

    void VideoUploadProgressChanged(IUploadProgress progress)
    {
      switch (progress.Status)
      {
        case UploadStatus.Uploading:
          _logger.LogInformation("||==> Uploading video: {progress.BytesSent} bytes sent.", progress.BytesSent);
          break;

        case UploadStatus.Failed:
          _logger.LogError("||==> An error prevented the upload from completing.{progress.Exception}", progress.Exception);

          var file = new FileInfo(_tempFilePath);
          file.Delete();
          break;
      }
    }
    private YouTubeService FetchYouTubeService(TokenResponse tokenResponse, string clientId, string clientSecret)
    {
      var initializer = new GoogleAuthorizationCodeFlow.Initializer
      {
        ClientSecrets = new ClientSecrets
        {
          ClientId = clientId,
          ClientSecret = clientSecret
        }
      };

      var credentials = new UserCredential(new GoogleAuthorizationCodeFlow(initializer), "user", tokenResponse);
      var youtubeService = new YouTubeService(new BaseClientService.Initializer()
      {
        HttpClientInitializer = credentials,
        ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
      });

      return youtubeService;
    }

    private async Task<TokenResponse> FetchToken(OAuthUserCredential user)
    {
      var token = JsonSerializer.Deserialize<Token>(System.IO.File.ReadAllText(TOKEN_FILE));

      var isValid = await IsValid(token.AccessToken);
      if (!isValid)
      {
        token = await RefreshToken(user, token.RefreshToken);
      }

      var tokenResponse = new TokenResponse
      {
        AccessToken = token.AccessToken,
        RefreshToken = token.RefreshToken,
        Scope = token.Scope,
        TokenType = token.TokenType
      };

      return tokenResponse;
    }
    private async Task<Token> RefreshToken(OAuthUserCredential user, string refreshToken)
    {
      Token token = null;

      var payload = new Dictionary<string, string>
            {
              { "client_id" , user.ClientId } ,
              { "client_secret" , user.ClientSecret } ,
              { "refresh_token" , refreshToken } ,
              { "grant_type" , "refresh_token" }
            };

      var content = new FormUrlEncodedContent(payload);

      var client = _clientFactory.CreateClient();
      var response = await client.PostAsync(user.TokenUri, content);
      response.EnsureSuccessStatusCode();

      if (response.IsSuccessStatusCode)
      {
        var jsonResponse = await response.Content.ReadAsStringAsync();

        token = JsonSerializer.Deserialize<Token>(jsonResponse);
        token.RefreshToken = refreshToken;

        var jsonString = JsonSerializer.Serialize(token);

        string fileName = System.IO.Path.Combine(_hostingEnvironment.ContentRootPath, TOKEN_FILE);
        await System.IO.File.WriteAllTextAsync(fileName, jsonString, Encoding.UTF8);
      }

      return token;
    }

    private async Task<bool> IsValid(string accessToken)
    {
      const string TOKEN_INFO_URL = "https://www.googleapis.com/oauth2/v3/tokeninfo?access_token=";

      var url = $"{TOKEN_INFO_URL}{accessToken}";
      var response = await _clientFactory.CreateClient().GetAsync(url);
      var jsonString = await response.Content.ReadAsStringAsync();

      return !jsonString.Contains("error_description");
    }

    private static Video GetVideoData(string title, string description)
    {
      var video = new Video()
      {
        Status = new VideoStatus
        {
          PrivacyStatus = "private", // set to public to make it available to public
          SelfDeclaredMadeForKids = false,
          PublishAt = "2020-08-20"  // can only be set if privacy status of video is private.
        },
        Snippet = new VideoSnippet
        {
          CategoryId = "28", // See https://developers.google.com/youtube/v3/docs/videoCategories/list
          Title = title,
          Description = description,
          Tags = new string[] { "Construction business", "construction in kenya", "construction management", "construction technology" },
        }
      };
      return video;
    }

    public void OnGetRequestCode()
    {
      const string OAUTH_URL = "https://accounts.google.com/o/oauth2/v2/auth";

      if (System.IO.File.Exists(TOKEN_FILE))
      {
        return;
      }

      var user = OAuthClientInfo.LoadClientSecretsInfo();
      var redirectUri = user.RedirectUris.FirstOrDefault();

      if (redirectUri == null)
      {
        throw new SystemException("Missing redirect url in user credentials");
      }

      var queryParams = new Dictionary<string, string>
            {
                 { "client_id", user.ClientId },
                 { "scope", YouTubeService.Scope.YoutubeUpload },
                 { "response_type", "code" },
                 { "redirect_uri", redirectUri  },
                 { "access_type", "offline" }
            };

      var newUrl = QueryHelpers.AddQueryString(OAUTH_URL, queryParams);

      base.Response.Redirect(newUrl);
    }

    public void OnGetAuthorize()
    {
      var request = Request;

      if (request.Query.Keys.Contains("error"))
      {
        var error = QueryHelpers.ParseQuery(request.QueryString.Value)
                    .FirstOrDefault(x => x.Key == "error").Value;
        _logger.LogError("||==> OnGetAuthorize: {error}", error);
      }

      if (request.Query.Keys.Contains("code"))
      {
        var values = QueryHelpers.ParseQuery(request.QueryString.Value);
        var code = values.FirstOrDefault(x => x.Key == "code").Value;
        _logger.LogInformation("||==> authorization code granted: {code} ", code);

        ExchangeCodeForTokenAsync(code);
        _logger.LogInformation("||==> Token exchanged with access token");
      }
    }

    private async void ExchangeCodeForTokenAsync(string code)
    {
      var user = OAuthClientInfo.LoadClientSecretsInfo();
      var redirectUri = user.RedirectUris.FirstOrDefault();

      if (string.IsNullOrWhiteSpace(redirectUri))
      {
        throw new SystemException("Missing redirect url in user credentials");
      }

      var payload = new Dictionary<string, string>
            {
              { "code" , code } ,
              { "client_id" , user.ClientId } ,
              { "client_secret" , user.ClientSecret } ,
              { "redirect_uri" , redirectUri } ,
              { "grant_type" , "authorization_code" }
            };

      var content = new FormUrlEncodedContent(payload);

      var client = _clientFactory.CreateClient();
      var response = await client.PostAsync(user.TokenUri, content);
      response.EnsureSuccessStatusCode();

      if (response.IsSuccessStatusCode)
      {
        var jsonString = await response.Content.ReadAsStringAsync();

        string fileName = System.IO.Path.Combine(_hostingEnvironment.ContentRootPath, TOKEN_FILE);
        await System.IO.File.WriteAllTextAsync(fileName, jsonString, Encoding.UTF8);
      }
    }

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
      if (context.HandlerMethod.HttpMethod.ToLower().Equals("post") && this.ModelState.IsValid)
      {
        _tempFilePath = Path.GetTempFileName();

        using (var fileStream = new FileStream(_tempFilePath, FileMode.Create))
        {
          this.VideoUpload.VideoFile.CopyTo(fileStream);
        }
        _logger.LogInformation("||==> Temporary video file created: {_tempFilePath}", _tempFilePath);
      }
    }
  }


  public class VideoUploadModel
  {
    public IFormFile VideoFile { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
  }

  public class Token
  {
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public long? ExpiresInSeconds { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }

  }
}