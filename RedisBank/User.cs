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
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string ID;
        
        static byte[] GetStringBytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public void pushUsrToRedis(IRedisNativeClient redis)
        {
            byte[][] keysArr = { GetStringBytes("First_Name"), GetStringBytes("Last_Name") };
            byte[][] valueArr = { GetStringBytes(first_name), GetStringBytes(last_name) };
            redis.HMSet($"person:{ID}", keysArr, valueArr);
        }
    }

    public class Account
    {
        public string ID { get; set; }
        public string holder { get; set; }
        public string balance { get; set; }

        public void pushAccToRedis(IRedisNativeClient redis)
        {
            redis.HSet($"account:{ID}", GetStringBytes("Balance"), GetStringBytes(balance));
            redis.Set($"account:{ID}:holder", GetStringBytes(holder));
            redis.RPush($"{holder}_accounts", GetStringBytes(ID));
        }

        //redis.SetEntryInHash($"account:{ID}", "Balance", balance);


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
