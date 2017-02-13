using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using System.Diagnostics;

namespace ONUW_SERVER
{
    public class ONUW_SERVER
    {
        public ONUW_SERVER()
        {
        }

        #region TCP functions

        // Thread signal.
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        static IPAddress ip = IPAddress.Parse("127.0.0.1");

        public static void StartListening(IPAddress ip)
        {
            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.
            // The DNS name of the computer
            // running the listener is "host.contoso.com".
            //IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName()); ;
            //IPAddress ipAddress = ip;// ipHostInfo.AddressList[0]; //IPAddress.Parse("192.168.10.159");
            
            IPEndPoint localEndPoint = new IPEndPoint(ip, 11000);
            

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();

        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            allDone.Set();

            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket. 
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read 
                // more data.
                content = state.sb.ToString();
                if (content.IndexOf("<EOF>") > -1)
                {
                    // All the data has been read from the 
                    // client. Display it on the console.
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                        content.Length, content);
                    // Echo the data back to the client.
                    //Send(handler, content.Remove(content.IndexOf("<EOF>")));
                    MainGameLoop(handler, content.Remove(content.IndexOf("<EOF>")));
                }
                else
                {
                    // Not all data received. Get more.
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private static void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            Console.WriteLine("sending data: " + data + "<EOF>");
            byte[] byteData = Encoding.ASCII.GetBytes(data+"<EOF>");

            // Begin sending the data to the remote device.
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        #endregion

        #region variables for the server
        static bool setup = true;
        static bool started = false;
        static bool ended = false;
        static Dictionary<string, Players> players = new Dictionary<string, Players>();
        static JsonStruct playerData = new JsonStruct();
        static Dictionary<int, string> middleCards = new Dictionary<int, string>();
        static bool everyOneDoneAction = false;
        static bool computerDoneActions = false;
        static bool computerProcessingActions = false;
        static Stopwatch t = new Stopwatch();
        static bool countedVote = false;
        static int NumPlayer = 5;
        static List<string> chosenRoles = new List<string>();

        #endregion

        #region ONUW server functions
        private static string getAction(string s)//split the incoming request to get the player action
        {
            return s.Substring(s.IndexOf("|||") + 3);
        }
        private static string getUser(string s)//split the incoming request to get the player guid
        {
            return s.Substring(0, s.IndexOf("|||"));
        }
        private static void generateRoles()//function to generate the roles that will be in play and assign them to players
        {
            List<string> rolesLeft = chosenRoles;
            Random rand = new Random();
            if (chosenRoles.Count != NumPlayer + 3)
                throw new Exception("not enough roles to play game");

            foreach (KeyValuePair<string, Players> p in players)
            {
                int randy = rand.Next(0, rolesLeft.Count);
                p.Value.role = rolesLeft[randy];
                rolesLeft.RemoveAt(randy);
            }
            //3 roles left
            for (int i = 0; i < 3; i++)
            {
                int randy = rand.Next(0, rolesLeft.Count);
                middleCards.Add(i, rolesLeft[randy]);
                rolesLeft.RemoveAt(randy);
            }
            if (rolesLeft.Count != 0)
                throw new Exception("not all roles assigned");

        }
        private static bool registerRoleActions(string id, string action)//set a flag in the player datas, not much use
        {
            //actions should be formatted as the follows: guid|||performRoleAction>>>input1+input2+input3...
            //do action.
            List<string> inputs;
            try
            {
                inputs = action.Substring(action.IndexOf(">>>") + 3).Split(char.Parse("+")).ToList();
            }
            catch (Exception e)
            {
                inputs = null;
            }

            var p = players[id];
            if (p.role == "werewolf")
            {
                //in: NA or 1 index
                //return: 2 guids of the werewolves
               /* if (inputs.Count == 1)
                {
                    int index = -1;

                    if (int.TryParse(inputs[0], out index))
                    {
                        //valid indice number
                        if (index >= 0 && index < 3)
                            p.actionInput = inputs;
                        else
                        {
                            p.actionInput = null;
                            return false;
                        }
                    }
                    else
                    {
                        p.actionInput = null;
                        return false;
                    }
                }
                else if (inputs.Count == 0)
                    p.actionInput = inputs;
                else
                {
                    p.actionInput = null;
                    return false;
                }*/
                p.actionDone = true;
            }
            else if (p.role == "seer")
            {
                //in: 1guid or 2 indices
                //return 1x guid:role OR 2x index:role. 
                /*if (inputs.Count == 1)
                {
                    Guid guid;
                    if (Guid.TryParse(inputs[0], out guid))
                        p.actionInput = inputs;
                    else
                    {
                        p.actionInput = null;
                        return false;
                    }
                }
                else if (inputs.Count == 2)
                {
                    int index = -1;
                    foreach (string s in inputs)
                    {
                        if (int.TryParse(s, out index))
                        {
                            //valid indice number
                            if (index >= 0 && index < 3)
                                p.actionInput = inputs;
                            else
                            {
                                p.actionInput = null;
                                return false;
                            }
                        }
                        else
                        {
                            p.actionInput = null;
                            return false;
                        }

                    }
                }
                else
                {
                    p.actionInput = null;
                    return false;
                }
                */
                p.actionDone = true;
            }
            else if (p.role == "robber")
            {
                //in: 1guid
                //return 1x guid:role
                if (inputs.Count == 1)
                {
                    Guid guid;
                    if (Guid.TryParse(inputs[0], out guid))
                        p.actionInput = inputs;
                    else
                    {
                        p.actionInput = null;
                        return false;
                    }
                }
                else
                {
                    p.actionInput = null;
                    return false;
                }

                p.actionDone = true;
            }
            else if (p.role == "troublemaker")
            {
                //in: 2 guid thats not hte players
                //return: NA
                if (inputs.Count == 2)
                {
                    Guid guid;
                    foreach (string s in inputs)
                    {
                        if (Guid.TryParse(s, out guid))
                        {
                            if (guid.ToString() != p.gameID)
                                p.actionInput = inputs;
                            else
                            {
                                p.actionInput = null;
                                return false;
                            }
                        }
                        else
                        {
                            p.actionInput = null;
                            return false;
                        }
                    }
                }
                else
                {
                    p.actionInput = null;
                    return false;
                }

                p.actionDone = true;
            }
            else if (p.role == "insomniac")
            {
                //in: NA
                //return self.role
                p.actionDone = true;
            }
            else if (p.role == "minion")
            {
                //in: NA
                //return: 2 guid of the werewolves
                p.actionDone = true;
            }
            else if (p.role == "drunk")
            {
                //in: indice
                //return: NA
                if (inputs.Count == 1)
                {
                    int index = -1;

                    if (int.TryParse(inputs[0], out index))
                    {
                        //valid indice number
                        if (index >= 0 && index < 3)
                            p.actionInput = inputs;
                        else
                        {
                            p.actionInput = null;
                            return false;
                        }
                    }
                    else
                    {
                        p.actionInput = null;
                        return false;
                    }
                }
                else
                {
                    p.actionInput = null;
                    return false;
                }

                p.actionDone = true;
            }
            else
            {
                p.actionDone = true;
            }

            foreach (var pl in players)
            {
                if (pl.Value.actionDone) everyOneDoneAction = true;
                else { everyOneDoneAction = false; break; }
            }
            return true;

        }
        private static void performRoleActions()//after all the performAction requests are received, process the actions in a specific order
        {
            computerDoneActions = false;
            computerProcessingActions = true;
            //all this does is assign a jsonStruct to player.actionResponse.
            //order: werewolf, minion, seer, robber, troublemaker, drunk, insomniac
            Dictionary<string, string> playerRoles = new Dictionary<string, string>();
            foreach (var p in players)
            {
                //bad logic!
                if (!playerRoles.ContainsKey(p.Value.role))
                    playerRoles.Add(p.Value.role, p.Value.gameID);
            }

            if (playerRoles.ContainsKey("copycat"))
            {
            }
            if (playerRoles.ContainsKey("doppleganger"))
            {
            }
            if (playerRoles.ContainsKey("diseased"))
            {
            }
            if (playerRoles.ContainsKey("cupid"))
            {
            }
            if (playerRoles.ContainsKey("instigator"))
            {
            }
            if (playerRoles.ContainsKey("priest") || playerRoles.ContainsKey("doppleganger priest"))
            {
            }
            if (playerRoles.ContainsKey("assassin") || playerRoles.ContainsKey("doppleganger assassin") || playerRoles.ContainsKey("apprentice assassin") || playerRoles.ContainsKey("doppleganger apprentice assassin"))
            {
            }
            if (playerRoles.ContainsKey("lovers"))
            {
            }
            if (playerRoles.ContainsKey("sentinel"))
            {
            }
            if (playerRoles.ContainsKey("alien"))
            {
            }
            if (playerRoles.ContainsKey("werewolf") || playerRoles.ContainsKey("mystic wolf") ||playerRoles.ContainsKey("alpha wolf") )
            {
                //client side
                //only 1 werewolf at this point !!!
                if (playerRoles.ContainsKey("werewolf"))
                {
                    players[playerRoles["werewolf"]].actionResult = "Completed";
                    Console.WriteLine("werewolf action registered");
                }
                if (playerRoles.ContainsKey("mystic wolf"))
                {
                    players[playerRoles["mystic wolf"]].actionResult = "Completed";
                    Console.WriteLine("mystic wolf action registered");
                }
                if (playerRoles.ContainsKey("alpha wolf"))
                {
                    players[playerRoles["alpha wolf"]].actionResult = "Completed";
                    Console.WriteLine("alpha wolf action registered");
                }

            }
            if (playerRoles.ContainsKey("minion"))
            {
                //client side
                players[playerRoles["minion"]].actionResult = "Completed";
                Console.WriteLine("minion action registered");
            }
            if (playerRoles.ContainsKey("apprentice tanner") || playerRoles.ContainsKey("doppleganger apprentice tanner"))
            {
            }
            if (playerRoles.ContainsKey("mason"))
            {
                players[playerRoles["mason"]].actionResult = "Completed";
                Console.WriteLine("mason action registered");
            }
            if (playerRoles.ContainsKey("thing"))
            {
            }
            if (playerRoles.ContainsKey("seer"))
            {
                //client side
                players[playerRoles["seer"]].actionResult = "Completed";
                Console.WriteLine("seer action registered");
            }
            if (playerRoles.ContainsKey("apprentice seer"))
            {
            }
            if (playerRoles.ContainsKey("robber"))
            {
                //in: 1guid
                players[playerRoles["robber"]].actionResult = players[players[playerRoles["robber"]].actionInput[0]].role;
                players[playerRoles["robber"]].role = players[players[playerRoles["robber"]].actionInput[0]].role;
                players[players[playerRoles["robber"]].actionInput[0]].role = "robber";
                Console.WriteLine("robber robbed: " + players[players[playerRoles["robber"]].actionInput[0]].gameTag);
            }
            if (playerRoles.ContainsKey("witch"))
            {
            }
            if (playerRoles.ContainsKey("pickpocket"))
            {
            }
            if (playerRoles.ContainsKey("troublemaker"))
            {
                string temp = players[players[playerRoles["troublemaker"]].actionInput[0]].role;
                players[players[playerRoles["troublemaker"]].actionInput[0]].role = players[players[playerRoles["troublemaker"]].actionInput[1]].role;
                players[players[playerRoles["troublemaker"]].actionInput[1]].role = temp;
                temp = "";
                players[playerRoles["troublemaker"]].actionResult = "Completed";
                Console.WriteLine("troublemaker switched: " + players[players[playerRoles["troublemaker"]].actionInput[0]].gameTag + " and " + players[players[playerRoles["troublemaker"]].actionInput[1]].gameTag);

            }
            if (playerRoles.ContainsKey("drunk"))
            {
                players[playerRoles["drunk"]].role = middleCards[int.Parse(players[playerRoles["drunk"]].actionInput[0])-1];
                middleCards[int.Parse(players[playerRoles["drunk"]].actionInput[0])-1] = "drunk";
                players[playerRoles["drunk"]].actionResult = "Completed";
                Console.WriteLine("drunk switched with middle card  " + (int.Parse(players[playerRoles["drunk"]].actionInput[0]) - 1).ToString() + " with the role: " + middleCards[int.Parse(players[playerRoles["drunk"]].actionInput[0]) - 1] + "\ndrunk's new role is: " + players[playerRoles["drunk"]].role);

            }
            if (playerRoles.ContainsKey("insomniac"))
            {
                players[playerRoles["insomniac"]].actionResult = players[playerRoles["insomniac"]].role;
                Console.WriteLine("insomniac's role is now: " + players[playerRoles["insomniac"]].role);
            }

            Console.WriteLine(players.ToString());

            computerDoneActions = true;
            computerProcessingActions = false;
        }

        private static void MainGameLoop(Socket handler, string data)//the main loop that takes in the request and progresses the game based on the current state
        {
            string action, id;

            try
            {
                action = getAction(data);
                id = getUser(data);
            }
            catch (Exception e)
            {
                Send(handler, "BAD REQUEST FORMAT");
                //throw new Exception();
                return;
            }

            if (setup)
            {
                if (action == "Start")
                {
                    if (players.ContainsKey(getUser(data)))
                    {
                        players[getUser(data)].ready = true;
                        Send(handler, "Status:Ready, waiting on others");
                    }
                    else
                    {
                        string guid = Guid.NewGuid().ToString();
                        players.Add(guid, new Players { gameTag = getUser(data), gameID = guid, role = "unassigned", voteCount = 0, voted = "", ready = false, actionResult = "", actionDone = false, actionInput = null });
                        Send(handler, "Status:GUID, " + guid);
                        players[getUser(data)].ready = true;
                    }

                }
                else if (action == "WaitingToStart")
                {
                    //return json of player data
                    playerData.totalPlayers = NumPlayer;
                    playerData.players = new List<Players>();
                    foreach (KeyValuePair<string, Players> p in players)
                        playerData.players.Add(p.Value);
                    playerData.middleCards = middleCards;

                    Send(handler, JsonConvert.SerializeObject(playerData));

                    foreach (var p in players)
                    {
                        if (p.Value.ready && players.Count == NumPlayer) setup = false;
                        else { setup = true; break; }
                    }
                }
                else if (action == "NewPlayer")
                {
                    string guid = Guid.NewGuid().ToString();
                    if (players.ContainsKey(id))
                        Send(handler, "NewUserRequired");
                    else if (players.Count + 1 > NumPlayer)
                        Send(handler, "TooManyPlayers");
                    else
                    {
                        players.Add(guid, new Players { gameTag = getUser(data), gameID = guid, role = "unassigned", voteCount = 0, voted = "", ready = false, actionResult = "", actionDone = false, actionInput = null });
                        Send(handler, "Status:GUID, " + guid);
                    }
                    //foreach (KeyValuePair<string, Players> p in players)
                    // {
                    //    Console.WriteLine(p.Key + p.Value.gameTag);
                    //}
                    //  Console.WriteLine();
                }
            }
            else if (!started && !setup)
            {
                started = true;
                //assign role
                generateRoles();
                //return json of player data
                playerData.totalPlayers = NumPlayer;
                playerData.players = new List<Players>();
                foreach (KeyValuePair<string, Players> p in players)
                    playerData.players.Add(p.Value);
                playerData.middleCards = middleCards;

                Send(handler, "setup complete");//JsonConvert.SerializeObject(playerData));

                foreach (KeyValuePair<string, Players> p in players)
                {
                    Console.WriteLine(p.Key + "\\" + p.Value.gameTag + "\\" + p.Value.role);
                }

            }
            else if (started && !ended)
            {
                if (action == "Start" || action == "WaitingToStart")
                {
                    //return json of player data
                    playerData.totalPlayers = NumPlayer;
                    playerData.players = new List<Players>();
                    foreach (KeyValuePair<string, Players> p in players)
                        playerData.players.Add(p.Value);
                    playerData.middleCards = middleCards;

                    Send(handler, "Status: GameStarting, " + JsonConvert.SerializeObject(playerData));

                    // Send(handler, "Status: GameStarted, " + s);
                    //here it should return all player's guid
                }
                else if (action == "GetRole")
                {
                    Send(handler, players[id].role);
                }
                else if (action.Contains("performRoleAction"))
                {
                    // Send(handler, "Status: ActionRegistered");// "+);
                    if (registerRoleActions(id, action))
                        Send(handler, "Status: ActionRegistered");
                    else
                        Send(handler, "Status: IllegalAction");
                }
                else if (action.Contains("WaitingForActions"))
                {
                    if (computerProcessingActions)
                        Send(handler, "Status:ActionWait");
                    else if (everyOneDoneAction && !computerDoneActions)
                    {
                        performRoleActions();
                        Send(handler, "Status:ActionWait");
                    }
                    else if (computerDoneActions)
                    {
                        Send(handler, "Status:ActionComplete, " + players[id].actionResult);// + performRoleActions(players[id].role, action));
                    }
                    else
                        Send(handler, "Status:ActionWait");
                }

                else if (action == "DiscussionWait")
                {
                    //10minute timer to vote
                    if (t.IsRunning)
                    {
                        if (t.Elapsed.TotalSeconds < 175)
                            Send(handler, "Status: DiscussionWait, " + t.Elapsed.TotalSeconds);
                        else
                        {
                            //t.Reset();
                            Send(handler, "Status: VoteStart");
                        }
                    }
                    else
                    {
                        t.Start();
                        Send(handler, "Status: DiscussionStart," + t.Elapsed.TotalSeconds);
                    }
                    //                    Send(handler, "Status: DiscussionWait");
                }
                else if (action.Contains("VoteWerewolf"))
                {
                    players[id].voted = action.Substring(action.IndexOf(">>>") + 3);
                    Send(handler, "Status: Voted " + action.ToString());

                }
                else if (action == "waitingforvoteresults")
                {
                    foreach (var p in players)
                    {
                        if (p.Value.voted != "") ended = true;
                        else { ended = false; break; }
                    }
                    Send(handler, "Status: VoteWait");
                }
            }
            else if (ended)
            {
                if (!countedVote)
                {
                    foreach (var p in players)
                    {
                        players[p.Value.voted].voteCount++;
                        if (p.Value.role == "hunter")
                            p.Value.actionResult = p.Value.voted;
                    }
                    countedVote = true;
                }

                if (action == "waitingforvoteresults")
                {
                    //return json of player data
                    playerData.totalPlayers = NumPlayer;
                    playerData.players = new List<Players>();
                    foreach (KeyValuePair<string, Players> p in players)
                        playerData.players.Add(p.Value);
                    playerData.middleCards = middleCards;

                    // Send(handler, JsonConvert.SerializeObject(playerData));

                    Send(handler, "Status: GameEnd, " + JsonConvert.SerializeObject(playerData));
                }

                Console.WriteLine("press any key to restart a new game, else press 'x' to close...");
                Console.ReadKey();
                Process.Start(System.IO.Directory.GetCurrentDirectory() + "\\ONUW_SERVER.exe", ip.ToString());
                Environment.Exit(0);
            }



            //set up the game.
            //data structure: username>>>action|||<eof>

        }
        #endregion

        public static int Main(String[] args) //entry point, set server/game parameters
        {
            if (Environment.MachineName.ToLower().Contains("brian") || Environment.MachineName.ToLower().Contains("tam") || Environment.MachineName.ToLower().Contains("auxori") || Environment.MachineName.ToLower().Contains("gucciball"))
                Console.WriteLine("screw you brian tam");
            while (true)
            {
                Console.WriteLine("Enter number of players: ");
                bool isInt = int.TryParse(Console.ReadLine(), out NumPlayer);
                if (isInt && NumPlayer >= 3 && NumPlayer <= 10)
                    break;
            }
            // NumPlayer = 3;//int.Parse(args[1]);
            if (NumPlayer <= 4)
                Console.WriteLine("Games more fun with >4 people fyi");

            //IPAddress ip = IPAddress.Parse("127.0.0.1");
            if (args.Length > 0)
            {
                if (!IPAddress.TryParse(args[0], out ip))
                    goto getip;
                else
                    goto noNeedToGetOP;
            }
            else
                goto getip;

        getip:
            while (true)
            {
                Console.WriteLine("Enter your ip address (see readme for server setup): ");
                string input = Console.ReadLine();
                if (input == "l") { input = "127.0.0.1"; }
                else if (input == "h") { input = "192.168.10.159"; }
                bool isIP = IPAddress.TryParse(input, out ip); //REMEBER TO CHANGE THIS
                if (isIP)
                    break;
            }

        noNeedToGetOP:

            Console.WriteLine("IP of the server set to: " + ip.ToString());
            int count = 0;

       reset:
            List<string> availableRoles = new List<string>();
            availableRoles.Add("villager");
            availableRoles.Add("villager");
            availableRoles.Add("villager");
            availableRoles.Add("werewolf");
            availableRoles.Add("werewolf");
            availableRoles.Add("seer");
            availableRoles.Add("robber");
            availableRoles.Add("troublemaker");
            availableRoles.Add("insomniac");
            availableRoles.Add("tanner");
            availableRoles.Add("hunter");
            availableRoles.Add("drunk");
            availableRoles.Add("minion");
            availableRoles.Add("mason");
            availableRoles.Add("mason");
            availableRoles.Add("mystic wolf");

            Console.WriteLine("Please choose {0} roles to begin the game...", (NumPlayer+3).ToString());
            Console.WriteLine("type a role from the following list or type in 'recommended' for auto role picks: ");
            Console.WriteLine(string.Join(",", availableRoles));

            while (count < NumPlayer + 3)
            {
                count++;
                string input = Console.ReadLine();
                if (input.ToLower() == "recommended")
                {
                    if (NumPlayer < 3)
                    {
                        Console.WriteLine("not enough players");
                        throw new Exception("game started without enough players");
                    }
                    else if (NumPlayer > 10)
                    {
                        Console.WriteLine("too many players");
                        throw new Exception("game started with too many players");
                    }
                    else //if (NumPlayer == 3)
                    {
                        chosenRoles.Add("villager");
                        chosenRoles.Add("werewolf");
                        chosenRoles.Add("werewolf");
                        chosenRoles.Add("seer");
                        chosenRoles.Add("robber");
                        chosenRoles.Add("troublemaker");
                        if (NumPlayer >= 4)
                            chosenRoles.Add("insomniac");
                        if (NumPlayer >= 5)
                            chosenRoles.Add("villager");
                        if (NumPlayer >= 6)
                            chosenRoles.Add("tanner");
                        if (NumPlayer >= 7)
                            chosenRoles.Add("hunter");
                        if (NumPlayer >= 8)
                            chosenRoles.Add("minion");
                        if (NumPlayer >= 9)
                            chosenRoles.Add("villager");
                        if (NumPlayer >= 10)
                            chosenRoles.Add("drunk");
                    }
                    break;
                }
                else if (availableRoles.Contains(input.ToLower()))
                {
                    chosenRoles.Add(input.ToLower());
                    availableRoles.Remove(input.ToLower());
                    Console.WriteLine("chosen " + input.ToLower());
                }
                else
                {
                    count--;
                    Console.WriteLine("invalid entry");
                }

                Console.WriteLine("type a role from the following list: ");
                Console.WriteLine(string.Join(",", availableRoles));
            }

            Console.WriteLine("The following roles are in play: ");
            Console.WriteLine(string.Join(",", chosenRoles));

        keyinput:
            Console.WriteLine("\nis it okay to start the server? Y/N");
            ConsoleKey keyInput = Console.ReadKey(true).Key;
            if (keyInput == ConsoleKey.N)
                goto reset;
            else if (keyInput == ConsoleKey.Y)
                Console.WriteLine("Server Starting....");
            else
            {
                Console.WriteLine("Invalid Choice");
                goto keyinput;
            }

            StartListening(ip);
            return 0;
        }
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

