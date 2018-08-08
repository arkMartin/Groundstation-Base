using Epfl.SwissCube.GroundSegment.Data.Egse;
using Epfl.SwissCube.GroundSegment.Data.Egse.DataTypes;
using Epfl.SwissCube.GroundSegment.Egse.RouterClient;
using System;
using System.Configuration;
using System.Globalization;

namespace Astrocast.GroundSegment.GroundstationTransceiverController
{
    /// <summary>EGSE program that connects to the EGSE Router. Commands are sent via EGSE router to radios and rotators.</summary>
    public static class Program
    {
        /// <summary>Contains whether the program is being closed.</summary>
        private static volatile bool _closing = false;

        /// <summary>Example SCOE Service.</summary>
        private static RadioTestingScoeService _exampleScoeService;

        /// <summary>The EGSE Router Client instance.</summary>
        private static RouterClient _egseClient;

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

        private static void SendHamlibCommand()
        {
            ushort destinationId = ushort.Parse(ConfigurationManager.AppSettings[name: "EgseTargetClientId"]);
            uint token = 1;
            ushort spacecraftId = 1;
            string line;
            while (true)
            {
                Console.WriteLine("Enter valid hamlib command and press enter: ");
                line = Console.ReadLine();
                if ((line == "online") || (line == "o"))
                    ;
                else if ((line == "offline") || (line == "f"))
                    ;
                else if (line == "tcr")
                    ;
                else if (line == string.Empty)
                    break;
                else;

                ScoeCommand myScoeCommand = new ScoeCommand(132, 2, System.Text.Encoding.UTF8.GetBytes(line));
                myScoeCommand.Length.ToString();
                _egseClient.SendScoeCommandRequest(myScoeCommand, destinationId, token, spacecraftId);
            }
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
                    byte exampleScoeServiceType = byte.Parse(ConfigurationManager.AppSettings["ExampleScoeServiceType"], CultureInfo.InvariantCulture);
                    _exampleScoeService = new RadioTestingScoeService(exampleScoeServiceType, _egseClient);

                    // Try to connect to the EGSE Router
                    Console.WriteLine("Connecting to EGSE Router at {0}:{1}...", egseClient.RouterHost, egseClient.RouterPort);
                    egseClient.Connect();

                    // TODO: Implement here actions to perform outside of responding to incoming messages

                    SendHamlibCommand();

                    waitQuit();
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

        /// <summary>Handles the DataMessageReceived event of the EGSE Router Client.</summary>
        /// <param name="sender">The RouterClient instance, source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void egseClient_DataMessageReceived(object sender, DataMessageReceivedEventArgs e)
        {
            var egseClient = (RouterClient)sender;
            try
            {
                var message = e.DataMessage;

                // string textCommand = System.Text.Encoding.UTF8.GetString(message.).Trim('\0');

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
                        else if (command.ServiceType == _exampleScoeService.ServiceType)
                        {
                            _exampleScoeService.ProcessScoeCommand(message, command);
                        }
                        // Report failure to accept SCOE Command for unknown service
                        else
                        {
                            egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.FailedAcceptance, message.Token, message.SourceId, message.Token, message.SpacecraftId);
                        }
                    }
                }
                // get Observation report
                if (message.DataType == DataType.ScoeObservationReport)
                {
                    var prms = ((DataType3)message.Data).ScoeObservation;
                    string txt = System.Text.Encoding.UTF8.GetString(prms.Parameters).Trim('\0');
                    Console.WriteLine("Response from Groundstation: " + txt);
                }
            }
            catch (Exception ex)
            {
                outputError("Failed to process data message from EGSE Router: " + ex.Message);
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
