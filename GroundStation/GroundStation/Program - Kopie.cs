using Epfl.SwissCube.GroundSegment.Data.Egse;
using Epfl.SwissCube.GroundSegment.Data.Egse.DataTypes;
using Epfl.SwissCube.GroundSegment.Egse.RouterClient;
using System;
using System.Configuration;
using System.Globalization;
using System.Diagnostics; // @Klm
using System.Net.Sockets; // @Klm

namespace Astrocast.GroundSegment.GroundStation
{
    /// <summary>EGSE program that connects to the EGSE Router and dispatches SCOE Groundstation Commands to a radio and rotator.</summary>
    public static class Program
    {
        /// <summary>Contains whether the program is being closed.</summary>
        private static volatile bool _closing = false;

        /// <summary>Transceiver SCOE Service.</summary>
        private static GroundStation _GroundStation;

        /// <summary>The EGSE Router Client instance.</summary>
        private static RouterClient _egseClient;

        // Attributes for reservation state of VHFUHF and S-band equipment
        private static Boolean VHFUHFentityReserved = false;
        private static Boolean SbandentityReserved = false;


        // Hamlib processes for controlling transceivers and rotators
        private static Process rigControlDemon, rigControlDemon2, rotControlDemon;
        private static Process rigControlDemon_S_Band, rotControlDemon_S_Band_Dish;

        // TCP connections to and from Hamlib processes
        private static TcpClient clientVHFUHF1, clientRotControl;       // VHFUHF entity
        private static TcpClient clientVHFUHF2;                         // in case of separate VHFUHF UL/DL radios
        private static TcpClient clientSband, clientDishControl;        // S Band entity

        // TCP streams to and from Hamlib processes
        private static NetworkStream streamVHFUHF1, streamRotControl;  // VHFUHF entity
        private static NetworkStream streamVHFUHF2;                    // in case of separate VHFUHF UL/DL radios
        private static NetworkStream streamSband, streamDishControl;   // S Band entity
        
        // Status display for reservation state of VHFUHF and S-band equipment
        private static StatusDisplay sts = new StatusDisplay();

        /// <summary>Output an error message to the console.</summary>
        /// <param name="message">The error message.</param>
        static void outputError(string message)
        {
            lock (Console.Out)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(message);
                Console.ResetColor();
            }
        }
        static void StartProcessAndConnectPort(Process p, Int32 port, ref TcpClient client, ref NetworkStream stream)
        {
            try
            {
                rigControlDemon.Start();
                Console.WriteLine("rig control started");
            }
            catch (Exception ex)
            {
                outputError(message: $"Hamlib demon could not be started: check path and filename! \n \r {p.StartInfo.FileName + ex.ToString()}");
            }
            try
            {
                client = new TcpClient("127.0.0.1", port);
                stream = client.GetStream();
                stream.ReadTimeout = 2000;
            }
            catch (Exception ex)
            {
                outputError(message: $"Hamlib demon could not be TCP connected: check port number! \n \r {p.StartInfo.Arguments + ex.ToString()}");
            }
        }

        static void HamLibInit()
        {
            ProcessStartInfo processStartInfo;
            processStartInfo = new ProcessStartInfo();
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.Arguments = ConfigurationManager.AppSettings[name: "HamlibInvocationParametersTRX1"];
            processStartInfo.FileName = ConfigurationManager.AppSettings[name: "HamlibDirectory"];

            rigControlDemon = new Process();
            rigControlDemon.StartInfo = processStartInfo;
            rigControlDemon.EnableRaisingEvents = true;  // ??????? XXXXX

            StartProcessAndConnectPort(rigControlDemon, 4534, ref clientVHFUHF1, ref streamVHFUHF1);

            rotControlDemon = new Process();
            processStartInfo.Arguments = ConfigurationManager.AppSettings[name: "HamlibInvocationParametersROT99"];
            processStartInfo.FileName = ConfigurationManager.AppSettings[name: "RotlibDirectory"];
            rotControlDemon.StartInfo = processStartInfo;

            StartProcessAndConnectPort(rigControlDemon_S_Band, 4538, ref clientRotControl, ref streamRotControl);

            /////////    XXXXXX   StartProcessAndConnectPort(rigControlDemon, 4534, ref clientVHFUHF1, ref streamVHFUHF1);
            

        }


    /// <summary>Entry point method of the program.</summary>
    /// <param name="args">The command line arguments.</param>
    static void Main(string[] args)
        {
            try
            {
                // Create and configure router client instance using .config file settings
                using (var egseClient = new RouterClient("Egse"))
                {
                    _egseClient = egseClient;
                    egseClient.Connected += egseClient_Connected;
                    egseClient.ConnectionFailed += egseClient_ConnectionFailed;
                    egseClient.Disconnected += egseClient_Disconnected;
                    egseClient.DataMessageReceived += egseClient_DataMessageReceived;

                    // Read the Service Type of the Example SCOE Service from the configuration file and create it
                    byte GroundStationTransceiverClientScoeServiceType = byte.Parse(ConfigurationManager.AppSettings["ExampleScoeServiceType"], CultureInfo.InvariantCulture);
                    _GroundStation = new GroundStation(GroundStationTransceiverClientScoeServiceType, _egseClient);

                    // Try to connect to the EGSE Router
                    Console.WriteLine("Connecting to EGSE Router at {0}:{1}...", egseClient.RouterHost, egseClient.RouterPort);
                    egseClient.Connect();

                    // TODO: Implement here actions to perform outside of responding to incoming messages

                    HamLibInit();

                    waitQuit();
                    if (rigControlDemon != null)
                        { rigControlDemon.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                outputError("Unexpected error: " + ex.ToString());
            }
        }

        /// <summary>Waits for user to press enter or Ctrl+C to quit.</summary>
        private static void waitQuit()
        {
            // Wait for Ctrl-C to exit
            Console.WriteLine("Press Enter or Ctrl+C to quit...");
            Console.TreatControlCAsInput = true;
            while (!_closing)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == 3 || key.Key == ConsoleKey.Enter)
                    _closing = true;
            }

            Console.WriteLine("Stopping program...");
        }
        static String sendTCPstreamToHamLibD(string textCommand, TcpClient client, NetworkStream stream)
        {


            Byte[] data = System.Text.Encoding.ASCII.GetBytes(textCommand);
            stream.Write(data, 0, data.Length);
            stream.WriteByte(0x0D);
            stream.WriteByte(0x0A);

            if (stream.CanRead)
            {
                // Reads NetworkStream into a byte buffer.
                byte[] bytes = new byte[client.ReceiveBufferSize];

                // Read can return anything from 0 to numBytesToRead. 
                // This method blocks until at least one byte is read.
                try
                {
                    stream.Read(bytes, 0, (int)client.ReceiveBufferSize);

                    // Returns the data received from the host to the console.
                    string returndata = System.Text.Encoding.UTF8.GetString(bytes);
                    returndata = returndata.Trim('\0');
                    Console.WriteLine("This is what the host returned to you: " + returndata);
                    return returndata;
                }
                catch {
                    Console.WriteLine("TIMEOUT: ");
                    return "";
                }

            }
            return "";
        }

        /// <summary>Handles the DataMessageReceived event of the EGSE Router Client.</summary>
        /// <param name="sender">The RouterClient instance, source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void egseClient_DataMessageReceived(object sender, DataMessageReceivedEventArgs e)
        {
            var egseClient = (RouterClient)sender;
            String rsp, txt, rsp2, txt2 = "";

            try
            {
                var message = e.DataMessage;
                Console.WriteLine("Received data message from EGSE Router (Token={0}): {1}", message.Token, message.DataType);

                // Accept only SCOE Command Requests
                if (message.DataType == DataType.ScoeCommandRequest)
                {
                    // Get SCOE Command from message
                    var command = ((DataType1)message.Data).ScoeCommand;
                    if (command != null)
                    {
                        // Specific Ground Station Command: Send a frame
                        if (command.ServiceType == 1 && command.ServiceSubtype == 1)
                        {
                            processSendFrame(message, command);
                        }
                        // Check if command for SCOE Example Service
                        else if (command.ServiceType == _GroundStation.ServiceType)
                        {
                            // Transfer received CAT command to Hamlib @Klm

                            string textCommand =
                                System.Text.Encoding.UTF8.GetString(command.Parameters).Trim('\0');
                            Console.WriteLine("receive command {0}", textCommand);

                            string[] target = textCommand.Split(':');
                            switch (target[0]) {
                                case "rigctlVHFUHFUpLink":
                                    if (VHFUHFentityReserved) { 
                                       rsp = sendTCPstreamToHamLibD(target[1], clientVHFUHF1, stream: streamVHFUHF1);
                                       txt = target[0]; txt += " ";
                                       txt += target[1]; txt += " -> ";
                                       txt += rsp;
                                       command.Parameters = System.Text.Encoding.UTF8.GetBytes(s: txt);
                                       _GroundStation.ProcessScoeCommand(message, command);
                                    }
                                    else {
                                        Console.WriteLine("VHFUHF entity not Reserved");
                                    }
                                    break;
                                case "rigctlVHFUHFDownLink":
                                    if (VHFUHFentityReserved)
                                    {
                                        rsp = sendTCPstreamToHamLibD(target[1], clientVHFUHF1, stream: streamVHFUHF1);
                                        txt = target[0]; txt += " ";
                                        txt += target[1]; txt += " -> ";
                                        txt += rsp;
                                        command.Parameters = System.Text.Encoding.UTF8.GetBytes(s: txt);
                                        _GroundStation.ProcessScoeCommand(message, command);
                                    }
                                    else
                                    {
                                        Console.WriteLine("VHFUHF entity not Reserved (rigctl can not be executed) ");
                                    }
                                    break;
                           
                                case "rotctlVHFUHF":
                                    if (VHFUHFentityReserved)
                                    {
                                        rsp2 = sendTCPstreamToHamLibD(target[1], clientRotControl, stream: streamRotControl);
                                        txt2 = target[0]; txt2 += " ";
                                        txt2 += target[1]; txt2 += " -> ";
                                        txt2 += rsp2;
                                        command.Parameters = System.Text.Encoding.UTF8.GetBytes(s: txt2);
                                        _GroundStation.ProcessScoeCommand(message, command);
                                    }
                                    else
                                    {
                                        Console.WriteLine("VHFUHF entity not Reserved (rotctl can not be executed) ");
                                    }
                                    break;
                                case "rigctlSband":
                                    _GroundStation.ProcessScoeCommand(message, command);
                                    break;
                                case "rotctlS-Band":
                                    _GroundStation.ProcessScoeCommand(message, command);
                                    break;
                                case "requestVHFUHF":
                                    VHFUHFentityReserved = true;
                                    sts.SetVHFUHFstate(s: Lamps.State.occupied);
                                    command.Parameters = System.Text.Encoding.UTF8.GetBytes("access to VHFUHF entity granted");
                                    _GroundStation.ProcessScoeCommand(message, command);
                                    break;
                                case "releaseVHFUHF":
                                    VHFUHFentityReserved = false;
                                    sts.SetVHFUHFstate(s: Lamps.State.free);
                                    command.Parameters = System.Text.Encoding.UTF8.GetBytes("access to VHFUHF entity released");
                                    _GroundStation.ProcessScoeCommand(message, command);
                                    break;
                                case "requestSband":
                                    SbandentityReserved = true;
                                    sts.SetSbandstate(s: Lamps.State.occupied);
                                    command.Parameters = System.Text.Encoding.UTF8.GetBytes("access to S Band entity granted");
                                    _GroundStation.ProcessScoeCommand(message, command);
                                    break;
                                case "releaseSband":
                                    SbandentityReserved = false;
                                    sts.SetSbandstate(s: Lamps.State.free);
                                    command.Parameters = System.Text.Encoding.UTF8.GetBytes("access to S Band entity rleased");
                                    _GroundStation.ProcessScoeCommand(message, command);
                                    break;
                                default:
                                    Console.WriteLine("received illegal command {0}", textCommand);
                                    break;
                            }
                        }
                        // Report failure to accept SCOE Command for unknown service
                        else
                        {
                            egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.FailedAcceptance, message.Token, message.SourceId, message.Token, message.SpacecraftId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                outputError("Failed to rigControlDemon data message from EGSE Router: " + ex.Message);
            }
        }

        /// <summary>Processes a SCOE Command Request to send a frame (Ground Station specific).</summary>
        /// <param name="message">The ReceiveData message that contains the SCOE Command Request.</param>
        /// <param name="command">The SCOE Command Request.</param>
        private static void processSendFrame(ReceiveDataMessage message, ScoeCommand command)
        {
            // Only parameter of command is telecommand transfer frame
            byte[] frame = command.Parameters;

            // Acknowledge successful reception
            if (command.AckAccept)
                _egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.SuccessfulAcceptance, message.Token, message.SourceId, message.Token, message.SpacecraftId);

            // Process Telecommand Transfer Frame
            try
            {
                // TODO: Do something with Transfer Frame instead of just printing frame
                Console.WriteLine("TC Frame of {0} bytes received: {1}", frame.Length, BitConverter.ToString(frame));
            }
            catch (Exception ex)
            {
                outputError("Failed to forward frame to the satellite: " + ex.Message);

                // Report failure to transmit
                _egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.FailedCompletion, message.Token, message.SourceId, message.Token, message.SpacecraftId);
                return;
            }

            // Acknowledge successful completion
            if (command.AckComplete)
                _egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.SuccessfulCompletion, message.Token, message.SourceId, message.Token, message.SpacecraftId);
        }

        /// <summary>Handles the Connected event of the EGSE Router Client.</summary>
        /// <param name="sender">The RouterClient instance, source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void egseClient_Connected(object sender, EventArgs e)
        {
            Console.WriteLine("Connected to EGSE Router.");
        }

        /// <summary>Handles the ConnectionFailed event of the EGSE Router Client.</summary>
        /// <param name="sender">The RouterClient instance, source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void egseClient_ConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            e.Reconnect = true;
            Console.WriteLine("Failed to connect to the EGSE Router (will retry): " + (e.Exception != null ? e.Exception.Message : "no exception"));
        }

        /// <summary>Handles the Disconnected event of the EGSE Router Client.</summary>
        /// <param name="sender">The RouterClient instance, source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void egseClient_Disconnected(object sender, DisconnectedEventArgs e)
        {
            if (e.Reason == DisconnectionReason.ClientClose)
            {
                e.Reconnect = false;
                Console.WriteLine("EGSE Router disconnected.");
            }
            else
            {
                e.Reconnect = true;
                Console.WriteLine("Disconnected by the EGSE Router: {0}. Will try to reconnect...", e.Reason);
            }
        }
    }
}
