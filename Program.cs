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
    /// <summary>Example EGSE program that connects to the EGSE Router and dispatches SCOE Commands to an example SCOE Service.</summary>
    public static class Program
    {
        /// <summary>Contains whether the program is being closed.</summary>
        private static volatile bool _closing = false;

        /// <summary>Transceiver SCOE Service.</summary>
        private static GroundStation _GroundStation;

        /// <summary>The EGSE Router Client instance.</summary>
        private static RouterClient _egseClient;

        private static Process rigControlDemon, rigControlDemon2, rigControlDemon_S_Band, 
                               rotControlDemon, rotControlDemon_S_Band;

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

        ///////////////////////////////////////////////////////////////////////////////
        static void HamLibInit()

        {
            System.Text.StringBuilder outputBuilder;
            ProcessStartInfo processStartInfo;

            outputBuilder = new System.Text.StringBuilder();

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
            // enable raising events because Process does not raise events by default
            rigControlDemon.EnableRaisingEvents = true;
            // attach the event handler for OutputDataReceived before starting the rigControlDemon
            rigControlDemon.OutputDataReceived += new DataReceivedEventHandler
            (
                delegate (object sender, DataReceivedEventArgs e)
                {
        // append the new data to the data already read-in
        outputBuilder.Append(e.Data);
                }
            );
            // start the rigControlDemon
            // then begin asynchronously reading the output
            // then wait for the rigControlDemon to exit
            // then cancel asynchronously reading the output
            try { rigControlDemon.Start();
                // string output2 = rigControlDemon.StandardOutput.ReadToEnd();
                // Console.WriteLine(output2);

                // string err = rigControlDemon.StandardError.ReadLine();
                // Console.WriteLine(err + "wrong parameterlist for hamlib: " + processStartInfo.Arguments.ToString());
                Console.WriteLine("rig control started");
            }
            catch (Exception ex)
            {
                outputError(message: $"Hamlib demon could not be started: check path and filename! \n \r {processStartInfo.FileName + ex.ToString()}");
            }
    }
    ///////////////////////////////////////////////////////////////////////////////////



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

                    // LaunchCommandLineApp2();

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
        static String openTCPstreamToHamLibD(string textCommand)
        {
            Int32 port = 4532;

            Int32 cnt = 0;

            byte [] response =  new byte [64];
         
                TcpClient client = new TcpClient(hostname: "127.0.0.1", port: port);
                char[] message = new char[22];
                message[0] = 'f';
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(textCommand);
                NetworkStream stream = client.GetStream();

            stream.ReadTimeout = 2000;  //milliseconds, so 2 seconds   XXXXXXXXXXXXXXX

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

        static void LaunchCommandLineApp2()
        {
            ProcessStartInfo start = new ProcessStartInfo();
            // Enter in the command line arguments, everything you would enter after the executable name itself
            start.Arguments = "";
            // Enter the executable to run, including the complete path
            start.FileName = "H:\\Eigene Dateien\\hamlib\\hamlib-w32-3.1\\bin\\rigctl.exe";
            // Do you want to show a console window?
            start.WindowStyle = ProcessWindowStyle.Hidden;
            start.CreateNoWindow = true;
            int exitCode;

            // Run the external rigControlDemon & wait for it to finish
            using (Process proc = Process.Start(start))
            {
                proc.WaitForExit();

                // Retrieve the app's exit code
                exitCode = proc.ExitCode;
            }
        }

        static void LaunchCommandLineApp()  // NOT USED @Klm
        {
            // For the example
            const string ex1 = "C:\\";
            const string ex2 = "Dir";

            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.Arguments = "-m 214 -r COM1 -s 9600";
            startInfo.FileName = "H:\\Eigene Dateien\\hamlib\\hamlib-w32-3.1\\bin\\rigctl.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = ex2;

            try
            {
                // Start the rigControlDemon with the info we specified.
                // Call WaitForExit and then the using statement will close.
                using (Process exeProcess = Process.Start(startInfo))
                {
                    exeProcess.WaitForExit();
                }
            }
            catch
            {
                // Log error.
            }
        }


        /// <summary>Handles the DataMessageReceived event of the EGSE Router Client.</summary>
        /// <param name="sender">The RouterClient instance, source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void egseClient_DataMessageReceived(object sender, DataMessageReceivedEventArgs e)
        {
            var egseClient = (RouterClient)sender;
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
                                    break;
                                case "rigctlVHFUHFDownLink":
                                    break;
                                case "rigctlSband":
                                    break;
                                case "rotctlVHFUHF":
                                    break;
                                case "rotctlS-Band":
                                    break;
                                default:
                                    break;
                            }
                            String rsp = openTCPstreamToHamLibD(target [1] /* textCommand */);
                            String txt = target[0]; txt += " ";
                            txt += target[1]; txt += " -> ";
                            txt += rsp;
                            command.Parameters = System.Text.Encoding.UTF8.GetBytes(s: txt);

                            _GroundStation.ProcessScoeCommand(message, command);
                          
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
