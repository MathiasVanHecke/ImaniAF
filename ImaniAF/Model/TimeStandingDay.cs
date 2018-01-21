using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaniAF.Model
{
    public class TimeStandingDay
    {
        [JsonProperty("hour")]
        public int Hour { get; set; }
        [JsonProperty("timestandingseconds")]
        public Double TimeStandingSeconds { get; set; }
    }
}
