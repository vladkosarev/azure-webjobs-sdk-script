﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Script.Binding
{
    public static class HttpExtensions
    {
        public static string Serialize(this HttpResponseMessage response)
        {
            return SerializeObject(response);
        }

        public static string Serialize(this HttpRequestMessage request)
        {
            return SerializeObject(request);
        }

        public static void Deserialize(this HttpRequestMessage request, string value)
        {
            JToken jToken = null;

            try
            {
                jToken = JToken.Parse(value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Invalid Json object! " + ex.Message);
            }
            request.Deserialize(jToken);
        }

        public static void Deserialize(this HttpRequestMessage request, JToken jsonToken)
        {
            if (jsonToken["Method"] != null)
            {
                request.Method = JsonConvert.DeserializeObject<HttpMethod>(jsonToken["Method"].ToString());
            }

            if (jsonToken["RequestUri"] != null)
            {
                Uri uri = null;
                if (Uri.TryCreate(jsonToken["RequestUri"].ToString(), UriKind.Absolute, out uri))
                {
                    request.RequestUri = uri;
                }
            }

            var content = jsonToken["Content"];
            if (content != null && content.HasValues)
            {
                DeserializeContent(request, content);
            }

            if (jsonToken["Headers"] != null && jsonToken["Headers"].HasValues)
            {
                foreach (var header in jsonToken["Headers"].Children())
                {
                    AddHeader(request.Headers, header);
                }
            }
        }

        public static void Deserialize(this HttpResponseMessage res, string response)
        {
            JObject jObject = null;

            try
            {
                jObject = JObject.Parse(response);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Invalid Json object! " + ex.Message);
            }

            HttpStatusCode statusCode;
            if (jObject["StatusCode"] != null && Enum.TryParse(jObject["StatusCode"].ToString(), out statusCode))
            {
                res.StatusCode = statusCode;
            }

            if (jObject["ReasonPhrase"] != null)
            {
                res.ReasonPhrase = jObject["ReasonPhrase"].ToString();
            }

            var content = jObject["Content"];
            if (content != null && content.HasValues)
            {
                DeserializeContent(res, content);
            }

            if (jObject["Headers"] != null && jObject["Headers"].HasValues)
            {
                foreach (var header in jObject["Headers"].Children())
                {
                    AddHeader(res.Headers, header);
                }
            }

            if (jObject["RequestMessage"] != null && jObject["RequestMessage"].HasValues)
            {
                res.RequestMessage = new HttpRequestMessage();
                res.RequestMessage.Deserialize(jObject["RequestMessage"]);
            }
        }

        private static void DeserializeContent(dynamic req, JToken content)
        {
            var headers = new List<JToken>();

            MediaTypeHeaderValue contentType = null;
            string charSet = null;

            if (content["Headers"] != null && content["Headers"].HasValues)
            {
                foreach (var header in content["Headers"].Children())
                {
                    if (header["Key"] != null && header["Key"].ToString().IndexOf("Content-Type", 0, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (header["Value"] != null && header["Value"].HasValues)
                        {
                            // only getting the first value for content-type, if more they are invalid
                            var contentTypeHeader = header["Value"][0].ToString().Split(';');

                            // ignore invalid content-types
                            if (!MediaTypeHeaderValue.TryParse(contentTypeHeader[0], out contentType))
                            {
                                continue;
                            }

                            if (contentTypeHeader.Length > 1 && contentTypeHeader[1].Trim().StartsWith("charset", StringComparison.OrdinalIgnoreCase))
                            {
                                charSet = contentTypeHeader[1].Trim().Replace("charset=", "");
                            }
                        }
                        continue;
                    }
                    headers.Add(header);
                }
            }

            if (content["Content"] != null)
            {
                if (ShouldUseBase64Encoding(contentType))
                {
                    req.Content = new ByteArrayContent(Convert.FromBase64String(content["Content"].ToString()));
                }
                else
                {
                    req.Content = new StringContent(content["Content"].ToString(), GetCurrentEncoding(charSet));
                }
            }
            else
            {
                req.Content = new StringContent(string.Empty);
            }

            if (contentType != null)
            {
                req.Content.Headers.ContentType = contentType;
                if (!string.IsNullOrWhiteSpace(charSet))
                {
                    req.Content.Headers.ContentType.CharSet = charSet;
                }
            }

            foreach (var header in headers)
            {
                AddHeader(req.Content.Headers, header);
            }
        }

        private static string SerializeObject(object obj)
        {
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver = new IgnoreErrorPropertiesResolver(),
                Converters = new List<JsonConverter>() { new HttpContentJsonConverter() }
            };

            string objStr = JsonConvert.SerializeObject(
                obj,
                settings
            );

            return objStr;
        }

        private static void AddHeader(dynamic req, JToken header)
        {
            if (header["Key"] == null)
            {
                return;
            }

            if (header["Value"] != null && header["Value"].HasValues)
            {
                if (header["Value"].Children().Count() > 1)
                {
                    List<string> headerValues = new List<string>();
                    foreach (var value in header["Value"].Children())
                    {
                        headerValues.Add(value.ToString());
                    }
                    req.TryAddWithoutValidation(header["Key"].ToString(), headerValues);
                }
                else
                {
                    req.TryAddWithoutValidation(header["Key"].ToString(), header["Value"].Children().FirstOrDefault().ToString());
                }
            }
            else
            {
                req.TryAddWithoutValidation(header["Key"].ToString(), string.Empty);
            }
        }

        private static bool ShouldUseBase64Encoding(MediaTypeHeaderValue contentType)
        {
            string[] textMediaTypes = {
                "application/x-javascript",
                "application/javascript",
                "application/json",
                "application/xml",
                "application/xhtml+xml",
                "application/x-www-form-urlencoded"
                };

            bool isTextData = false;
            if (contentType != null)
            {
                isTextData = contentType.MediaType != null &&
                    (contentType.MediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                    textMediaTypes.Any(s => string.Equals(s, contentType.MediaType, StringComparison.OrdinalIgnoreCase)));
            }
            return !isTextData;
        }

        public static Encoding GetCurrentEncoding(HttpContentHeaders headers)
        {
            if (headers != null &&
                headers.ContentType != null)
            {
                return GetCurrentEncoding(headers.ContentType.CharSet);
            }

            return Encoding.UTF8;
        }

        private static Encoding GetCurrentEncoding(string charSet)
        {
            Encoding encoding;
            try
            {
                if (!string.IsNullOrWhiteSpace(charSet))
                {
                    encoding = Encoding.GetEncoding(charSet);
                }
                else
                {
                    encoding = Encoding.UTF8;
                }
            }
            catch (ArgumentException)
            {
                //Invalid char set value, defaulting to UTF8
                encoding = Encoding.UTF8;
            }

            return encoding;
        }
    }
}