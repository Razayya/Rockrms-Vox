using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.razayya.PlanningCenterSDK.Http
{
    internal static class Requesters
    {
        public static Requester StaticApiRequester;
        public static Requester StatusApiRequester;
        public static RateLimitedRequester ServicesAppRequester;
    }
}
