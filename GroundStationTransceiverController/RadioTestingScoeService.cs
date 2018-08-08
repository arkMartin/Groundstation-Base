using Epfl.SwissCube.GroundSegment.Data.Egse;
using Epfl.SwissCube.GroundSegment.Egse.RouterClient;

namespace Astrocast.GroundSegment.GroundstationTransceiverController
{
    /// <summary>Implements an example SCOE Service, echoes incoming SCOE Command's data into a SCOE Observation report.</summary>
    public class RadioTestingScoeService
    {
        /// <summary>The Service Type of this service.</summary>
        public byte ServiceType { get; private set; }

        /// <summary>The EGSE Router Client instance to use to send messages.</summary>
        private readonly RouterClient _egseClient;

        /// <summary>Creates an instance of this SCOE Service.</summary>
        /// <param name="serviceType">The SCOE Service Type to use.</param>
        /// <param name="egseClient">The EGSE Router Client instance to use to send messages.</param>
        public RadioTestingScoeService(byte serviceType, RouterClient egseClient)
        {
            ServiceType = serviceType;
            _egseClient = egseClient;
        }

        /// <summary>Processes a SCOE Command Request for this service.</summary>
        /// <param name="message">The ReceiveData message that contains the SCOE Command Request.</param>
        /// <param name="command">The SCOE Command Request.</param>
        public void ProcessScoeCommand(ReceiveDataMessage message, ScoeCommand command)
        {
            // Do nothing if SCOE Command not for this service
            if (command.ServiceType != ServiceType)
                return;

            // Process command depending on service subtype
            switch(command.ServiceSubtype)
            {
                // SCOE Command (Type,1): Echo data
                case 1:
                    processEchoCommand(message, command);
                    break;
                // Unknown subtype
                default:
                    // Report failure to accept SCOE Command for unknown service subtype
                    _egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.FailedAcceptance, message.Token, message.SourceId, message.Token, message.SpacecraftId);
                    break;
            }
        }

        /// <summary>Echoes the data received in a SCOE Command Request (Type,1) into a report of type (Type,2).</summary>
        /// <param name="message">The ReceiveData message that contains the SCOE Command Request.</param>
        /// <param name="command">The SCOE Command Request.</param>
        private void processEchoCommand(ReceiveDataMessage message, ScoeCommand command)
        {
            // Acknowledge successful reception
            if (command.AckAccept)
                _egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.SuccessfulAcceptance, message.Token, message.SourceId, message.Token, message.SpacecraftId);


            // Construct and send SCOE Observation report with same data as command
            var scoeObvservationReport = new ScoeObservation(ServiceType, 2, command.Parameters);
            _egseClient.SendScoeObservationReport(scoeObvservationReport, message.SourceId, message.Token, message.SpacecraftId);


            // Acknowledge successful completion
            if (command.AckComplete)
                _egseClient.SendScoeCommandVerificationReport(ScoeVerificationType.SuccessfulCompletion, message.Token, message.SourceId, message.Token, message.SpacecraftId);
        }
    }
}
