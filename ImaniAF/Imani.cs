using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using ImaniAF.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace ImaniAF
{
    public static class Imani
    {
        //SQL CONNECTIONSTRING
        private static string CONNECTIONSTRING = Environment.GetEnvironmentVariable("ConnectionString");

        #region leeg
        [FunctionName("Imani")]
        public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "HttpTriggerCSharp/name/{name}")]HttpRequestMessage req, string name, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // Fetching the name from the path parameter in the request URL
            return req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
        }
        #endregion

        #region Add user
        [FunctionName("AddUser")]
        public static async System.Threading.Tasks.Task<HttpResponseMessage> AddUser([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "registeruser")]HttpRequestMessage req, TraceWriter log)
        {
            //Inlezen van externe json
            var content = await req.Content.ReadAsStringAsync();
            var user_try = JsonConvert.DeserializeObject<RegisterUser>(content);
            var user_fail = new RegisterUser();
            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "SELECT email FROM [user] WHERE email = @email";
                        command.Parameters.AddWithValue("@email", user_try.Email.ToString());
                        command.CommandText = sql;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            user_try.Email = reader["email"].ToString();
                        }
                        if (reader.HasRows == true)
                        {
                            //Gebruiker bestaat al
                            return req.CreateResponse(HttpStatusCode.OK, user_fail);
                        }
                        else
                        {
                            //Gebruiker toevoegen
                            using (SqlConnection connection2 = new SqlConnection(CONNECTIONSTRING))
                            {
                                connection2.Open();
                                using (SqlCommand command2 = new SqlCommand())
                                {
                                    string sql2 = "INSERT INTO [user] VALUES(@userID,@created, @name, @email, @password,@sharekey, @wantsNotifications)";
                                    command2.CommandText = sql2;
                                    command2.Connection = connection2;
                                    Boolean wantNotifications = true;
                                    String salt = CreateSalt(8);
                                    String hash = GenerateSaltedHash(user_try.Password.ToString(), salt);
                                    command2.Parameters.AddWithValue("@userID", user_try.UserId.ToString());
                                    command2.Parameters.AddWithValue("@created", DateTime.Now);
                                    command2.Parameters.AddWithValue("@name", user_try.Name);
                                    command2.Parameters.AddWithValue("@email", user_try.Email);
                                    command2.Parameters.AddWithValue("@password", salt + ":" + hash);
                                    command2.Parameters.AddWithValue("@sharekey", GenerateSharkey());
                                    command2.Parameters.AddWithValue("@wantsNotifications", wantNotifications);
                                    command2.ExecuteNonQuery();
                                    return req.CreateResponse(HttpStatusCode.OK, user_try);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region Add follower
        [FunctionName("AddFollower")]
        public static HttpResponseMessage AddFollower([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addfollower/{base_userid}/{sharekey}")]HttpRequestMessage req, String base_userID, String sharekey, TraceWriter log)
        {
            RegisterUser user = new RegisterUser();
            try
            {
                //Zoek de gebruiker waar de sharekey over een komt

                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "SELECT * FROM [user] WHERE sharekey = @sharekey";
                        command.Parameters.AddWithValue("@sharekey", sharekey);
                        command.CommandText = sql;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            user.UserId = new Guid(reader["userID"].ToString());
                            user.Name = reader["name"].ToString();
                            user.Email = reader["email"].ToString();
                            user.Sharekey = reader["sharekey"].ToString();
                        }
                        if (reader.HasRows == false)
                        {
                            //User bestaat niet
                            return req.CreateResponse(HttpStatusCode.OK, "User niet gevonden");
                        }
                        else
                        {
                            try
                            {
                                using (SqlConnection connection2 = new SqlConnection(CONNECTIONSTRING))
                                {
                                    connection2.Open();
                                    using (SqlCommand command2 = new SqlCommand())
                                    {
                                        command2.Connection = connection2;
                                        string sql2 = "INSERT INTO [follow_users] VALUES(@userID, @follow_userID)";
                                        command2.CommandText = sql2;
                                        command2.Parameters.AddWithValue("@userID", user.UserId.ToString());
                                        command2.Parameters.AddWithValue("@follow_userID", base_userID);
                                        command2.ExecuteNonQuery();
                                        return req.CreateResponse(HttpStatusCode.OK, user);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
            //Follower toevoegen aan de vorige user.
        }
        #endregion

        #region Add Track
        [FunctionName("AddTrack")]
        public static async System.Threading.Tasks.Task<HttpResponseMessage> AddTrack([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addtrack")]HttpRequestMessage req, TraceWriter log)
        {
            //Inlezen van externe json
            var content = await req.Content.ReadAsStringAsync();
            var track = JsonConvert.DeserializeObject<Track>(content);

            //Schrijven naar database
            using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    string GuidId = Guid.NewGuid().ToString();
                    string sql = "INSERT INTO tracking VALUES(@userID,@time,@isStanding, @macDevice)";

                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@userID", track.UserId);
                    command.Parameters.AddWithValue("@time", track.Date);
                    command.Parameters.AddWithValue("@isStanding", track.isStanding);
                    command.Parameters.AddWithValue("@macDevice", track.MacDevice);
                    
                    command.ExecuteNonQuery();

                    return req.CreateResponse(HttpStatusCode.OK, track);
                }
            }
        }
        #endregion

        #region Get Track
        [FunctionName("GetTimeStandingDay")]
        public static HttpResponseMessage GetTimeStandingDay([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "gettimestandingday/{UserID}/{first_date}/{last_date}")]HttpRequestMessage req, String UserID,String first_date, String last_date, TraceWriter log)
        {
            try
            {
                List<Track> tracks = new List<Track>();
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "SELECT [time],[isStanding] FROM [dbo].[tracking] WHERE [userID] = @userID and [time] between @first_date and @last_date";
                        command.Parameters.AddWithValue("@userID", UserID);
                        command.Parameters.AddWithValue("@first_date", Convert.ToDateTime(first_date));
                        command.Parameters.AddWithValue("@last_date", Convert.ToDateTime(last_date));
                        command.CommandText = sql;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            Track track = new Track();
                            track.Date = Convert.ToDateTime(reader["time"]);
                            track.isStanding = Convert.ToBoolean(reader["isStanding"]);
                            tracks.Add(track);
                        }
                    }
                }
                //var json = JsonConvert.SerializeObject(garbageTypes);
                List<TimeStandingDay> listCalculateTimeStandingDay = CalculateTimeStandingDay(tracks, Convert.ToInt32(Convert.ToDateTime(last_date).Hour));

                return req.CreateResponse(HttpStatusCode.OK, listCalculateTimeStandingDay);
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);

            }
        }
        #endregion

        #region Add Bug
        [FunctionName("AddBug")]
        public static async System.Threading.Tasks.Task<HttpResponseMessage> AddBug([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addbug")]HttpRequestMessage req, TraceWriter log)
        {
            //Inlezen van externe json
            var content = await req.Content.ReadAsStringAsync();
            var bug = JsonConvert.DeserializeObject<Bug>(content);

            //Schrijven naar database
            using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    string sql = "INSERT INTO bug VALUES(@userID,@time,@bugText)";

                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@userID", bug.UserId);
                    command.Parameters.AddWithValue("@time", bug.Date);
                    command.Parameters.AddWithValue("@bugText",bug.BugText);

                    command.ExecuteNonQuery();

                    return req.CreateResponse(HttpStatusCode.OK, bug);
                }
            }
        }
        #endregion

        #region Get Followers
        [FunctionName("GetFollowers")]
        public static HttpResponseMessage GetFollowers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getfollowers/{UserID}")]HttpRequestMessage req, String UserID, TraceWriter log)
        {
            try
            {
                List<RegisterUser> tracks = new List<RegisterUser>();
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "SELECT * FROM [dbo].[follow_users] INNER JOIN [dbo].[user] ON [dbo].[follow_users].userID = [dbo].[user].userID WHERE follow_userID = @follow_userID";
                        command.Parameters.AddWithValue("@follow_userID", UserID);
                        command.CommandText = sql;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            RegisterUser track = new RegisterUser();
                            track.UserId = new Guid(reader["userID"].ToString());
                            track.Name = reader["name"].ToString();
                            track.Email = reader["email"].ToString();
                            track.Password = reader["password"].ToString();
                            track.Sharekey = reader["sharekey"].ToString();
                            tracks.Add(track);
                        }
                    }
                }
                //var json = JsonConvert.SerializeObject(garbageTypes);
                return req.CreateResponse(HttpStatusCode.OK, tracks);
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);

            }
        }

        #endregion

        #region Delete follower
        [FunctionName("DeleteFollower")]
        public static HttpResponseMessage DeleteFollower([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "deletefollower/{userid}/{delete_follower}")]HttpRequestMessage req, String userid, String delete_follower, TraceWriter log)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "DELETE FROM [follow_users] WHERE follow_userID = @follow_userID AND userID = @delete_follower";
                        command.Parameters.AddWithValue("@follow_userID", userid);
                        command.Parameters.AddWithValue("@delete_follower", delete_follower);
                        command.CommandText = sql;
                        command.ExecuteNonQuery();

                        return req.CreateResponse(HttpStatusCode.OK);
                    }
                }
                //var json = JsonConvert.SerializeObject(garbageTypes);
               
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region Delete user
        [FunctionName("DeleteUser")]
        public static HttpResponseMessage DeleteUser([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "deleteeuser/{userid}")]HttpRequestMessage req, String userid, TraceWriter log)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "DELETE FROM [dbo].[follow_users] WHERE [dbo].[follow_users].follow_userID = @userID or [dbo].[follow_users].userID = @userID " +
                                     "DELETE FROM[dbo].[tracking] WHERE[dbo].[tracking].userID = @userID " +
                                     "DELETE FROM[dbo].[user] WHERE[dbo].[user].userID = @userID";

                        command.Parameters.AddWithValue("@userID", userid);
                        command.CommandText = sql;
                        command.ExecuteNonQuery();
                    }
                }
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region Get User
        [FunctionName("GetUser")]
        public static HttpResponseMessage GetUser([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getuser/{UserID}")]HttpRequestMessage req, String UserID, TraceWriter log)
        {
            try
            {
                RegisterUser user = new RegisterUser();
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "SELECT * FROM [user] WHERE userID = @userID";
                        command.Parameters.AddWithValue("@userID", UserID);
                        command.CommandText = sql;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            user.UserId = new Guid(reader["userID"].ToString());
                            user.Name = reader["name"].ToString();
                            user.Email = reader["email"].ToString();
                            user.Password = reader["password"].ToString();
                            user.Sharekey = reader["sharekey"].ToString();
                        }
                    }
                }
                return req.CreateResponse(HttpStatusCode.OK, user);

            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region Login User
        [FunctionName("LoginUser")]
        public static async Task<HttpResponseMessage> LoginUserAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "loginuser")]HttpRequestMessage req,TraceWriter log)
        {
            //Inlezen van externe json
            var content = await req.Content.ReadAsStringAsync();
            RegisterUser user_try = JsonConvert.DeserializeObject<RegisterUser>(content);
            RegisterUser user_database = new RegisterUser();
            try
            {

             
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "SELECT email, password FROM [user] WHERE email = @email";
                        command.Parameters.AddWithValue("@email", user_try.Email.ToString());
                        command.CommandText = sql;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            user_database.Password = reader["password"].ToString();
                        }
                        if(reader.HasRows == false)
                        {
                            Debug.WriteLine("Acces denied");
                            return req.CreateResponse(HttpStatusCode.OK, user_database);
                        }

                        String[] delen = user_database.Password.ToString().Split(':');
                        String database_salt = delen[0].ToString();
                    
                        String database_hash = delen[1].ToString();

                        String hash_try = GenerateSaltedHash(user_try.Password.ToString(), database_salt);

                        bool acces = CompareByteArrays(Encoding.UTF8.GetBytes(database_hash), Encoding.UTF8.GetBytes(hash_try));

                        byte[] dbhash = Encoding.UTF8.GetBytes(database_hash);
                        byte[] dbhashsalt = Encoding.UTF8.GetBytes(database_salt);
                        Debug.WriteLine(database_hash);
                        Debug.WriteLine(hash_try);
                        connection.Close();

                        if (acces == true)
                        {
                            Debug.WriteLine("Acces granted");
                            try
                            {
                                RegisterUser user = new RegisterUser();
                                using (SqlConnection connection2 = new SqlConnection(CONNECTIONSTRING))
                                {
                                    connection.Open();
                                    using (SqlCommand command2 = new SqlCommand())
                                    {
                                        command.Connection = connection;
                                        string sql2 = "SELECT * FROM [user] WHERE email = @email2";
                                        command.Parameters.AddWithValue("@email2", user_try.Email);
                                        command.CommandText = sql2;
                                        SqlDataReader reader2 = command.ExecuteReader();
                                        while (reader2.Read())
                                        {
                                            user.UserId = new Guid(reader2["userID"].ToString());
                                            user.Name = reader2["name"].ToString();
                                            user.Email = reader2["email"].ToString();
                                            user.Sharekey = reader2["sharekey"].ToString();
                                            user.WantsNotifications = Convert.ToBoolean(reader2["wantsnotifications"]);
                                        }
                                    }
                                }
                                return req.CreateResponse(HttpStatusCode.OK, user);

                            }
                            catch (Exception ex)
                            {
                                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Acces denied");
                            return req.CreateResponse(HttpStatusCode.OK, user_try);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region AddProfilePicture
        //ps: vergeet de links niet te zetten in azure functions
        [FunctionName("AddProfilePicture")]
        public async static Task<HttpResponseMessage> AddProfilePicture([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pictures/send/{containerName}/{fileName}")]HttpRequestMessage req, string containerName, string fileName, TraceWriter log)
        {
            //!!!! TODO !!!! Add two parameters called containerName and fileName to the function header

            //read data stream from request
            var stream = await req.Content.ReadAsStreamAsync();

            //get access to Azure blob storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", Environment.GetEnvironmentVariable("BlobAccount"), Environment.GetEnvironmentVariable("BlobAccountKey")));

            //create a client on this account
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            //check if the container already exists; if not, create it
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            container.CreateIfNotExists();

            //set correct permissions for blob object
            BlobContainerPermissions permissions = container.GetPermissions();
            permissions.PublicAccess = BlobContainerPublicAccessType.Container;
            container.SetPermissions(permissions);

            //upload the actual image using the filename that was provided
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
            blockBlob.UploadFromStream(stream);

            //send the created url as a response
            return req.CreateResponse(HttpStatusCode.OK, blockBlob.StorageUri.PrimaryUri);


        }
        #endregion

        #region UpdateUser
        [FunctionName("UpdateUser")]
        public static async System.Threading.Tasks.Task<HttpResponseMessage> UpdateUser([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "updateuser")]HttpRequestMessage req, TraceWriter log)
        {
            var content = await req.Content.ReadAsStringAsync();
            var user_update = JsonConvert.DeserializeObject<RegisterUser>(content);

            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "UPDATE [user] SET name = @name, password = @password WHERE userID = @userID;";
                        command.CommandText = sql;
                        String salt = CreateSalt(8);
                        String hash = GenerateSaltedHash(user_update.Password.ToString(), salt);
                        command.Parameters.AddWithValue("@userID", user_update.UserId);
                        command.Parameters.AddWithValue("@name", user_update.Name);
                        command.Parameters.AddWithValue("@password", salt + ":" + hash);
                        command.ExecuteNonQuery();
                        user_update.Password = "";
                        return req.CreateResponse(HttpStatusCode.OK, user_update);
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region Password check
        [FunctionName("CheckPsw")]
        public static async Task<HttpResponseMessage> CheckPsw([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkpsw")]HttpRequestMessage req, TraceWriter log)
        {
            //Inlezen van externe json
            var content = await req.Content.ReadAsStringAsync();
            RegisterUser user_try = JsonConvert.DeserializeObject<RegisterUser>(content);
            RegisterUser user_database = new RegisterUser();
            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "SELECT email, password FROM [user] WHERE userID = @userid";
                        command.Parameters.AddWithValue("@userid", user_try.UserId.ToString());
                        command.CommandText = sql;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            user_database.Password = reader["password"].ToString();
                        }
                        if (reader.HasRows == false)
                        {
                            return req.CreateResponse(HttpStatusCode.OK, "NOK");
                        }
                        else
                        {
                            String[] delen = user_database.Password.ToString().Split(':');
                            String database_salt = delen[0].ToString();

                            String database_hash = delen[1].ToString();

                            String hash_try = GenerateSaltedHash(user_try.Password.ToString(), database_salt);

                            bool acces = CompareByteArrays(Encoding.UTF8.GetBytes(database_hash), Encoding.UTF8.GetBytes(hash_try));

                            byte[] dbhash = Encoding.UTF8.GetBytes(database_hash);
                            byte[] dbhashsalt = Encoding.UTF8.GetBytes(database_salt);
                            Debug.WriteLine(database_hash);
                            Debug.WriteLine(hash_try);
                            connection.Close();
                            if(acces == true)
                            {
                                return req.CreateResponse(HttpStatusCode.OK, "OK");
                            }
                            else
                            {
                                return req.CreateResponse(HttpStatusCode.OK, "NOK");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region Update Notifications
        [FunctionName("UpdateNotifications")]
        public static HttpResponseMessage UpdateNotifications([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "updatenotifications/{userid}/{wantsNotification}")]HttpRequestMessage req, string UserID, Boolean wantsnotification, TraceWriter log)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "UPDATE [user] SET wantsNotifications = @wantsNotifications WHERE userID = @userID;";
                        command.CommandText = sql;
                        command.Parameters.AddWithValue("@userID", UserID);
                        command.Parameters.AddWithValue("@wantsNotifications", wantsnotification);
                        command.ExecuteNonQuery();
                        return req.CreateResponse(HttpStatusCode.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region Functions
        public static string GenerateSharkey()
        {
            Random rnd = new Random();
            const string pool = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var builder = new StringBuilder();

            for (var i = 0; i < 6; i++)
            {
                var c = pool[rnd.Next(0, pool.Length)];
                builder.Append(c);
            }

            Debug.WriteLine(builder.ToString());

            return builder.ToString();
        }

        private static string CreateSalt(int size)
        {
            //Generate a cryptographic random number.
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] buff = new byte[size];
            rng.GetBytes(buff);

            // Return a Base64 string representation of the random number.
            return Convert.ToBase64String(buff);
        }

        static String GenerateSaltedHash(String plainPassword, String salt)
        {
            SHA256Managed sha256hashstring = new SHA256Managed();

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(plainPassword + salt);

            byte[] hash = sha256hashstring.ComputeHash(bytes);

            return ByteArrayToHexString(hash);
            
        }

        public static bool CompareByteArrays(byte[] array1, byte[] array2)
        {
            if (array1.Length != array2.Length)
            {
                return false;
            }

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static string ByteArrayToHexString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

       public static List<TimeStandingDay> CalculateTimeStandingDay(List<Track> tracks, int timeUur)
        {

            try
            {
                List<TimeStandingDay> list = new List<TimeStandingDay>();
                int a = 0;
                for (int x = 9; x < timeUur ; x++) //elk uur wordt overlopen, werkdag is van 9:00 tot 17:00
                {

                    int tussentijdseX = x;
                    for (int i = 0; i < tracks.Count; i++) //elke track wordt overlopen 
                    {
                        if (i == 0 || tracks[i - 1].isStanding != tracks[i].isStanding)
                        {
                            TimeStandingDay timeStandingDay = new TimeStandingDay();
                            if (tracks[i].Date.Hour.ToString() == x.ToString())
                            {
                                timeStandingDay.Hour = x;
                                a = 0;

                                if (tracks[i].isStanding == true) //als de track.isStanding = 1 is --> dus de persoon is gaan rechtstaan
                                {

                                    if (i + 1 == tracks.Count || tracks[i].Date.Hour != tracks[i + 1].Date.Hour)
                                    //als de laatste track rechtstaan is dan moet men de tijd tot het einde van de berekenen
                                    {
                                        DateTime StartOfRecorded = new DateTime(tracks[0].Date.Year, tracks[0].Date.Month, tracks[0].Date.Day, x + 1, 0, 0);
                                        timeStandingDay.TimeStandingSeconds = (StartOfRecorded - tracks[i].Date).TotalSeconds;
                                        list.Add(timeStandingDay);
                                    }
                                }
                                else  //(tracks[i].isStanding == false) --> bij zitten, bij een 0
                                {

                                    if (i == 0 || tracks[i - 1].Date.Hour != tracks[i].Date.Hour)
                                    {

                                        DateTime StartOfRecorded = new DateTime(tracks[0].Date.Year, tracks[0].Date.Month, tracks[0].Date.Day, x, 0, 0);
                                        timeStandingDay.TimeStandingSeconds = (tracks[i].Date - StartOfRecorded).TotalSeconds;
                                        list.Add(timeStandingDay);

                                    }
                                    else
                                    {
                                        timeStandingDay.TimeStandingSeconds = (tracks[i].Date - tracks[i - 1].Date).TotalSeconds;
                                        list.Add(timeStandingDay);
                                    }
                                }
                            }
                            else if (i < tracks.Count && i != 0)
                            {
                                //als (tracks[i].Date.Hour.ToString() != x.ToString())

                                /* VOLLEDIG UUR STAAN
                                    als de vorige track isStanding = true (1)
                                & als het uur van huidige track != het uur van de vorige track
                                & als het uur van huidige track - 1 != het uur van de vorige track
                                & als het uur van de vorige track + 1 = x
                                ==> dan 3600
                                */
                                if (tracks[i - 1].isStanding == true && tracks[i].Date.Hour != tracks[i - 1].Date.Hour && tracks[i].Date.Hour - 1 != tracks[i - 1].Date.Hour && tracks[i - 1].Date.Hour + 1 + a == x)
                                {
                                    timeStandingDay.Hour = x;

                                    timeStandingDay.TimeStandingSeconds = 3600;
                                    list.Add(timeStandingDay);
                                    a += 1;

                                }

                                else if (i == tracks.Count - 1 && list.Count != 0)
                                {
                                    if (list[list.Count - 1].Hour != x && tracks[i].isStanding == false)
                                    {
                                        timeStandingDay.Hour = x;
                                        timeStandingDay.TimeStandingSeconds = 0;
                                        list.Add(timeStandingDay);
                                    }
                                    else if (list[list.Count - 1].Hour != x && tracks[i].isStanding == true)
                                    {
                                        timeStandingDay.Hour = x;
                                        timeStandingDay.TimeStandingSeconds = 3600;
                                        list.Add(timeStandingDay);
                                    }

                                }
                                else if (i == 1)
                                {
                                    if (tracks[i].Date.Hour > x)
                                    {
                                        if (tracks[i - 1].isStanding == false)
                                        {
                                            timeStandingDay.Hour = x;
                                            timeStandingDay.TimeStandingSeconds = 3600;
                                            list.Add(timeStandingDay);
                                        }
                                        else if (tracks[i - 1].isStanding == true)
                                        {
                                            timeStandingDay.Hour = x;
                                            timeStandingDay.TimeStandingSeconds = 0;
                                            list.Add(timeStandingDay);
                                        }
                                    }
                                }
                            }
                        }


                    }
                }
                List<TimeStandingDay> gefilterde_list = new List<TimeStandingDay>(); ;

                // elk item van de lijst moet overlopen worden dus kijken we hoevaak we dit moeten doen
                int listlengte = list.Count;
                for (int y = 0; y < listlengte; y++)
                {
                    TimeStandingDay timeStandingDay = new TimeStandingDay();
                    if (y < listlengte)
                    {

                        if (y == listlengte - 1)
                        {
                            timeStandingDay.Hour = list[y].Hour;
                            timeStandingDay.TimeStandingSeconds = list[y].TimeStandingSeconds;
                            gefilterde_list.Add(timeStandingDay);
                        }
                        else if (list[y].Hour == list[y + 1].Hour)
                        {
                            int aantalitems = 0;

                            while (y + aantalitems < listlengte && list[y].Hour == list[y + aantalitems].Hour)
                            {
                                timeStandingDay.TimeStandingSeconds += list[y + aantalitems].TimeStandingSeconds;

                                timeStandingDay.Hour = list[y].Hour;

                                aantalitems += 1;
                            }
                            gefilterde_list.Add(timeStandingDay);
                            y += aantalitems - 1;
                        }
                        else
                        {
                            timeStandingDay.Hour = list[y].Hour;
                            timeStandingDay.TimeStandingSeconds = list[y].TimeStandingSeconds;
                            gefilterde_list.Add(timeStandingDay);
                        }
                    }
                }

                List<TimeStandingDay> extraGefilterde_list = new List<TimeStandingDay>(); ;
                int aantalNullen = 0;
                int uur = 9;
                while (uur < timeUur)
                {
                    TimeStandingDay timeStandingDay = new TimeStandingDay();

                    if (gefilterde_list[uur - 9 - aantalNullen].Hour == uur)
                    {
                        extraGefilterde_list.Add(gefilterde_list[uur - 9 - aantalNullen]);
                        uur += 1;
                    }
                    else
                    {
                        timeStandingDay.Hour = uur;
                        timeStandingDay.TimeStandingSeconds = 0;
                        extraGefilterde_list.Add(timeStandingDay);
                        aantalNullen += 1;
                        uur += 1;

                    }
                }
                return extraGefilterde_list;
            }
            catch (Exception)
            {

                return new List<TimeStandingDay>();
            }
            
            
        }
        #endregion
    }
}
