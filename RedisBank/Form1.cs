﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServiceStack;
using ServiceStack.Redis;

namespace RedisBank
{
    
    public partial class Form1 : Form
    {
        IRedisNativeClient redis = new RedisClient("localhost", 6379);
        RedisClient redisClient = new RedisClient("localhost", 6379);

        public Form1()
        {
            InitializeComponent();

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        //User registration
        private void button1_Click(object sender, EventArgs e)
        {
            
            Random rnd = new Random();
            if (!string.IsNullOrWhiteSpace(textBox1.Text) &&
                !string.IsNullOrWhiteSpace(textBox2.Text) &&
                !string.IsNullOrEmpty(comboBox1.Text))
            {
                string ID = comboBox1.SelectedItem + rnd.Next(99999).ToString();
                User user = new User
                {
                    redisClient = redisClient,
                    first_name = textBox1.Text,
                    last_name = textBox2.Text,
                    ID = ID,
                    Nationality = comboBox1.SelectedItem.ToString()
                };
                user.pushUsrToRedis(redis);
                UserSession.Login(user);
                label5.Text = user.ID;
                CreateAccount();
            }
            else label5.Text = "Please fill all the forms";
        }

        //Create new bank account
        private void LoginBut_Click(object sender, EventArgs e)
        {
            CreateAccount();
        }

        public void CreateAccount()
        {
            Random rnd = new Random();
            string ID = rnd.Next(99999).ToString();
            Account acc = new Account
            {
                redisClient = redisClient,
                ID = ID,
                holder = UserSession.GetUser().ID,
                balance = "0"
            };
            acc.pushAccToRedis(redis);
            string[] row = { acc.ID, Encoding.UTF8.GetString(redis.HGet($"account:{acc.ID}", Encoding.UTF8.GetBytes("Balance"))) };
            var listViewItem = new ListViewItem(row);
            listView1.Items.Add(listViewItem);
        }

        //Show active users accounts
        private void button2_Click_1(object sender, EventArgs e)
        {
            ShowAccounts();
        }

        public void ShowAccounts()
        {
            string usrID = UserSession.GetUser().ID;
            listView1.Items.Clear();

            var length = redis.LLen($"{usrID}_accounts");
            for (int i = 0; i < length; i++)
            {
                string accountID = Encoding.UTF8.GetString(redis.LIndex($"{usrID}_accounts", i));
                string[] row = { accountID, Encoding.UTF8.GetString(redis.HGet($"account:{accountID}", Encoding.UTF8.GetBytes("Balance"))) };
                var listViewItem = new ListViewItem(row);
                listView1.Items.Add(listViewItem);
            }
        }

        //Add money to the account
        private void button4_Click(object sender, EventArgs e)
        {
            if(!string.IsNullOrWhiteSpace(AddMBox.Text) &&
               !string.IsNullOrWhiteSpace(textBox4.Text))
            {
                string accID = AddMBox.Text;
                if(redis.Get($"account:{accID}:holder") != null)
                {
                    if (Encoding.UTF8.GetString(redis.Get($"account:{accID}:holder")) == UserSession.GetUser().ID)
                    {
                        label11.Text = UserSession.GetUser().ID;
                        SetBalanceInfo(accID);
                        ShowAccounts();
                        textBox3.Text = AddMBox.Text;
                    }
                    else label11.Text = AddMBox.Text + "Is not your account";
                }
                else label11.Text = "Account doesn't exist";
            }
        }

        //Transfer money from -> to account
        private void button5_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBox3.Text) &&
               !string.IsNullOrWhiteSpace(textBox5.Text) &&
               !string.IsNullOrWhiteSpace(textBox6.Text))
            {
                if (redis.Get($"account:{textBox3.Text}:holder") != null && redis.Get($"account:{textBox5.Text}:holder") != null)
                {
                    if (Encoding.UTF8.GetString(redis.Get($"account:{textBox3.Text}:holder")) == UserSession.GetUser().ID)
                    {
                        label11.Text = "Processing";
                        int response = Transaction();
                        while (response == 0) response = Transaction();

                        if (response == 1)
                        {
                            ShowAccounts();
                            label11.Text = "Success";
                        }
                        if (response == -1) label11.Text = "Not enough money in the account";
                    }
                    else label11.Text = "The account youre sending money from is not yours";
                }
                else label11.Text = "One of the chosen accounts is not existant";
            }
            else label11.Text = "Put all the required information";
            
        }

        public int Transaction()
        {
                string fromAccID = textBox3.Text;
                string toAccID = textBox5.Text;
                Double ammount = textBox6.Text.ToDouble();
                string[] watchFromKeys = { $"account:{textBox3.Text}", "Balance" };
                string[] watchToKeys = { $"account:{ textBox5.Text}", "Balance" };
            

                redisClient.Watch(watchFromKeys);
                redisClient.Watch(watchToKeys);
                using (IRedisTransaction trans = redisClient.CreateTransaction())
                {
                    Double balanceFrom = GetBalanceInfo(fromAccID);
                    Double balanceTo = GetBalanceInfo(toAccID);

                    if (balanceFrom >= ammount)
                    {
                        balanceFrom -= ammount;
                        balanceTo += ammount;
                        trans.QueueCommand(redisClient => redisClient.SetEntryInHash($"account:{fromAccID}", "Balance", balanceFrom.ToString()));
                        trans.QueueCommand(redisClient => redisClient.SetEntryInHash($"account:{toAccID}", "Balance", balanceTo.ToString()));
                        return trans.Commit() == false ? 0 : 1; //TODO: limit cycles
                    }
                    else return -1;
                }
            
        }

        private Double GetBalanceInfo(string accID, IRedisTransaction trans)
        {
            using (trans)
            {
                string balanceSt="";
                trans.QueueCommand(redisClient => redisClient.GetValueFromHash($"account:{accID}", "Balance") , x => balanceSt = x);
                trans.Commit();
                Double balance = balanceSt.ToDouble();
                return balance;
            }
        }
        private Double GetBalanceInfo(string accID)
        {
                string balanceStr = Encoding.UTF8.GetString(redis.HGet($"account:{accID}",
                    Encoding.UTF8.GetBytes("Balance")));
                Double balance = balanceStr.ToDouble();
                return balance;
        }

        private void SetBalanceInfo(string accID)
        {
            redisClient.Watch($"account:{accID}", "Balance");
            using (IRedisTransaction trans = redisClient.CreateTransaction())
            {
                Double balance = GetBalanceInfo(accID);
                balance += textBox4.Text.ToDouble();
                trans.QueueCommand(redisClient => redisClient.SetEntryInHash($"account:{accID}", "Balance", balance.ToString()));
                if (trans.Commit() == true)
                {
                    label11.Text = "Success";
                }
                else label11.Text = "Check your account number and try again";
            }
                
        }

        //Money to add to account TextBox KeyPress check (only numbers and one comma allowed)
        private void textBox4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
                (e.KeyChar != ','))
            {
                e.Handled = true;
            }

            // only allow one decimal point
            if ((e.KeyChar == ',') && ((sender as TextBox).Text.IndexOf(',') > -1))
            {
                e.Handled = true;
            }
        }

        //Account for the money to be transfered to KeyPress check (only numbers and letters)
        private void AddMBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) &&
                !char.IsLetterOrDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        //Transfer money from KeyCheck (only numbers and letters)
        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) &&
                !char.IsLetterOrDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        //Transfer money to KeyCheck (only numbers and letters)
        private void textBox5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) &&
                !char.IsLetterOrDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        //Ammount KeyCheck (only numbers and one comma allowed)
        private void textBox6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
                (e.KeyChar != ','))
            {
                e.Handled = true;
            }

            // only allow one decimal point
            if ((e.KeyChar == ',') && ((sender as TextBox).Text.IndexOf(',') > -1))
            {
                e.Handled = true;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void button2_Click(object sender, EventArgs e)
        {
        }
        
        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void AddMBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        
        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {

        }

        
        private void textBox5_KeyDown(object sender, KeyEventArgs e)
        {

        }
        
        private void textBox6_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void label11_Click(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged_1(object sender, EventArgs e)
        {

        }
    }
}
