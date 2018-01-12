using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaniAF.Model
{
    public class Track
    { 
        [JsonProperty("userid")]
        public Guid UserId { get; set; }
        [JsonProperty("time")]
        public DateTime Date { get; set; }
        [JsonProperty("isstanding")]
        public int isStanding { get; set; }
        [JsonProperty("macdevice")]
        public String MacDevice { get; set; }
    }
}
