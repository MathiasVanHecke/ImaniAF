using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaniAF.Model
{
    class Bug
    {
        [JsonProperty("userid")]
        public Guid UserId { get; set; }
        [JsonProperty("time")]
        public DateTime Date { get; set; }
        [JsonProperty("bug")]
        public String BugText { get; set; }
    }
}
