using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaniAF.Model
{
    class RegisterUser
    {
        [JsonProperty("userid")]
        public Guid UserId { get; set; }
        [JsonProperty("created")]
        public DateTime Created { get; set; }
        [JsonProperty("name")]
        public String Name { get; set; }
        [JsonProperty("email")]
        public String Email { get; set; }
        [JsonProperty("password")]
        public String Password { get; set; }
        [JsonProperty("sharekey")]
        public String Sharekey { get; set; }
    }
}
