/*
 * ═══════════════════════════════════════════════════════════════
 *                          UNITY FORGE
 *                    REST API Action Package
 * ═══════════════════════════════════════════════════════════════
 * 
 * Author: Unity Forge
 * Github: https://github.com/unityforgedev
 * 
 */



using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Network")]
    [Tooltip("Performs an OPTIONS request to a REST API endpoint. Returns allowed HTTP methods and other options.")]
    public class APIOptions : FsmStateAction
    {
        [Tooltip("Full URL or combined with Base URL + Endpoint")]
        public FsmString url;

        [Tooltip("Base URL (optional)")]
        public FsmString baseUrl;

        [Tooltip("Endpoint path to append to base URL")]
        public FsmString endpointPath;

        [Title("Authentication")]
        [Tooltip("Authentication type")]
        public AuthType authType = AuthType.None;

        [Tooltip("Bearer token or API key")]
        public FsmString authToken;

        [Tooltip("Username for Basic Auth")]
        public FsmString username;

        [Tooltip("Password for Basic Auth")]
        public FsmString password;

        [Tooltip("Custom auth header name")]
        public FsmString customAuthHeader;

        [Title("Headers & Parameters")]
        [Tooltip("Custom headers (format: Key:Value, one per line)")]
        [UIHint(UIHint.TextArea)]
        public FsmString customHeaders;

        [Tooltip("Query parameters (format: Key=Value, one per line)")]
        [UIHint(UIHint.TextArea)]
        public FsmString queryParameters;

        [Tooltip("Accept header")]
        public FsmString acceptHeader;

        [Tooltip("User-Agent header")]
        public FsmString userAgent;

        [Title("Advanced Options")]
        [Tooltip("Request timeout in seconds")]
        public FsmFloat timeout = 30f;

        [Tooltip("Follow redirects automatically")]
        public FsmBool followRedirects = true;

        [Tooltip("Maximum retry attempts on failure")]
        public FsmInt maxRetries = 0;

        [Tooltip("Delay between retries in seconds")]
        public FsmFloat retryDelay = 1f;

        [Title("Response Storage")]
        [Tooltip("Store raw response body")]
        [UIHint(UIHint.Variable)]
        public FsmString responseBody;

        [Tooltip("Store HTTP status code")]
        [UIHint(UIHint.Variable)]
        public FsmInt statusCode;

        [Tooltip("Store status message")]
        [UIHint(UIHint.Variable)]
        public FsmString statusMessage;

        [Tooltip("Store error message if request fails")]
        [UIHint(UIHint.Variable)]
        public FsmString errorMessage;

        [Tooltip("Store response time in milliseconds")]
        [UIHint(UIHint.Variable)]
        public FsmFloat responseTime;

        [Tooltip("Store response headers as formatted string")]
        [UIHint(UIHint.Variable)]
        public FsmString responseHeaders;

        [Tooltip("Store allowed HTTP methods (e.g., GET, POST, PUT)")]
        [UIHint(UIHint.Variable)]
        public FsmString allowedMethods;

        [Tooltip("Store allowed headers")]
        [UIHint(UIHint.Variable)]
        public FsmString allowedHeaders;

        [Tooltip("Store max age for CORS preflight")]
        [UIHint(UIHint.Variable)]
        public FsmString maxAge;

        [Title("Events")]
        [Tooltip("Event sent on successful response (200-299)")]
        public FsmEvent successEvent;

        [Tooltip("Event sent on client error (400-499)")]
        public FsmEvent clientErrorEvent;

        [Tooltip("Event sent on server error (500-599)")]
        public FsmEvent serverErrorEvent;

        [Tooltip("Event sent on network/connection error")]
        public FsmEvent networkErrorEvent;

        [Tooltip("Event sent on timeout")]
        public FsmEvent timeoutEvent;

        [Title("Debug")]
        [Tooltip("Log request details to console")]
        public FsmBool logRequest = false;

        [Tooltip("Log response details to console")]
        public FsmBool logResponse = false;

        [Tooltip("Enable verbose debug logging")]
        public FsmBool debugMode = false;

        private float startTime;
        private int retryCount = 0;

        public enum AuthType
        {
            None,
            BearerToken,
            APIKey,
            BasicAuth,
            CustomHeader
        }

        public override void Reset()
        {
            url = null;
            baseUrl = null;
            endpointPath = null;
            authType = AuthType.None;
            authToken = null;
            username = null;
            password = null;
            customAuthHeader = null;
            customHeaders = null;
            queryParameters = null;
            acceptHeader = "application/json";
            userAgent = "Unity PlayMaker API Options";
            timeout = 30f;
            followRedirects = true;
            maxRetries = 0;
            retryDelay = 1f;
            responseBody = null;
            statusCode = null;
            statusMessage = null;
            errorMessage = null;
            responseTime = null;
            responseHeaders = null;
            allowedMethods = null;
            allowedHeaders = null;
            maxAge = null;
            successEvent = null;
            clientErrorEvent = null;
            serverErrorEvent = null;
            networkErrorEvent = null;
            timeoutEvent = null;
            logRequest = false;
            logResponse = false;
            debugMode = false;
        }

        public override void OnEnter()
        {
            startTime = Time.realtimeSinceStartup;
            retryCount = 0;
            Fsm.Owner.StartCoroutine(SendOptionsRequest());
        }

        private IEnumerator SendOptionsRequest()
        {
            string finalUrl = BuildUrl();

            if (logRequest.Value || debugMode.Value)
            {
                Debug.Log($"[API OPTIONS] Request to: {finalUrl}");
            }

            UnityWebRequest request = new UnityWebRequest(finalUrl, "OPTIONS");
            request.downloadHandler = new DownloadHandlerBuffer();

            SetupHeaders(request);
            SetupAuthentication(request);

            if (timeout.Value > 0)
            {
                request.timeout = Mathf.RoundToInt(timeout.Value);
            }

            request.redirectLimit = followRedirects.Value ? 32 : 0;

            if (debugMode.Value)
            {
                LogRequestDetails(request);
            }

            yield return request.SendWebRequest();

            float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
            if (!responseTime.IsNone)
            {
                responseTime.Value = elapsed;
            }

            bool isNetworkError = request.result == UnityWebRequest.Result.ConnectionError;
            bool isTimeout = request.result == UnityWebRequest.Result.ConnectionError &&
                           request.error != null && request.error.Contains("timeout");

            if (request.result == UnityWebRequest.Result.Success ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                HandleResponse(request);
            }
            else if (isTimeout)
            {
                HandleTimeout(request);
            }
            else if (isNetworkError)
            {
                HandleNetworkError(request);
            }
            else
            {
                HandleOtherError(request);
            }

            request.Dispose();
            Finish();
        }

        private string BuildUrl()
        {
            string finalUrl = "";

            if (!string.IsNullOrEmpty(url.Value))
            {
                finalUrl = url.Value;
            }
            else
            {
                if (!string.IsNullOrEmpty(baseUrl.Value))
                {
                    finalUrl = baseUrl.Value.TrimEnd('/');
                }

                if (!string.IsNullOrEmpty(endpointPath.Value))
                {
                    string path = endpointPath.Value.TrimStart('/');
                    finalUrl = string.IsNullOrEmpty(finalUrl) ? path : $"{finalUrl}/{path}";
                }
            }

            if (!string.IsNullOrEmpty(queryParameters.Value))
            {
                string queryString = BuildQueryString();
                char separator = finalUrl.Contains("?") ? '&' : '?';
                finalUrl = $"{finalUrl}{separator}{queryString}";
            }

            return finalUrl;
        }

        private string BuildQueryString()
        {
            var parameters = new List<string>();
            string[] lines = queryParameters.Value.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.Contains("="))
                {
                    string[] parts = trimmed.Split(new[] { '=' }, 2);
                    string key = UnityWebRequest.EscapeURL(parts[0].Trim());
                    string value = parts.Length > 1 ? UnityWebRequest.EscapeURL(parts[1].Trim()) : "";
                    parameters.Add($"{key}={value}");
                }
            }

            return string.Join("&", parameters);
        }

        private void SetupHeaders(UnityWebRequest request)
        {
            // Accept
            if (!string.IsNullOrEmpty(acceptHeader.Value))
            {
                request.SetRequestHeader("Accept", acceptHeader.Value);
            }

            // User-Agent
            if (!string.IsNullOrEmpty(userAgent.Value))
            {
                request.SetRequestHeader("User-Agent", userAgent.Value);
            }

            // Custom headers
            if (!string.IsNullOrEmpty(customHeaders.Value))
            {
                string[] lines = customHeaders.Value.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (trimmed.Contains(":"))
                    {
                        string[] parts = trimmed.Split(new[] { ':' }, 2);
                        string key = parts[0].Trim();
                        string value = parts.Length > 1 ? parts[1].Trim() : "";
                        request.SetRequestHeader(key, value);
                    }
                }
            }
        }

        private void SetupAuthentication(UnityWebRequest request)
        {
            switch (authType)
            {
                case AuthType.BearerToken:
                    if (!string.IsNullOrEmpty(authToken.Value))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {authToken.Value}");
                    }
                    break;

                case AuthType.APIKey:
                    if (!string.IsNullOrEmpty(authToken.Value))
                    {
                        request.SetRequestHeader("X-API-Key", authToken.Value);
                    }
                    break;

                case AuthType.BasicAuth:
                    if (!string.IsNullOrEmpty(username.Value))
                    {
                        string credentials = $"{username.Value}:{password.Value}";
                        string encoded = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
                        request.SetRequestHeader("Authorization", $"Basic {encoded}");
                    }
                    break;

                case AuthType.CustomHeader:
                    if (!string.IsNullOrEmpty(customAuthHeader.Value) && !string.IsNullOrEmpty(authToken.Value))
                    {
                        request.SetRequestHeader(customAuthHeader.Value, authToken.Value);
                    }
                    break;
            }
        }

        private void HandleResponse(UnityWebRequest request)
        {
            int code = (int)request.responseCode;
            string response = request.downloadHandler?.text ?? "";

            if (!statusCode.IsNone)
            {
                statusCode.Value = code;
            }

            if (!statusMessage.IsNone)
            {
                statusMessage.Value = GetStatusMessage(code);
            }

            if (!responseBody.IsNone)
            {
                responseBody.Value = response;
            }

            if (!responseHeaders.IsNone)
            {
                responseHeaders.Value = GetResponseHeaders(request);
            }

            // Extract OPTIONS-specific headers
            var headers = request.GetResponseHeaders();
            if (headers != null)
            {
                if (!allowedMethods.IsNone && headers.ContainsKey("Allow"))
                {
                    allowedMethods.Value = headers["Allow"];
                }

                if (!allowedHeaders.IsNone && headers.ContainsKey("Access-Control-Allow-Headers"))
                {
                    allowedHeaders.Value = headers["Access-Control-Allow-Headers"];
                }

                if (!maxAge.IsNone && headers.ContainsKey("Access-Control-Max-Age"))
                {
                    maxAge.Value = headers["Access-Control-Max-Age"];
                }
            }

            if (logResponse.Value || debugMode.Value)
            {
                Debug.Log($"[API OPTIONS] Response {code}: {response}");
                Debug.Log($"[API OPTIONS] Allowed Methods: {allowedMethods.Value}");
            }

            // Trigger appropriate event
            if (code >= 200 && code < 300)
            {
                Fsm.Event(successEvent);
            }
            else if (code >= 400 && code < 500)
            {
                if (!errorMessage.IsNone)
                {
                    errorMessage.Value = $"Client Error {code}: {request.error}";
                }
                Fsm.Event(clientErrorEvent);
            }
            else if (code >= 500)
            {
                if (!errorMessage.IsNone)
                {
                    errorMessage.Value = $"Server Error {code}: {request.error}";
                }
                Fsm.Event(serverErrorEvent);
            }
        }

        private void HandleTimeout(UnityWebRequest request)
        {
            if (!errorMessage.IsNone)
            {
                errorMessage.Value = "Request timeout";
            }

            if (logResponse.Value || debugMode.Value)
            {
                Debug.LogWarning("[API OPTIONS] Request timeout");
            }

            if (retryCount < maxRetries.Value)
            {
                retryCount++;
                if (debugMode.Value)
                {
                    Debug.Log($"[API OPTIONS] Retrying... Attempt {retryCount}/{maxRetries.Value}");
                }
                Fsm.Owner.StartCoroutine(RetryRequest());
            }
            else
            {
                Fsm.Event(timeoutEvent);
            }
        }

        private void HandleNetworkError(UnityWebRequest request)
        {
            if (!errorMessage.IsNone)
            {
                errorMessage.Value = $"Network Error: {request.error}";
            }

            if (logResponse.Value || debugMode.Value)
            {
                Debug.LogError($"[API OPTIONS] Network Error: {request.error}");
            }

            if (retryCount < maxRetries.Value)
            {
                retryCount++;
                if (debugMode.Value)
                {
                    Debug.Log($"[API OPTIONS] Retrying... Attempt {retryCount}/{maxRetries.Value}");
                }
                Fsm.Owner.StartCoroutine(RetryRequest());
            }
            else
            {
                Fsm.Event(networkErrorEvent);
            }
        }

        private void HandleOtherError(UnityWebRequest request)
        {
            if (!errorMessage.IsNone)
            {
                errorMessage.Value = $"Error: {request.error}";
            }

            if (logResponse.Value || debugMode.Value)
            {
                Debug.LogError($"[API OPTIONS] Error: {request.error}");
            }

            Fsm.Event(networkErrorEvent);
        }

        private IEnumerator RetryRequest()
        {
            yield return new WaitForSeconds(retryDelay.Value);
            yield return SendOptionsRequest();
        }

        private string GetResponseHeaders(UnityWebRequest request)
        {
            var headers = request.GetResponseHeaders();
            if (headers == null || headers.Count == 0)
                return "";

            var sb = new StringBuilder();
            foreach (var header in headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }
            return sb.ToString();
        }

        private string GetStatusMessage(int code)
        {
            switch (code)
            {
                case 200: return "OK";
                case 201: return "Created";
                case 204: return "No Content";
                case 400: return "Bad Request";
                case 401: return "Unauthorized";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 500: return "Internal Server Error";
                case 502: return "Bad Gateway";
                case 503: return "Service Unavailable";
                default: return $"HTTP {code}";
            }
        }

        private void LogRequestDetails(UnityWebRequest request)
        {
            Debug.Log("=== API OPTIONS Request Details ===");
            Debug.Log($"Method: {request.method}");
            Debug.Log($"URL: {request.url}");
            Debug.Log($"Timeout: {request.timeout}s");

            var contentType = request.GetRequestHeader("Content-Type");
            if (!string.IsNullOrEmpty(contentType))
            {
                Debug.Log($"Content-Type: {contentType}");
            }

            Debug.Log("====================================");
        }
    }
}