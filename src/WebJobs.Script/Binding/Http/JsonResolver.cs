using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Script.Binding
{
    public class IgnoreErrorPropertiesResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            List<string> ignoreProperties = new List<string>()
            {
            "Properties",
            "Version"
            };

            if (ignoreProperties.Contains(property.PropertyName))
            {
                property.Ignored = true;
            }
            return property;
        }
    }
}
