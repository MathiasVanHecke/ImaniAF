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
        //SQL
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
                                    string sql2 = "INSERT INTO [user] VALUES(@userID,@created, @name, @email, @password,@sharekey)";
                                    command2.CommandText = sql2;
                                    command2.Connection = connection2;
                                    String salt = CreateSalt(8);
                                    String hash = GenerateSaltedHash(user_try.Password.ToString(), salt);
                                    command2.Parameters.AddWithValue("@userID", user_try.UserId.ToString());
                                    command2.Parameters.AddWithValue("@created", DateTime.Now);
                                    command2.Parameters.AddWithValue("@name", user_try.Name);
                                    command2.Parameters.AddWithValue("@email", user_try.Email);
                                    command2.Parameters.AddWithValue("@password", salt + ":" + hash);
                                    command2.Parameters.AddWithValue("@sharekey", GenerateSharkey());
                             
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
                            user.Password = reader["password"].ToString();
                            user.Sharekey = reader["sharekey"].ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
            //Follower toevoegen aan de vorige user.
            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "INSERT INTO [follow_users] VALUES(@userID, @follow_userID)";
                        command.CommandText = sql;
                        command.Parameters.AddWithValue("@userID", user.UserId.ToString());
                        command.Parameters.AddWithValue("@follow_userID", base_userID);
                        command.ExecuteNonQuery();
                        return req.CreateResponse(HttpStatusCode.OK, user);
                    }
                }
            }
            catch (Exception ex)
            {
                 return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
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
                List<RegisterUser> followers = new List<RegisterUser>();
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
                            RegisterUser user = new RegisterUser();
                            user.UserId = new Guid(reader["userID"].ToString());
                            user.Name = reader["name"].ToString();
                            user.Email = reader["email"].ToString();
                            user.Password = reader["password"].ToString();
                            user.Sharekey = reader["sharekey"].ToString();
                            followers.Add(user);
                        }
                    }
                }
                //var json = JsonConvert.SerializeObject(garbageTypes);
                return req.CreateResponse(HttpStatusCode.OK, followers);
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);

            }
        }

        #endregion

        #region Delete follower
        [FunctionName("DeleteFollower")]
        public static HttpResponseMessage DeleteFollower([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "delete/{userid}/{delete_follower}")]HttpRequestMessage req, String userid, String delete_follower, TraceWriter log)
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
                    }
                }
                //var json = JsonConvert.SerializeObject(garbageTypes);
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);

            }
        }
        #endregion

        #region Delete user
        [FunctionName("DeleteUser")]
        public static HttpResponseMessage DeleteUser([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "delete/{userid}")]HttpRequestMessage req, String userid, TraceWriter log)
        {
            //Nog niet af tracking history moet men ook kunnen verwijderen
            try
            {
                using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = connection;
                        string sql = "DELETE FROM [dbo].[follow_users] WHERE [dbo].[follow_users].follow_userID = @userID DELETE FROM[dbo].[user] WHERE[dbo].[user].userID = @userID";
                        command.Parameters.AddWithValue("@userID", userid);
                        command.CommandText = sql;
                        command.ExecuteNonQuery();
                    }
                }
                //var json = JsonConvert.SerializeObject(garbageTypes);
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }
        #endregion

        #region GetUser
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
        public static async System.Threading.Tasks.Task<HttpResponseMessage> UpdateUser([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "updateuser")]HttpRequestMessage req, TraceWriter log)
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
                        string sql = "UPDATE [user] SET email =  @email, name = @name, password = @password WHERE userID = @userID;";
                        command.CommandText = sql;
                        String salt = CreateSalt(8);
                        String hash = GenerateSaltedHash(user_update.Password.ToString(), salt);
                        command.Parameters.AddWithValue("@userID", user_update.UserId);
                        command.Parameters.AddWithValue("@email", user_update.Email);
                        command.Parameters.AddWithValue("@name", user_update.Name);
                        command.Parameters.AddWithValue("@password", salt + ":" + hash);
             
                   
                        command.ExecuteNonQuery();
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
        #endregion
    }
}
