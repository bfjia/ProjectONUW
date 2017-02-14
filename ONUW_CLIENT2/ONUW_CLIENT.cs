using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;

namespace ONUW_CLIENT2
{
    public partial class ONUW_CLIENT : Form
    {
        #region variables
        // The port number for the remote device.
        private const int port = 11000;
        private string IPOrHostName = "bfjia.net";

        // ManualResetEvent instances signal completion.
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);
        private static ManualResetEvent sendReady = new ManualResetEvent(false);

        // The response from the remote device.
        private static String response = String.Empty;
        static bool allowButtonClicks = true;

        // Create a TCP/IP socket.
        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //variables refered by the actual ONUW game
        private string ReceivedContent = "";
        Players self = new Players();
        bool serverSet = false;
        int delay = 0;
        JsonStruct playerData = new JsonStruct();
        private int numRevealed = 0;
        List<Button> playerButtons = new List<Button>();
        List<Button> middleButtons = new List<Button>();
        private string guidClicked = "";
        #endregion

        #region TCP functions
        private void StartClient()
        {
            // Connect to a remote device.
            try
            {
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // Establish the remote endpoint for the socket.
                // The name of the 
                // remote device is "host.contoso.com".
                richTextBox1.AppendText("\n" + "connecting to server..");
                IPAddress ipAddress;
                if (!IPAddress.TryParse(IPOrHostName, out ipAddress))
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(IPOrHostName);
                    ipAddress = ipHostInfo.AddressList[0];
                }
                //ipAddress = IPAddress.Parse("127.0.0.1");
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
                richTextBox1.AppendText("\n" + "connecting to server..");
                // Connect to the remote endpoint.
                client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();
                richTextBox1.AppendText("\n" + "connected to " + ipAddress.ToString() + ":" + port.ToString());
                // sendReady.Set();

            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString()); Environment.Exit(0);
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                // MessageBox.Show("Socket connected to " + client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString()); Environment.Exit(0);
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString()); Environment.Exit(0);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Check for end-of-file tag. If it is not there, read 
                    // more data.
                    string content = state.sb.ToString();
                    if (content.IndexOf("<EOF>") > -1)
                    {
                        // All the data has been read from the 
                        // client. Display it on the console.
                        richTextBox1.AppendText("\n" + ("Received: " + content));
                        ReceivedContent = content.Remove(content.IndexOf("<EOF>"));
                        // Echo the data back to the client.
                        receiveDone.Set();
                    }
                    else
                    {
                        // Not all data received. Get more.
                        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString()); Environment.Exit(0);
            }
        }

        private void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data + "<EOF>");
            richTextBox1.AppendText("\nSending: " + data.ToString());
            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                richTextBox1.AppendText("\n" + string.Format("Sent {0} bytes to server.", bytesSent.ToString()));

                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString()); Environment.Exit(0);
            }
        }

        private string sendRequest(string request)
        {
            //   return "thisisateststatement|||complete";
            try
            {
                // richTextBox1.Clear();
                sendReady.Reset();
                receiveDone.Reset();
                sendDone.Reset();
                connectDone.Reset();
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                StartClient();

                // Send test data to the remote device.
                richTextBox1.AppendText("\n" + "Sending Text...");
                Send(client, request);
                sendDone.WaitOne();
                // Receive the response from the remote device.
                Receive(client);
                receiveDone.WaitOne();
                richTextBox1.AppendText("\n" + "Closing connection to server...");
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                client.Dispose();
                richTextBox1.AppendText("\n" + "Connection Closed");
                return ReceivedContent;

            }
            catch (Exception exc) { MessageBox.Show(exc.ToString()); return "error"; }
        }

        #endregion

        #region ONUW game functions
        public ONUW_CLIENT()
        {
            InitializeComponent();
        }

        private void ONUW_CLIENT_Load(object sender, EventArgs e) //on form load, set the label and button to get the server address
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            button2.Hide(); button3.Hide(); button3.Hide();
            textBox1.Show(); label1.Show();
            label1.Text = "Type in the server address (IP or hostname) then click the button: ";
            button1.Text = "Save!";
            if (Environment.MachineName.ToLower().Contains("brian") || Environment.MachineName.ToLower().Contains("tam") || Environment.MachineName.ToLower().Contains("auxori") || Environment.MachineName.ToLower().Contains("gucciball"))
                Console.WriteLine("screw you brian tam");
            //button4.Visible = true;
        }

        private void button1_Click_1(object sender, EventArgs e) //connect
        {
            if (!serverSet)
            {
                IPOrHostName = textBox1.Text;
                if (IPOrHostName == "l")
                    IPOrHostName = "127.0.0.1";
                else if (IPOrHostName == "h")
                    IPOrHostName = "192.168.10.159";
                
                serverSet = true;
                textBox1.Clear();
                label1.Text = "Type in an IGN then click the connect button: ";
                button1.Text = "Connect!";
            }
            else
            {
                //NEW PLAYER JOINS, FIRST COMMAND GETS GUID
                if (textBox1.Text != "")
                {
                    string request = textBox1.Text + "|||NewPlayer";
                    string response2 = sendRequest(request);
                    if (response2 == "error")
                        MessageBox.Show("error: 0x00000, FATAL ERROR: NO ERROR XD");
                    else if (response2.Contains("TooManyPlayers") || response2.Contains("NewUserRequired"))
                        MessageBox.Show("Number of players in lobby maxed, try again");
                    else
                    {
                        self.gameID = response2.Substring(13);
                        self.gameTag = textBox1.Text;
                        self.ready = false;
                        self.voted = "Nobody";
                        self.role = "Unassigned";
                        self.voteCount = 0;
                        self.actionDone = false;
                        self.actionResult = "";
                        self.actionInput = null;
                        //richTextBox1.AppendText("\nMy role's action's results: " + self.actionResult);
                        textBox1.Clear();
                        textBox1.Hide();
                        label1.Text = "Click Ready Button when ready";
                        button1.Hide();
                        button2.Text = "Ready";
                        button2.Show();
                    }
                }
                else { MessageBox.Show("Choose an IGN first"); }
            }
        }

        private void button2_Click(object sender, EventArgs e) //ready button
        {
            //ready
            button2.Hide();
            Stopwatch t = new Stopwatch();


            string request = self.gameID + "|||Start";
            self.ready = true;
            string response2 = sendRequest(request);
            if (response2 == "Status:Ready, waiting on others")
            {
                richTextBox2.AppendText("\nWaiting for other players to join the lobby...");
                label1.Text = "Wait for other players....";
                t.Start();
                while (true)
                {
                    if (t.Elapsed.Seconds < 10)
                    {
                        //wait
                        Application.DoEvents();
                    }
                    else
                    {
                        //send in wait request
                        request = self.gameID + "|||WaitingToStart";
                        response2 = sendRequest(request);
                        if (response2.Contains("Status: GameStarting,"))
                        {
                            richTextBox2.AppendText("\nGame is starting now!");
                            playerData = JsonConvert.DeserializeObject<JsonStruct>(response2.Substring(22));
                            break;
                        }

                        t.Restart();
                    }
                }
                t.Reset();
                //at this point, all player data should be received.
                foreach (Players p in playerData.players)
                    if (p.gameID == self.gameID)
                        self.role = p.role; //get my own role
                richTextBox2.AppendText("\n\nMy GUID:" + self.gameID);
                richTextBox2.AppendText("\nMy IGN:" + self.gameTag);
                richTextBox2.AppendText("\nMy Role:" + self.role);

                List<string> rolesInPlay = new List<string>();

                richTextBox2.AppendText("Current roles in play: \n");
                foreach (Players p in playerData.players)
                    rolesInPlay.Add(p.role);
                foreach (KeyValuePair<int, string> p in playerData.middleCards)
                    rolesInPlay.Add(p.Value);

                rolesInPlay.Sort();

                foreach (string s in rolesInPlay)
                    richTextBox2.AppendText(s + ", ");

                generatePlayerButtons(playerData);
                label1.Text = "Waiting to perform role action...";
                button3.Show();
                button3.Text = "Perform Action";
            }
            else
            {
                MessageBox.Show("Error sending ready response");
            }


        }

        private void button3_Click(object sender, EventArgs e) //perform action button
        {
            button3.Hide();
            allowButtonClicks = true;
            guidClicked = "";
            // generatePlayerButtons(null);
            //perform action
            //actions should be formatted as the follows: guid|||performRoleAction>>>input1+input2+input3...
            if (self.role == "werewolf")
            {
                #region werewolf
                bool onlyOne = true;
                // foreach (Players p in playerData.players)
                for (int p = 0; p < playerData.players.Count; p++)
                {
                    if (playerData.players[p].role == "werewolf" && playerData.players[p].gameID != self.gameID || playerData.players[p].role == "mystic wolf" && playerData.players[p].gameID != self.gameID)
                    {
                        onlyOne = false;
                        if ((string)playerButtons[p].Tag == playerData.players[p].gameID)
                            playerButtons[p].Text = "<Player> " + playerData.players[p].gameTag + "\nRole: " + playerData.players[p].role + "?";
                    }
                }

                if (onlyOne)
                {
                    label1.Text = "Bummers, you are the only werewolf. Choose a middle card to see what it is";
                    while (numRevealed < 1)
                    {
                    retry:
                        if (guidClicked == "")
                        {
                            Application.DoEvents();
                        }
                        else
                        {
                            try { int.Parse(guidClicked); }
                            catch { MessageBox.Show("Choose a middleCard"); guidClicked = ""; goto retry; }

                            foreach (Button b in middleButtons)
                                if ((string)b.Tag == guidClicked)
                                    b.Text = "Middle Card " + guidClicked + " Role: " + playerData.middleCards[int.Parse(guidClicked) - 1] + "?";
                            guidClicked = "";
                            numRevealed++;
                        }
                    }
                }

                guidClicked = "";
                numRevealed = 0;
                string actionResult = sendPerformActionRequest("");
                self.actionResult = actionResult;
                richTextBox1.AppendText("\n" + self.actionResult);

                #endregion
            }

            else if (self.role == "mason")
            {
                #region mason
                bool onlyOne = true;
                // foreach (Players p in playerData.players)
                for (int p = 0; p < playerData.players.Count; p++)
                {
                    if (playerData.players[p].role == "mason" && playerData.players[p].gameID != self.gameID)
                    {
                        onlyOne = false;
                        if ((string)playerButtons[p].Tag == playerData.players[p].gameID)
                            playerButtons[p].Text = "<Player> " + playerData.players[p].gameTag + "\nRole: " + playerData.players[p].role + "?";
                    }
                }

                if (onlyOne)
                {
                    label1.Text = "Bummers, you are the only mason. Good Luck!";
                }

                guidClicked = "";
                numRevealed = 0;
                string actionResult = sendPerformActionRequest("");
                self.actionResult = actionResult;
                richTextBox1.AppendText("\n" + self.actionResult);

                #endregion
            }

            else if (self.role == "mystic wolf")
            {
                #region mystic wolf
                bool onlyOne = true;

                // foreach (Players p in playerData.players)
                for (int p = 0; p < playerData.players.Count; p++)
                {
                    if (playerData.players[p].role == "werewolf" && playerData.players[p].gameID != self.gameID || playerData.players[p].role == "mystic wolf" && playerData.players[p].gameID != self.gameID)
                    {
                        onlyOne = false;
                        if ((string)playerButtons[p].Tag == playerData.players[p].gameID)
                            playerButtons[p].Text = "<Player> " + playerData.players[p].gameTag + "\nRole: " + playerData.players[p].role + "?";
                    }
                }

                if (onlyOne)
                {
                    label1.Text = "You are the mystic wolf, too bad you are also the lone werewolf. You may see any one card in play!";
                    while (numRevealed < 1)
                    {
                    retry:
                        if (guidClicked == "")
                        {
                            Application.DoEvents();
                        }
                        else
                        {
                            
                            try { Guid.Parse(guidClicked); goto playerSelected; }
                            catch { goto middleSelected; }

                            playerSelected:
                            if (guidClicked == self.gameID)
                            {
                                MessageBox.Show("Dont you want to see ANOTHER player's card?");
                                guidClicked = "";
                                goto retry;
                            }
                            for (int p = 0; p < playerData.players.Count; p++)
                                if ((string)playerButtons[p].Tag == guidClicked)
                                    playerButtons[p].Text = "<Player> " + playerData.players[p].gameTag + "\nRole: " + playerData.players[p].role + "?";
                            guidClicked = "";
                            numRevealed++;
                            goto endOfMysticWolf;

                        middleSelected:
                            try { int.Parse(guidClicked); }
                            catch { MessageBox.Show("Something went wrong in the mystic wolf's action:("); guidClicked = "1";  }
                            
                            foreach (Button b in middleButtons)
                                if ((string)b.Tag == guidClicked)
                                    b.Text = "Middle Card " + guidClicked + " Role: " + playerData.middleCards[int.Parse(guidClicked) - 1] + "?";
                            guidClicked = "";
                            numRevealed++;
                            goto endOfMysticWolf;
                        }
                    }
                }
                else
                {
                    //not the only werewolf, can only look at middle cards.
                    label1.Text = "You are the mystic wolf. Choose another player to see their role!";
                    while (numRevealed < 1)
                    {
                    retry:
                        if (guidClicked == "")
                        {
                            Application.DoEvents();
                        }
                        else
                        {
                            if (guidClicked == self.gameID)
                            {
                                MessageBox.Show("Dont you want to see ANOTHER player's card?");
                                guidClicked = "";
                                goto retry;
                            }

                            try { Guid.Parse(guidClicked); }
                            catch { MessageBox.Show("Choose another player"); guidClicked = ""; goto retry; }

                            for (int p = 0; p < playerData.players.Count; p++)
                                if ((string)playerButtons[p].Tag == guidClicked)
                                {
                                    if (playerButtons[p].Text.Contains("Unknown"))
                                        playerButtons[p].Text = "<Player> " + playerData.players[p].gameTag + "\nRole: " + playerData.players[p].role + "?";
                                    else
                                    {
                                        MessageBox.Show("Why would you waste your ability, you already know that player is the werewolf?");
                                        guidClicked = "";
                                        goto retry;
                                    }
                                }
                            guidClicked = "";
                            numRevealed++;
                        }
                    }
                }

                endOfMysticWolf:
                guidClicked = "";
                numRevealed = 0;
                string actionResult = sendPerformActionRequest("");
                self.actionResult = actionResult;
                richTextBox1.AppendText("\n" + self.actionResult);

                #endregion
            }
            else if (self.role == "minion")
            {
                #region minion
                /*
                for (int p = 0; p < playerData.players.Count; p++)
                {
                    if (playerData.players[p].role == "werewolf")
                    {
                        if ((string)playerButtons[p].Tag == (playerData.players[p].gameID))
                            playerButtons[p].Text = "<Player> " + playerData.players[p].gameTag + "\nRole: " + playerData.players[p].role + "?";
                    }
                }*/
                foreach (Players p in playerData.players)
                    if (p.role == "werewolf")
                        foreach (Button b in playerButtons)
                            if ((string)b.Tag == p.gameID)
                                b.Text = "<Player> " + p.gameTag + "\nRole: " + p.role + "?";

                string actionResult = sendPerformActionRequest("");
                self.actionResult = actionResult;
                richTextBox1.AppendText("\n" + self.actionResult);
                #endregion
            }
            else if (self.role == "seer")
            {
                //fk me im broken...both in code and in game...NOT ANYMORE HHHHHH
                #region seer
                label1.Text = "Choose either 2 middle cards or 1 player card...";
                bool playerChosen = false;
                guidClicked = "";
                while (numRevealed < 2 || !playerChosen)
                {
                retry:
                    if (guidClicked == "")
                    {
                        Application.DoEvents();
                    }
                    else
                    {
                        if (!playerChosen)
                        {
                            foreach (Button b in middleButtons)
                            {
                                if ((string)b.Tag == guidClicked)
                                {
                                    if (b.Text.Contains("Unknown"))
                                    {
                                        b.Text = "Middle Card " + guidClicked + " Role: " + playerData.middleCards[int.Parse(guidClicked) - 1] + "?";
                                        numRevealed++;// = 3;
                                        guidClicked = "";
                                        playerChosen = true;
                                        goto retry;
                                    }
                                    else
                                    {
                                        guidClicked = "";
                                        MessageBox.Show("Dont you want to see another card?");
                                        goto retry;
                                    }
                                }
                                //guidClicked = "";
                                //numRevealed++;
                            }

                            foreach (Button b in playerButtons)
                            {
                                if ((string)b.Tag == guidClicked)
                                {
                                    foreach (Players p in playerData.players)
                                    {
                                        if (p.gameID == (string)b.Tag)
                                        {
                                            if (b.Text.Contains("<Player>") && b.Text.Contains("Unknown"))
                                            {
                                                b.Text = b.Text = "<Player> " + p.gameTag + "\nRole: " + p.role + "?";
                                                numRevealed = 3;
                                                goto Foo;
                                            }
                                            else
                                            {
                                                guidClicked = "";
                                                MessageBox.Show("Dont you want to see another card?");
                                                goto retry;
                                            }
                                        }
                                    }

                                    guidClicked = "";
                                    playerChosen = true;
                                    //goto Foo;
                                }
                            }
                        }
                        else
                        {
                            try { int.Parse(guidClicked); }
                            catch { MessageBox.Show("Choose another middle card!"); guidClicked = ""; goto retry; }

                            foreach (Button b in middleButtons)
                            {
                                if ((string)b.Tag == guidClicked)
                                {
                                    if (b.Text.Contains("Unknown"))
                                    {
                                        b.Text = "Middle Card " + guidClicked + " Role: " + playerData.middleCards[int.Parse(guidClicked) - 1] + "?";
                                        numRevealed++;// = 3;
                                        guidClicked = "";
                                        goto Foo;
                                    }
                                    else
                                    {
                                        guidClicked = "";
                                        MessageBox.Show("Dont you want to see another card?");
                                        goto retry;
                                    }
                                }
                            }

                            /*  foreach (Button b in playerButtons)
                              {
                                  if ((string)b.Tag == guidClicked)
                                  {
                                      foreach (Players p in playerData.players)
                                      {
                                          if (p.gameID == (string)b.Tag)
                                          {
                                              if (b.Text.Contains("<Player>") && b.Text.Contains("Unknown"))
                                                  b.Text = b.Text = "<Player> " + p.gameTag + " Role: " + p.role + "?";
                                              else
                                              {
                                                  guidClicked = "";
                                                  MessageBox.Show("Dont you want to see another card?");
                                                  goto retry;
                                              }
                                          }
                                      }

                                      guidClicked = "";
                                      playerChosen = true;
                                      goto Foo;
                                  }*
                              }*/

                            //guidClicked = "";
                            ////MessageBox.Show("Choose A Player Button");
                            // goto retry;

                        }

                        guidClicked = "";
                        numRevealed++;
                    }
                }

            Foo:
                guidClicked = "";
                numRevealed = 0;
                playerChosen = false;
                string actionResult = sendPerformActionRequest("");
                self.actionResult = actionResult;
                richTextBox1.AppendText("\n" + self.actionResult);
                #endregion
            }
            else if (self.role == "robber")
            {
                #region robber
                label1.Text = "Choose a player to switch cards with them";
                string targetRole = "";
                while (true)
                {
                retry:
                    if (guidClicked == "")
                        Application.DoEvents();
                    else
                    {
                        try { Guid.Parse(guidClicked); }
                        catch { MessageBox.Show("Choose a player"); guidClicked = ""; goto retry; }
                        foreach (Players p in playerData.players)
                        {
                            if (p.gameID == (string)guidClicked)
                            {
                                if (guidClicked == self.gameID)
                                {
                                    MessageBox.Show("You cant rob yourself");
                                    guidClicked = "";
                                    goto retry;
                                }
                                targetRole = p.role;
                                p.role = "robber";
                                self.role = targetRole;
                                foreach (Button b in playerButtons)
                                {
                                    if ((string)b.Tag == p.gameID)
                                        b.Text = "<Player> " + p.gameTag + "\nRole: " + p.role + "?";
                                    else if ((string)b.Tag == self.gameID)
                                        b.Text = "<ME> " + self.gameTag + "\nRole: " + self.role + "?";
                                }
                                guidClicked = "";
                                string actionResult = sendPerformActionRequest(p.gameID);
                                self.actionResult = actionResult;
                                richTextBox1.AppendText("\n" + self.actionResult);
                                goto Foo;
                            }
                        }
                    }
                }
            Foo:
                Console.WriteLine("done");
                #endregion
            }
            else if (self.role == "troublemaker")
            {
                #region troublemaker
                label1.Text = "Choose two players to switch their cards...";
                string first = "";
                while (true)
                {
                retry:
                    if (guidClicked == "")
                    {
                        Application.DoEvents();
                    }
                    else
                    {
                        try
                        {
                            Guid.Parse(guidClicked);
                            if (guidClicked == self.gameID)
                            {
                                MessageBox.Show("You can only switch OTHER people's roles");
                                guidClicked = "";
                                goto retry;
                            }
                            if (first == "")
                            {
                                first = guidClicked;
                                guidClicked = "";
                            }
                            else
                            {
                                if (guidClicked == first)
                                {
                                    MessageBox.Show("two of the same player clicked. try DIFFERENT players");
                                    guidClicked = "";
                                    goto retry;
                                }
                                string actionResult = sendPerformActionRequest(first + "+" + guidClicked);
                                self.actionResult = actionResult;
                                richTextBox1.AppendText("\n" + self.actionResult);
                                label1.Text = "You switched the two player's cards";
                                string one = "1", two = "2";
                                foreach (Players s in playerData.players)
                                {
                                    if (s.gameID == first)
                                        one = s.gameTag;
                                    else if (s.gameID == guidClicked)
                                        two = s.gameTag;
                                }

                                richTextBox2.AppendText("\nYou Switched " + one + "'s role with " + two);
                                guidClicked = "";
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("invalid choice, please choose a player");
                            guidClicked = "";
                        }
                    }
                }
                #endregion
            }
            else if (self.role == "drunk")
            {
                #region drunk
                label1.Text = "Choose a middle card to determine your new role as a drunk...";
                while (true)
                {
                    if (guidClicked == "")
                    {
                        Application.DoEvents();
                    }
                    else
                    {
                        try
                        {
                            //int.Parse(guidClicked);
                            string actionResult = sendPerformActionRequest((int.Parse(guidClicked) - 1).ToString());
                            self.actionResult = actionResult;
                            richTextBox1.AppendText("\n" + self.actionResult);
                            label1.Text = "Your new role has been set";
                            break;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("invalid choice, please choose a middle card");
                            guidClicked = "";
                        }
                    }
                }
                #endregion
            }
            else if (self.role == "insomniac")
            {
                //broken, the role is revealed before the server even processes it. >> not anymore :)
                #region insomniac
                string actionResult = sendPerformActionRequest("");
                self.actionResult = actionResult;
                richTextBox1.AppendText("\n" + self.actionResult);

                if (self.role != self.actionResult)
                {
                    richTextBox2.AppendText("YOUR ROLE CHANGED!!!");
                    MessageBox.Show("YOUR ROLE CHANGED!!!");
                }

                self.role = self.actionResult;

                foreach (Button b in playerButtons)
                    if ((string)b.Tag == self.gameID)
                        b.Text = "<ME> " + self.gameTag + "\nRole: " + self.role;
                #endregion
            }
            /*else if (self.role == "hunter")
            {
                //hunter
                #region hunter
                label1.Text = "Choose a player to shoot when you get accidently lynched.";
                while (true)
                {
                retry:
                    if (guidClicked == "")
                        Application.DoEvents();
                    else
                    {
                        try { Guid.Parse(guidClicked); }
                        catch { MessageBox.Show("Choose a player"); guidClicked = ""; goto retry; }

                        if (guidClicked == self.gameID)
                        {
                            MessageBox.Show("You cant shoot yourself");
                            guidClicked = "";
                            goto retry;
                        }
                        else
                        {
                            string actionResult = sendPerformActionRequest(guidClicked);
                            self.actionResult = actionResult;
                            richTextBox1.AppendText("\n" + self.actionResult);
                        }

                    }
                }
            Foo:
                Console.WriteLine("done");
                #endregion
            }*/
            else
            {
                #region all other roles
                string actionResult = sendPerformActionRequest("");
                self.actionResult = actionResult;
                richTextBox1.AppendText("\n" + self.actionResult);
                #endregion
            }

            guidClicked = "";
            allowButtonClicks = false;

            //wait 10min
            Stopwatch t = new Stopwatch();
            double timeToStop = 3 * 59;

            string request = self.gameID + "|||DiscussionWait";
            string response2 = sendRequest(request);
            double serverTimeInSeconds = double.Parse(response2.Substring(24));
            timeToStop -= serverTimeInSeconds;
            t.Start();
            label1.Text = "Discussion Start! Find the werewolf!";
            while (true)
            {
                if (t.Elapsed.TotalSeconds < timeToStop)
                {
                    //wait
                    label1.Text = "Discussion Start! Find the werewolf!" + "Time left: " + (Math.Floor(timeToStop - t.Elapsed.TotalSeconds)).ToString() + " Seconds";
                    Application.DoEvents();
                }
                else
                {
                    //send in wait request
                    label1.Text = "Syncing Timer & Requesting game state...";
                    request = self.gameID + "|||DiscussionWait";
                    response2 = sendRequest(request);
                    if (response2.Contains("VoteStart"))
                    {
                        //playerData = JsonConvert.DeserializeObject<JsonStruct>(response2.Substring(22));
                        break;
                    }

                    //t.Restart();
                }
            }
            t.Reset();

            //voting started
            label1.Text = "Vote the werewolf!";
            if (self.role == "hunter")
                label1.Text = "Vote the werewolf! \n--Remeber, if you get lynched, whoever you voted for also dies!";
            button5.Text = "Vote!";
            guidClicked = "";
            button5.Show();

        }

        private void button4_Click(object sender, EventArgs e)//test button to generate buttons using ./data.txt
        {
            //richTextBox1.AppendText(JsonConvert.SerializeObject(playerData));
            //TEST FUNCTIONS
            
            Jia.Library l = new Jia.Library();
            string data = string.Join("\n", l.read(path: System.IO.Directory.GetCurrentDirectory() + "\\data.txt"));
            JsonStruct pd = JsonConvert.DeserializeObject<JsonStruct>(data);
            self.gameID = pd.players[0].gameID;
            self.gameTag = pd.players[0].gameTag;
            self.role = pd.players[0].role;
            playerData = pd;
            generatePlayerButtons(pd);
            button3.Show();

            /*
            var radius = 210;
            var angle = 360 / 15 * Math.PI / 180.0f;
            var center = new Point(210, 210);
            var testButtons = new List<Button>();

            for (int i = 0; i < 15; i++)
            {
                var x = richTextBox2.Width + 50 + center.X + Math.Cos(angle * i) * radius;
                var y = center.Y + Math.Sin(angle * i) * radius;
                Button newButton = new Button();
                newButton.Left = (int)(x);
                newButton.Top = (int)(y);
                newButton.Height = 65;
                newButton.Width = 125;
                newButton.Text = i.ToString() + "asdfasdfasdfasdfasdfasdfsadfasdfasfd";
                newButton.Click += new EventHandler(this.generatedButtonClickEvent);

                this.Controls.Add(newButton);
                newButton.Show();
                playerButtons.Add(newButton);
            }

            for (int i = 0; i < 3; i++)
            {
                var x = center.X + richTextBox2.Width + 25;
                var y = center.Y + i * 25;
                Button newButton = new Button();
                newButton.Left = (int)(x);
                newButton.Top = (int)(y);
                newButton.Height = 20;
                newButton.Width = 200;
                newButton.Text = i.ToString() + "middle asdfasdfasdfasdf";
                newButton.Click += new EventHandler(this.generatedButtonClickEvent);

                this.Controls.Add(newButton);
                newButton.Show();
                middleButtons.Add(newButton);
            }*/
        }

        private void button5_Click(object sender, EventArgs e) //vote button
        {

            //     retry:
            label1.Text = "Vote a player...";
            button5.Hide();
            allowButtonClicks = true;
            string request, response2;

            while (true)
            {
            retry:
                if (guidClicked == "")
                    Application.DoEvents();
                else
                {
                    try { Guid.Parse(guidClicked); }
                    catch { MessageBox.Show("You cant vote for middle cards"); guidClicked = ""; goto retry; }
                    if (guidClicked == self.gameID)
                    {
                        MessageBox.Show("Why would you want to lynch yourself?"); guidClicked = ""; goto retry;
                    }

                    self.voted = guidClicked;
                    request = self.gameID + "|||VoteWerewolf>>>" + guidClicked;
                    response2 = sendRequest(request);
                    if (response2.Contains("Status: Voted"))
                        goto waitForVoteEnd;
                    else
                        throw new Exception("Expected Voted, but got something else. ///CRASH OUTRAGOUSLY!");
                }
            }


        waitForVoteEnd:
            button5.Hide();
            label1.Text = "Waiting for others to vote...";
            allowButtonClicks = false;
            Stopwatch t = new Stopwatch();
            t.Start();
            while (true)
            {
                if (t.Elapsed.Seconds < 10)
                {
                    Application.DoEvents();
                    //t.Restart();
                }
                else
                {
                    request = self.gameID + "|||waitingforvoteresults";// + guidClicked;
                    response2 = sendRequest(request);
                    if (response2.Contains("Status: GameEnd"))
                    {
                        playerData = JsonConvert.DeserializeObject<JsonStruct>(response2.Substring(17));
                        break;
                    }

                    t.Restart();
                }
            }
            t.Reset();

            //getvoting result:
            //request = self.gameID + "|||getloser";// + guidClicked;
            // string response2 = sendRequest(request);
            //string loserID = sendRequest(request).Substring(17);
            int highest = -1;
            Dictionary<string, Players> players = new Dictionary<string, Players>();
            foreach (Players p in playerData.players)
            {
                players.Add(p.gameID, p);
                if (p.voteCount > highest)
                    highest = p.voteCount;
            }
            List<Players> losers = new List<Players>();
            foreach (Players p in playerData.players)
            {
                if (p.voteCount == highest)
                    losers.Add(p);
            }

            richTextBox2.AppendText("\n\nTHE FOLLOWING PLAYERS WERE LYNCHED: ");
            for (int i = 0; i < losers.Count; i++)// Players s in losers)
            {
                Players s = losers[i];
                richTextBox2.AppendText("\nPlayer " + s.gameTag + " had the role: " + s.role + " and was voted to by lynched by " + highest.ToString() + " players");
                if (s.role == "hunter")
                {
                    richTextBox2.AppendText("\n!!!With the hunter's last breath, he took " + players[s.actionResult].gameTag + " (" + players[s.actionResult].role + ") down with him");
                    losers.Add(players[s.actionResult]);
                }
            }

            foreach (Players p in losers)
            {
                if (p.role == "tanner")
                    richTextBox2.AppendText("\n\nTHE TANNER SUCCESSFULLY SUICIDED, THE WINNER IS THE TANNER");
                else if (p.role == "werewolf")
                    richTextBox2.AppendText("\n\nONE OF THE WEREWOLF(S) GOT LYNCHED, THE WINNER IS THE TOWNS PEOPLE");
                else
                    richTextBox2.AppendText("\n\nALL THE WEREWOLVES LIVED, THE WINNER IS THE WEREWOLVES");
            }

            richTextBox2.AppendText("\n\nGAME OVER\nFor a new game, restart the client :)");


            foreach (Button b in playerButtons)
                b.Hide();
            foreach (Button b in middleButtons)
                b.Hide();

            playerButtons.Clear();
            playerButtons = new List<Button>();
            middleButtons.Clear();
            middleButtons = new List<Button>();

            generatePlayerButtonsRevealed(playerData);
        }

        private void button6_Click(object sender, EventArgs e)//oos button
        {
            DialogResult dr = MessageBox.Show("Does the game feel out of sync with the other players? \nPress yes to resync the current game state with the server. \nWARNING: \n/OOS SUPPORT MUST BE ENABLED ON THE SERVER,\nTHE CLIENT WILL CRASH IF ITS NOT", "/OOS - Attempt to resync game data",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
            if (dr == DialogResult.Yes)
            {
                //fml i dont work :(
                richTextBox2.AppendText("Server did not reply with a valid resync response -- is /oos support turned on?");
            }
        }
        
        private string sendPerformActionRequest(string action)
        {
            string request = self.gameID + "|||performRoleAction>>>" + action;
            string response2 = sendRequest(request);
            label1.Text = "Waiting for other players to complete their action";
            Stopwatch t = new Stopwatch();
            if (response2.Contains("Status: IllegalAction"))
            {
                MessageBox.Show("A wrong command was issued");
            }
            else
            {
                request = self.gameID + "|||WaitingForActions";
                response2 = sendRequest(request);
                if (response2.Contains("Status:ActionWait"))
                {
                    button3.Hide();
                    label1.Text = "Waiting for other players to complete their action...";
                    t.Start();
                    while (true)
                    {
                        if (t.Elapsed.Seconds < 10)
                        {
                            //wait
                            Application.DoEvents();
                        }
                        else
                        {
                            //send in wait request
                            request = self.gameID + "|||WaitingForActions";
                            response2 = sendRequest(request);
                            if (response2.Contains("Status:ActionComplete"))
                            {
                                //playerData = JsonConvert.DeserializeObject<JsonStruct>(response2.Substring(22));
                                break;
                            }

                            t.Restart();
                        }
                    }
                    t.Reset();
                }
            }

            // string retStr;
            try { return response2.Substring(23); }
            catch { return ""; }
            //   return response2.Substring(23);
        }

        //player buttons generation
        private void generatePlayerButtons(JsonStruct playerData)
        {
            //generate player buttons in a circle
            var radius = 210;
            var angle = 360 / playerData.totalPlayers * Math.PI / 180.0f;
            var center = new Point(210, 210);
            var testButtons = new List<Button>();

            for (int i = 0; i < playerData.totalPlayers; i++)
            {
                var x = richTextBox2.Width + 50 + center.X + Math.Cos(angle * i) * radius;
                var y = center.Y + Math.Sin(angle * i) * radius;
                Button newButton = new Button();
                newButton.Left = (int)(x);
                newButton.Top = (int)(y);
                newButton.Height = 65;
                newButton.Width = 125;
                if (playerData.players[i].gameID == self.gameID)
                    newButton.Text = "<ME> " + playerData.players[i].gameTag + " \nRole: " + playerData.players[i].role;
                else
                    newButton.Text = "<Player> " + playerData.players[i].gameTag + " \nRole: " + "Unknown"; // playerData.players[i].role;
                newButton.Tag = playerData.players[i].gameID;
                newButton.Click += new EventHandler(this.generatedButtonClickEvent);

                this.Controls.Add(newButton);
                newButton.Show();
                playerButtons.Add(newButton);
            }

            //middle cards
            for (int i = 0; i < 3; i++)
            {
                var x = center.X + richTextBox2.Width + 15 ;
                var y = center.Y -2 + i*25;
                Button newButton = new Button();
                newButton.Left = (int)(x);
                newButton.Top = (int)(y);
                newButton.Height = 20;
                newButton.Width = 200;
                newButton.Text = "Middle Card " + (i + 1).ToString() + ": " + "Unknown"; // + playerData.middleCards[i];
                newButton.Tag = (i + 1).ToString();
                newButton.Click += new EventHandler(this.generatedButtonClickEvent);

                this.Controls.Add(newButton);
                newButton.Show();
                middleButtons.Add(newButton);
            }
        }
        private void generatePlayerButtonsRevealed(JsonStruct playerData)
        {

            Button button;

            for (int i = 0; i < playerData.totalPlayers; i++) //
            {
                button = new Button();
                button.Width = 350;
                button.Height = 20;
                button.Left = this.Width - button.Width / 4 - button.Width;
                button.Top = button.Height + i * button.Height;

                if (playerData.players[i].gameID == self.gameID)
                    button.Text = "<ME> " + playerData.players[i].gameTag + "\nRole: " + playerData.players[i].role;
                else
                    button.Text = "<Player> " + playerData.players[i].gameTag + "\nRole: " + playerData.players[i].role;
                button.Tag = playerData.players[i].gameID;
                button.Click += new EventHandler(this.generatedButtonClickEvent);
                //button.Text = i.ToString();
                this.Controls.Add(button);
                button.Show();
                playerButtons.Add(button);
            }
            for (int i = 0; i < 3; i++)
            {
                button = new Button();
                button.Width = 200;
                button.Height = 20;
                button.Left = this.Width - button.Width - button.Width;
                button.Top = this.Height - button.Height * 3 - 50 + i * button.Height;
                button.Text = "Middle Card " + (i + 1).ToString() + ": " + playerData.middleCards[i];
                button.Tag = (i + 1).ToString();
                button.Click += new EventHandler(this.generatedButtonClickEvent);
                this.Controls.Add(button);
                button.Show();
                middleButtons.Add(button);
            }
        }

        private void generatedButtonClickEvent(object sender, EventArgs e)//assign guid (button.tag) clicked to a variable
        {
            var button = sender as Button;
            if (allowButtonClicks)
                guidClicked = (string)button.Tag;
        }

        //on textchanged of the rich textbox, scroll to the bottom.
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.ScrollToCaret();
        }
        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {
            richTextBox2.ScrollToCaret();
        }

        //allow enter key to have the same function as save/connect - 'button1'.click();
        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                if (!serverSet)
                {
                    IPOrHostName = textBox1.Text;
                    if (IPOrHostName == "l")
                        IPOrHostName = "127.0.0.1";
                    else if (IPOrHostName == "h")
                        IPOrHostName = "192.168.10.159";

                    serverSet = true;
                    textBox1.Clear();
                    label1.Text = "Type in an IGN then click the connect button: ";
                    button1.Text = "Connect!";
                }
                else
                {

                    //NEW PLAYER JOINS, FIRST COMMAND GETS GUID
                    if (textBox1.Text != "")
                    {
                        string request = textBox1.Text + "|||NewPlayer";
                        string response2 = sendRequest(request);
                        if (response2 == "error")
                            MessageBox.Show("error: 0x00000, FATAL ERROR: NO ERROR XD");
                        else if (response2.Contains("TooManyPlayers") || response2.Contains("NewUserRequired"))
                            MessageBox.Show("Number of players in lobby maxed, try again");
                        else
                        {
                            self.gameID = response2.Substring(13);
                            self.gameTag = textBox1.Text;
                            self.ready = false;
                            self.voted = "Nobody";
                            self.role = "Unassigned";
                            self.voteCount = 0;
                            self.actionDone = false;
                            self.actionResult = "";
                            self.actionInput = null;
                            //richTextBox1.AppendText("\nMy role's action's results: " + self.actionResult);
                            textBox1.Clear();
                            textBox1.Hide();
                            label1.Text = "Click Ready Button when ready";
                            button1.Hide();
                            button2.Text = "Ready";
                            button2.Show();
                        }
                    }
                    else { MessageBox.Show("Choose an IGN first"); }
                }
            }
        }
        #endregion
    }


    // State object for receiving data from remote device.
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    public class Players //data structure for individual player data
    {
        public string gameID { get; set; }
        public string role { get; set; }
        public string gameTag { get; set; }
        public bool ready { get; set; }
        public string voted { get; set; }
        public int voteCount { get; set; }
        public string actionResult { get; set; }
        public bool actionDone { get; set; }
        public List<string> actionInput { get; set; }
    }
    public class JsonStruct //json structure for player datas and middle cards
    {
        public int totalPlayers { get; set; }
        public List<Players> players { get; set; }
        public Dictionary<int, string> middleCards { get; set; }
    }
}
