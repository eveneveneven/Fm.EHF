using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Fm.EHF.SMP;
using log4net;
using STARTLibrary.accesspointService;
using STARTLibrary.src.eu.peppol.start.common;
using STARTLibrary.src.eu.peppol.start.impl;
using STARTLibrary.src.eu.peppol.start.io;
using STARTLibrary.src.eu.peppol.start.security.common;
using STARTLibrary.src.eu.peppol.start.security.handler;
using STARTLibrary.src.eu.peppol.start.xml.parser;
using STARTLibrary.src.eu.peppol.start.xml.structure;
using ServiceMetadata = STARTLibrary.src.eu.peppol.start.impl.ServiceMetadata;

namespace Fm.EHF
{
    /// <summary>
    /// EHFUtility
    /// - validate UBL/EHF-documents using DIFIs validationservice (REST)
    /// - send UBL/EHF-documents using modified STARTLibrary45 from PEPPOLs sample .NET-implementation
    /// - perform SMP og SML lookup
    /// 
    /// SML - Service Metadata Locator, The PEPPOL SML defines which Service Metadata Publisher (SMP) to use for finding out the delivery details of any PEPPOL participant
    /// SMP - Service Metadata Publisher, Service Metadata Publishers (SMPs). They store information about the receiving capabilities of users connected to the PEPPOL network, providing details about the business document types supported, and the business collaboration profiles that can be processed through the infrastructure.
    /// UBL - Unified Business Language
    /// EHF - Elektronisk Handels Faktura (norwegian extension of UBL)
    /// PEPPOL - Pan-European Public eProcurement On-Line
    /// 
    /// TODO:
    /// - check if channelfactory is cached (overhead when creating proxy).
    /// </summary>
    public class EHFUtility
    {
        #region Init

        private static readonly ILog Log = LogManager.GetLogger(typeof(EHFUtility));
        private ChannelFactory<STARTLibrary.accesspointService.Resource> _channelFactory;
        private const int AssuranceLevel = 3;
        
        public const string DefaultSmlDomain = "sml.peppolcentral.org";
        public const string DefaultChannel = ""; //CH1? In PEPPOLs sample .NET-implementation receiverId was used. In oxalis CH1 is used? Seems to work woth empty string
        
        private static X509Certificate2 _serviceCertificate;
        private static X509Certificate2 _clientCertificate;
        private readonly string _peppolEndpointName;
        private readonly string _validatorServiceUrl;

        /// <summary>
        /// Creates a EHFUtility with channelfactory, configuration and certificates from app.config using default validation url http://vefa.difi.no/validate-ws/ and default endpointname SecurePeppolClient
        /// </summary>
        public EHFUtility()
            : this("SecurePeppolClient")
        {
        }

        /// <summary>
        /// Creates a EHFUtility with channelfactory, configuration and certificates from app.config using default validation url http://vefa.difi.no/validate-ws/
        /// </summary>
        /// <param name="peppolEndpointName"></param>
        public EHFUtility(string peppolEndpointName)
            : this(peppolEndpointName, "http://vefa.difi.no/validate-ws/")
        {            
        }

        /// <summary>
        /// Creates a EHFUtility with channelfactory, configuration and certificates from app.config, ..
        /// </summary>
        /// <param name="peppolEndpointName">default == SecurePeppolClient</param>
        /// <param name="validatorServiceUrl">url to validation service</param>
        public EHFUtility(string peppolEndpointName, string validatorServiceUrl)
        {
            if(string.IsNullOrEmpty(peppolEndpointName))
                throw new ArgumentNullException("peppolEndpointName", "peppolEndpointName cannot be null, use same peppolEndpointName as in app.config");
            if(string.IsNullOrEmpty(validatorServiceUrl))
                throw new ArgumentNullException("validatorServiceUrl", "cannot be null");

            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, error) => true;
            _peppolEndpointName = peppolEndpointName;
            _validatorServiceUrl = validatorServiceUrl;
            LoadCertificatesFromConfiguration();
            _channelFactory = Utilities.CreateResourceChannelFactory<Resource>(_peppolEndpointName, _clientCertificate, _serviceCertificate);
            Log.InfoFormat("EHFUtility created OK. EndpointName: {0} ValidationURL: {1}", _peppolEndpointName, _validatorServiceUrl);
        }

        private void LoadCertificatesFromConfiguration()
        {
            Log.Info("Loading certificates from config...");
            var credentials = STARTLibrary.src.eu.peppol.start.security.configuration.CertificatesConfigurationSection.Section.ClientCredentials.ByEndpointName(_peppolEndpointName);
            if (credentials == null)
            {
                var msg = String.Format("No certificate configuration found for endpoint \"{0}\" in PEPPOL configuration in app.config.", _peppolEndpointName);
                Log.Error(msg);
                throw new Exception(msg);
            }

            _clientCertificate = credentials.ClientCertificate.Certificate;
            _serviceCertificate = credentials.ServiceCertificate.Certificate;
            Log.InfoFormat("Load certificates OK. Client: {0} Server: {1}", _clientCertificate != null ? _clientCertificate.FriendlyName : "NA", _serviceCertificate != null ? _serviceCertificate.FriendlyName : "NA");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Performs automatic validation of UBL/EHF-document against DIFIs REST-based validationService (document-type is autodetected in validationservice, make sure ProfileID and CustomizationID is present in xml!)
        /// </summary>
        /// <param name="xmlDocument">UBL/EHF-document to validate</param>
        /// <returns>A validationresult containing REST-response from DIFIs validationservice</returns>
        public virtual ValidationResult ValidateDocument(XmlDocument xmlDocument)
        {
            var start = DateTime.Now;
            if(xmlDocument==null)
                throw new ArgumentNullException("xmlDocument", "cannot be null");

            var response = PostXmlDocument(_validatorServiceUrl, xmlDocument);
            var tempResult = new ValidationResult(response, DateTime.Now.Subtract(start));
            return tempResult;            
        }

        /// <summary>
        /// Performs validation of UBL/EHF-document against DIFIs REST-based validationService using given schema and version.        
        /// </summary>
        /// <param name="xmlDocument">UBL/EHF-document to validate</param>
        /// <param name="documentType">Used to construct REST-based URL. See <see cref="PeppolDocumentType"/> for helpers. </param> 
        /// <param name="version">Used to construct REST-based URL. See <see cref="UBLVersion"/> for helpers. Samplevalue = 2.0 </param>
        /// <returns>A validationresult containing REST-response from DIFIs validationservice</returns>
        public virtual ValidationResult ValidateDocument(XmlDocument xmlDocument, string documentType, string version)
        {
            var start = DateTime.Now;
            if (xmlDocument == null)
                throw new ArgumentNullException("xmlDocument", "cannot be null");
            
            var completeValidationUrl = string.Format("{0}{1}{2}/{3}", _validatorServiceUrl, _validatorServiceUrl.EndsWith("/") ? string.Empty : "/", version, HttpUtility.UrlEncode(documentType));

            var response = PostXmlDocument(completeValidationUrl, xmlDocument);
            var tempResult = new ValidationResult(response, DateTime.Now.Subtract(start));
            return tempResult;        
        }

        /// <summary>
        /// Helper-method to perform REST-request
        /// </summary>
        /// <param name="url"></param>
        /// <param name="xmlDocument">xml-request to post</param>
        /// <returns>REST-response as xml</returns>
        private XmlDocument PostXmlDocument(string url, XmlDocument xmlDocument)
        {
            Log.Info("Start PostXmlDocument");
            XmlDocument xmlResponse = null;
            HttpWebResponse objHttpWebResponse = null;
            Stream objRequestStream = null;
            Stream objResponseStream = null;

            var objHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);

            try
            {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(xmlDocument.InnerXml);
                objHttpWebRequest.Method = "POST";
                objHttpWebRequest.ContentLength = bytes.Length;
                objHttpWebRequest.ContentType = "application/xml; encoding='utf-8'";
                objHttpWebRequest.MediaType = "application/xml";
                objRequestStream = objHttpWebRequest.GetRequestStream();
                objRequestStream.Write(bytes, 0, bytes.Length);
                objRequestStream.Close();

                objHttpWebResponse = (HttpWebResponse)objHttpWebRequest.GetResponse();

                if (objHttpWebResponse.StatusCode == HttpStatusCode.OK)
                {

                    objResponseStream = objHttpWebResponse.GetResponseStream();
                    if (objResponseStream == null)
                        throw new Exception("Could not get stream from response. objResponseStream was null.");
                    var objXmlReader = new XmlTextReader(objResponseStream);

                    var xmldoc = new XmlDocument();
                    xmldoc.Load(objXmlReader);
                    xmlResponse = xmldoc;
                    objXmlReader.Close();
                }
                objHttpWebResponse.Close();
                Log.Info("PostXmlDocument returned OK");
            }
            catch (WebException webException)
            {
                Log.Error(webException);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
            finally
            {
                if (objRequestStream != null) 
                    objRequestStream.Close();
                if (objResponseStream != null) 
                    objResponseStream.Close();
                if (objHttpWebResponse != null) 
                    objHttpWebResponse.Close();
            }

            return xmlResponse;
        }

        #endregion

        #region SMP og SML lookup

        /// <summary>
        /// Lookup SMP (service-metadata) for given participant. 
        /// Constructs SMP-url and extracts metadata from SMP (endpointaddress for given participant and receiving-capabilities for that endpoint)
        /// SMP = Service Metadata Provider, See also http://www.peppol.eu/pilot-reporting/infrastructure/post-award-infrastructure-1/smp-providers
        /// </summary>
        /// <param name="participantIdentifier">participant-id, f.eks. 9908:974763907 where 9908 means norway no tax, and 974763907 is org.nr</param>       
        /// <returns>Information about the accesspoint for given participant. throws <see cref="SmpLookupException"/> if participant is not found</returns>
        public virtual SmpInformation LookupSMP(string participantIdentifier)
        {
            try
            {
                Log.Info("Start LookupSMP");
                if(string.IsNullOrEmpty(participantIdentifier))
                    throw new ArgumentNullException("participantIdentifier", "cannot be null");

                var helper = new Helper();

                const string businessIdScheme = "iso6523-actorid-upis"; //når vi spør SMP om info MÅ vi bruke denne verdien. Denne betyr at vi bruker en eller annen internasjonal ISO-standard for å definere participant-iden. POLICY 7 XML attributes for Participant Identifiers in BusDox: The “scheme” attribute must be populated with the value "iso6523-actorid-upis" (see POLICY 5) in all instances of the “ParticipantIdentifier” element               
                const string documentIdScheme = "busdox-docid-qns"; //denne er alltid fast.. POLICY 10: The PEPPOL document type identifier scheme to be used is: busdox-docid-qns. Applies to: all document type identifiers in all components

                //fungerer: (men då spør vi bare om aksesspunkt som støtter generell type?) Men satser på at det går greit, siden oxalis i hverty fall bare har ett endpoint for alle dokument? trur denne spør om AP godtar fra versjon 1 til 2 og..
                const string documentIdValue = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:www.cenbii.eu:transaction:biicoretrdm010:ver1.0:#urn:www.peppol.eu:bis:peppol4a:ver1.0::2.0";

                //Participant identifiers – consisting of scheme and value – are encoded as follows into a DNS name: 292 B-<hash-of-value>.<scheme>.<SML-zone-name>
                var smpUrl = helper.BuildSmpUrl(participantIdentifier, businessIdScheme, DefaultSmlDomain);

                try
                {
                    Log.InfoFormat("Contacting SMP URL: {0}", smpUrl);

                    using (var client = new SimpleWebClient())
                    {
                        client.HeadOnly = true;
                        var s1 = client.DownloadString(smpUrl);
                    }

                    Log.Info("SMP OK");
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                    throw new SmpLookupException(string.Format("Error contacting smpUrl ({0})", smpUrl), smpUrl, exception);
                }

                string endpointAddress;

                try
                {
                    Log.Info("Getting endpointaddres from service metadata..");
                    endpointAddress = helper.GetEndPointAddress(participantIdentifier, businessIdScheme, DefaultSmlDomain, documentIdScheme, documentIdValue);
                    Log.InfoFormat("Endpoint found OK: {0}", endpointAddress);
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                    throw new SmpLookupException("Error getting endpointaddress from smp: " + exception.Message, smpUrl, exception);
                }

                ServiceGroup serviceGroupElements;

                try
                {
                    Log.Info("Parsing servicegroup-info from smp...");
                    serviceGroupElements = new ServiceGroupParser().GetServiceGroup(smpUrl);
                    Log.Info("Parse OK");
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                    throw new SmpLookupException("Error getting servicegroups from smp: " + exception.Message, smpUrl, exception);
                }

                var temp = new SmpInformation
                {
                    Address = endpointAddress,
                    FromSmpUrl = smpUrl,
                    ServiceGroupElements = serviceGroupElements
                };

                Log.Info("Returning SmpInfo");
                return temp;
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                throw;
            }
        }

        /// <summary>
        /// Returns servicemetadata NOT FROM SML, but from app.config. should only be used in debug when sending to a custom AP
        /// </summary>
        /// <returns>ServiceMetadata (address of access point and public server certificate)</returns>
        public ServiceMetadata GetServiceMetadataFromKnownEndpoint()
        {
            return ServiceMetadata.FromKnownEndpoint(_channelFactory);
        }

        /// <summary>
        /// Returns servicemetadata from SML - Service Metadata Locator (lookup receiver, get AP-address and AP-certificates)        
        /// See also http://www.peppol.eu/peppol_components/-transport-infrastructure/main-components/the-peppol-central-registry
        /// </summary>
        /// <param name="createRequest"></param>
        /// <returns>ServiceMetadata (address of access point and public server certificate)</returns>
        public ServiceMetadata GetServiceMetadataFromSML(CreateRequest createRequest)
        {
            try
            {
                if (createRequest == null)
                    throw new ArgumentNullException("createRequest", "createRequest cannot be null whne looking up servicemetadata");
                Log.InfoFormat("Looking up servicemetadata from request {0}", createRequest.MessageIdentifier);
                var metadata = ServiceMetadata.FromSml(DefaultSmlDomain, createRequest);
                Log.InfoFormat("Metadata lookup OK. Address: {0} Certificate: {1}", metadata.Address.AbsoluteUri, metadata.Certificate.FriendlyName);
                return metadata;
            }
            catch (Exception exception)
            {
                Log.Error("Error getting servicemetadata from sml");
                Log.Error(exception);
                throw;
            }
        }

        #endregion

        #region SendInvoice

        /// <summary>
        /// Super-simple method to send a norwegian EHF-invoice (targeting profile invoice only, not creditnota, <see cref="PeppolDocumentType.InvoicePeppol4aEHF"/> )
        /// </summary>
        /// <param name="xmlDocument">EHF-document to send</param>
        /// <param name="sender">sender-id, sample:9908:974763907</param>
        /// <param name="receiver">receiver-id, sample:9908:974763907</param>
        /// <returns>Returns a <see cref="SendResult"/> containing the unique ID of the sent message and response from access-point-service. If message is not sent, an exception is thrown </returns>
        public SendResult SendDocument(XmlDocument xmlDocument, string sender, string receiver)
        {
            string tempProfileId = PeppolProcessType.Bii04V1;

            try
            {
                var xDocument = xmlDocument.ToXDocument();
                XNamespace ns = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
                if (xDocument.Root != null)
                {
                    var xElement = xDocument.Root.Element(ns + "ProfileID");
                    if (xElement != null) 
                        tempProfileId = xElement.Value;
                }
            }
            catch (Exception exception)
            {
                Log.Error("Error in xpath to get customizationid from xml: " + exception.Message);
                Log.Error(exception);
            }

            Log.InfoFormat("Using profileID {0}", tempProfileId);

            var tempResult = SendDocument(xmlDocument, sender, receiver, PeppolDocumentType.InvoicePeppol4aEHF, tempProfileId);
            return tempResult;
        }

        /// <summary>
        /// Super-simple method to send a norwegian EHF-invoice (targeting profile invoice only, not creditnota, <see cref="PeppolDocumentType.InvoicePeppol4aEHF"/> )
        /// </summary>
        /// <param name="xDocument">EHF-document to send</param>
        /// <param name="sender">sender-id, sample:9908:974763907</param>
        /// <param name="receiver">receiver-id, sample:9908:974763907</param>
        /// <returns>Returns a <see cref="SendResult"/> containing the unique ID of the sent message and response from access-point-service. If message is not sent, an exception is thrown </returns>
        public SendResult SendDocument(XDocument xDocument, string sender, string receiver)
        {
            var oldXml = xDocument.ToXmlDocument();
            return SendDocument(oldXml, sender, receiver);
        }

        /// <summary>
        /// Super-simple method to send a EHF-invoice 
        /// </summary>
        /// <param name="xDocument">EHF-document to send</param>
        /// <param name="sender">sender-id, sample:9908:974763907</param>
        /// <param name="receiver">receiver-id, sample:9908:974763907</param>
        /// <param name="documentType">See <see cref="PeppolDocumentType"/></param>
        /// <param name="processType"></param>
        /// <returns>Returns a <see cref="SendResult"/> containing the unique ID of the sent message and response from access-point-service. If message is not sent, an exception is thrown </returns>
        public SendResult SendDocument(XDocument xDocument, string sender, string receiver, string documentType, string processType)
        {
            var oldXml = xDocument.ToXmlDocument();
            return SendDocument(oldXml, sender, receiver, documentType, processType);
        }

        /// <summary>
        /// Super-simple method to send a EHF-invoice 
        /// </summary>
        /// <param name="xmlDocument">EHF-document to send</param>
        /// <param name="sender">sender-id, sample:9908:974763907</param>
        /// <param name="receiver">receiver-id, sample:9908:974763907</param>
        /// <param name="documentType">See <see cref="PeppolDocumentType"/></param>
        /// <param name="processType"></param>
        /// <returns>Returns a <see cref="SendResult"/> containing the unique ID of the sent message and response from access-point-service. If message is not sent, an exception is thrown </returns>
        public SendResult SendDocument(XmlDocument xmlDocument, string sender, string receiver, string documentType, string processType)
        {
            Log.Info("Start send invoice...");
            var start = DateTime.Now;
            if (xmlDocument == null || xmlDocument.DocumentElement == null)
                throw new Exception("xmlDocument was null or xmlDocument.DocumentElement was null, aborting send..");
            if (string.IsNullOrEmpty(sender))
                throw new ArgumentNullException("sender", "sender cannot be null. samlpevalue: 9908:974763907");
            if (string.IsNullOrEmpty(receiver))
                throw new ArgumentNullException("receiver", "receiver cannot be null. samlpevalue: 9908:974763907");

            //Faste verdier for EHF faktura 2.0:
            const string businessIdScheme = "iso6523-actorid-upis"; //trur denne bestemmer at vi skal bruke norsk standard på sender og mottaker-id. Usikker på kva "u"-en gjær..
            const string businessIdSchemeSender = "iso6523-actorid-upis"; //med ein ekstra u (samme som eksempel i pdf fra difi)
            string documentIdScheme = PeppolDocumentType.DocumentTypeSchema; //"busdox-docid-qns"; //denne er fast!
            string processIdScheme = PeppolProcessType.ProcessIdSchema; 

            
            //variable verdier avhengig av dokument:
            var documentIdValue = documentType;
            var processIdValue = processType;//processId identifiserer den prosess forretningsdokumentet er en del av. bii04 == kun faktura,  bii05 == faktura og kreditnota

            //Metadata for dokumentet som skal sendes
            IMessageMetadata metadata = new MessageMetadata()
            {
                RecipientIdentifier = new ParticipantIdentifierType { Value = receiver.ToLower(), scheme = businessIdScheme },
                SenderIdentifier = new ParticipantIdentifierType { Value = sender.ToLower(), scheme = businessIdSchemeSender },
                DocumentIdentifier = new DocumentIdentifierType { Value = documentIdValue, scheme = documentIdScheme },
                ProcessIdentifier = new ProcessIdentifierType { Value = processIdValue, scheme = processIdScheme },
                ChannelIdentifier = DefaultChannel,
                MessageIdentifier = "uuid:" + Guid.NewGuid().ToString("D")
            };

            var messageBody = new STARTLibrary.accesspointService.Create { Any = new[] { xmlDocument.DocumentElement } };

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

            Log.InfoFormat("Message {0} created OK", metadata.MessageIdentifier);
            
            var serviceMetadata = GetServiceMetadataFromSML(createRequest);
            
            var result = SendDocument(createRequest, sender, receiver, serviceMetadata);
            var timerTotal = DateTime.Now.Subtract(start);
            result.TimeSpent = timerTotal;
            return result;
        }

        /// <summary>
        /// Use this method if you want to send to a custom access-point (debug)        
        /// </summary>
        /// <param name="createRequest">SOAP-request to send to accesspoint</param>
        /// <param name="sender">sender-id, sample:9908:974763907</param>
        /// <param name="receiver">receiver-id, sample:9908:974763907</param>
        /// <param name="serviceMetadata">Servicemetadata containing endpoint and certificates</param>
        /// <returns></returns>
        public SendResult SendDocument(CreateRequest createRequest, string sender, string receiver, ServiceMetadata serviceMetadata)
        {
            var start = DateTime.Now;
            try
            {
                Log.Info("Start SendInvoice...");
                if(serviceMetadata==null)
                    throw new ArgumentNullException("serviceMetadata", "serviceMetadata cannot be null");
                if (serviceMetadata.Certificate == null)
                    throw new ArgumentException("Certificate required (both for custom validation and for authenticationMode \"MutualCertificate\").");
                if (string.IsNullOrEmpty(sender))
                    throw new ArgumentNullException("sender", "sender cannot be null. samlpevalue: 9908:974763907");
                if (string.IsNullOrEmpty(receiver))
                    throw new ArgumentNullException("receiver", "receiver cannot be null. samlpevalue: 9908:974763907");

                //Recreating channelfactory... this is resource-intensive, and should be cached. recreating since certificate is readonly after channel is open.
                Log.InfoFormat("Recreating channelfactory for ep {0} using certificate {1}", serviceMetadata.Address.AbsoluteUri, serviceMetadata.Certificate.FriendlyName);
                _channelFactory = Utilities.CreateResourceChannelFactory<Resource>(_peppolEndpointName, _clientCertificate, _serviceCertificate);

                //1. Her angir vi at vi forventer at servicen vi kontakter har følgande sertifikat (f.eks. EVRY sitt)
                if (_channelFactory.Credentials == null)
                    throw new Exception("_channelFactory.Credentials was null..");

                //Her setter vi channelfactory sitt sertifikat til sertfikatet vi henta fra AP (f.eks. EVRY sitt):           
                _channelFactory.Credentials.ServiceCertificate.DefaultCertificate = new X509Certificate2(serviceMetadata.Certificate);

                //Slår på custom validator for sertifikatet fra eksternt aksesspunkt:
                _channelFactory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.Custom;
                _channelFactory.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
                _channelFactory.Credentials.ServiceCertificate.Authentication.CustomCertificateValidator = new CustomCertificateValidator
                {
                    ExpectedCertificate = serviceMetadata.Certificate,
                    ExpectedIssuer = CertificateIssuer.AccessPointCA,
                    RevocationMode = X509RevocationMode.NoCheck
                };

                //2. set identity til SERVER. gjær nokon endringar på channelfactory sitt endpoint (createDnsIdentity basert på dns i sertifikat)
                var dnsName = serviceMetadata.Certificate.GetNameInfo(X509NameType.SimpleName, false);
                if (string.IsNullOrEmpty(dnsName))
                    throw new Exception("Could not resolve dnsName for certificate..");

                //Her endrer vi adressen til channelfactory til å gå mot gitt aksesspunkt:
                _channelFactory.Endpoint.Address = new EndpointAddress(_channelFactory.Endpoint.Address.Uri, EndpointIdentity.CreateDnsIdentity(dnsName), _channelFactory.Endpoint.Address.Headers);

                //set identity til CLIENT (oss), og create token:
                Log.Info("Creating token..");
                var thisUrl = serviceMetadata.Address; //_channelFactory.Endpoint.Address.Uri;
                X509Certificate2 clientCertificate = _channelFactory.Credentials.ClientCertificate.Certificate;
                ClaimsIdentity myOwnClaims = AccessPointToken.MakeClaims(createRequest.SenderIdentifier.Value, AssuranceLevel);
                Saml2SecurityToken token = AccessPointToken.ClaimsToSaml2SenderVouchesToken(myOwnClaims, thisUrl.AbsoluteUri, clientCertificate);
                
                //åpner proxy:
                Log.Info("Creating proxy on channelFactory with issuedToken...");
                var proxy = _channelFactory.CreateChannelWithIssuedToken(token, new EndpointAddress(thisUrl, _channelFactory.Endpoint.Address.Identity));
                Log.Info("Proxy created ok.");

                //kaller service:
                Log.Info("Calling service");
                var response = proxy.Create(createRequest);
                

                var sendResult = new SendResult()
                {
                    MessageId = createRequest.MessageIdentifier,
                    Response = response,
                    TimeSpent = DateTime.Now.Subtract(start)
                };

                Log.InfoFormat("response OK. Message {0} sent OK!", sendResult.MessageId);
                return sendResult;
            }
            catch (Exception exception)
            {
                Log.Error("Error when sending invoice..");
                Log.Error(exception);
                throw;
            }
        }

        #endregion


    }
}
