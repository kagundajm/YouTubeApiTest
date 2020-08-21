using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YouTubeApiTest
{
  public class OAuthClientInfo
  {
    const string CLIENT_SECRETS_FILE = "client_secrets.json";
    private static string _secretsFile;

    public static OAuthUserCredential LoadClientSecretsInfo(string clientSecretsFile = "")
    {
      string clientSecrets;
      _secretsFile = string.IsNullOrWhiteSpace(clientSecretsFile)
            ? CLIENT_SECRETS_FILE
            : clientSecretsFile;
      try
      {
        clientSecrets = File.ReadAllText(_secretsFile);
      }
      catch (SystemException ex)
      {
        throw ex;
      }

      var options = new JsonSerializerOptions { AllowTrailingCommas = true };
      // root credential could either be "web" or "installed" depending on application type
      // but not interested in root credential
      var clientInfo = JsonSerializer.Deserialize<Dictionary<string, OAuthUserCredential>>(clientSecrets, options).Values.SingleOrDefault();

      if (clientInfo == null)
        throw new SystemException($"Missing data or malformed client secrets file '{_secretsFile}'.");

      return clientInfo;
    }
  }

  public class OAuthUserCredential
  {
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; }

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; }

    [JsonPropertyName("auth_uri")]
    public string AuthUri { get; set; }

    [JsonPropertyName("token_uri")]
    public string TokenUri { get; set; }

    [JsonPropertyName("redirect_uris")]
    public string[] RedirectUris { get; set; }
  }
}
