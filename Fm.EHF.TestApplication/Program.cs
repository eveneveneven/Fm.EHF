using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Fm.EHF.SMP;
using STARTLibrary.accesspointService;
using STARTLibrary.src.eu.peppol.start.impl;
using STARTLibrary.src.eu.peppol.start.io;

namespace Fm.EHF.TestApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            var xml = new XmlDocument();
            xml.Load(@"SampleData\fminvoice.xml");
            var ehfUtility = new EHFUtility();

            //To validate document using DIFIs validationservice:
            ValidationResult validationResult1 = ehfUtility.ValidateDocument(xml); //auto-validate
            ValidationResult validationResult2 = ehfUtility.ValidateDocument(xml, PeppolDocumentType.InvoicePeppol4aEHF, UBLVersion.Version20); //validate using given documentType and version

            //Lookup service metadata:
            SmpInformation smpInfo = ehfUtility.LookupSMP("9908:974763907");
            Console.WriteLine("Found accesspoint url for 9908:974763907 at: " + smpInfo.Address);

            //Send invoice (SMP lookup to find correct access point):
            SendResult sendResult = ehfUtility.SendDocument(xml, "9908:453463465", "9908:974763907");

            //Send invoice (override endpoint-address, skipping SMP-lookup):
            var serviceMetadata = ehfUtility.GetServiceMetadataFromKnownEndpoint(); //from app.config
            var createRequest = GenerateTestRequest(xml, "9908:453463465", "9908:974763907");
            var result2 = ehfUtility.SendDocument(createRequest, "9908:453463465", "9908:974763907", serviceMetadata);
            Console.WriteLine("ok send to test: " + result2.MessageId);
        }

        private static CreateRequest GenerateTestRequest(XmlDocument xml, string sender, string receiver)
        {
            const string businessIdScheme = "iso6523-actorid-upis"; //trur denne bestemmer at vi skal bruke norsk standard på sender og mottaker-id. Usikker på kva "u"-en gjær..
            const string businessIdSchemeSender = "iso6523-actorid-upis"; //med ein ekstra u (samme som eksempel i pdf fra difi)
            const string documentIdScheme = "busdox-docid-qns"; //denne er fast!
            string documentIdValue = PeppolDocumentType.InvoicePeppol4a; //"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:www.cenbii.eu:transaction:biicoretrdm010:ver1.0:#urn:www.peppol.eu:bis:peppol5a:ver1.0:#urn:www.difi.no:ehf:faktura:ver1::2.0"; //norsk EHF efaktura 2.0 trur eg
            string processIdValue = PeppolProcessType.Bii04V1; //"urn:www.cenbii.eu:profile:bii05:ver1.0"; //processId identifiserer den prosess forretningsdokumentet er en del av. bii04 == kun faktura,  bii05 == faktura og kreditnota
            const string processIdScheme = "cenbii-procid-ubl"; //denne er fast!

            //Metadata for dokumentet som skal sendes
            IMessageMetadata metadata = new MessageMetadata()
            {
                RecipientIdentifier = new ParticipantIdentifierType { Value = receiver.ToLower(), scheme = businessIdScheme },
                SenderIdentifier = new ParticipantIdentifierType { Value = sender.ToLower(), scheme = businessIdSchemeSender },
                DocumentIdentifier = new DocumentIdentifierType { Value = documentIdValue, scheme = documentIdScheme },
                ProcessIdentifier = new ProcessIdentifierType { Value = processIdValue, scheme = processIdScheme },
                ChannelIdentifier = EHFUtility.DefaultChannel,
                MessageIdentifier = "uuid:" + Guid.NewGuid().ToString("D")
            };

            var messageBody = new STARTLibrary.accesspointService.Create { Any = new[] { xml.DocumentElement } };

            var createRequest = new STARTLibrary.accesspointService.CreateRequest
            {
                MessageIdentifier = metadata.MessageIdentifier,
                ChannelIdentifier = metadata.ChannelIdentifier,
                RecipientIdentifier = metadata.RecipientIdentifier,
                SenderIdentifier = metadata.SenderIdentifier,
                DocumentIdentifier = metadata.DocumentIdentifier,
                ProcessIdentifier = metadata.ProcessIdentifier,
                Create = messageBody
            };

            return createRequest;
        }
    }
}
