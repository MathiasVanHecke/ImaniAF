using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
            var user = JsonConvert.DeserializeObject<RegisterUser>(content);

            //Schrijven naar database
            using (SqlConnection connection = new SqlConnection(CONNECTIONSTRING))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    string sql = "INSERT INTO [user] VALUES(@userID, @name, @email, @password,@sharekey)";
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@userID", user.UserId.ToString());
                    command.Parameters.AddWithValue("@name", user.Name);
                    command.Parameters.AddWithValue("@email", user.Email);
                    command.Parameters.AddWithValue("@password", user.Password);
                    command.Parameters.AddWithValue("@sharekey", GenerateSharkey());
                    command.ExecuteNonQuery();

                    return req.CreateResponse(HttpStatusCode.OK, user);
                }
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
            //Follower toevoegen aan de vorige user.UserID

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
        public static HttpResponseMessage LoginUser([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "loginuser/{email}/{hashpsw}")]HttpRequestMessage req, String email, String hashpsw, TraceWriter log)
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
                        string sql = "SELECT * FROM [user] WHERE email = @email and password = @password";
                        command.Parameters.AddWithValue("@email", email);
                        command.Parameters.AddWithValue("@password", hashpsw);
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
                //var json = JsonConvert.SerializeObject(garbageTypes);
                return req.CreateResponse(HttpStatusCode.OK, user);

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
        #endregion
    }
}
