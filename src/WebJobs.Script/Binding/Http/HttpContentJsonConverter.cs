using System;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Script.Binding
{
    public class HttpContentJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(StringContent) || objectType == typeof(StreamContent) || objectType == typeof(ByteArrayContent))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Not needed            
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string str = string.Empty;
            HttpContentHeaders headers = null;

            var field = typeof(ByteArrayContent).GetField("content", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                field = typeof(ByteArrayContent).GetField("_content", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (value is StringContent)
            {
                var content = (StringContent)value;
                headers = content.Headers;

                var encoding = HttpExtensions.GetCurrentEncoding(headers);

                str = encoding.GetString((byte[])field.GetValue(content));
            }
            else if (value is ByteArrayContent)
            {
                var content = (ByteArrayContent)value;
                headers = content.Headers;
                str = Convert.ToBase64String((byte[])field.GetValue(content));
            }
            else
            {
                var content = (StreamContent)value;
                headers = content.Headers;
                var bytes = content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                str = Convert.ToBase64String(bytes);
            }

            writer.WriteStartObject();
            writer.WritePropertyName("Content");
            writer.WriteValue(str);
            writer.WritePropertyName("Headers");
            writer.WriteStartArray();

            foreach (var header in headers)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Key");
                writer.WriteValue(header.Key);
                writer.WritePropertyName("Value");
                writer.WriteStartArray();
                foreach (var v in header.Value)
                {
                    writer.WriteValue(v);
                }
                writer.WriteEnd();
                writer.WriteEndObject();
            }

            writer.WriteEnd();
            writer.WriteEndObject();
        }
    }
}
