using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.Text;
using ServiceStack.Redis;

namespace RedisBank
{
    
    public class User
    {
        public IRedisClient redisClient { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string ID;
        public string Nationality;

        static byte[] GetStringBytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public void pushUsrToRedis(IRedisNativeClient redis)
        {
            int response = Transaction(redis);
            while (response == 0) response = Transaction(redis);
        }

        public int Transaction(IRedisNativeClient redis)
        {
            Random rnd = new Random();
            while (redis.Exists($"person:{ID}") == 1) ID = Nationality + rnd.Next(99999).ToString();
            string[] watchkeys = { $"person:{ID}", "First_Name", "Last_Name" };
            redisClient.Watch(watchkeys);
            using (IRedisTransaction trans = redisClient.CreateTransaction())
            {
                trans.QueueCommand(redisClient => redisClient.SetEntryInHash($"person:{ID}", "First_Name", first_name));
                trans.QueueCommand(redisClient => redisClient.SetEntryInHash($"person:{ID}", "Last_Name", last_name));
                return trans.Commit() == false ? 0 : 1;
            }
        }
    }

    public class Account
    {
        public IRedisClient redisClient { get; set; }
        public string ID { get; set; }
        public string holder { get; set; }
        public string balance { get; set; }

        public void pushAccToRedis(IRedisNativeClient redis)
        {
            int response = Transaction(redis);
            while (response == 0) response = Transaction(redis);
        }

        public int Transaction(IRedisNativeClient redis)
        {
            Random rnd = new Random();
            while (redis.Exists($"account:{ID}:holder") == 1) ID = rnd.Next(99999).ToString(); 
            //How to update watch in MULTI down below
            redisClient.Watch("account:{ ID}:holder");
            using (IRedisTransaction trans = redisClient.CreateTransaction())
            {
                trans.QueueCommand(redisClient => redisClient.SetEntryInHash($"account:{ID}", "Balance", balance));
                trans.QueueCommand(redisClient => redisClient.Set($"account:{ID}:holder", GetStringBytes(holder)));
                trans.QueueCommand(redisClient => redisClient.AddItemToList($"{holder}_accounts", ID));
                return trans.Commit() == false ? 0 : 1;
            }
        }


        static byte[] GetStringBytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }
    }

    public class UserSession
    {
        private static volatile User currentUser;
        private static object syncRoot = new Object();

        private UserSession() { }

        public static User GetUser()
        {
            if (currentUser == null) throw new Exception("Not logged in.");
            return currentUser;
        }

        public static void Login(User user)
        {
            if (currentUser != null) throw new Exception("Already logged in");
            lock (syncRoot)
            {
                currentUser = user;
            }
        }

        public static void Logout()
        {
            lock (syncRoot)
            {
                currentUser = null;
            }
        }
    }

}
