using System;
using System.Collections.Generic;
using System.Text;

namespace AzDoExtensionNews
{
    public class RequestBody
    {
        public string[] assetTypes { get; set; }
        public Filter[] filters { get; set; }
        public int flags { get; set; }

        public static readonly string RawBody = "{\"assetTypes\":[\"Microsoft.VisualStudio.Services.Icons.Default\",\"Microsoft.VisualStudio.Services.Icons.Branding\",\"Microsoft.VisualStudio.Services.Icons.Small\"],\"filters\":[{\"criteria\":[{\"filterType\":8,\"value\":\"Microsoft.VisualStudio.Services\"},{\"filterType\":8,\"value\":\"Microsoft.VisualStudio.Services.Integration\"},{\"filterType\":8,\"value\":\"Microsoft.VisualStudio.Services.Cloud\"},{\"filterType\":8,\"value\":\"Microsoft.TeamFoundation.Server\"},{\"filterType\":8,\"value\":\"Microsoft.TeamFoundation.Server.Integration\"},{\"filterType\":8,\"value\":\"Microsoft.VisualStudio.Services.Cloud.Integration\"},{\"filterType\":8,\"value\":\"Microsoft.VisualStudio.Services.Resource.Cloud\"},{\"filterType\":10,\"value\":\"target:\\\"Microsoft.VisualStudio.Services\\\" target:\\\"Microsoft.VisualStudio.Services.Integration\\\" target:\\\"Microsoft.VisualStudio.Services.Cloud\\\" target:\\\"Microsoft.TeamFoundation.Server\\\" target:\\\"Microsoft.TeamFoundation.Server.Integration\\\" target:\\\"Microsoft.VisualStudio.Services.Cloud.Integration\\\" target:\\\"Microsoft.VisualStudio.Services.Resource.Cloud\\\" \"},{\"filterType\":12,\"value\":\"37888\"}],\"direction\":2,\"pageSize\":54,\"pageNumber\":2,\"sortBy\":10,\"sortOrder\":0,\"pagingToken\":null}],\"flags\":870}";

        public static readonly string RawBody2 = "{\"assetTypes\":],\",{\"filterType\":12,\"value\":\"37888\"}],\"direction\":2,\"pageSize\":5000,\"pageNumber\":0,\"sortBy\":10,\"sortOrder\":0,\"pagingToken\":null}],\"flags\":870}";

        public static RequestBody GetDefault(int pageNumber, int pageSize)
        {
            var body = new RequestBody
            {
                assetTypes = new string[3]
                {
                    "\"Microsoft.VisualStudio.Services.Icons.Default\"",
                    "\"Microsoft.VisualStudio.Services.Icons.Branding\"",
                    "\"Microsoft.VisualStudio.Services.Icons.Small\""
                },
                filters = new Filter[1]
                {
                    new Filter {
                        criteria = new Criterion[9]
                        {
                          new Criterion { filterType = 8, value = "Microsoft.VisualStudio.Services" }  ,
                          new Criterion { filterType = 8, value = "Microsoft.VisualStudio.Services.Integration" }  ,
                          new Criterion { filterType = 8, value = "Microsoft.VisualStudio.Services.Cloud" } ,
                          new Criterion { filterType = 8, value = "Microsoft.TeamFoundation.Server" }  ,
                          new Criterion { filterType = 8, value = "Microsoft.TeamFoundation.Server.Integration" }  ,
                          new Criterion { filterType = 8, value = "Microsoft.VisualStudio.Services.Cloud.Integration" }  ,
                          new Criterion { filterType = 8, value = "Microsoft.VisualStudio.Services.Resource.Cloud" }  ,
                          new Criterion { filterType = 10, value = "\"target:\\\"Microsoft.VisualStudio.Services\\\" target:\\\"Microsoft.VisualStudio.Services.Integration\\\" target:\\\"Microsoft.VisualStudio.Services.Cloud\\\" target:\\\"Microsoft.TeamFoundation.Server\\\" target:\\\"Microsoft.TeamFoundation.Server.Integration\\\" target:\\\"Microsoft.VisualStudio.Services.Cloud.Integration\\\" target:\\\"Microsoft.VisualStudio.Services.Resource.Cloud\\\"" }  ,
                          new Criterion { filterType = 12, value= "37888"}
                        },
                        direction = 2,
                        pageNumber = pageNumber,
                        pageSize = pageSize,
                        sortBy = 10,
                        sortOrder = 0
                    }
                },
                flags = 870
            };

            return body;
        }
    }

    public class Filter
    {
        public Criterion[] criteria { get; set; }
        public int direction { get; set; }
        public int pageSize { get; set; }
        public int pageNumber { get; set; }
        public int sortBy { get; set; }
        public int sortOrder { get; set; }
        public object pagingToken { get; set; }
    }

    public class Criterion
    {
        public int filterType { get; set; }
        public string value { get; set; }
    }
}
